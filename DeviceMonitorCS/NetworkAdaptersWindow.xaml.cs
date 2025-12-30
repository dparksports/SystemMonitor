using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Management;
using System.Windows;
using DeviceMonitorCS.Models;

namespace DeviceMonitorCS
{
    public partial class NetworkAdaptersWindow : Window
    {
        public ObservableCollection<NetworkAdapterItem> AdaptersData { get; set; } = new ObservableCollection<NetworkAdapterItem>();

        public NetworkAdaptersWindow()
        {
            InitializeComponent();
            NetworkAdaptersGrid.ItemsSource = AdaptersData;

            RefreshAdaptersBtn.Click += (s, e) => LoadAdapters();
            
            UninstallWifiDirectBtn.Click += (s, e) => UninstallDevice("*Microsoft Wi-Fi Direct Virtual Adapter*", "Microsoft WiFi Direct Virtual Adapter");
            UninstallWanMiniportBtn.Click += (s, e) => UninstallDevice("*WAN Miniport*", "WAN Miniport");
            UninstallBluetoothPanBtn.Click += (s, e) => UninstallDevice("*Bluetooth Device (Personal Area Network)*", "Bluetooth Device (Personal Area Network)");
            UninstallIntelWifiBtn.Click += (s, e) => UninstallDevice("*Intel*Wi-Fi 6E AX210*", "Intel WiFi 6E AX210");

            LoadAdapters();
        }

        private void LoadAdapters()
        {
            try
            {
                AdaptersData.Clear();
                // Use Win32_NetworkAdapter via WMI
                var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_NetworkAdapter");
                foreach (ManagementObject obj in searcher.Get())
                {
                    AdaptersData.Add(new NetworkAdapterItem
                    {
                        Name = obj["Name"]?.ToString(),
                        Description = obj["Description"]?.ToString(),
                        Status = obj["NetConnectionStatus"]?.ToString() ?? "Unknown", // integer codes usually, but this is simple port
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

        private void UninstallDevice(string namePattern, string deviceLabel)
        {
            try
            {
                // To find devices by pattern matching FriendlyName, we might need PowerShell logic or WMI query
                // WMI: Win32_PnPEntity where Name like '%...%'
                string wql = $"SELECT * FROM Win32_PnPEntity WHERE Name LIKE '{namePattern.Replace("*", "%")}'";
                var searcher = new ManagementObjectSearcher(wql);
                var devices = searcher.Get().Cast<ManagementObject>().ToList();

                if (devices.Count == 0)
                {
                   MessageBox.Show($"No devices found matching: {deviceLabel}", "Not Found", MessageBoxButton.OK, MessageBoxImage.Information);
                   return;
                }

                var result = MessageBox.Show(
                    $"Found {devices.Count} device(s) matching '{deviceLabel}'.\n\nAre you sure you want to uninstall? This may require a system restart.",
                    "Confirm Uninstall",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    int successCount = 0;
                    int failCount = 0;

                    foreach (var device in devices)
                    {
                        string instanceId = device["DeviceID"]?.ToString();
                        if (string.IsNullOrEmpty(instanceId)) continue;

                        try
                        {
                            var startInfo = new ProcessStartInfo
                            {
                                FileName = "pnputil.exe",
                                Arguments = $"/remove-device \"{instanceId}\"",
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                                UseShellExecute = false,
                                CreateNoWindow = true
                            };

                            using (var process = Process.Start(startInfo))
                            {
                                process.WaitForExit();
                                if (process.ExitCode == 0) successCount++;
                                else failCount++;
                            }
                        }
                        catch
                        {
                            failCount++;
                        }
                    }

                    if (successCount > 0 && failCount == 0)
                        MessageBox.Show($"Successfully uninstalled {successCount} device(s).\n\nChanges may require restart.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    else
                        MessageBox.Show($"Uninstalled {successCount} devices, {failCount} failed.", "Partial Success", MessageBoxButton.OK, MessageBoxImage.Warning);

                    LoadAdapters();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during uninstall: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
