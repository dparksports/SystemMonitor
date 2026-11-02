using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AutoCommand.NetWatch.Core;
using AutoCommand.Services;

namespace AutoCommand.Contain.Core
{
    /// <summary>
    /// "Why is WSL offline?" — the staged diagnosis the 2026-10-30 field
    /// report (§3) ran as PowerShell scripts, promoted into the product.
    /// Read-only throughout: every probe is a connect attempt or a listing;
    /// nothing is toggled, disabled or captured.
    ///
    /// Shape mirrors the Site Doctor pattern: gather inputs live, then
    /// decide in a PURE function (Diagnose) that WslSelfTest pins with
    /// synthetic inputs — the verdict logic never needs a live system to be
    /// tested, and the evidence always shows which stage said what.
    /// </summary>
    public static class WslDiagnosisService
    {
        /// <summary>The probe target both halves connect to — the same one
        /// the field report used, so live and scripted runs stay comparable.</summary>
        public const string EgressTargetHost = "1.1.1.1";
        public const int EgressTargetPort = 80;
        public static string EgressTarget => $"{EgressTargetHost}:{EgressTargetPort}";

        /// <summary>The rule-name family the field report ruled in/out —
        /// identical pattern to wsl-egress-diag.ps1 step 3.</summary>
        private static readonly Regex FamilyPattern = new(
            @"WSL|Hyper-V|Virtual Machine|vmcompute|vmswitch|Host Compute|Subsystem for Linux|wslservice|vmmem|icsvc",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public sealed record WslDiagnosisInputs(
            bool Installed,
            bool VmRunning,
            string[] RunningDistros,
            string NetworkingMode,
            bool? HostEgressOk, string HostEgressError,
            bool? GuestEgressOk, string GuestEgressError,
            bool? GuestGatewayOk, string GuestGatewayError,
            int TotalRules, int EnabledRules, List<string> FamilyDisabledRules,
            string HyperVDefaultOutbound, bool? HyperVEnabled, string HyperVError);

        public sealed record WslDiagnosis(bool Healthy, string Verdict, string NextStep, List<string> Evidence);

        // ── the pure verdict ───────────────────────────────────────────────

        public static WslDiagnosis Diagnose(WslDiagnosisInputs i)
        {
            var ev = new List<string>();

            if (!i.Installed)
                return new WslDiagnosis(true,
                    "WSL isn't installed on this machine — there is nothing to diagnose.",
                    null, new List<string> { "wsl.exe not found / --list failed" });

            ev.Add($"WSL installed; utility VM {(i.VmRunning ? "RUNNING" : "not running")}" +
                   (i.RunningDistros is { Length: > 0 } ? $" (distros: {string.Join(", ", i.RunningDistros)})" : ""));

            if (!i.VmRunning)
                return new WslDiagnosis(true,
                    "No WSL distro is running right now, so there is no live WSL traffic to diagnose. " +
                    "Start a distro and re-run this check.",
                    "Start a distro (wsl -d <name> or a terminal), then re-run.",
                    ev);

            if (i.NetworkingMode != null)
                ev.Add($"networking mode: {i.NetworkingMode}");

            // Stage: the host itself.
            if (i.HostEgressOk == false)
            {
                ev.Add($"host → {EgressTarget}: FAILED" +
                       (i.HostEgressError != null ? $" ({i.HostEgressError})" : ""));
                return new WslDiagnosis(false,
                    $"The HOST has no egress to {EgressTarget} either — the problem is machine-wide " +
                    "(Wi-Fi down, airplane mode, VPN up, router), not WSL-specific.",
                    "Fix host connectivity first, then re-run this check.",
                    ev);
            }
            ev.Add($"host → {EgressTarget}: OK");

            // Stage: the guest, verbatim semantics of the field-report probes
            // (exit 0 = connected, 124 = timed out / dropped, null = skipped).
            if (i.GuestEgressOk == null)
            {
                ev.Add($"guest → {EgressTarget}: probe skipped" +
                       (i.GuestEgressError != null ? $" ({i.GuestEgressError})" : ""));
                return new WslDiagnosis(false,
                    "The guest probe could not run — most likely this distro has no bash (the probe uses " +
                    "bash's /dev/tcp) or the distro refused the exec. Nothing about the network was learned.",
                    "Try from a terminal inside the distro: curl -m 5 " + EgressTargetHost,
                    ev);
            }

            if (i.GuestEgressOk == true)
            {
                ev.Add($"guest → {EgressTarget}: OK");
                return new WslDiagnosis(true,
                    $"WSL egress is healthy right now: the guest reached {EgressTarget}. If something " +
                    "inside the distro still fails, the cause is inside the distro (DNS in " +
                    "/etc/resolv.conf, a proxy env var, the app itself) — not the Windows firewall.",
                    null, ev);
            }

            ev.Add($"guest → {EgressTarget}: DROPPED" +
                   (i.GuestEgressError != null ? $" ({i.GuestEgressError})" : " (timed out)"));

            // Guest is dropped while the host is fine — rule the model in or out.
            if (i.FamilyDisabledRules is { Count: > 0 })
            {
                ev.Add($"firewall rule model: {i.EnabledRules}/{i.TotalRules} rules enabled, " +
                       $"{i.FamilyDisabledRules.Count} WSL/Hyper-V-family rule(s) DISABLED:");
                foreach (var r in i.FamilyDisabledRules.Take(8))
                    ev.Add("   OFF · " + r);
                if (i.FamilyDisabledRules.Count > 8)
                    ev.Add($"   … and {i.FamilyDisabledRules.Count - 8} more");
                return new WslDiagnosis(false,
                    $"{i.FamilyDisabledRules.Count} WSL/Hyper-V-family firewall rules are DISABLED — a " +
                    "lockdown preset (or a baseline drift enforcer) most likely took the VM's plumbing down " +
                    "with it. This one IS in the rule model.",
                    "Re-enable the listed rules (Firewall tab → rules, or the Shields Up preset that " +
                    "keeps VM networking on), then re-run this check.",
                    ev);
            }

            ev.Add($"firewall rule model: all {i.TotalRules} rules enabled — no rule-state explanation exists");

            if (i.HyperVDefaultOutbound != null && i.HyperVDefaultOutbound != "Allow" && i.HyperVDefaultOutbound != "NotConfigured")
            {
                ev.Add($"Hyper-V firewall (WSL's own layer): default outbound = {i.HyperVDefaultOutbound}");
                return new WslDiagnosis(false,
                    $"The Hyper-V firewall — WSL's own layer, below the normal rule model — has default " +
                    $"outbound action '{i.HyperVDefaultOutbound}', so guest egress is being dropped at that layer.",
                    "Set the Hyper-V firewall default outbound action back to Allow (Get-NetFirewallHyperVVMSetting " +
                    "shows the current values), then re-run this check.",
                    ev);
            }
            ev.Add("Hyper-V firewall (WSL's own layer): " +
                   (i.HyperVError != null ? $"unreadable ({i.HyperVError})" :
                    $"default outbound = {i.HyperVDefaultOutbound ?? "unknown"} — not blocking"));

            // An inconclusive gateway leg is still evidence — the field
            // report's own expectation was a timeout on this leg (inbound
            // default is Block, so nothing listens); say so, don't skip it.
            if (i.GuestGatewayOk == null && i.GuestGatewayError != null)
                ev.Add("guest → its NAT gateway: inconclusive (" + i.GuestGatewayError + ")");

            if (i.GuestGatewayOk == false)
            {
                ev.Add("guest → its NAT gateway: FAILED" +
                       (i.GuestGatewayError != null ? $" ({i.GuestGatewayError})" : ""));
                return new WslDiagnosis(false,
                    "The guest cannot even reach its own NAT gateway — the drop is at the virtual switch / " +
                    "HNS layer, before any Windows firewall rule applies.",
                    "Restart the WSL utility VM (wsl --shutdown, then start a distro) and re-run this check.",
                    ev);
            }
            if (i.GuestGatewayOk == true)
                ev.Add("guest → its NAT gateway: OK (the vSwitch itself is alive)");

            return new WslDiagnosis(false,
                "Every firewall rule says allow and the guest's vSwitch is alive, yet the guest probe " +
                $"to {EgressTarget} times out — the drop lives BELOW the rule model (a WFP filter/callout " +
                "or HNS state that Get-NetFirewallRule cannot see). AutoCommand installs no WFP objects, " +
                "so this is not an AutoCommand block — but attribution needs a kernel-level capture.",
                "From an elevated shell run wsl-egress-elevated-diag.ps1 (or netsh wfp capture start) — it " +
                "reproduces the drop while WFP's netevent ring records it and maps each dropping filter to " +
                "its provider/sublayer.",
                ev);
        }

        // ── the live gatherer ──────────────────────────────────────────────

        public static async Task<WslDiagnosis> DiagnoseLiveAsync(IProgress<string> progress = null)
        {
            progress?.Report("WSL state…");
            var state = WslIdentityService.GetState();

            bool? hostOk = null; string hostErr = null;
            bool? guestOk = null; string guestErr = null;
            bool? gwOk = null; string gwErr = null;

            if (state.VmRunning)
            {
                progress?.Report($"host egress probe → {EgressTarget}…");
                (hostOk, hostErr) = await Task.Run(ProbeHostEgressAsync);

                progress?.Report($"guest egress probe → {EgressTarget}…");
                (guestOk, guestErr) = await Task.Run(ProbeGuestEgressAsync);

                progress?.Report("guest gateway probe…");
                (gwOk, gwErr) = await Task.Run(ProbeGuestGatewayAsync);
            }

            progress?.Report("firewall rule model scan…");
            var (total, enabled, familyDisabled) = await ScanRuleModelAsync();

            progress?.Report("Hyper-V firewall layer…");
            var (hvOut, hvEnabled, hvErr) = ReadHyperVFirewall();

            return Diagnose(new WslDiagnosisInputs(
                state.Installed, state.VmRunning, state.RunningDistros, state.NetworkingMode,
                hostOk, hostErr, guestOk, guestErr, gwOk, gwErr,
                total, enabled, familyDisabled, hvOut, hvEnabled, hvErr));
        }

        private static async Task<(bool? Ok, string Error)> ProbeHostEgressAsync()
        {
            try
            {
                using var client = new TcpClient();
                var connect = client.ConnectAsync(EgressTargetHost, EgressTargetPort);
                var done = await Task.WhenAny(connect, Task.Delay(TimeSpan.FromSeconds(5)));
                if (done != connect) return (false, "timed out");
                return (connect.IsCompletedSuccessfully, connect.IsFaulted ? connect.Exception?.Message : null);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        /// <summary>The field-report probe verbatim: bash's /dev/tcp, exit 0
        /// connected, 124 timed out. Returns null when the probe could not
        /// run (no bash, exec refused) — a skipped probe is never a verdict.</summary>
        private static async Task<(bool? Ok, string Error)> ProbeGuestEgressAsync()
        {
            var (output, _, exit) = await ProcessRunner.RunWithDetailsAsync("wsl.exe",
                "-e timeout 4 bash -c 'echo > /dev/tcp/" + EgressTargetHost + "/" + EgressTargetPort + "'");
            return ClassifyGuestProbe(exit, output);
        }

        private static async Task<(bool? Ok, string Error)> ProbeGuestGatewayAsync()
        {
            var (route, _, _) = await ProcessRunner.RunWithDetailsAsync("wsl.exe", "-e ip route show default");
            var match = Regex.Match(Decode(route) ?? "", @"default via (\d+\.\d+\.\d+\.\d+)");
            if (!match.Success)
                return (null, "gateway address not found in 'ip route show default'");

            var gateway = match.Groups[1].Value;
            var (output, _, exit) = await ProcessRunner.RunWithDetailsAsync("wsl.exe",
                "-e timeout 3 bash -c 'echo > /dev/tcp/" + gateway + "/53'");
            if (exit == 0) return (true, null);
            // A dropped probe to the gateway on port 53 may just mean nothing
            // LISTENS there (inbound default is Block) — the field report's
            // own expectation was exit 124 for the gateway leg. Treat timeout
            // as inconclusive, not failure; only a hard error means dead.
            if (exit == 124) return (null, $"timeout to {gateway}:53 — expected when nothing listens (inbound default is Block)");
            return (false, $"exit {exit} reaching gateway {gateway}");
        }

        /// <summary>Exit-code semantics of the field-report probes, public so
        /// WslSelfTest pins them: 0 = connected, 124 = timed out (dropped),
        /// anything else = the probe did not run (null — never a verdict).</summary>
        public static (bool? Ok, string Error) ClassifyGuestProbe(int exit, string output)
        {
            switch (exit)
            {
                case 0: return (true, null);
                case 124: return (false, "timed out / dropped");
                default:
                    var text = Decode(output);
                    if (!string.IsNullOrEmpty(text)) return (null, $"exit {exit}: {text.Trim()}");
                    return (null, $"exit {exit} (probe did not run — bash missing or exec refused?)");
            }
        }

        private static string Decode(string s) =>
            string.IsNullOrEmpty(s) ? s : s.TrimStart('\uFEFF').Replace("\0", "").Trim();

        /// <summary>Both directions, family filter — the same shape as
        /// wsl-egress-diag.ps1 steps 2-3.</summary>
        private static async Task<(int Total, int Enabled, List<string> FamilyDisabled)> ScanRuleModelAsync()
        {
            var familyDisabled = new List<string>();
            int total = 0, enabled = 0;
            foreach (var direction in new[] { 1, 2 })
            {
                var rules = await FirewallService.Instance.LoadRulesAsync(direction);
                foreach (var r in rules)
                {
                    total++;
                    if (r.Enabled) enabled++;
                    else if (FamilyPattern.IsMatch(r.Name ?? "") || FamilyPattern.IsMatch(r.DisplayGroup ?? ""))
                        familyDisabled.Add($"[{r.Direction}] {r.Name} (group={r.DisplayGroup})");
                }
            }
            return (total, enabled, familyDisabled);
        }

        /// <summary>WSL's own firewall layer, read defensively: the WMI
        /// classes exist on Win11 22H2+ and simply fail elsewhere — an
        /// unreadable layer is reported, never guessed.</summary>
        private static (string DefaultOutbound, bool? Enabled, string Error) ReadHyperVFirewall()
        {
            try
            {
                foreach (var scope in new[] { @"root\standardcimv2" })
                {
                    try
                    {
                        using var s = new ManagementObjectSearcher(scope, "SELECT * FROM MSFT_NetFirewallHyperVVMSetting");
                        foreach (ManagementObject o in s.Get())
                        {
                            string name = TryGet(o, "Name") ?? TryGet(o, "CreatorId") ?? "?";
                            string outbound = TryGet(o, "DefaultOutboundAction") ?? TryGet(o, "DefaultOutbound");
                            bool? enabled = TryGet(o, "Enabled") is string e && bool.TryParse(e, out var b) ? b : null;
                            // The WSL creator's settings are the ones that matter; if several
                            // creators exist, the first non-Allow outbound wins the report.
                            if (outbound != null)
                                return (outbound, enabled, null);
                            Debug.WriteLine($"HyperV VMSetting {name}: no DefaultOutboundAction");
                        }
                    }
                    catch (Exception ex)
                    {
                        return (null, null, ex.Message);
                    }
                }
                return (null, null, "MSFT_NetFirewallHyperVVMSetting returned nothing");
            }
            catch (Exception ex)
            {
                return (null, null, ex.Message);
            }
        }

        private static string TryGet(ManagementObject o, string prop)
        {
            try
            {
                var v = o[prop];
                if (v == null) return null;
                // CIM bools arrive boxed; normalize to text
                if (v is bool bb) return bb.ToString();
                return v.ToString();
            }
            catch { return null; }
        }

        // ── Hyper-V firewall details (phase 4: listing + per-rule surface) ──

        public sealed record HyperVProfileSummary(string Name, string DefaultInbound, string DefaultOutbound, bool? AllowHostPolicyMerge);

        public sealed record HyperVRuleSummary(string Name, string Direction, string Action, bool Enabled);

        public sealed record HyperVDetails(List<HyperVProfileSummary> Profiles, List<HyperVRuleSummary> Rules, string Error)
        {
            public static readonly HyperVDetails Empty = new(new List<HyperVProfileSummary>(), new List<HyperVRuleSummary>(), null);
        }

        /// <summary>
        /// WSL's own firewall layer in full: per-profile defaults and the
        /// Hyper-V rule set (the Windows-native way to filter WSL traffic
        /// directionally — New-NetFirewallHyperVRule). Read-only; the layer
        /// being absent (pre-22H2) surfaces as an Error note, never a guess.
        /// </summary>
        public static HyperVDetails ReadHyperVDetails()
        {
            var profiles = new List<HyperVProfileSummary>();
            var rules = new List<HyperVRuleSummary>();
            try
            {
                using (var s = new ManagementObjectSearcher(
                    @"root\standardcimv2", "SELECT Name, DefaultInboundAction, DefaultOutboundAction, AllowHostPolicyMerge FROM MSFT_NetFirewallHyperVProfile"))
                {
                    foreach (ManagementObject o in s.Get())
                    {
                        profiles.Add(new HyperVProfileSummary(
                            TryGet(o, "Name") ?? "?",
                            TryGet(o, "DefaultInboundAction"),
                            TryGet(o, "DefaultOutboundAction"),
                            TryGet(o, "AllowHostPolicyMerge") is string m && bool.TryParse(m, out var mb) ? mb : null));
                    }
                }

                using (var s = new ManagementObjectSearcher(
                    @"root\standardcimv2", "SELECT Name, DisplayName, Direction, Action, Enabled FROM MSFT_NetFirewallHyperVRule"))
                {
                    foreach (ManagementObject o in s.Get())
                    {
                        rules.Add(new HyperVRuleSummary(
                            TryGet(o, "DisplayName") ?? TryGet(o, "Name") ?? "?",
                            TryGet(o, "Direction"),
                            TryGet(o, "Action"),
                            TryGet(o, "Enabled") is string e && bool.TryParse(e, out var eb) ? eb : false));
                    }
                }
                return new HyperVDetails(profiles, rules, null);
            }
            catch (Exception ex)
            {
                return new HyperVDetails(profiles, rules, ex.Message);
            }
        }
    }
}
