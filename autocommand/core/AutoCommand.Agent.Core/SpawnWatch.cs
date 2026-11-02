using System;

namespace AutoCommand.Models
{
    /// <summary>
    /// A user-defined spawn watch — "the moment a process like THIS is
    /// spawned by a parent like THAT, remember it, so nobody has to chase
    /// it after the fact". Rules are data, not code: one readable JSON
    /// file each under %LOCALAPPDATA%\AutoCommand\watches, re-read on
    /// every load, so a new rule never needs a rebuild (and a rule can be
    /// shared by copying the file's text).
    /// </summary>
    public class SpawnWatch
    {
        /// <summary>Display name; also the file stem, so keep it path-safe.</summary>
        public string Name { get; set; } = "new watch";

        /// <summary>
        /// Process image to watch, case-insensitive, '*' wildcards allowed
        /// ("updater*", "consent.exe"). Matched against the leaf name AND
        /// the full path, so "svchost.exe" hits regardless of directory.
        /// </summary>
        public string MatchProcess { get; set; } = "*.exe";

        /// <summary>
        /// Optional parent image filter — same matching rules. This is the
        /// svchost question: MatchParent "svchost.exe" restricts the watch
        /// to processes a service host spawned. Empty = any parent.
        /// </summary>
        public string MatchParent { get; set; } = "";

        /// <summary>
        /// Optional SCM service key ("bits", "usosvc") — restricts the match
        /// to spawns whose parent svchost command line claims that service
        /// (the -s token on the parent's command line). Empty = any.
        /// </summary>
        public string ServiceKey { get; set; } = "";

        /// <summary>
        /// Optional case-insensitive substring the new process's own
        /// command line must contain. Empty = any command line.
        /// </summary>
        public string CommandLineContains { get; set; } = "";

        /// <summary>
        /// When true, images matching allowlist.txt (in the watches
        /// directory, one wildcard pattern per line) never count as hits —
        /// subtraction, so "everything from svchost" stays readable
        /// instead of drowning in taskhostw/dllhost noise.
        /// </summary>
        public bool MinusAllowlist { get; set; } = false;

        public bool Enabled { get; set; } = true;

        /// <summary>Free-form note shown on the card.</summary>
        public string Notes { get; set; } = "";

        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        public override string ToString() => Name;
    }
}
