using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ShieldsUp.Core.Services
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
    /// Shields Up's named presets over the shared AutoCommand III firewall
    /// engine (Engines.Firewall through Contain.Core). The user-facing names
    /// (Strict Public / Home / Gaming / Office / Shield Up) are this app's
    /// language; every rule touch underneath goes through the same engine
    /// AutoCommand uses, so the two apps can never disagree about a group
    /// toggle or the AC-BLOCK rule family.
    /// </summary>
    public class FirewallProfileService
    {
        private static FirewallProfileService _instance;
        public static FirewallProfileService Instance => _instance ??= new FirewallProfileService();

        public enum ProfileType
        {
            Custom,
            StrictPublic,
            HomeTrusted,
            GamingMedia,
            ShieldUp,
            Office
        }

        public async Task<ProfileApplyResult> ApplyProfile(ProfileType profile, IProgress<string> progress = null)
        {
            var result = new ProfileApplyResult { ProfileName = profile.ToString() };

            switch (profile)
            {
                case ProfileType.StrictPublic:
                    await Toggle(result, progress, "File and Printer Sharing", false);
                    await Toggle(result, progress, "Network Discovery", false);
                    await Toggle(result, progress, "Remote Desktop", false);
                    break;
                case ProfileType.HomeTrusted:
                    await Toggle(result, progress, "File and Printer Sharing", true);
                    await Toggle(result, progress, "Network Discovery", true);
                    break;
                case ProfileType.GamingMedia:
                    await Toggle(result, progress, "Network Discovery", true);
                    await Toggle(result, progress, "Cast to Device functionality", true);
                    break;
                case ProfileType.ShieldUp:
                {
                    progress?.Report("Shield Up: disabling all grouped rules…");
                    var shield = await ApplyShieldUp();
                    result.Groups.Add(shield);
                    result.TotalMatched += shield.Matched;
                    result.TotalChanged += shield.Changed;
                    result.TotalFailed += shield.Failed;
                    if (shield.FirstError != null && result.FirstError == null) result.FirstError = shield.FirstError;
                    result.Notes = shield.Notes;
                    break;
                }
                case ProfileType.Office:
                    await Toggle(result, progress, "File and Printer Sharing", true);
                    await Toggle(result, progress, "Network Discovery", true);
                    await Toggle(result, progress, "Remote Desktop", true);
                    break;
            }

            return result;
        }

        private async Task Toggle(ProfileApplyResult acc, IProgress<string> progress, string group, bool enable)
        {
            progress?.Report($"{(enable ? "Enabling" : "Disabling")} group '{group}'…");
            GroupToggleResult r = await FirewallService.Instance.ToggleGroupAsync(group, enable);
            acc.Groups.Add(r);
            acc.TotalMatched += r.Matched;
            acc.TotalChanged += r.Changed;
            acc.TotalFailed += r.Failed;
            if (r.FirstError != null && acc.FirstError == null) acc.FirstError = r.FirstError;
        }

        // ── Shield Up (block all) ─────────────────────────────────────────
        // History: this preset was implemented locally because its whitelist
        // was WIDER than the engine's — the 2026-10-30 field report (§3)
        // caught a mass-disable with only mDNS + Core Networking surviving,
        // which silently took WSL2/Hyper-V VM egress down with it. The
        // widening has since been upstreamed into the engine
        // (Engines.Firewall/Profiles.fs), so this preset is a thin
        // delegation — see ApplyShieldUp below.

        private Task<GroupToggleResult> ApplyShieldUp()
        {
            // One whitelist truth: this used to be a local COM two-pass with
            // its own widened whitelist. The widening now lives upstream in
            // the engine (Engines.Firewall/Profiles.fs) — WSL/Hyper-V
            // infrastructure stays up — and the Contain.Core facade
            // delegates to it. This copy simply forwards.
            return Task.Run(async () =>
            {
                var facade = AutoCommand.Contain.Core.FirewallProfileService.Instance;
                var result = await facade.ApplyProfile(
                    AutoCommand.Contain.Core.FirewallProfileService.ProfileType.ShieldsUpMdns);
                return result.Groups.Count > 0
                    ? result.Groups[0]
                    : new GroupToggleResult { GroupName = "Shield Up (block all)" };
            });
        }
    }
}
