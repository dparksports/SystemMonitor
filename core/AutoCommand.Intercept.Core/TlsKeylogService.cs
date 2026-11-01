using System;
using System.Collections.Generic;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;

namespace AutoCommand.Intercept.Core
{
    /// <summary>
    /// Instance core of the TLS keylog engine: reads and tails the
    /// NSS-format keylog that instrumented python processes write
    /// (SSLKEYLOGFILE → pytrace\sslkeys.log) and owns the file's hygiene:
    /// created with a current-user-only ACL, rotated at 5 MB, the rotation
    /// leftover deleted at app exit, and a no-keylog flag file that makes
    /// the hook stop setting the env var entirely. The directory is
    /// constructor-injected — a test owns its store outright, the app uses
    /// the machine location.
    ///
    /// Secrets live in memory only — they are never exported or logged.
    /// </summary>
    public sealed class TlsKeylogStore
    {
        private readonly string _dir;

        public TlsKeylogStore() : this(null) { }

        public TlsKeylogStore(string dir)
        {
            _dir = dir ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AutoCommand", "pytrace");
        }

        public string Dir => _dir;
        public string KeylogPath => Path.Combine(Dir, "sslkeys.log");
        public string OldKeylogPath => KeylogPath + ".old";
        public string NoKeylogFlagPath => Path.Combine(Dir, "no-keylog");

        private const long RotateAtBytes = 5 * 1024 * 1024;

        // client_random (uppercase hex) → label → secret bytes
        private readonly Dictionary<string, Dictionary<string, byte[]>> _secrets =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly object _lock = new();
        private long _readOffset;
        private long _fileGeneration = -1;

        public event Action<string> OnError;

        /// <summary>False when the user turned TLS keylogging off (flag file present).</summary>
        public bool KeylogEnabled => !File.Exists(NoKeylogFlagPath);

        /// <summary>Creates the keylog file with a current-user-only ACL if absent, rotates when oversized.</summary>
        public void EnsureHygiene()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                if (!File.Exists(KeylogPath))
                    CreateWithUserOnlyAcl(KeylogPath);
                else if (new FileInfo(KeylogPath).Length > RotateAtBytes)
                    Rotate();
            }
            catch (Exception)
            {
                // hygiene is best-effort — decryption simply finds no secrets
            }
        }

        private void CreateWithUserOnlyAcl(string path)
        {
            using (File.Create(path, 4096, FileOptions.None)) { }
            var security = new FileSecurity();
            security.SetAccessRuleProtection(true, false); // nothing inherited: current user only
            security.AddAccessRule(new FileSystemAccessRule(
                WindowsIdentity.GetCurrent().User,
                FileSystemRights.FullControl, AccessControlType.Allow));
            new FileInfo(path).SetAccessControl(security);
        }

        private void Rotate()
        {
            // Children open the log per record (append), so moving it under
            // them is safe; the .old from any previous rotation is dropped.
            File.Delete(OldKeylogPath);
            File.Move(KeylogPath, OldKeylogPath);
            CreateWithUserOnlyAcl(KeylogPath);
        }

        /// <summary>App-exit cleanup — call from MainWindow.OnClosed.</summary>
        public void CleanupAtExit()
        {
            try { File.Delete(OldKeylogPath); } catch { }
        }

        /// <summary>
        /// Turns keylogging off for future instrumented launches: wipes the
        /// log and drops the flag file the hook checks before user code runs.
        /// </summary>
        public void DisableKeylogging()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                File.WriteAllText(NoKeylogFlagPath, "TLS keylogging disabled from AutoCommand settings");
                File.Delete(KeylogPath);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Could not disable keylogging: {ex.Message}", ex);
            }
        }

        /// <summary>Re-enables keylogging (removes the flag, recreates the ACL'd log).</summary>
        public void EnableKeylogging()
        {
            try
            {
                File.Delete(NoKeylogFlagPath);
                EnsureHygiene();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Could not enable keylogging: {ex.Message}", ex);
            }
        }

        /// <summary>Parses any new lines appended since the last call.</summary>
        public void Refresh()
        {
            try
            {
                var fi = new FileInfo(KeylogPath);
                if (!fi.Exists)
                {
                    lock (_lock) { _secrets.Clear(); _readOffset = 0; _fileGeneration = -1; }
                    return;
                }

                long generation = fi.CreationTimeUtc.Ticks ^ fi.Length;
                bool rotated = generation != _fileGeneration && _fileGeneration != -1;
                long offset = rotated ? 0 : _readOffset;
                if (!rotated && fi.Length <= offset) return;

                using var fs = new FileStream(KeylogPath, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);
                fs.Seek(offset, SeekOrigin.Begin);
                using var reader = new StreamReader(fs);

                string line;
                var parsed = new List<(string, string, byte[])>(16);
                while ((line = reader.ReadLine()) != null)
                {
                    var entry = ParseLine(line);
                    if (entry.HasValue) parsed.Add(entry.Value);
                }

                lock (_lock)
                {
                    foreach (var (randomHex, label, secret) in parsed)
                    {
                        if (!_secrets.TryGetValue(randomHex, out var labels))
                            _secrets[randomHex] = labels = new Dictionary<string, byte[]>(8, StringComparer.Ordinal);
                        labels[label] = secret;
                    }
                    _readOffset = fs.Position;
                    _fileGeneration = generation;
                }
            }
            catch (IOException)
            {
                // mid-append read; the next Refresh retries from the last good offset
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"TLS keylog read failed: {ex.Message}");
            }
        }

        /// <summary>All secrets logged for the given 32-byte client random, keyed by label.</summary>
        public IReadOnlyDictionary<string, byte[]> GetSecrets(byte[] clientRandom)
        {
            string key = BytesToHex(clientRandom);
            lock (_lock)
            {
                if (!_secrets.TryGetValue(key, out var labels)) return null;
                return new Dictionary<string, byte[]>(labels, StringComparer.Ordinal);
            }
        }

        public int SessionCount { get { lock (_lock) return _secrets.Count; } }

        /// <summary>
        /// NSS keylog line: "LABEL client_random_hex secret_hex". TLS 1.2 uses
        /// CLIENT_RANDOM; TLS 1.3 logs the handshake/application traffic
        /// secrets per direction (plus _N successors after KeyUpdate).
        /// </summary>
        private static (string randomHex, string label, byte[] secret)? ParseLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) return null;
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 3) return null;
            if (parts[1].Length % 2 != 0 || parts[2].Length % 2 != 0) return null;

            switch (parts[0])
            {
                case "CLIENT_RANDOM":
                case "CLIENT_HANDSHAKE_TRAFFIC_SECRET":
                case "SERVER_HANDSHAKE_TRAFFIC_SECRET":
                case "CLIENT_TRAFFIC_SECRET_0":
                case "SERVER_TRAFFIC_SECRET_0":
                    break;
                default:
                    return null; // EXPORTER_* and KeyUpdate successors are not needed for decryption
            }

            try
            {
                byte[] secret = HexToBytes(parts[2]);
                if (secret.Length < 16) return null;
                return (parts[1].ToUpperInvariant(), parts[0], secret);
            }
            catch
            {
                return null;
            }
        }

        internal static byte[] HexToBytes(string hex)
        {
            var bytes = new byte[hex.Length / 2];
            for (int i = 0; i < hex.Length / 2; i++)
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            return bytes;
        }

        internal static string BytesToHex(byte[] bytes)
        {
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes) sb.Append(b.ToString("X2"));
            return sb.ToString();
        }
    }

    /// <summary>
    /// Compatibility facade over a <see cref="TlsKeylogStore"/> — the app's
    /// static hygiene call sites and the instance reader call sites both
    /// compile unchanged. Static members forward to one default store (the
    /// machine location); instances carry their own injected store, so a
    /// test sweeps a temp dir with <c>new TlsKeylogService(dir)</c> and
    /// never touches real state. The old settable DirOverride global is
    /// gone with its set-before-use ordering trap.
    /// </summary>
    public class TlsKeylogService
    {
        private static readonly TlsKeylogStore Default = new();
        private readonly TlsKeylogStore _store;

        /// <summary>Reader over the machine pytrace dir (the app's usage).</summary>
        public TlsKeylogService() { _store = Default; }

        /// <summary>Reader over an injected dir (the test seam).</summary>
        public TlsKeylogService(string dir) { _store = new TlsKeylogStore(dir); }

        public event Action<string> OnError
        {
            add => _store.OnError += value;
            remove => _store.OnError -= value;
        }

        /// <summary>Parses any new lines appended since the last call.</summary>
        public void Refresh() => _store.Refresh();

        /// <summary>All secrets logged for the given 32-byte client random, keyed by label.</summary>
        public IReadOnlyDictionary<string, byte[]> GetSecrets(byte[] clientRandom) =>
            _store.GetSecrets(clientRandom);

        public int SessionCount => _store.SessionCount;

        // ── static hygiene facade (the machine location) ──────────────────

        public static string Dir => Default.Dir;
        public static string KeylogPath => Default.KeylogPath;
        public static string OldKeylogPath => Default.OldKeylogPath;
        public static string NoKeylogFlagPath => Default.NoKeylogFlagPath;

        /// <summary>False when the user turned TLS keylogging off (flag file present).</summary>
        public static bool KeylogEnabled => Default.KeylogEnabled;

        /// <summary>Creates the keylog file with a current-user-only ACL if absent, rotates when oversized.</summary>
        public static void EnsureHygiene() => Default.EnsureHygiene();

        /// <summary>App-exit cleanup — call from MainWindow.OnClosed.</summary>
        public static void CleanupAtExit() => Default.CleanupAtExit();

        /// <summary>Turns keylogging off for future instrumented launches.</summary>
        public static void DisableKeylogging() => Default.DisableKeylogging();

        /// <summary>Re-enables keylogging (removes the flag, recreates the ACL'd log).</summary>
        public static void EnableKeylogging() => Default.EnableKeylogging();

        internal static byte[] HexToBytes(string hex) => TlsKeylogStore.HexToBytes(hex);
        internal static string BytesToHex(byte[] bytes) => TlsKeylogStore.BytesToHex(bytes);
    }
}
