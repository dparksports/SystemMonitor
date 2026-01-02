using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Management.Automation; // Required for PowerShell
using System.Management.Automation.Runspaces; // Required for InitialSessionState
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace DeviceMonitorCS.Views
{
    public partial class DeviceManagementView : UserControl
    {
        public ObservableCollection<PnpDeviceInfo> ConnectedList { get; set; } = new ObservableCollection<PnpDeviceInfo>();
        public ObservableCollection<PnpDeviceInfo> DisconnectedList { get; set; } = new ObservableCollection<PnpDeviceInfo>();

        public DeviceManagementView()
        {
            InitializeComponent();
            ConnectedGrid.ItemsSource = ConnectedList;
            DisconnectedGrid.ItemsSource = DisconnectedList;
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            // Lazy loading: Do not load automatically. User must click Refresh.
            // await RefreshDataAsync();
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
                var allDevices = await Task.Run(() => PnpDeviceReader.GetDevices(new[] { "Keyboard", "Mouse", "Monitor" }));
                
                ConnectedList.Clear();
                DisconnectedList.Clear();
                
                // Identify Connected Devices (Status == "OK")
                var connected = allDevices.Where(d => string.Equals(d.Status, "OK", StringComparison.OrdinalIgnoreCase)).ToList();

                foreach (var d in connected)
                {
                    ConnectedList.Add(d);
                }

                // Identify Disconnected Devices (Status != "OK")
                var disconnected = allDevices.Where(d => 
                    !string.Equals(d.Status, "OK", StringComparison.OrdinalIgnoreCase)
                ).ToList();

                foreach (var d in disconnected)
                {
                    DisconnectedList.Add(d);
                }
                
                ConnectedCountText.Text = $"({ConnectedList.Count})";
                DisconnectedCountText.Text = $"({DisconnectedList.Count})";
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
    }

    public class PnpDeviceInfo
    {
        public string Class { get; set; }
        public string Status { get; set; }
        public string FriendlyName { get; set; }
        public string InstanceId { get; set; }
    }

    public static class PnpDeviceReader
    {
        public static List<PnpDeviceInfo> GetDevices(string[] classes)
        {
            var results = new List<PnpDeviceInfo>();

            try 
            {
                // Create a default session state and set ExecutionPolicy to Bypass for this session
                var iss = InitialSessionState.CreateDefault();
                iss.ExecutionPolicy = Microsoft.PowerShell.ExecutionPolicy.Bypass;
                
                using (var runspace = RunspaceFactory.CreateRunspace(iss))
                {
                    runspace.Open();
                    using (PowerShell ps = PowerShell.Create())
                    {
                        ps.Runspace = runspace;

                        // Build PowerShell command
                        string classList = string.Join(",", classes.Select(c => $"'{c}'"));
                        
                        // -SkipEditionCheck is crucial when running Windows modules (5.1) from .NET SDK (Core)
                        // Note: ExecutionPolicy is now handled by the SessionState
                        string script = $@"
                            $env:PSModulePath = $env:PSModulePath + ';C:\Windows\System32\WindowsPowerShell\v1.0\Modules'
                            Import-Module PnpDevice -SkipEditionCheck -ErrorAction Stop
                            Get-PnpDevice -Class {classList} | 
                            Select-Object Class, Status, FriendlyName, InstanceId
                        ";

                        ps.AddScript(script);

                        // Invoke and check for errors
                        // We catch the specific Import-Module error via exception usually, but checking streams is safe
                        var output = ps.Invoke();

                        if (ps.Streams.Error.Count > 0)
                        {
                            var errors = string.Join("\n", ps.Streams.Error.Select(e => e.ToString()));
                            // Only show if it's impactful. With SilentlyContinue on Get-PnpDevice, errors there are muted.
                            // Import-Module errors will show.
                            if (!string.IsNullOrWhiteSpace(errors))
                                MessageBox.Show($"PowerShell Errors:\n{errors}", "Debug: Powershell Error");
                        }
                        
                        // If no output and no errors, it might be a silent failure or actually 0 devices
                        if (output.Count == 0 && ps.Streams.Error.Count == 0)
                        {
                             // Only show debug if we really expected devices (which we do if classes are common)
                             MessageBox.Show("PowerShell execution finished but returned 0 devices.\nPossible env mismatch.", "Debug: 0 Devices");
                        }

                        foreach (var item in output)
                        {
                            if (item == null) continue;

                            results.Add(new PnpDeviceInfo
                            {
                                Class = item.Properties["Class"]?.Value?.ToString() ?? "Unknown",
                                Status = item.Properties["Status"]?.Value?.ToString() ?? "Unknown",
                                FriendlyName = item.Properties["FriendlyName"]?.Value?.ToString() ?? "Unknown Device",
                                InstanceId = item.Properties["InstanceId"]?.Value?.ToString() ?? "N/A"
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"C# Exception:\n{ex.Message}", "Debug: Exception");
            }

            return results;
        }
    }
}
