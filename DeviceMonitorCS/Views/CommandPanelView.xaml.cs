using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.ServiceProcess;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DeviceMonitorCS.Models;

namespace DeviceMonitorCS.Views
{
    public partial class CommandPanelView : UserControl
    {
        public ObservableCollection<NetworkAdapterItem> WanMiniports { get; set; } = new ObservableCollection<NetworkAdapterItem>();

        public CommandPanelView()
        {
            InitializeComponent();
            WanMiniportGrid.ItemsSource = WanMiniports;
        }

        public void InitializeAndLoad()
        {
            RefreshData();
        }

        private void RefreshBtn_Click(object sender, RoutedEventArgs e)
        {
            RefreshData();
        }

        private void RefreshData()
        {
            LoadSstpStatus();
            LoadWanMiniports();
        }

        // --- Microsoft VPN Hole (SSTP) ---

        private void LoadSstpStatus()
        {
            try
            {
                using (ServiceController sc = new ServiceController("SstpSvc"))
                {
                    string status = sc.Status.ToString();
                    string startType = GetServiceStartType("SstpSvc");
                    
                    SstpStatusText.Text = $"Status: {status} | Startup: {startType}";

                    if (sc.Status == ServiceControllerStatus.Running)
                    {
                        SstpToggleBtn.Content = "Stop & Disable";
                        SstpToggleBtn.Background = new SolidColorBrush(Color.FromRgb(255, 107, 107)); // Red
                    }
                    else
                    {
                        SstpToggleBtn.Content = "Start & Enable";
                        SstpToggleBtn.Background = new SolidColorBrush(Color.FromRgb(0, 122, 204)); // Blue
                    }
                }
            }
            catch (Exception ex)
            {
                SstpStatusText.Text = "Error reading service";
                SstpToggleBtn.IsEnabled = false;
                Debug.WriteLine($"SSTP Error: {ex.Message}");
            }
        }

        private void SstpToggleBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (ServiceController sc = new ServiceController("SstpSvc"))
                {
                    if (sc.Status == ServiceControllerStatus.Running)
                    {
                        // Stop and Disable
                        RunPowerShellCommand("Set-Service -Name SstpSvc -StartupType Disabled; Stop-Service -Name SstpSvc -Force", "Disable SSTP");
                    }
                    else
                    {
                        // Enable and Start
                        RunPowerShellCommand("Set-Service -Name SstpSvc -StartupType Manual; Start-Service -Name SstpSvc", "Enable SSTP");
                    }
                }
                RefreshData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error toggling service: {ex.Message}");
            }
        }

        private string GetServiceStartType(string serviceName)
        {
            try
            {
                using (ManagementObject service = new ManagementObject(new ManagementPath($"Win32_Service.Name='{serviceName}'")))
                {
                    return service["StartMode"]?.ToString();
                }
            }
            catch
            {
                return "Unknown";
            }
        }

        private void LoadWanMiniports()
        {
            try
            {
                WanMiniports.Clear();
                // Query for WAN Miniports
                var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_NetworkAdapter WHERE Name LIKE 'WAN Miniport%'");
                var adapters = searcher.Get();

                foreach (ManagementObject obj in adapters)
                {
                    WanMiniports.Add(new NetworkAdapterItem
                    {
                        Name = obj["Name"]?.ToString(),
                        Description = obj["Description"]?.ToString(),
                        Status = obj["NetConnectionStatus"]?.ToString() ?? "Unknown", // 0=Disconnected, 2=Connected, etc.
                        InterfaceType = obj["AdapterType"]?.ToString(),
                        DeviceID = obj["PNPDeviceID"]?.ToString()
                    });
                }
                
                // Add friendly status text if needed in the binded property or converter, but basic string is okay for now
                // NetConnectionStatus values: https://learn.microsoft.com/en-us/windows/win32/cimwin32prov/win32-networkadapter
                // We'll stick to raw or simple mapping if UI looks weird.
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading WAN Miniports: {ex.Message}");
            }
        }

        private void RunPowerShellCommand(string command, string actionName)
        {
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
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error executing command: {ex.Message}");
            }
        }

        private void UninstallSstpBtn_Click(object sender, RoutedEventArgs e)
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
                        RefreshData();
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

        private void UninstallAllWanNativeBtn_Click(object sender, RoutedEventArgs e)
        {
             if (MessageBox.Show("Uninstall ALL WAN Miniports using Native API?\n\nThis will attempt to remove:\n- WAN Miniport (IKEv2)\n- WAN Miniport (IP)\n- WAN Miniport (IPv6)\n- WAN Miniport (L2TP)\n- WAN Miniport (Network Monitor)\n- WAN Miniport (PPPOE)\n- WAN Miniport (PPTP)\n- WAN Miniport (SSTP)", "Confirm Native Uninstall", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
             {
                 try
                 {
                     var results = DeviceMonitorCS.Helpers.WanMiniportRemover.Execute();
                     string message = string.Join("\n", results);
                     
                     if (string.IsNullOrWhiteSpace(message)) message = "No actions taken or no devices found.";

                     MessageBox.Show(message, "Uninstall Results", MessageBoxButton.OK, MessageBoxImage.Information);
                     RefreshData();
                 }
                 catch (Exception ex)
                 {
                     MessageBox.Show($"Error: {ex.Message}", "Error");
                 }
             }
        }
    }
}
