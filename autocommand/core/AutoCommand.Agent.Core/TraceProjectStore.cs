using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AutoCommand.Models;
using System.Text.Json;

namespace AutoCommand.Agent.Core
{
    /// <summary>
    /// Persistence for trace projects: one JSON file per project under
    /// %LOCALAPPDATA%\AutoCommand\traces\projects. Files are the interface —
    /// hand-edits and dropped-in files are picked up by the watcher, and a
    /// project definition is exactly the text of its file (that's the
    /// "share" story: copy the file, paste it on another machine).
    ///
    /// Two faces, the BlocksLedger pattern: <see cref="TraceProjectLibrary"/>
    /// is the instance core (constructor-injected directory — a test can own
    /// one outright, no ordering traps), and the static
    /// <see cref="TraceProjectStore"/> facade forwards to one default
    /// instance so every existing call site compiles untouched.
    /// </summary>
    public sealed class TraceProjectLibrary
    {
        private readonly string _projectsDirectory;
        private readonly JsonSerializerOptions JsonOpts = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        public TraceProjectLibrary() : this(null) { }

        /// <summary>Injected directory (null = the default machine location).</summary>
        public TraceProjectLibrary(string projectsDirectory)
        {
            _projectsDirectory = projectsDirectory ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AutoCommand", "traces", "projects");
        }

        public string ProjectsDirectory => _projectsDirectory;

        public List<TraceProject> Load()
        {
            var result = new List<TraceProject>();
            try
            {
                if (!Directory.Exists(_projectsDirectory)) return result;
                foreach (var file in Directory.GetFiles(_projectsDirectory, "*.json")
                             .OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
                {
                    try
                    {
                        var p = JsonSerializer.Deserialize<TraceProject>(
                            File.ReadAllText(file), JsonOpts);
                        if (p != null && !string.IsNullOrWhiteSpace(p.Name)) result.Add(p);
                    }
                    catch { /* one unreadable file must not take the tab down */ }
                }
            }
            catch { /* unreadable dir — empty list is honest */ }
            return result;
        }

        private string PathFor(string name) =>
            Path.Combine(_projectsDirectory, SafeFileName(name) + ".json");

        /// <summary>File-system-safe stem for a project name (letters,
        /// digits, space, dash, underscore; everything else becomes '-').</summary>
        public string SafeFileName(string name)
        {
            var chars = (name ?? "project").Trim().Select(c =>
                char.IsLetterOrDigit(c) || c == ' ' || c == '-' || c == '_' ? c : '-');
            var stem = new string(chars.ToArray()).Trim();
            return stem.Length == 0 ? "project" : stem;
        }

        public void Save(TraceProject project)
        {
            Directory.CreateDirectory(_projectsDirectory);
            File.WriteAllText(PathFor(project.Name),
                JsonSerializer.Serialize(project, JsonOpts));
        }

        public bool Delete(string name)
        {
            var path = PathFor(name);
            if (!File.Exists(path)) return false;
            File.Delete(path);
            return true;
        }

        /// <summary>Creates the projects directory with a seed project the
        /// first time the Traces tab opens, so the tab never greets with an
        /// empty list.</summary>
        public void EnsureSeeded()
        {
            try
            {
                if (Directory.Exists(_projectsDirectory) &&
                    Directory.GetFiles(_projectsDirectory, "*.json").Length > 0) return;
                Save(new TraceProject
                {
                    Name = "svchost — all services",
                    MatchProcess = "svchost.exe",
                    ServiceKey = "",
                    Notes = "Starter project: every svchost instance together. For the deep " +
                            "1-second capture use the Svchost Trace tab; for one service, make a " +
                            "copy of this project and set its service key (e.g. diagtrack).",
                });
            }
            catch { /* seeding is cosmetic — never fatal */ }
        }
    }

    /// <summary>
    /// Static facade over one default <see cref="TraceProjectLibrary"/> —
    /// every existing call site (Traces tab, agent bridge tools) compiles
    /// untouched. The old settable ProjectsDirectory is gone with its
    /// set-before-first-use ordering trap; tests construct a
    /// TraceProjectLibrary outright instead.
    /// </summary>
    public static class TraceProjectStore
    {
        private static readonly TraceProjectLibrary Default = new();

        public static string ProjectsDirectory => Default.ProjectsDirectory;

        public static List<TraceProject> Load() => Default.Load();

        public static string SafeFileName(string name) => Default.SafeFileName(name);

        public static void Save(TraceProject project) => Default.Save(project);

        public static bool Delete(string name) => Default.Delete(name);

        public static void EnsureSeeded() => Default.EnsureSeeded();
    }
}
