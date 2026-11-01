using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics.Eventing.Reader;

namespace AutoCommand.NetWatch.Core
{
    /// <summary>
    /// Live feed of DNS queries from Sysmon EID 22 (the app's own Sysmon
    /// config already captures them; before this service nothing consumed
    /// them). Owns its watcher subscription — EID 22 only, same channel the
    /// other Sysmon engines read — so it composes with, and never doubles,
    /// the connect watcher's EID 3 stream.
    ///
    /// Two honest limits, surfaced where the data is shown: DNS-over-HTTPS
    /// produces NO EID 22 events (a browser resolving via a known encrypted
    /// resolver shows as resolver traffic, not queries), and EID 22 carries
    /// no answers for the query itself beyond the resolved addresses Sysmon
    /// recorded. Queries persist across restarts (JSONL under
    /// %LOCALAPPDATA%\AutoCommand\dns), so yesterday's beacon domain is
    /// still explainable today.
    /// </summary>
    public sealed class DnsQueryFeedService
    {
        public static DnsQueryFeedService Instance => _instance ??= new DnsQueryFeedService();
        private static DnsQueryFeedService _instance;

        private const int MaxQueries = 5000;
        private static readonly TimeSpan Retain = TimeSpan.FromHours(24);
        private static readonly TimeSpan SaveInterval = TimeSpan.FromSeconds(30);

        /// <summary>Persistence root; settable for offline self-tests (house pattern).</summary>
        public static string StoreDirectory { get; set; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AutoCommand", "dns");

        private static string StorePath => Path.Combine(StoreDirectory, "dns-queries.jsonl");

        /// <summary>One observed DNS query as recorded from Sysmon EID 22.</summary>
        public sealed class DnsQuery
        {
            public DateTime TimeUtc { get; set; }
            public string Image { get; set; }
            public int Pid { get; set; }
            public string QueryName { get; set; }
            public List<string> Results { get; set; } = new();
            public bool EncryptedResolverHit { get; set; }   // the query name itself is a known resolver
        }

        /// <summary>Per-domain rollup: who asks for this name, how often, how regularly.</summary>
        public sealed class DomainAggregate
        {
            public string QueryName;
            public int TotalQueries;
            public DateTime FirstUtc, LastUtc;
            public List<(string Process, int Pid, int Count)> RequestedBy = new();
            public List<string> ResolvedIps = new();
            /// <summary>Coefficient of variation of inter-query gaps (lower =
            /// more regular — the beacon signature). Null when too few samples.</summary>
            public double? GapCv;
            public bool EncryptedResolverHit;
        }

        /// <summary>Per-process facet: everything a program resolved.</summary>
        public sealed class ProcessAggregate
        {
            public string Process;
            public int Pid;
            public int DistinctDomains;
            public int TotalQueries;
            public DateTime FirstUtc, LastUtc;
            public List<string> TopDomains = new();
        }

        private readonly object _gate = new();
        private readonly List<DnsQuery> _queries = new();
        private EventLogWatcher _watcher;
        private Timer _saveTimer;
        private bool _running, _dirty;

        public string WatcherError { get; private set; }
        public bool IsRunning { get { lock (_gate) return _running; } }
        public int QueryCount { get { lock (_gate) return _queries.Count; } }

        /// <summary>Endpoint names that answer DNS inside TLS — EID 22 never
        /// fires for their lookups, so a query FOR one of these names means
        /// the process is about to resolve everything invisibly.</summary>
        public static readonly HashSet<string> KnownEncryptedResolvers = new(StringComparer.OrdinalIgnoreCase)
        {
            "1.1.1.1", "1.0.0.1", "8.8.8.8", "8.8.4.4", "9.9.9.9", "149.112.112.112",
            "208.67.222.222", "208.67.220.220", "94.140.14.14", "94.140.15.15",
            "cloudflare-dns.com", "dns.google", "dns.quad9.net", "mozilla.cloudflare-dns.com",
            "use-application-dns.net",
        };

        private DnsQueryFeedService() { }

        /// <summary>Idempotent: safe to call from the DNS tab, bridge handlers, startup.</summary>
        public void Start() => Start(withWatcher: true);

        /// <summary>
        /// The watcher-optional overload — the offline self-test starts the
        /// feed without an event subscription, so a live Sysmon channel can
        /// never pollute its synthetic expectations.
        /// </summary>
        public void Start(bool withWatcher)
        {
            lock (_gate)
            {
                if (_running) return;
                _running = true;
            }
            if (_queries.Count == 0) Load(); // a fresh process restores its ring; an in-process restart already holds it (loading on top would double-count)
            if (!withWatcher)
            {
                WatcherError = "watcher not started (offline mode)";
                _saveTimer = new Timer(_ => SaveIfDirty(), null, SaveInterval, SaveInterval);
                return;
            }
            try
            {
                var query = new EventLogQuery("Microsoft-Windows-Sysmon/Operational",
                    PathType.LogName, "*[System[(EventID=22)]]");
                _watcher = new EventLogWatcher(query);
                _watcher.EventRecordWritten += Watcher_EventRecordWritten;
                _watcher.Enabled = true;
                WatcherError = null;
            }
            catch (EventLogNotFoundException)
            {
                WatcherError = "Sysmon/Operational channel not found — is Sysmon installed?";
                _watcher = null;
            }
            catch (Exception ex)
            {
                WatcherError = ex.Message;
                _watcher = null;
            }
            _saveTimer = new Timer(_ => SaveIfDirty(), null, SaveInterval, SaveInterval);
        }

        public void Stop()
        {
            lock (_gate)
            {
                if (!_running) return;
                _running = false;
                _saveTimer?.Dispose();
                _saveTimer = null;
            }
            try { if (_watcher != null) _watcher.Enabled = false; _watcher = null; } catch { }
            SaveIfDirty();
        }

        private void Watcher_EventRecordWritten(object sender, EventRecordWrittenEventArgs e)
        {
            try
            {
                if (e.EventRecord == null) return;
                string xml = e.EventRecord.ToXml();
                var doc = System.Xml.Linq.XDocument.Parse(xml);
                var ns = (System.Xml.Linq.XNamespace)"http://schemas.microsoft.com/win/2004/08/events/event";

                string image = "", pid = "", queryName = "", results = "", utcTime = "";
                foreach (var data in doc.Root?.Element(ns + "EventData")?.Elements(ns + "Data") ?? Enumerable.Empty<System.Xml.Linq.XElement>())
                {
                    string name = data.Attribute("Name")?.Value;
                    if (name == "Image") image = data.Value;
                    else if (name == "ProcessId") pid = data.Value;
                    else if (name == "QueryName") queryName = data.Value;
                    else if (name == "QueryResults") results = data.Value;
                    else if (name == "UtcTime") utcTime = data.Value;
                }
                if (string.IsNullOrEmpty(queryName) || queryName == "-") return;

                Record(DateTime.UtcNow, image,
                       int.TryParse(pid, out int p) ? p : 0,
                       queryName, ParseResultIps(results));
            }
            catch { /* one malformed event never takes the feed down */ }
        }

        /// <summary>
        /// Records one query. Public so the offline self-test drives the feed
        /// with synthetic rows — the watcher path funnels here too. A query
        /// FOR a known encrypted-resolver name is flagged: it is the moment a
        /// process switches to DNS the feed cannot see.
        /// </summary>
        public void Record(DateTime timeUtc, string image, int pid, string queryName, List<string> resultIps)
        {
            if (string.IsNullOrWhiteSpace(queryName)) return;
            var q = new DnsQuery
            {
                TimeUtc = timeUtc,
                Image = image ?? "",
                Pid = pid,
                QueryName = queryName.TrimEnd('.'),
                Results = (resultIps ?? new List<string>()).Take(8).ToList(),
            };
            q.EncryptedResolverHit = KnownEncryptedResolvers.Contains(q.QueryName);
            lock (_gate)
            {
                _queries.Add(q);
                PruneLocked();
                _dirty = true;
            }
        }

        public static List<string> ParseResultIps(string results)
        {
            var ips = new List<string>();
            if (string.IsNullOrWhiteSpace(results) || results == "(0)") return ips;
            foreach (var token in results.Split(';'))
            {
                var t = token.Trim();
                // Sysmon format: "type:  1 203.0.113.5" or a bare address; keep
                // whichever substring parses as an IP, drop the rest (names, types)
                var candidates = t.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                foreach (var c in candidates.Where(c => c.Contains(".") || c.Contains(":")))
                    if (IPAddress.TryParse(c, out var a))
                    {
                        string s = a.ToString();
                        if (!ips.Contains(s)) ips.Add(s);
                        break;
                    }
            }
            return ips;
        }

        private void PruneLocked()
        {
            var cutoff = DateTime.UtcNow - Retain;
            int stale = _queries.FindLastIndex(q => q.TimeUtc < cutoff) + 1;
            if (stale > 0) _queries.RemoveRange(0, stale);
            if (_queries.Count > MaxQueries) _queries.RemoveRange(0, _queries.Count - MaxQueries);
        }

        // ── queries (thread-safe; DNS tab + AI bridge) ───────────────────

        public List<DnsQuery> Recent(int minutes, int max = 200, string contains = null)
        {
            var cutoff = DateTime.UtcNow.AddMinutes(-Math.Max(1, minutes));
            lock (_gate)
                return _queries
                    .Where(q => q.TimeUtc >= cutoff &&
                                (contains == null ||
                                 q.QueryName.Contains(contains, StringComparison.OrdinalIgnoreCase) ||
                                 q.Image.Contains(contains, StringComparison.OrdinalIgnoreCase)))
                    .OrderByDescending(q => q.TimeUtc)
                    .Take(max)
                    .Select(q => new DnsQuery
                    {
                        TimeUtc = q.TimeUtc, Image = q.Image, Pid = q.Pid,
                        QueryName = q.QueryName, Results = q.Results.ToList(),
                        EncryptedResolverHit = q.EncryptedResolverHit,
                    })
                    .ToList();
        }

        /// <summary>Per-domain rollup over the window, busiest first.
        /// contains filters on domain or requesting process.</summary>
        public List<DomainAggregate> DomainAggregates(int minutes, int max = 100, string contains = null)
        {
            var cutoff = DateTime.UtcNow.AddMinutes(-Math.Max(1, minutes));
            List<DnsQuery> window;
            lock (_gate) window = _queries.Where(q => q.TimeUtc >= cutoff).ToList();
            window = window.Where(q => contains == null ||
                q.QueryName.Contains(contains, StringComparison.OrdinalIgnoreCase) ||
                q.Image.Contains(contains, StringComparison.OrdinalIgnoreCase)).ToList();

            var result = new List<DomainAggregate>();
            foreach (var g in window.GroupBy(q => q.QueryName, StringComparer.OrdinalIgnoreCase))
            {
                var rows = g.OrderBy(q => q.TimeUtc).ToList();
                var agg = new DomainAggregate
                {
                    QueryName = g.Key,
                    TotalQueries = rows.Count,
                    FirstUtc = rows.First().TimeUtc,
                    LastUtc = rows.Last().TimeUtc,
                    ResolvedIps = rows.SelectMany(q => q.Results).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                    EncryptedResolverHit = rows.Any(q => q.EncryptedResolverHit),
                };
                foreach (var pg in rows.GroupBy(q => new { q.Image, q.Pid }).OrderByDescending(p => p.Count()))
                    agg.RequestedBy.Add((string.IsNullOrEmpty(pg.Key.Image) ? "?" : Path.GetFileName(pg.Key.Image),
                                         pg.Key.Pid, pg.Count()));

                // beacon regularity: mean-relative spread of inter-query gaps;
                // needs ≥3 timestamps to mean anything
                if (rows.Count >= 3)
                {
                    var gaps = new List<double>(rows.Count - 1);
                    for (int i = 1; i < rows.Count; i++)
                        gaps.Add((rows[i].TimeUtc - rows[i - 1].TimeUtc).TotalSeconds);
                    var mean = gaps.Average();
                    if (mean > 0)
                    {
                        var sd = Math.Sqrt(gaps.Sum(x => (x - mean) * (x - mean)) / gaps.Count);
                        agg.GapCv = sd / mean;
                    }
                }
                result.Add(agg);
            }
            return result.OrderByDescending(a => a.TotalQueries).Take(max).ToList();
        }

        /// <summary>Per-process facet over the window: what each program resolves.</summary>
        public List<ProcessAggregate> ProcessAggregates(int minutes, int max = 100, string contains = null)
        {
            var cutoff = DateTime.UtcNow.AddMinutes(-Math.Max(1, minutes));
            List<DnsQuery> window;
            lock (_gate) window = _queries.Where(q => q.TimeUtc >= cutoff).ToList();
            window = window.Where(q => contains == null ||
                q.QueryName.Contains(contains, StringComparison.OrdinalIgnoreCase) ||
                q.Image.Contains(contains, StringComparison.OrdinalIgnoreCase)).ToList();

            var result = new List<ProcessAggregate>();
            foreach (var g in window.GroupBy(q => new { q.Image, q.Pid }).OrderByDescending(p => p.Count()))
            {
                var rows = g.OrderBy(q => q.TimeUtc).ToList();
                result.Add(new ProcessAggregate
                {
                    Process = string.IsNullOrEmpty(g.Key.Image) ? "?" : Path.GetFileName(g.Key.Image),
                    Pid = g.Key.Pid,
                    DistinctDomains = rows.Select(q => q.QueryName).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                    TotalQueries = rows.Count,
                    FirstUtc = rows.First().TimeUtc,
                    LastUtc = rows.Last().TimeUtc,
                    TopDomains = rows.GroupBy(q => q.QueryName, StringComparer.OrdinalIgnoreCase)
                                     .OrderByDescending(d => d.Count()).Take(5)
                                     .Select(d => d.Key).ToList(),
                });
            }
            return result.Take(max).ToList();
        }

        // ── persistence (JSONL, one query per line — house pattern) ──────

        private void Load()
        {
            try
            {
                if (!File.Exists(StorePath)) return;
                foreach (var line in File.ReadLines(StorePath))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    try
                    {
                        var q = JsonSerializer.Deserialize<DnsQuery>(line);
                        if (q == null || string.IsNullOrWhiteSpace(q.QueryName)) continue;
                        if (q.TimeUtc >= DateTime.UtcNow - Retain) _queries.Add(q);
                    }
                    catch { /* corrupt line — skip */ }
                }
                if (_queries.Count > MaxQueries)
                    _queries.RemoveRange(0, _queries.Count - MaxQueries);
            }
            catch { /* corrupt or missing file — start fresh */ }
        }

        private void SaveIfDirty()
        {
            List<DnsQuery> snapshot;
            lock (_gate)
            {
                if (!_dirty) return;
                _dirty = false;
                snapshot = _queries.ToList();
            }
            try
            {
                Directory.CreateDirectory(StoreDirectory);
                File.WriteAllLines(StorePath, snapshot.Select(q =>
                    JsonSerializer.Serialize(q, new JsonSerializerOptions { WriteIndented = false })));
            }
            catch { /* disk full, permissions — best effort */ }
        }
    }
}
