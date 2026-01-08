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
                        
                        // User specifically requested to uninstall wan miniports when stopping sstpsvc
                        DeviceMonitorCS.Helpers.WanMiniportRemover.Execute();
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
    }
}
