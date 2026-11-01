using System;
using AutoCommand.NetWatch.Core;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AutoCommand.NetWatch.Core
{
    /// <summary>
    /// The geo/org engine of <see cref="DnsResolutionService"/> (partial class):
    /// the /24-prefix cache, the throttled ip-api client with its adaptive
    /// backoff, the RDAP registry fallback, and the persistence for all of
    /// it. Split out of the main file so the hostname pipeline and the geo
    /// engine read separately; the state (prefix cache, backoff) is shared
    /// by construction — one instance, one budget.
    /// </summary>
    public sealed partial class DnsResolutionService
    {
        // ── constants ────────────────────────────────────────────────────

        /// <summary>Geo org/country per /24 prefix — ownership changes rarely.</summary>
        private static readonly TimeSpan GeoCacheTtl   = TimeSpan.FromDays(14);

        /// <summary>A definitive "no org data" answer — retry the prefix next day.</summary>
        private static readonly TimeSpan GeoNegativeTtl = TimeSpan.FromHours(24);

        /// <summary>Minimum delay between successive ip-api.com calls (45 req/min free tier).</summary>
        private static readonly TimeSpan GeoApiThrottle = TimeSpan.FromMilliseconds(1_400);

        /// <summary>Cap for the transient-failure backoff doubling.</summary>
        private static readonly TimeSpan GeoBackoffMax = TimeSpan.FromMinutes(5);

        // ── state ────────────────────────────────────────────────────────

        private static readonly string GeoCacheFilePath = ResolveCachePath("geo_prefix_cache.json");

        // "a.b.c.0/24" (IPv4; full address for IPv6) → (org·code, resolvedAt, negative)
        private readonly ConcurrentDictionary<string, GeoCacheEntry> _geoCache = new();

        private DateTime _lastGeoCall = DateTime.MinValue;

        /// <summary>Grows on transient ip-api failures (rate limit, timeouts), resets on success.</summary>
        private TimeSpan _geoBackoff = GeoApiThrottle;

        // ── resolution pipeline ──────────────────────────────────────────

        private string MinutesUntilNextGeoCall()
        {
            var remaining = _geoBackoff - (DateTime.UtcNow - _lastGeoCall);
            return Math.Max(0, Math.Ceiling(remaining.TotalMinutes)).ToString("0");
        }

        private enum GeoResultKind { Label, Negative, Transient }

        private readonly struct GeoResult
        {
            public readonly GeoResultKind Kind;
            public readonly string Label;
            private GeoResult(GeoResultKind kind, string label) { Kind = kind; Label = label; }
            public static GeoResult Ok(string label) => new(GeoResultKind.Label, label);
            public static readonly GeoResult NoData = new(GeoResultKind.Negative, null);
            public static readonly GeoResult RetryLater = new(GeoResultKind.Transient, null);
        }

        /// <summary>
        /// Geo/owner label for an IP, cheapest source first:
        ///   1. locally classified ranges (IpClassifier) — no network at all;
        ///   2. non-routable addresses (RFC1918, loopback, link-local…) — no
        ///      answer exists, so no trip either;
        ///   3. the /24-prefix cache — one API answer serves a whole rotating
        ///      fleet block for two weeks;
        ///   4. ip-api.com, throttled, with backoff on transient failures.
        /// Transient failures (rate limit, timeout) return RetryLater and cache
        /// NOTHING — a wrong "unresolved" answer must not outlive the outage.
        /// </summary>
        private async Task<GeoResult> GeoIpLookupAsync(string ip, CancellationToken ct)
        {
            // 1–2. Local answers cost nothing and are always in stock.
            if (!IsGloballyRoutable(ip)) return GeoResult.NoData;
            string local = IpClassifier.Classify(ip);
            if (local != "Unclassified" && local != "IPv6/other")
                return GeoResult.Ok(local);

            // 3. Prefix cache (positive and negative both stop API trips)
            string prefix = GeoPrefixKey(ip);
            if (_geoCache.TryGetValue(prefix, out var cached))
            {
                TimeSpan ttl = cached.Negative ? GeoNegativeTtl : GeoCacheTtl;
                if (!cached.IsStale(ttl))
                    return cached.Negative ? GeoResult.NoData : GeoResult.Ok(cached.Label);
            }

            // 4. The API. Spacing uses the current backoff so a rate-limiting
            //    episode widens the gap instead of compounding the 429s —
            //    unless this is the user-initiated urgent call ResolveNow
            //    queued (one skipped spacing; failures still back off below).
            if (Interlocked.Exchange(ref _skipNextSpacing, 0) != 1)
            {
                var elapsed = DateTime.UtcNow - _lastGeoCall;
                if (elapsed < _geoBackoff)
                    await Task.Delay(_geoBackoff - elapsed, ct);
            }
            _lastGeoCall = DateTime.UtcNow;

            try
            {
                string url = $"http://ip-api.com/json/{Uri.EscapeDataString(ip)}?fields=status,message,org,countryCode";
                string json = await _http.GetStringAsync(url, ct);

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (!root.TryGetProperty("status", out var status) || status.GetString() != "success")
                {
                    // A well-formed "fail" (invalid query, reserved range the
                    // local check didn't cover) is a definitive answer.
                    _geoBackoff = GeoApiThrottle;
                    _geoCache[prefix] = new GeoCacheEntry("", DateTime.UtcNow, negative: true);
                    return await RdapFallbackOrAsync(ip, GeoResult.NoData, ct);
                }

                string org  = root.TryGetProperty("org",         out var o) ? o.GetString() ?? "" : "";
                string code = root.TryGetProperty("countryCode", out var c) ? c.GetString() ?? "" : "";

                // Strip "AS12345 " prefix
                if (org.Length > 3 && org[0] == 'A' && org[1] == 'S')
                {
                    int space = org.IndexOf(' ');
                    if (space > 0) org = org[(space + 1)..];
                }

                string label = string.IsNullOrEmpty(code) ? org : $"{org} · {code}";
                _geoBackoff = GeoApiThrottle;
                _geoCache[prefix] = new GeoCacheEntry(
                    string.IsNullOrEmpty(label) ? "" : label, DateTime.UtcNow,
                    negative: string.IsNullOrEmpty(label));
                return string.IsNullOrEmpty(label)
                    ? await RdapFallbackOrAsync(ip, GeoResult.NoData, ct)
                    : GeoResult.Ok(label);
            }
            catch (OperationCanceledException) { throw; }
            catch
            {
                // HTTP 429 / timeout / unparseable body — transient: back off
                // (doubling, capped) and let the caller retry later.
                _geoBackoff = _geoBackoff >= GeoBackoffMax ? GeoBackoffMax : TimeSpan.FromTicks(Math.Min(_geoBackoff.Ticks * 2, GeoBackoffMax.Ticks));
                return await RdapFallbackOrAsync(ip, GeoResult.RetryLater, ct);
            }
        }

        /// <summary>
        /// Registry fallback for the two outcomes that leave an address
        /// unresolved — a definitive ip-api "no data" and a transient
        /// throttle/timeout. The RDAP engine (IANA-bootstrap routed, its own
        /// cache, its own pacing) answers the same org question for many of
        /// those prefixes at zero cost to the ip-api free tier, and writes
        /// the positive prefix entry itself so a found answer overwrites the
        /// negative one. <paramref name="fallback"/> is returned untouched
        /// when RDAP has nothing — never a worse answer than the caller had.
        /// </summary>
        private async Task<GeoResult> RdapFallbackOrAsync(string ip, GeoResult fallback, CancellationToken ct)
        {
            try
            {
                var entry = await RdapService.Instance.LookupAsync(ip);
                if (entry == null || entry.IsNegative || string.IsNullOrEmpty(entry.Org))
                    return fallback;

                string org = entry.Org;
                if (org.Length > 3 && org[0] == 'A' && org[1] == 'S')
                {
                    int space = org.IndexOf(' ');
                    if (space > 0) org = org[(space + 1)..];
                }
                if (string.IsNullOrEmpty(org)) return fallback;

                string label = string.IsNullOrEmpty(entry.Country) ? org : $"{org} · {entry.Country}";
                _geoCache[GeoPrefixKey(ip)] = new GeoCacheEntry(label, DateTime.UtcNow, negative: false);
                return GeoResult.Ok(label + " · RDAP");
            }
            catch { return fallback; }   // RDAP unavailable/throttled — keep the caller's outcome
        }

        /// <summary>/24 network for IPv4 ("a.b.c.0/24"); IPv6 keeps its full address as key.</summary>
        internal static string GeoPrefixKey(string ip)
        {
            if (IPAddress.TryParse(ip, out var a) && a.AddressFamily == AddressFamily.InterNetwork)
            {
                var b = a.GetAddressBytes();
                return $"{b[0]}.{b[1]}.{b[2]}.0/24";
            }
            return ip;
        }

        /// <summary>False for addresses that can never have a geo answer — never spend an API call on them.</summary>
        internal static bool IsGloballyRoutable(string ip)
        {
            if (!IPAddress.TryParse(ip, out var a) || a.AddressFamily != AddressFamily.InterNetwork)
                return true;   // IPv6 etc. — don't gate here
            var b = a.GetAddressBytes();
            if (b[0] == 0 || b[0] == 10 || b[0] == 127 || b[0] >= 224) return false;   // this-net, private, loopback, multicast+
            if (b[0] == 172 && b[1] >= 16 && b[1] <= 31) return false;                 // RFC1918
            if (b[0] == 192 && b[1] == 168) return false;                              // RFC1918
            if (b[0] == 169 && b[1] == 254) return false;                              // link-local
            if (b[0] == 100 && b[1] >= 64 && b[1] <= 127) return false;                // CGNAT
            return true;
        }

        // ── persistence ──────────────────────────────────────────────────

        // ── geo prefix cache persistence (same pattern, its own file) ─────

        private void LoadGeoCacheFromDisk()
        {
            try
            {
                if (!File.Exists(GeoCacheFilePath)) return;
                var dict = JsonSerializer.Deserialize<Dictionary<string, PersistedGeoEntry>>(
                    File.ReadAllText(GeoCacheFilePath));
                if (dict == null) return;
                foreach (var kv in dict)
                {
                    var entry = new GeoCacheEntry(kv.Value.L ?? "", kv.Value.T, kv.Value.N);
                    if (!entry.IsStale(entry.Negative ? GeoNegativeTtl : GeoCacheTtl))
                        _geoCache[kv.Key] = entry;
                }
            }
            catch { /* corrupt or missing file — start fresh */ }
        }

        private void SaveGeoCacheToDisk()
        {
            try
            {
                var dict = new Dictionary<string, PersistedGeoEntry>(_geoCache.Count);
                foreach (var kv in _geoCache)
                    dict[kv.Key] = new PersistedGeoEntry { L = kv.Value.Label, T = kv.Value.ResolvedAt, N = kv.Value.Negative };
                AtomicWriteJson(GeoCacheFilePath, JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = false }));
            }
            catch { /* best effort */ }
        }

        // ── inner types ──────────────────────────────────────────────────

        /// <summary>One org·country answer for a whole /24 block (or one IPv6 address).</summary>
        private sealed class GeoCacheEntry
        {
            public string   Label      { get; }   // "Microsoft Corporation · US"
            public DateTime ResolvedAt { get; }
            public bool     Negative   { get; }   // definitive no-data — shorter TTL

            public GeoCacheEntry(string label, DateTime resolvedAt, bool negative)
            {
                Label = label; ResolvedAt = resolvedAt; Negative = negative;
            }

            public bool IsStale(TimeSpan ttl) => (DateTime.UtcNow - ResolvedAt) > ttl;
        }

        private sealed class PersistedGeoEntry
        {
            public string   L { get; set; } // label ("" for negative entries)
            public DateTime T { get; set; } // resolved-at (UTC)
            public bool     N { get; set; } // negative?
        }
    }
}
