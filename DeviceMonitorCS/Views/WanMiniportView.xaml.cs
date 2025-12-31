using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Windows;
using System.Windows.Controls;
using DeviceMonitorCS.Models;

namespace DeviceMonitorCS.Views
{
    public partial class WanMiniportView : UserControl
    {
        public ObservableCollection<NetworkAdapterItem> WanAdapters { get; set; } = new ObservableCollection<NetworkAdapterItem>();

        public WanMiniportView()
        {
            InitializeComponent();
            WanGrid.ItemsSource = WanAdapters;

            RefreshBtn.Click += (s, e) => LoadAdapters();
            
            // SSTP Service Controls
            DisableSstpBtn.Click += (s, e) => RunPowerShellCommand("Set-Service -Name SstpSvc -StartupType Disabled; Stop-Service -Name SstpSvc -Force", "Disable SstpSvc");
            StopSstpBtn.Click += (s, e) => RunPowerShellCommand("Stop-Service -Name SstpSvc -Force", "Stop SstpSvc");
            StartSstpBtn.Click += (s, e) => RunPowerShellCommand("Set-Service -Name SstpSvc -StartupType Manual; Start-Service -Name SstpSvc", "Start SstpSvc");

            // WAN Miniport Controls
            DisableAllWanBtn.Click += (s, e) => DisableAllWanMiniports();
            UninstallAllWanBtn.Click += (s, e) => UninstallAllWanMiniports();
            UninstallSstpBtn.Click += (s, e) => UninstallWanMiniportSstp();

            LoadAdapters();
        }

        private void LoadAdapters()
        {
            StatusText.Text = "Loading...";
            try
            {
                WanAdapters.Clear();
                var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_NetworkAdapter WHERE Name LIKE '%WAN Miniport%'");
                foreach (ManagementObject obj in searcher.Get())
                {
                    WanAdapters.Add(new NetworkAdapterItem
                    {
                        Name = obj["Name"]?.ToString(),
                        Description = obj["Description"]?.ToString(),
                        Status = obj["NetConnectionStatus"]?.ToString() ?? "Unknown",
                        InterfaceType = obj["AdapterType"]?.ToString(),
                        DeviceID = obj["PNPDeviceID"]?.ToString()
                    });
                }
                StatusText.Text = $"Loaded {WanAdapters.Count} adapters.";
            }
            catch (Exception ex)
            {
                StatusText.Text = "Error loading adapters.";
                MessageBox.Show($"Error: {ex.Message}");
            }
        }

        private void RunPowerShellCommand(string command, string actionName)
        {
            StatusText.Text = $"Executing: {actionName}...";
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-Command \"{command}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    Verb = "runas"
                };

                using (var process = Process.Start(startInfo))
                {
                    process.WaitForExit();
                    if (process.ExitCode == 0)
                    {
                        StatusText.Text = $"Success: {actionName}";
                        MessageBox.Show($"{actionName} completed successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        string err = process.StandardError.ReadToEnd();
                        StatusText.Text = $"Failed: {actionName}";
                        MessageBox.Show($"Failed to execute {actionName}.\nError: {err}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Exception: {actionName}";
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DisableAllWanMiniports()
        {
            if (MessageBox.Show("Disable ALL WAN Miniports?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                RunPowerShellCommand("Get-NetAdapter -IncludeHidden | Where-Object { $_.InterfaceDescription -like '*WAN Miniport*' } | Disable-NetAdapter -Confirm:$false -ErrorAction SilentlyContinue", "Disable All WAN Miniports");
                LoadAdapters();
            }
        }

        private void UninstallAllWanMiniports()
        {
             if (MessageBox.Show("Uninstall ALL WAN Miniports?\nNOTE: This logic uses pnputil to remove devices matching 'WAN Miniport'.", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
             {
                 try 
                 {
                     string wql = "SELECT * FROM Win32_PnPEntity WHERE Name LIKE '%WAN Miniport%'";
                     var searcher = new ManagementObjectSearcher(wql);
                     var devices = searcher.Get().Cast<ManagementObject>().ToList();
                     
                     int success = 0;
                     foreach(var dev in devices)
                     {
                         string id = dev["DeviceID"]?.ToString();
                         if(!string.IsNullOrEmpty(id))
                         {
                             var proc = Process.Start(new ProcessStartInfo {
                                 FileName = "pnputil.exe",
                                 Arguments = $"/remove-device \"{id}\"",
                                 CreateNoWindow = true,
                                 UseShellExecute = false
                             });
                             proc.WaitForExit();
                             if(proc.ExitCode == 0) success++;
                         }
                     }
                     
                     MessageBox.Show($"Uninstalled {success} devices. Please reboot if needed.", "Done", MessageBoxButton.OK, MessageBoxImage.Information);
                     LoadAdapters();
                 }
                 catch(Exception ex)
                 {
                     MessageBox.Show($"Error: {ex.Message}");
                 }
                }
            }

        private void UninstallWanMiniportSstp()
        {
             if (MessageBox.Show("Uninstall specifically 'WAN Miniport (SSTP)'?\nNOTE: This uses pnputil to remove the device.", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
             {
                 try 
                 {
                     string deviceId = null;

                     // Strategy 1: Win32_NetworkAdapter (Most reliable for "Network Adapters")
                     try 
                     {
                         var searcher = new ManagementObjectSearcher("SELECT PNPDeviceID FROM Win32_NetworkAdapter WHERE Name LIKE '%WAN Miniport (SSTP)%'");
                         foreach (ManagementObject obj in searcher.Get())
                         {
                             deviceId = obj["PNPDeviceID"]?.ToString();
                             if (!string.IsNullOrEmpty(deviceId)) break;
                         }
                     }
                     catch {}

                     // Strategy 2: Win32_PnPEntity (Broader search) if not found
                     if (string.IsNullOrEmpty(deviceId))
                     {
                         try
                         {
                            var searcher = new ManagementObjectSearcher("SELECT DeviceID FROM Win32_PnPEntity WHERE Name LIKE '%WAN Miniport%SSTP%'");
                            foreach (ManagementObject obj in searcher.Get())
                            {
                                deviceId = obj["DeviceID"]?.ToString();
                                if (!string.IsNullOrEmpty(deviceId)) break;
                            }
                         }
                         catch {}
                     }

                     // Strategy 3: PowerShell Get-PnpDevice (Final Fallback)
                     if (string.IsNullOrEmpty(deviceId))
                     {
                         try
                         {
                             var ps = new ProcessStartInfo
                             {
                                 FileName = "powershell.exe",
                                 Arguments = "-Command \"Get-PnpDevice -FriendlyName '*WAN Miniport (SSTP)*' | Select-Object -ExpandProperty InstanceId\"",
                                 RedirectStandardOutput = true,
                                 UseShellExecute = false,
                                 CreateNoWindow = true
                             };
                             using (var p = Process.Start(ps))
                             {
                                 string output = p.StandardOutput.ReadToEnd();
                                 p.WaitForExit();
                                 if (!string.IsNullOrWhiteSpace(output))
                                 {
                                     deviceId = output.Trim().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                                 }
                             }
                         }
                         catch {}
                     }

                     if (string.IsNullOrEmpty(deviceId))
                     {
                         MessageBox.Show("WAN Miniport (SSTP) not found via WMI or PowerShell scan.", "Not Found", MessageBoxButton.OK, MessageBoxImage.Information);
                         return;
                     }

                     var proc = Process.Start(new ProcessStartInfo {
                         FileName = "pnputil.exe",
                         Arguments = $"/remove-device \"{deviceId}\"",
                         CreateNoWindow = true,
                         UseShellExecute = false
                     });
                     proc.WaitForExit();
                     
                     if(proc.ExitCode == 0)
                     {
                        MessageBox.Show($"Uninstalled device successfully.\nID: {deviceId}\nPlease reboot if needed.", "Done", MessageBoxButton.OK, MessageBoxImage.Information);
                        LoadAdapters();
                     }
                     else
                     {
                        MessageBox.Show($"Failed to uninstall device.\nID: {deviceId}\nExit Code: {proc.ExitCode}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                     }
                 }
                 catch(Exception ex)
                 {
                     MessageBox.Show($"Error: {ex.Message}");
                 }
             }
        }
    }
}
