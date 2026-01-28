using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace DeviceMonitorCS.Helpers
{
    public static class DbxRemediator
    {
        // Official UEFI.org DBX Update (x64)
        private const string DbxUrl = "https://uefi.org/sites/default/files/resources/dbxupdate_amd64.bin";
        private const string DbxFileName = "dbxupdate_amd64.bin";

        public static async Task<(string Path, string Checksum)> DownloadUpdateAsync()
        {
            string tempPath = Path.Combine(Path.GetTempPath(), DbxFileName);
            try
            {
                using (var client = new HttpClient())
                {
                    var data = await client.GetByteArrayAsync(DbxUrl);
                    await File.WriteAllBytesAsync(tempPath, data);
                    
                    using (var sha256 = System.Security.Cryptography.SHA256.Create())
                    {
                        var hash = sha256.ComputeHash(data);
                        return (tempPath, BitConverter.ToString(hash).Replace("-", ""));
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Download Failed: {ex.Message}");
                return (string.Empty, string.Empty);
            }
        }

        public static async Task<bool> InstallUpdateAsync(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;

            try
            {
                // 2. Apply via PowerShell (Set-SecureBootUEFI)
                var psCommand = $"Set-SecureBootUEFI -Name dbx -ContentFilePath '{path}'";

                var psi = new ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = $"-Command \"{psCommand}\"",
                    Verb = "runas", // Request Admin
                    UseShellExecute = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                var process = Process.Start(psi);
                if (process != null)
                {
                    await process.WaitForExitAsync();
                    return process.ExitCode == 0;
                }
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Installation Failed: {ex.Message}");
                return false;
            }
        }
    }
}
