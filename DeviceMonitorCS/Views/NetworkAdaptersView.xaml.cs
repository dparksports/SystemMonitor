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
    public partial class NetworkAdaptersView : UserControl
    {
        public ObservableCollection<NetworkAdapterItem> AdaptersData { get; set; } = new ObservableCollection<NetworkAdapterItem>();

        public NetworkAdaptersView()
        {
            InitializeComponent();
            NetworkAdaptersGrid.ItemsSource = AdaptersData;

            RefreshAdaptersBtn.Click += (s, e) => LoadAdapters();
            
            DisableWifiDirectBtn.Click += (s, e) => DisableDevice("*Microsoft Wi-Fi Direct Virtual Adapter*", "Microsoft WiFi Direct Virtual Adapter");
            DisableWanMiniportBtn.Click += (s, e) => DisableDevice("*WAN Miniport*", "WAN Miniport");

            LoadAdapters();
        }

        private void LoadAdapters()
        {
            try
            {
                AdaptersData.Clear();
                var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_NetworkAdapter");
                foreach (ManagementObject obj in searcher.Get())
                {
                    AdaptersData.Add(new NetworkAdapterItem
                    {
                        Name = obj["Name"]?.ToString(),
                        Description = obj["Description"]?.ToString(),
                        Status = obj["NetConnectionStatus"]?.ToString() ?? "Unknown",
                        MacAddress = obj["MACAddress"]?.ToString(),
                        InterfaceType = obj["AdapterType"]?.ToString(),
                        DeviceID = obj["PNPDeviceID"]?.ToString()
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load network adapters: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DisableDevice(string namePattern, string deviceLabel)
        {
            try
            {
                var result = MessageBox.Show(
                    $"Are you sure you want to DISABLE '{deviceLabel}'?\n\nThis will likely require Administrator privileges.",
                    "Confirm Disable",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    // Use Get-NetAdapter instead of Get-PnpDevice for better stability with network interfaces
                    // Use InterfaceDescription to match FriendlyName-like patterns
                    string psCommand = $"Get-NetAdapter -IncludeHidden | Where-Object {{ $_.InterfaceDescription -like '{namePattern}' }} | Disable-NetAdapter -Confirm:$false -ErrorAction SilentlyContinue";
                    
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-Command \"{psCommand}\"",
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
                            MessageBox.Show($"Successfully sent disable command for: {deviceLabel}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                            LoadAdapters();
                        }
                        else
                        {
                            string err = process.StandardError.ReadToEnd();
                            MessageBox.Show($"Failed to disable device. Error: {err}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during disable: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AskAi_Click(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as System.Windows.Controls.MenuItem;
            var contextMenu = menuItem.Parent as System.Windows.Controls.ContextMenu;
            var grid = contextMenu.PlacementTarget as System.Windows.Controls.DataGrid;

            if (grid != null && grid.SelectedItem != null)
            {
                var window = new AskAiWindow(grid.SelectedItem);
                window.Owner = Window.GetWindow(this); // Important change: Get Owner from UserControl's container
                window.ShowDialog();
            }
        }
    }
}
