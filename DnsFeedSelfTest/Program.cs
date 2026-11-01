// DnsFeedSelfTest — drives DnsQueryFeedService with synthetic EID-22 rows:
// aggregation, window/contains filters, per-process facet, beacon
// regularity, encrypted-resolver flagging, result-IP parsing, persistence.
// No event channel and no network — the watcher is only exercised through
// Start/Stop lifecycle (it degrades cleanly without Sysmon installed).
using System.Net;
using AutoCommand.NetWatch.Core;

int failures = 0;
void Check(string name, bool pass, string evidence = "")
{
    Console.WriteLine($"{(pass ? "PASS" : "FAIL")}  {name}{(pass || evidence.Length == 0 ? "" : "  — " + evidence)}");
    if (!pass) failures++;
}

var baseTime = DateTime.UtcNow;

// ─────────────────────────────────────────────────────────────────────
// 1. Result-IP parsing — Sysmon's "type:  N ip;type:  N ip" shape, dedupe,
//    non-IP tokens dropped
// ─────────────────────────────────────────────────────────────────────
var parsed = DnsQueryFeedService.ParseResultIps("type:  1 203.0.113.5;type:  28 2606:2800:220:1:248:1893:25c8:1946;type:  1 203.0.113.5");
Check("ParseResultIps keeps only addresses", parsed.Count == 2 &&
      parsed.Contains("203.0.113.5") && parsed.Contains(IPAddress.Parse("2606:2800:220:1:248:1893:25c8:1946").ToString()),
      string.Join(",", parsed));
Check("ParseResultIps on empty/'(0)' → empty",
      DnsQueryFeedService.ParseResultIps("(0)").Count == 0 &&
      DnsQueryFeedService.ParseResultIps("").Count == 0);

// ─────────────────────────────────────────────────────────────────────
// 2. Feed lifecycle + persistence (temp store; Start degrades cleanly
//    without Sysmon, Stop flushes the ring to disk)
// ─────────────────────────────────────────────────────────────────────
string store = Path.Combine(Path.GetTempPath(), "dnsfeed-selftest-" + Guid.NewGuid().ToString("N")[..8]);
DnsQueryFeedService.StoreDirectory = store;
var feed = DnsQueryFeedService.Instance;
try { Directory.Delete(store, true); } catch { }

feed.Record(baseTime.AddMinutes(-30), "chrome.exe", 100, "example.com", new List<string> { "203.0.113.5" });
feed.Record(baseTime.AddMinutes(-20), "svchost.exe", 200, "update.microsoft.com", new List<string> { "20.42.65.92" });
feed.Record(baseTime.AddMinutes(-20), "chrome.exe", 100, "dns.google", new List<string>());  // DoH switch
feed.Record(baseTime.AddMinutes(-10), "chrome.exe", 100, "example.com", new List<string> { "203.0.113.5" });
feed.Record(baseTime.AddMinutes(-5), "chrome.exe", 100, "beacon.test", new List<string> { "198.51.100.9" });
feed.Record(baseTime.AddMinutes(-4), "chrome.exe", 100, "beacon.test", new List<string> { "198.51.100.9" });
feed.Record(baseTime.AddMinutes(-3), "chrome.exe", 100, "beacon.test", new List<string> { "198.51.100.9" });

feed.Start(withWatcher: false); // offline mode: the live Sysmon channel must never pollute synthetic expectations
Check("Start marks the feed running", feed.IsRunning);
feed.Stop();
Check("Stop marks the feed stopped and flushed", File.Exists(Path.Combine(store, "dns-queries.jsonl")));
Check("flushed file carries the queries",
      File.ReadAllText(Path.Combine(store, "dns-queries.jsonl")).Contains("beacon.test"));
try { Directory.Delete(store, true); } catch { }

// ─────────────────────────────────────────────────────────────────────
// 3. Aggregates on a fresh feed: grouping, counts, facets, regularity
// ─────────────────────────────────────────────────────────────────────
feed.Record(baseTime, "chrome.exe", 100, "beacon.test", new List<string> { "198.51.100.9" });
feed.Record(baseTime.AddSeconds(60), "chrome.exe", 100, "beacon.test", new List<string> { "198.51.100.9" });
feed.Record(baseTime.AddSeconds(120), "chrome.exe", 100, "beacon.test", new List<string> { "198.51.100.9" });
feed.Record(baseTime.AddSeconds(61), "msedge.exe", 300, "beacon.test", new List<string> { "198.51.100.9" });
feed.Record(baseTime.AddSeconds(10), "chrome.exe", 100, "news.example.org", new List<string> { "203.0.113.7", "203.0.113.8" });
feed.Record(baseTime.AddSeconds(300), "python.exe", 400, "dns.google", new List<string>());

var domains = feed.DomainAggregates(60);
var beacon = domains.FirstOrDefault(d => d.QueryName == "beacon.test");
Check("domain aggregate groups across processes",
      beacon != null && beacon.TotalQueries == 7 &&
      beacon.RequestedBy.Any(r => r.Process == "chrome.exe" && r.Count == 6) &&
      beacon.RequestedBy.Any(r => r.Process == "msedge.exe"),
      beacon == null ? "missing" : $"count={beacon.TotalQueries}");
Check("regularity computed for ≥3 samples", beacon?.GapCv != null, beacon?.GapCv?.ToString("0.00"));
Check("resolved IPs deduped", beacon?.ResolvedIps.Count == 1 && beacon.ResolvedIps[0] == "198.51.100.9");

var doh = domains.FirstOrDefault(d => d.QueryName == "dns.google");
Check("encrypted resolver query is flagged", doh?.EncryptedResolverHit == true);
Check("plain domain is not flagged", beacon?.EncryptedResolverHit == false);

var news = domains.FirstOrDefault(d => d.QueryName == "news.example.org");
Check("multi-answer query keeps both IPs", news?.ResolvedIps.Count == 2, news?.ResolvedIps.Count.ToString());

var procs = feed.ProcessAggregates(60);
var chrome = procs.FirstOrDefault(p => p.Process == "chrome.exe");
Check("per-process facet: distinct domains + total queries",
      chrome != null && chrome.DistinctDomains == 4 && chrome.TotalQueries == 10,
      chrome == null ? "missing" : $"domains={chrome.DistinctDomains} total={chrome.TotalQueries}");
Check("per-process top domains ranked", chrome?.TopDomains.First() == "beacon.test",
      chrome?.TopDomains != null ? string.Join(",", chrome.TopDomains) : "");

// ─────────────────────────────────────────────────────────────────────
// 4. Filters — window and contains, on both facets
// ─────────────────────────────────────────────────────────────────────
Check("contains filter on domain", feed.DomainAggregates(60, 100, "beacon").All(d => d.QueryName.Contains("beacon")));
Check("contains filter on process", feed.ProcessAggregates(60, 100, "msedge").All(p => p.Process == "msedge.exe"));
feed.Record(baseTime.AddHours(-2), "old.exe", 900, "stale.example.net", new List<string>());
Check("window filter drops old queries",
      !feed.DomainAggregates(60).Any(d => d.QueryName == "stale.example.net") &&
      feed.DomainAggregates(180).Any(d => d.QueryName == "stale.example.net"));

// beacon-like: exact 45 s cadence stays low-CV; jittered goes high
var t2 = baseTime.AddMinutes(-15);
int[] cadence = { 0, 45, 90, 135 };
foreach (var off in cadence) feed.Record(t2.AddSeconds(off), "evil.exe", 500, "regular.evil.test", new List<string>());
feed.Record(t2.AddMinutes(1), "evil.exe", 500, "jittered.evil.test", new List<string>());
feed.Record(t2.AddMinutes(1).AddSeconds(7), "evil.exe", 500, "jittered.evil.test", new List<string>());
feed.Record(t2.AddMinutes(1).AddSeconds(9), "evil.exe", 500, "jittered.evil.test", new List<string>());
feed.Record(t2.AddMinutes(1).AddSeconds(70), "evil.exe", 500, "jittered.evil.test", new List<string>());
var reg = feed.DomainAggregates(60, 100).FirstOrDefault(d => d.QueryName == "regular.evil.test");
var jit = feed.DomainAggregates(60, 100).FirstOrDefault(d => d.QueryName == "jittered.evil.test");
Check("regular cadence → low CV", reg?.GapCv != null && reg.GapCv < 0.15, reg?.GapCv?.ToString("0.00"));
Check("jittered cadence → high CV", jit?.GapCv != null && jit.GapCv > 0.5, jit?.GapCv?.ToString("0.00"));

Console.WriteLine(failures == 0 ? "\nALL CHECKS PASSED" : $"\n{failures} CHECK(S) FAILED");
return failures == 0 ? 0 : 1;
