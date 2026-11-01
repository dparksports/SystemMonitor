using System;
using AutoCommand.NetWatch.Core;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using AutoCommand.Models;

namespace AutoCommand.NetWatch.Core
{
    /// <summary>
    /// Resolves IP → hostname in the background, with:
    ///   • A persistent JSON cache on disk (survives app restarts)
    ///   • An in-memory LRU/TTL layer for fast repeated lookups
    ///   • Rate-limited queue so we never spam any API
    ///   • Live update of SvchostMonitorItem.Hostname via INotifyPropertyChanged
    ///
    /// SINGLETON — every tab and watcher shares DnsResolutionService.Instance.
    /// Four separate instances once meant four in-memory caches (duplicate
    /// lookups for the same IP), four independent 1400ms throttles (4x the
    /// ip-api.com free-tier limit, so answers came back rate-limited), and
    /// four writers clobbering the same dns_cache.json (last save wins, so a
    /// restart re-resolved everything and re-hit the API).
    ///
    /// The geo-IP fallback is cached BY /24 PREFIX, not per address: the
    /// org/country answer is constant across an operator's whole address
    /// block, and DNS-rotating fleets (Azure, Google, Akamai) present
    /// hundreds of distinct addresses from a handful of blocks. Locally
    /// classified ranges (IpClassifier: Microsoft/Azure, Akamai, Gcore) and
    /// non-routable addresses (RFC1918, loopback, link-local) never touch
    /// the API at all. Transient API failures back off instead of caching
    /// a wrong "unresolved" answer for the TTL.
    /// </summary>
    public sealed partial class DnsResolutionService : IDisposable
    {
        public static DnsResolutionService Instance => _instance ??= new DnsResolutionService();
        private static DnsResolutionService _instance;

        private DnsResolutionService()
        {
            LoadCacheFromDisk();
            LoadForwardCacheFromDisk();
            LoadGeoCacheFromDisk();
        }

        // -----------------------------------------------------------------------
        // Constants
        // -----------------------------------------------------------------------

        private static readonly TimeSpan DnsCacheTtl   = TimeSpan.FromHours(24);


        private static readonly TimeSpan SaveInterval = TimeSpan.FromSeconds(60);

        // Machine-wide beside the RDAP cache: caches survive install-folder
        // moves instead of starting cold in every new unzip. A legacy
        // beside-the-exe cache is copied once on first run; the exe folder
        // stays the fallback where ProgramData is not writable.
        private static readonly string CacheFilePath = ResolveCachePath("dns_cache.json");

        private static readonly string ForwardCacheFilePath = ResolveCachePath("dns_forward_cache.json");


        private static string ResolveCachePath(string fileName)
        {
            try
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "AutoCommand");
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, fileName);
                try
                {
                    string legacy = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
                    if (!File.Exists(path) && File.Exists(legacy))
                        File.Copy(legacy, path);
                }
                catch { /* migration is opportunistic — the fresh location works either way */ }
                return path;
            }
            catch
            {
                return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
            }
        }

        /// <summary>Forward (hostname→IPs) entries are short-lived: CDN names rotate.</summary>
        private static readonly TimeSpan ForwardCacheTtl = TimeSpan.FromMinutes(60);
        private static readonly TimeSpan ForwardNegativeTtl = TimeSpan.FromMinutes(10);

        // ── idle reconciliation: drains unresolved addresses when the
        //    machine's traffic is quiet (no new Sysmon events) ────────────

        /// <summary>No new Sysmon events for this long counts as "traffic idle".</summary>
        internal static readonly TimeSpan IdleThreshold = TimeSpan.FromMinutes(5);

        /// <summary>How often the reconciler re-checks the idle condition.</summary>
        internal static readonly TimeSpan IdleTick = TimeSpan.FromMinutes(1);

        /// <summary>Addresses per idle episode. The paced worker drains them
        /// at the provider's normal rate — never the urgent path.</summary>
        internal const int IdleBatchSize = 8;

        /// <summary>Rest between idle episodes, even while still idle.</summary>
        internal static readonly TimeSpan IdleBatchCooldown = TimeSpan.FromMinutes(2);

        /// <summary>Per-session give-up count for one address: permanently
        /// unanswerable addresses stop being retried until their cache
        /// entries age out naturally (24 h) and re-enter the set fresh.</summary>
        internal const int IdleMaxAttemptsPerIp = 5;

        // -----------------------------------------------------------------------
        // Fields
        // -----------------------------------------------------------------------

        // ip → (hostname, resolvedAt)
        private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();


        // hostname → (ips, resolvedAt) — the BITS tab's "where do they connect"
        private readonly ConcurrentDictionary<string, ForwardCacheEntry> _forwardCache = new();
        private readonly SemaphoreSlim _forwardGate = new(1, 1);

        // Queue of items waiting for resolution; bounded to avoid runaway growth
        private readonly Channel<IResolvableHost> _queue =
            Channel.CreateBounded<IResolvableHost>(new BoundedChannelOptions(512)
            {
                FullMode    = BoundedChannelFullMode.DropOldest,
                SingleReader = true
            });

        private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(5) };

        private readonly CancellationTokenSource _cts = new();
        private Task _workerTask;
        private Timer _saveTimer;
        private int _started;


        /// <summary>Set by ResolveNow: the NEXT api call skips the forward
        /// inter-request spacing (user-initiated urgent lookup — the same
        /// rule as the Blocked tab's RDAP-verify). Whoever's call it lands
        /// on, it is one skipped spacing; failures still double the backoff.</summary>
        private int _skipNextSpacing;

        /// <summary>Addresses queued for background resolution — the honest
        /// "resolving" state for DescribeResolveState (a Channel cannot be
        /// peeked without consuming).</summary>
        private readonly ConcurrentDictionary<string, byte> _pendingIps = new();

        // idle reconciliation state
        private readonly ConcurrentDictionary<string, int> _idleAttempts = new();
        private int _idleStarted;
        private Func<TimeSpan> _idleFor = static () => TimeSpan.FromDays(365);

        // -----------------------------------------------------------------------
        // Public API
        // -----------------------------------------------------------------------

        /// <summary>
        /// Starts the background resolution worker. Idempotent — every consumer
        /// (Svchost monitor, Traffic Patterns, BITS) may call it on the shared
        /// instance; only the first spawns the single reader.
        /// </summary>
        public void Start()
        {
            if (Interlocked.Exchange(ref _started, 1) == 1) return;
            _workerTask = Task.Run(() => WorkerLoopAsync(_cts.Token));
            _saveTimer = new Timer(_ => { SaveCacheToDisk(); SaveGeoCacheToDisk(); }, null,
                SaveInterval, SaveInterval);
        }

        /// <summary>
        /// Returns the cached hostname for <paramref name="ip"/> synchronously,
        /// or null if not yet resolved.
        /// </summary>
        public string GetCachedHostname(string ip)
        {
            if (_cache.TryGetValue(ip, out var entry) && !entry.IsStale(DnsCacheTtl))
                return entry.Hostname;
            return null;
        }

        /// <summary>
        /// Resolves <paramref name="item"/>.RemoteIp → Hostname asynchronously.
        /// If a fresh cache entry exists, the item is updated immediately on the
        /// calling thread; otherwise it is queued for background resolution and
        /// the DataGrid row will update itself via INotifyPropertyChanged.
        /// </summary>
        public void EnqueueForResolution(IResolvableHost item)
        {
            if (item == null || string.IsNullOrEmpty(item.RemoteIp)) return;

            // Already has a real hostname
            if (!IsUnresolvedHostname(item.Hostname)) return;

            // Check cache first — update immediately if fresh
            if (_cache.TryGetValue(item.RemoteIp, out var entry) && !entry.IsStale(DnsCacheTtl))
            {
                item.Hostname = entry.Hostname;
                return;
            }

            // Otherwise push to background queue (drops silently if full)
            if (_queue.Writer.TryWrite(item))
                _pendingIps[item.RemoteIp] = 1;
        }

        /// <summary>
        /// Pre-seeds the cache with a Sysmon-provided hostname (already resolved at event time).
        /// </summary>
        public void SeedFromSysmon(string ip, string hostname)
        {
            if (string.IsNullOrEmpty(ip) || string.IsNullOrEmpty(hostname) || hostname == "-") return;
            _cache[ip] = new CacheEntry(hostname, DateTime.UtcNow);
        }

        // ── resolve-state visibility + user-initiated retries ─────────────

        /// <summary>
        /// User-initiated immediate retry for one address ("Resolve now" on a
        /// row): forget this prefix's cached answer (a negative one would
        /// otherwise suppress the re-ask for 24 h), reset the provider
        /// backoff, and queue a fresh lookup whose API call skips the
        /// inter-request spacing (cooldowns still apply). Returns false when
        /// nothing needed doing — the address already resolves.
        /// </summary>
        public bool ResolveNow(string ip)
        {
            if (string.IsNullOrEmpty(ip) || !IPAddress.TryParse(ip, out _)) return false;
            if (!IsGloballyRoutable(ip)) return false;          // nothing to ask anyone
            string localCls = IpClassifier.Classify(ip);
            if (localCls != "Unclassified" && localCls != "IPv6/other") return false;
            _geoCache.TryRemove(GeoPrefixKey(ip), out _);
            Interlocked.Exchange(ref _skipNextSpacing, 1);
            _geoBackoff = GeoApiThrottle;
            if (!IsUnresolvedHostname(GetCachedHostname(ip) ?? ip)) return false;
            _pendingIps[ip] = 1;
            _queue.Writer.TryWrite(new AdHocResolve(ip));
            return true;
        }

        /// <summary>
        /// The "Retry unresolved" bulk action: forget every negative geo
        /// answer (definitive only as of when it was asked) and every
        /// raw-IP placeholder in the hostname cache (those placeholders are
        /// what stops EnqueueForResolution from re-queuing), reset the
        /// provider backoff, and re-queue the given addresses — or every
        /// pending one when the caller passes none. Normal pacing applies
        /// throughout: the point is a fresh, polite re-ask. Returns the
        /// number of addresses re-queued.
        /// </summary>
        public int RetryUnresolved(IEnumerable<string> ips = null)
        {
            foreach (var kv in _geoCache)
                if (kv.Value.Negative) _geoCache.TryRemove(kv.Key, out _);
            foreach (var kv in _cache)
                if (IsUnresolvedHostname(kv.Value.Hostname)) _cache.TryRemove(kv.Key, out _);
            _geoBackoff = GeoApiThrottle;

            int queued = 0;
            foreach (var ip in ips ?? _pendingIps.Keys.ToArray())
            {
                if (string.IsNullOrEmpty(ip) || !IPAddress.TryParse(ip, out _)) continue;
                _pendingIps[ip] = 1;
                _queue.Writer.TryWrite(new AdHocResolve(ip));
                queued++;
            }
            return queued;
        }

        /// <summary>
        /// Why an address shows no hostname yet — the tooltip text the
        /// monitor rows carry instead of a blank. Null only for a string
        /// that is not an IP.
        /// </summary>
        public string DescribeResolveState(string ip)
        {
            if (string.IsNullOrEmpty(ip) || !IPAddress.TryParse(ip, out _)) return null;
            if (!IsGloballyRoutable(ip))
                return "local or non-routable address — no hostname/org lookup exists for it";
            string local = IpClassifier.Classify(ip);
            if (local != "Unclassified" && local != "IPv6/other")
                return "classified locally as " + local + " — no lookup needed";

            if (_cache.TryGetValue(ip, out var entry) && !entry.IsStale(DnsCacheTtl)
                && !IsUnresolvedHostname(entry.Hostname))
                return "resolved " + entry.ResolvedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

            if (_pendingIps.ContainsKey(ip))
                return _geoBackoff > GeoApiThrottle
                    ? "resolving — throttled, next attempt in ~" + MinutesUntilNextGeoCall() + " min"
                    : "resolving — queued (" + _pendingIps.Count + " waiting)";

            if (_geoCache.TryGetValue(GeoPrefixKey(ip), out var geo))
            {
                if (geo.Negative)
                    return "no owner data for this /24 — negative-cached, retries after " +
                           geo.ResolvedAt.Add(GeoNegativeTtl).ToLocalTime().ToString("HH:mm") +
                           " (or use Resolve now)";
                if (!geo.IsStale(GeoCacheTtl))
                    return "resolved from the /24 prefix cache";
            }

            return _geoBackoff > GeoApiThrottle
                ? "not resolved yet — the lookup provider is throttled, next attempt in ~" +
                  MinutesUntilNextGeoCall() + " min (or use Resolve now)"
                : "not resolved yet — queued on the next refresh pass";
        }


        // ── idle reconciliation ─────────────────────────────────────────────

        /// <summary>
        /// Every address whose cached answer is a placeholder (no real
        /// hostname yet), oldest attempts first — the idle reconciler's work
        /// queue. The hostname cache already persists these placeholders, so
        /// the set survives restarts: "collect all unresolved IPs" falls out
        /// of the existing cache rather than a second ledger.
        /// </summary>
        public IReadOnlyList<string> UnresolvedIps =>
            _cache.Where(kv => IsUnresolvedHostname(kv.Value.Hostname))
                  .OrderBy(kv => kv.Value.ResolvedAt)
                  .Select(kv => kv.Key)
                  .ToList();

        /// <summary>
        /// Starts the idle reconciler: whenever <paramref name="idleFor"/>
        /// reports the machine quiet past <see cref="IdleThreshold"/> (the
        /// app wires this to the Sysmon watcher's quiet period), drain the
        /// unresolved set in small batches through the normal paced
        /// pipeline — never the urgent path; normal spacing and backoff
        /// throughout. Idempotent.
        /// </summary>
        public void StartIdleReconciliation(Func<TimeSpan> idleFor)
        {
            if (Interlocked.Exchange(ref _idleStarted, 1) == 1) return;
            _idleFor = idleFor ?? _idleFor;
            _ = Task.Run(() => IdleLoopAsync(_cts.Token));
        }

        private async Task IdleLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try { await Task.Delay(IdleTick, ct); } catch (OperationCanceledException) { break; }
                try
                {
                    if (_idleFor() < IdleThreshold) continue;
                    if (TakeIdleBatch() == 0) continue;
                    // still idle? wait out the cooldown before the next batch —
                    // a busy machine cancels the wait and the loop re-checks
                    await Task.Delay(IdleBatchCooldown, ct);
                }
                catch (OperationCanceledException) { break; }
                catch { /* an idle pass is opportunistic — never fatal */ }
            }
        }

        /// <summary>One idle episode: hand a batch of oldest unresolved
        /// addresses to the normal paced pipeline. Fresh negative answers
        /// are skipped (they were a definitive "no" less than a day ago)
        /// and addresses past the per-session attempt cap are left until
        /// their cache entries age out naturally. Returns the batch size.</summary>
        private int TakeIdleBatch()
        {
            int queued = 0;
            foreach (var ip in UnresolvedIps)
            {
                if (queued >= IdleBatchSize) break;
                if (_idleAttempts.TryGetValue(ip, out var tries) && tries >= IdleMaxAttemptsPerIp) continue;
                if (_geoCache.TryGetValue(GeoPrefixKey(ip), out var geo) &&
                    geo.Negative && !geo.IsStale(GeoNegativeTtl)) continue;

                _idleAttempts[ip] = tries + 1;
                _cache.TryRemove(ip, out _);   // placeholder out — the pipeline must re-ask
                _pendingIps[ip] = 1;
                _queue.Writer.TryWrite(new AdHocResolve(ip));
                queued++;
            }
            return queued;
        }

        /// <summary>Test seam: one idle episode with an injected idle
        /// reading, bypassing the loop's timer. Internal for
        /// NetWatchSelfTest (InternalsVisibleTo).</summary>
        internal int RunIdlePassForTest(Func<TimeSpan> idleFor) =>
            idleFor() >= IdleThreshold ? TakeIdleBatch() : 0;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _started, 2) == 2) return;   // already disposed
            _cts.Cancel();
            _queue.Writer.TryComplete();
            _workerTask?.Wait(TimeSpan.FromSeconds(3));
            _saveTimer?.Dispose(); _saveTimer = null;
            SaveCacheToDisk();
            SaveForwardCacheToDisk();
            SaveGeoCacheToDisk();
            _cts.Dispose();
        }

        // -----------------------------------------------------------------------
        // Forward resolution (hostname → IPs) — used by the BITS tab to turn
        // a job's URL host into the IPs it actually connects from. Cached
        // with a short TTL because CDN names rotate; failed lookups are
        // negative-cached briefly so a dead resolver can't spin the loop.
        // -----------------------------------------------------------------------

        public async Task<string[]> ResolveForwardAsync(string host)
        {
            if (string.IsNullOrEmpty(host) || IPAddress.TryParse(host, out _)) return new[] { host };

            if (_forwardCache.TryGetValue(host, out var cached) && !cached.IsStale)
                return cached.Ips;

            await _forwardGate.WaitAsync();
            try
            {
                if (_forwardCache.TryGetValue(host, out var second) && !second.IsStale)
                    return second.Ips;   // resolved while waiting on the gate

                string[] ips = Array.Empty<string>();
                try
                {
                    var entry = await Dns.GetHostEntryAsync(host);
                    ips = entry.AddressList
                        .Where(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
                                 || a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                        .Select(a => a.ToString())
                        .Distinct()
                        .Take(6)
                        .ToArray();
                }
                catch { /* NXDOMAIN / no resolver answer — negative cache below */ }

                _forwardCache[host] = new ForwardCacheEntry(ips, DateTime.UtcNow,
                    ips.Length == 0 ? ForwardNegativeTtl : ForwardCacheTtl);
                if (_forwardCache.Count % 10 == 0) SaveForwardCacheToDisk();
                return ips;
            }
            finally { _forwardGate.Release(); }
        }

        private void LoadForwardCacheFromDisk()
        {
            try
            {
                if (!File.Exists(ForwardCacheFilePath)) return;
                var dict = JsonSerializer.Deserialize<Dictionary<string, PersistedForwardEntry>>(
                    File.ReadAllText(ForwardCacheFilePath));
                if (dict == null) return;
                foreach (var kv in dict)
                {
                    var entry = new ForwardCacheEntry(kv.Value.I ?? Array.Empty<string>(), kv.Value.T,
                        kv.Value.I?.Length == 0 ? ForwardNegativeTtl : ForwardCacheTtl);
                    if (!entry.IsStale) _forwardCache[kv.Key] = entry;
                }
            }
            catch { /* corrupt or missing file — start fresh */ }
        }

        private void SaveForwardCacheToDisk()
        {
            try
            {
                var dict = new Dictionary<string, PersistedForwardEntry>(_forwardCache.Count);
                foreach (var kv in _forwardCache)
                    dict[kv.Key] = new PersistedForwardEntry { I = kv.Value.Ips, T = kv.Value.ResolvedAt };
                AtomicWriteJson(ForwardCacheFilePath,
                    JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = false }));
            }
            catch { /* disk full, permissions — silently skip */ }
        }

        // -----------------------------------------------------------------------
        // Background worker — processes one item at a time
        // -----------------------------------------------------------------------

        private async Task WorkerLoopAsync(CancellationToken ct)
        {
            await foreach (var item in _queue.Reader.ReadAllAsync(ct))
            {
                if (ct.IsCancellationRequested) break;
                if (!IsUnresolvedHostname(item.Hostname)) continue; // resolved by another path

                try
                {
                    string hostname = await ResolveAsync(item.RemoteIp, ct);
                    item.Hostname = hostname; // triggers INotifyPropertyChanged → DataGrid refresh
                    _pendingIps.TryRemove(item.RemoteIp, out _);
                }
                catch (OperationCanceledException) { break; }
                catch { /* silently skip failed items */ }
            }

            SaveCacheToDisk();
        }

        // -----------------------------------------------------------------------
        // Resolution pipeline: reverse DNS → geo-IP fallback
        // -----------------------------------------------------------------------

        private async Task<string> ResolveAsync(string ip, CancellationToken ct)
        {
            // 1. Return fresh cache hit
            if (_cache.TryGetValue(ip, out var cached) && !cached.IsStale(DnsCacheTtl))
                return cached.Hostname;

            // 2. Reverse DNS
            string resolved = await ReversesDnsAsync(ip);

            // 3. If DNS only gave us the raw IP, try geo-IP (local ranges and
            //    prefix cache first; the API only for genuinely new blocks)
            if (resolved == ip || IsUnresolvedHostname(resolved))
            {
                var geo = await GeoIpLookupAsync(ip, ct);
                if (geo.Kind == GeoResultKind.Label && !string.IsNullOrEmpty(geo.Label))
                {
                    resolved = geo.Label;
                }
                else if (geo.Kind == GeoResultKind.Transient)
                {
                    // Rate limit / transport failure: no API answer existed, so
                    // caching the raw IP would poison this address for 24h.
                    // Leave it unresolved — the monitor's re-enqueue tick (or a
                    // later session) retries once the backoff window clears.
                    return ip;
                }
                // Negative (definitive no-data) falls through and caches the
                // raw IP as before: the prefix cache already stops API trips.
            }

            // 4. Store in cache (even failed lookups, so we don't retry immediately)
            _cache[ip] = new CacheEntry(resolved, DateTime.UtcNow);

            // 5. Periodically persist to disk (every 20 resolutions; a 60s
            //    timer is the other checkpoint)
            if (_cache.Count % 20 == 0)
                SaveCacheToDisk();

            return resolved;
        }

        private static async Task<string> ReversesDnsAsync(string ip)
        {
            try
            {
                var entry = await Dns.GetHostEntryAsync(ip);
                return string.IsNullOrEmpty(entry.HostName) ? ip : entry.HostName;
            }
            catch
            {
                return ip; // DNS NXDOMAIN or timeout
            }
        }


        // -----------------------------------------------------------------------
        // Disk persistence  (simple JSON: { "ip": { "h": "host", "t": "utc" } })
        // -----------------------------------------------------------------------

        private void LoadCacheFromDisk()
        {
            try
            {
                if (!File.Exists(CacheFilePath)) return;

                string json = File.ReadAllText(CacheFilePath);
                var dict = JsonSerializer.Deserialize<Dictionary<string, PersistedEntry>>(json);
                if (dict == null) return;

                foreach (var kv in dict)
                {
                    var entry = new CacheEntry(kv.Value.H, kv.Value.T);
                    // Only load entries that are still within TTL
                    if (!entry.IsStale(DnsCacheTtl))
                        _cache[kv.Key] = entry;
                }
            }
            catch { /* corrupt or missing file — start fresh */ }
        }

        private void SaveCacheToDisk()
        {
            try
            {
                var dict = new Dictionary<string, PersistedEntry>(_cache.Count);
                foreach (var kv in _cache)
                    dict[kv.Key] = new PersistedEntry { H = kv.Value.Hostname, T = kv.Value.ResolvedAt };

                AtomicWriteJson(CacheFilePath, JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = false }));
            }
            catch { /* disk full, permissions, etc. — silently skip */ }
        }


        /// <summary>Write-via-temp-then-replace: a crash mid-save can never truncate the cache.</summary>
        private static void AtomicWriteJson(string path, string json)
        {
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, json);
            if (File.Exists(path)) File.Replace(tmp, path, null);
            else File.Move(tmp, path);
        }

        // -----------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------

        /// <summary>True when the hostname value is effectively unresolved (raw IP or empty).</summary>
        public static bool IsUnresolvedHostname(string hostname)
        {
            if (string.IsNullOrEmpty(hostname)) return true;
            if (hostname == "Unknown" || hostname == "-") return true;
            // Looks like a raw IPv4 address
            return IPAddress.TryParse(hostname, out _);
        }

        public int CacheCount => _cache.Count;

        // -----------------------------------------------------------------------
        // Inner types
        // -----------------------------------------------------------------------

        private sealed class CacheEntry
        {
            public string   Hostname   { get; }
            public DateTime ResolvedAt { get; }

            public CacheEntry(string hostname, DateTime resolvedAt)
            {
                Hostname   = hostname;
                ResolvedAt = resolvedAt;
            }

            public bool IsStale(TimeSpan ttl) => (DateTime.UtcNow - ResolvedAt) > ttl;
        }

        // Used only for JSON serialization (short keys to keep file small)
        private sealed class PersistedEntry
        {
            public string   H { get; set; } // hostname
            public DateTime T { get; set; } // resolved-at (UTC)
        }


        /// <summary>Queue item for user-initiated lookups that have no row
        /// to write back to — the resolution lands in the cache, where the
        /// next refresh pass picks it up.</summary>
        private sealed class AdHocResolve : IResolvableHost
        {
            public AdHocResolve(string ip) { RemoteIp = ip; Hostname = ip; }
            public string RemoteIp { get; }
            public string Hostname { get; set; }
        }


        private sealed class ForwardCacheEntry
        {
            public string[] Ips { get; }
            public DateTime ResolvedAt { get; }
            private readonly DateTime _expiresAt;

            public ForwardCacheEntry(string[] ips, DateTime resolvedAt, TimeSpan ttl)
            {
                Ips = ips;
                ResolvedAt = resolvedAt;
                _expiresAt = resolvedAt + ttl;
            }

            public bool IsStale => DateTime.UtcNow >= _expiresAt;
        }

        private sealed class PersistedForwardEntry
        {
            public string[]  I { get; set; } // resolved IPs (empty = negative entry)
            public DateTime  T { get; set; } // resolved-at (UTC)
        }
    }
}
