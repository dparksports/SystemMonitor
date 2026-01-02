using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Management;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace DeviceMonitorCS.Views
{
    public partial class DeviceManagementView : UserControl
    {
        public ObservableCollection<PnpDeviceInfo> DeviceList { get; set; } = new ObservableCollection<PnpDeviceInfo>();

        public DeviceManagementView()
        {
            InitializeComponent();
            DeviceGrid.ItemsSource = DeviceList;
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            await RefreshDataAsync();
        }

        private async void RefreshBtn_Click(object sender, RoutedEventArgs e)
        {
            await RefreshDataAsync();
        }

        private async Task RefreshDataAsync()
        {
            RefreshBtn.IsEnabled = false;
            RefreshBtn.Content = "Refreshing...";
            
            try
            {
                var devices = await Task.Run(() => GetPnpDevices());
                
                DeviceList.Clear();
                int okCount = 0;
                
                foreach (var device in devices)
                {
                    DeviceList.Add(device);
                    if (device.Status?.Equals("OK", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        okCount++;
                    }
                }
                
                TotalDeviceCount.Text = DeviceList.Count.ToString();
                ActiveDeviceCount.Text = okCount.ToString();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error refreshing devices: {ex.Message}", "Device Management Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                RefreshBtn.IsEnabled = true;
                RefreshBtn.Content = "Refresh Devices";
            }
        }

        private List<PnpDeviceInfo> GetPnpDevices()
        {
            var results = new List<PnpDeviceInfo>();
            
            // Replicating PowerShell logic: Get-PnpDevice -Class Keyboard, Mouse, Monitor
            // Classes: Keyboard, Mouse, Monitor
            string[] classes = { "Keyboard", "Mouse", "Monitor" };
            
            try
            {
                // We'll use WMI Win32_PnPEntity as it's the standard for this data in C#.
                // Filtering by PNPClass to match PowerShell's -Class parameter behavior.
                string query = $"SELECT PNPClass, Status, Name, DeviceID FROM Win32_PnPEntity WHERE " + 
                               string.Join(" OR ", classes.Select(c => $"PNPClass = '{c}'"));

                using (var searcher = new ManagementObjectSearcher(query))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        results.Add(new PnpDeviceInfo
                        {
                            Class = obj["PNPClass"]?.ToString() ?? "Unknown",
                            Status = obj["Status"]?.ToString() ?? "Unknown",
                            FriendlyName = obj["Name"]?.ToString() ?? "Unknown Device",
                            InstanceId = obj["DeviceID"]?.ToString() ?? "N/A"
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WMI Query Error: {ex.Message}");
            }

            return results.OrderBy(d => d.Class).ThenBy(d => d.FriendlyName).ToList();
        }
    }

    public class PnpDeviceInfo
    {
        public string Class { get; set; }
        public string Status { get; set; }
        public string FriendlyName { get; set; }
        public string InstanceId { get; set; }
    }
}
