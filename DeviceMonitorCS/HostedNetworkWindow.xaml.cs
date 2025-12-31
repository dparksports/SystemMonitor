using System;
using System.Diagnostics;
using System.Windows;

using System.Collections.ObjectModel;
using System.Management;
using DeviceMonitorCS.Models;

namespace DeviceMonitorCS
{
    public partial class HostedNetworkWindow : Window
    {
        public ObservableCollection<NetworkAdapterItem> AdaptersData { get; set; } = new ObservableCollection<NetworkAdapterItem>();
        public HostedNetworkWindow()
        {
            InitializeComponent();

            StopHostedNetworkBtn.Click += (s, e) => RunCommand("netsh", "wlan stop hostednetwork");
            DisableHostedNetworkBtn.Click += (s, e) => RunCommand("netsh", "wlan set hostednetwork mode=disallow");
            DisableWifiDirectBtn.Click += (s, e) => RunPowerShell("Get-PnpDevice | Where-Object { $_.FriendlyName -like '*Microsoft Wi-Fi Direct Virtual Adapter*' } | Disable-PnpDevice -Confirm:$false");
            DisableAx210Btn.Click += (s, e) => RunPowerShell("Get-PnpDevice | Where-Object { $_.FriendlyName -like '*Intel*Wi-Fi 6E AX210*' } | Disable-PnpDevice -Confirm:$false");
            
            RefreshBtn.Click += (s, e) => LoadAdapters();
            
            AdaptersGrid.ItemsSource = AdaptersData;
            LoadAdapters();
        }

        private void LoadAdapters()
        {
            try
            {
                AdaptersData.Clear();
                // Win32_NetworkAdapter returns all adapters (Physical, Virtual, Hidden)
                var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_NetworkAdapter");
                foreach (ManagementObject obj in searcher.Get())
                {
                    string status = obj["NetConnectionStatus"]?.ToString();
                    status = GetStatusString(status);

                    AdaptersData.Add(new NetworkAdapterItem
                    {
                        Name = obj["Name"]?.ToString(),
                        Description = obj["Description"]?.ToString(),
                        Status = status,
                        InterfaceType = obj["AdapterType"]?.ToString(),
                        DeviceID = obj["PNPDeviceID"]?.ToString()
                    });
                }
            }
            catch (Exception ex)
            {
                AppendOutput($"Error loading adapters: {ex.Message}");
            }
        }

        private string GetStatusString(string code)
        {
            if (string.IsNullOrEmpty(code)) return "Unknown/Disabled";
            switch (code)
            {
                case "0": return "Disconnected";
                case "1": return "Connecting";
                case "2": return "Connected";
                case "3": return "Disconnecting";
                case "7": return "Media Disconnected";
                case "22": return "Disabled"; // Note: WMI often returns null for disabled devices instead of 22
                default: return $"Code {code}";
            }
        }

        private void AppendOutput(string text)
        {
            ConsoleOutput.AppendText($"{DateTime.Now:HH:mm:ss} >_ {text}\n");
            ConsoleOutput.ScrollToEnd();
        }

        private void RunCommand(string fileName, string args)
        {
            AppendOutput($"Running: {fileName} {args}");
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    Verb = "runas"
                };

                using (var process = Process.Start(startInfo))
                {
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    if (!string.IsNullOrWhiteSpace(output)) AppendOutput(output);
                    if (!string.IsNullOrWhiteSpace(error)) AppendOutput($"ERR: {error}");
                    
                    if (process.ExitCode == 0) 
                    {
                        AppendOutput("Command completed successfully.");
                        LoadAdapters(); // Refresh list to show changes
                    }
                    else AppendOutput($"Command failed with exit code {process.ExitCode}.");
                }
            }
            catch (Exception ex)
            {
                AppendOutput($"Exception: {ex.Message}");
            }
            AppendOutput("--------------------------------------------------");
        }

        private void RunPowerShell(string script)
        {
            AppendOutput($"Running PowerShell: {script}");
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-Command \"{script}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    Verb = "runas"
                };

                using (var process = Process.Start(startInfo))
                {
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    if (!string.IsNullOrWhiteSpace(output)) AppendOutput(output);
                    if (!string.IsNullOrWhiteSpace(error)) AppendOutput($"ERR: {error}");
                    
                    if (process.ExitCode == 0) 
                    {
                        AppendOutput("PowerShell command completed successfully.");
                        LoadAdapters(); // Refresh list to show changes
                    }
                    else AppendOutput($"PowerShell command failed with exit code {process.ExitCode}.");
                }
            }
            catch (Exception ex)
            {
                AppendOutput($"Exception: {ex.Message}");
            }
            AppendOutput("--------------------------------------------------");
        }
    }
}
