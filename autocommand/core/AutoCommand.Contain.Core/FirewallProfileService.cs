using AutoCommand.NetWatch.Core;
using System;
using AutoCommand.Contain.Core;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AutoCommand.Contain.Core
{
    /// <summary>
    /// Aggregated outcome of applying a firewall profile, for UI progress/summary reporting.
    /// </summary>
    public class ProfileApplyResult
    {
        public string ProfileName { get; set; }
        public List<GroupToggleResult> Groups { get; } = new List<GroupToggleResult>();
        public int TotalMatched { get; set; }
        public int TotalChanged { get; set; }
        public int TotalFailed { get; set; }
        public string FirstError { get; set; }
        public string Notes { get; set; }
    }

    /// <summary>
    /// Firewall profile presets using COM API.
    /// Ported from DeviceMonitorCS — now uses FirewallService instead of PowerShell.
    /// </summary>
    public class FirewallProfileService
    {
        private static FirewallProfileService _instance;
        public static FirewallProfileService Instance => _instance ??= new FirewallProfileService();

        public enum ProfileType
        {
            Custom,
            BlockSharingDiscoveryRemoteDesktop,
            AllowSharingDiscovery,
            AllowDiscoveryCast,
            ShieldsUpMdns,
            AllowSharingDiscoveryRemoteDesktop,
            ShieldsUp
        }

        public async Task<ProfileApplyResult> ApplyProfile(ProfileType profile, IProgress<string> progress = null)
        {
            var result = new ProfileApplyResult { ProfileName = profile.ToString() };

            switch (profile)
            {
                case ProfileType.BlockSharingDiscoveryRemoteDesktop:
                    await Toggle(result, progress, "File and Printer Sharing", false);
                    await Toggle(result, progress, "Network Discovery", false);
                    await Toggle(result, progress, "Remote Desktop", false);
                    break;
                case ProfileType.AllowSharingDiscovery:
                    await Toggle(result, progress, "File and Printer Sharing", true);
                    await Toggle(result, progress, "Network Discovery", true);
                    break;
                case ProfileType.AllowDiscoveryCast:
                    await Toggle(result, progress, "Network Discovery", true);
                    await Toggle(result, progress, "Cast to Device functionality", true);
                    break;
                case ProfileType.ShieldsUpMdns:
                {
                    progress?.Report("Shields Up + mDNS: disabling all grouped rules…");
                    var shield = await ApplyShieldsUpMdns();
                    result.Groups.Add(shield);
                    result.TotalMatched += shield.Matched;
                    result.TotalChanged += shield.Changed;
                    result.TotalFailed += shield.Failed;
                    if (shield.FirstError != null && result.FirstError == null) result.FirstError = shield.FirstError;
                    result.Notes = shield.Notes;
                    break;
                }
                case ProfileType.AllowSharingDiscoveryRemoteDesktop:
                    await Toggle(result, progress, "File and Printer Sharing", true);
                    await Toggle(result, progress, "Network Discovery", true);
                    await Toggle(result, progress, "Remote Desktop", true);
                    break;
                case ProfileType.ShieldsUp:
                {
                    progress?.Report("Shields Up: restoring Windows defaults…");
                    var lockdown = await FirewallService.Instance.ResetToDefaultsAndLockdownAsync(progress);
                    result.Groups.Add(lockdown);
                    result.TotalMatched += lockdown.Matched;
                    result.TotalChanged += lockdown.Changed;
                    result.TotalFailed += lockdown.Failed;
                    if (lockdown.FirstError != null && result.FirstError == null) result.FirstError = lockdown.FirstError;
                    result.Notes = lockdown.Notes;
                    break;
                }
            }

            return result;
        }

        private async Task Toggle(ProfileApplyResult acc, IProgress<string> progress, string group, bool enable)
        {
            progress?.Report($"{(enable ? "Enabling" : "Disabling")} group '{group}'…");
            GroupToggleResult r = enable ? await EnableGroup(group) : await DisableGroup(group);
            acc.Groups.Add(r);
            acc.TotalMatched += r.Matched;
            acc.TotalChanged += r.Changed;
            acc.TotalFailed += r.Failed;
            if (r.FirstError != null && acc.FirstError == null) acc.FirstError = r.FirstError;
        }

        private Task<GroupToggleResult> ApplyShieldsUpMdns()
        {
            // The engine owns the Shield Up two-pass and its widened
            // WSL/Hyper-V-safe whitelist (Engines.Firewall/Profiles.fs) — one
            // source of truth. This facade used to carry its own COM copy
            // with the narrow mDNS+Core-Networking whitelist, which is what
            // silently killed WSL2/Hyper-V egress (2026-10-30 field report §3).
            return ApplyEnginePresetAsync(AutoCommand.Engines.Firewall.ProfileType.ShieldsUpMdns);
        }

        private static async Task<GroupToggleResult> ApplyEnginePresetAsync(AutoCommand.Engines.Firewall.ProfileType profile)
        {
            var presets = new AutoCommand.Engines.Firewall.ProfilePresets(FirewallService.Instance);
            var result = await presets.ApplyProfile(profile);
            return result.Groups.Count > 0
                ? result.Groups[0]
                : new GroupToggleResult { GroupName = profile.ToString() };
        }

        private Task<GroupToggleResult> EnableGroup(string group)
        {
            return FirewallService.Instance.ToggleGroupAsync(group, true);
        }

        private Task<GroupToggleResult> DisableGroup(string group)
        {
            return FirewallService.Instance.ToggleGroupAsync(group, false);
        }
    }
}
