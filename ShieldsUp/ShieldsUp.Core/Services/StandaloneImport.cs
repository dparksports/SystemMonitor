using System.Diagnostics;
using System.Text.Json;

namespace ShieldsUp.Core.Services;

/// <summary>
/// One-time adoption of state created by the standalone Shields Up app
/// (ProgramData\ShieldsUp) into the host's machine root, so a user moving
/// from the standalone app keeps their edited bloatware list, their telemetry
/// revert history and their update-freeze ledger. Idempotent: files copy only
/// when the target does not exist yet; firewall_config.json MERGES (host
/// entries win) rather than copies — AutoCommand's drift enforcer may already
/// own rule intents there.
/// </summary>
public static class StandaloneImport
{
    public static string LegacyRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ShieldsUp");

    private static readonly string[] CopiedFiles =
    {
        "bloatware_config.json",
        "telemetry_state.json",
        "update_freeze.json",
        "efi_baseline.json",
    };

    public static bool HasLegacyState() => Directory.Exists(LegacyRoot);

    /// <summary>True while the standalone Shields Up app is running — both
    /// apps flipping the same vendor switches and firewall groups would fight,
    /// so the host tab surfaces this as a warning banner.</summary>
    public static bool IsStandaloneRunning() =>
        Process.GetProcessesByName("ShieldsUp").Length > 0;

    public sealed record ImportReport(List<string> Imported, List<string> Merged, List<string> Skipped)
    {
        public bool DidAnything => Imported.Count > 0 || Merged.Count > 0;
    }

    public static ImportReport Import(string dataRoot, string legacyRoot = null)
    {
        // legacyRoot is injectable so HardenSelfTest can pin the merge rules
        // against a synthetic folder without touching the real one.
        legacyRoot ??= LegacyRoot;
        var imported = new List<string>();
        var merged = new List<string>();
        var skipped = new List<string>();
        if (!Directory.Exists(legacyRoot))
            return new ImportReport(imported, merged, skipped);

        foreach (var name in CopiedFiles)
        {
            string src = Path.Combine(legacyRoot, name);
            string dst = Path.Combine(dataRoot, name);
            if (!File.Exists(src) || File.Exists(dst)) { skipped.Add(name); continue; }
            try
            {
                Directory.CreateDirectory(dataRoot);
                File.Copy(src, dst);
                imported.Add(name);
            }
            catch { skipped.Add(name); }
        }

        // freeze_backups carries the saved ACLs that undo an update freeze —
        // without them a pre-existing freeze cannot be reverted exactly.
        string srcBackups = Path.Combine(legacyRoot, "freeze_backups");
        string dstBackups = Path.Combine(dataRoot, "freeze_backups");
        if (Directory.Exists(srcBackups) && !Directory.Exists(dstBackups))
        {
            try
            {
                Directory.CreateDirectory(dstBackups);
                foreach (var file in Directory.GetFiles(srcBackups))
                    File.Copy(file, Path.Combine(dstBackups, Path.GetFileName(file)));
                imported.Add("freeze_backups\\");
            }
            catch { }
        }

        // firewall_config: add only rule names the host file does not have
        // yet — never overwrite. The schema is identical on both sides
        // (rule name → "True"/"False"), which is why the merge is safe.
        MergeFirewallConfig(dataRoot, legacyRoot, merged, skipped);

        return new ImportReport(imported, merged, skipped);
    }

    private static void MergeFirewallConfig(string dataRoot, string legacyRoot, List<string> merged, List<string> skipped)
    {
        string srcCfg = Path.Combine(legacyRoot, "firewall_config.json");
        string dstCfg = Path.Combine(dataRoot, "firewall_config.json");
        if (!File.Exists(srcCfg)) { skipped.Add("firewall_config.json"); return; }

        try
        {
            var srcRules = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(srcCfg))
                           ?? new Dictionary<string, string>();
            var dstRules = File.Exists(dstCfg)
                ? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(dstCfg))
                  ?? new Dictionary<string, string>()
                : new Dictionary<string, string>();

            int added = 0;
            foreach (var kv in srcRules)
                if (!dstRules.ContainsKey(kv.Key)) { dstRules[kv.Key] = kv.Value; added++; }

            if (added > 0)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(dstCfg)!);
                File.WriteAllText(dstCfg, JsonSerializer.Serialize(
                    dstRules, new JsonSerializerOptions { WriteIndented = true }));
                merged.Add($"firewall_config.json (+{added} rule(s))");
            }
            else
            {
                skipped.Add("firewall_config.json");
            }
        }
        catch { skipped.Add("firewall_config.json"); }
    }
}
