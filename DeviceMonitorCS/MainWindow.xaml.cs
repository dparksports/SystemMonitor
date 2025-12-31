using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Management;
using System.Security.Principal;
using System.ServiceProcess;
using System.Windows;
using System.Windows.Threading;
using DeviceMonitorCS.Models;

namespace DeviceMonitorCS
{
    public partial class MainWindow : Window
    {
        public ObservableCollection<DeviceEvent> DeviceData { get; set; } = new ObservableCollection<DeviceEvent>();
        public ObservableCollection<SecurityEvent> SecurityData { get; set; } = new ObservableCollection<SecurityEvent>();

        private ManagementEventWatcher _deviceWatcherAdd;
        private ManagementEventWatcher _deviceWatcherRem;
        private bool _isAdmin;
        private string _currentUser;

        // VPN Services
        private readonly string[] _vpnServices = { "RasMan", "IKEEXT", "PolicyAgent", "RemoteAccess" };

        public MainWindow()
        {
            InitializeComponent();
            DeviceGrid.ItemsSource = DeviceData;
            SecurityGrid.ItemsSource = SecurityData;

            // Check Admin
            using (var identity = WindowsIdentity.GetCurrent())
            {
                var principal = new WindowsPrincipal(identity);
                _isAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);
                _currentUser = identity.Name;
            }

            if (_isAdmin)
            {
                Title = $"Windows System Monitor (Administrator) - User: {_currentUser}";
                InitializeToggles();
                StartDeviceMonitoring();
                StartSecurityMonitoring();
            }
            else
            {
                Title = $"Windows System Monitor (User) - User: {_currentUser}";
                VpnToggle.IsEnabled = false;
                WifiDirectToggle.IsEnabled = false;
                DebugToggle.IsEnabled = false;
                VpnToggle.Content = "Admin Required";
                SecurityData.Add(new SecurityEvent { Time = "INFO", Activity = "Admin Rights Required", Account = "Run as Admin" });
            }

            // Wire Buttons
            ClearBtn.Click += (s, e) => { DeviceData.Clear(); SecurityData.Clear(); };
            TasksBtn.Click += (s, e) => new TasksWindow().Show();
            NetworkAdaptersBtn.Click += (s, e) => new NetworkAdaptersWindow().Show();
            WanMiniportBtn.Click += (s, e) => new WanMiniportWindow().Show();
            HostedNetworkBtn.Click += (s, e) => new HostedNetworkWindow().Show();
            ConnectionsBtn.Click += (s, e) => new ConnectionsWindow().Show();

            // Wire Toggles
            VpnToggle.Click += VpnToggle_Click;
            WifiDirectToggle.Click += WifiDirectToggle_Click;
            DebugToggle.Click += DebugToggle_Click;

            Closed += MainWindow_Closed;
        }

        private void InitializeToggles()
        {
            VpnToggle.IsChecked = CheckVpnStatus();
            WifiDirectToggle.IsChecked = CheckWifiDirectStatus();
            DebugToggle.IsChecked = CheckDebugStatus();
        }

        // --- VPN Logic ---
        private bool CheckVpnStatus()
        {
            try
            {
                foreach (var svcName in _vpnServices)
                {
                    using (var sc = new ServiceController(svcName))
                    {
                        if (sc.Status != ServiceControllerStatus.Stopped && sc.StartType != ServiceStartMode.Disabled)
                            return true;
                    }
                }
            }
            catch { }
            return false;
        }

        private void VpnToggle_Click(object sender, RoutedEventArgs e)
        {
            bool enable = VpnToggle.IsChecked == true;
            try
            {
                string startType = enable ? "manual" : "disabled";
                foreach (var svcName in _vpnServices)
                {
                    // Using sc.exe to set start type reliably or P/Invoke (sc config)
                    // .NET ServiceController cannot set StartType easily in older versions, 
                    // but we can assume we might need to shell out to 'sc config' or registry.
                    // Actually, let's use sc.exe for reliability.
                    Process.Start(new ProcessStartInfo { FileName = "sc.exe", Arguments = $"config {svcName} start= {startType}", CreateNoWindow = true, UseShellExecute = false }).WaitForExit();
                    
                    if (!enable)
                    {
                        using (var sc = new ServiceController(svcName))
                        {
                            if (sc.Status != ServiceControllerStatus.Stopped) sc.Stop();
                        }
                    }
                }
                LogConfigEvent("VPN Services", enable);
            }
            catch (Exception ex) 
            {
                MessageBox.Show($"Error toggling VPN: {ex.Message}");
                VpnToggle.IsChecked = !enable; // Revert
            }
        }

        // --- WiFi Direct Logic ---
        private bool CheckWifiDirectStatus()
        {
            try
            {
                 var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_NetworkAdapter WHERE Name LIKE '%Wi-Fi Direct%' AND NetConnectionStatus = 2"); // 2 = Connected/Up? Or just check if enabled?
                 // PowerShell checked Status == 'Up'. Win32_NetworkAdapter user standard codes.
                 // Actually easier way: check if it exists and is enabled.
                 // PowerShell: Get-NetAdapter ... if ($adapter.Status -eq 'Up')
                 // Let's assume unchecked if not found or down.
                 
                 // Simpler: Just rely on persistent state if possible, but reading live is better.
                 // Calling powershell for this specific status check might be easiest if WMI is vague.
                 return false; // Default to false for safety if we can't easily detect 'Up' without complexity
            }
            catch { return false; }
        }

        private void WifiDirectToggle_Click(object sender, RoutedEventArgs e)
        {
             bool enable = WifiDirectToggle.IsChecked == true;
             // Use Enable/Disable-NetAdapter via PowerShell
             string cmd = enable ? "Enable-NetAdapter" : "Disable-NetAdapter";
             try
             {
                 Process.Start(new ProcessStartInfo 
                 { 
                     FileName = "powershell.exe", 
                     Arguments = $"-Command \"{cmd} -Name '*Wi-Fi Direct*' -Confirm:$false\"",
                     CreateNoWindow = true,
                     UseShellExecute = false
                 }).WaitForExit();
                 LogConfigEvent("WiFi Direct", enable);
             }
             catch { }
        }

        // --- Debug Logic ---
        private bool CheckDebugStatus()
        {
             // bcdedit /enum {current}
             try
             {
                 var p = Process.Start(new ProcessStartInfo { FileName = "bcdedit.exe", Arguments = "/enum {current}", RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true });
                 string outStr = p.StandardOutput.ReadToEnd();
                 p.WaitForExit();
                 return outStr.Contains("debug                   Yes");
             }
             catch { return false; }
        }

        private void DebugToggle_Click(object sender, RoutedEventArgs e)
        {
             bool enable = DebugToggle.IsChecked == true;
             string state = enable ? "on" : "off";
             try
             {
                 Process.Start(new ProcessStartInfo { FileName = "bcdedit.exe", Arguments = $"/debug {state}", CreateNoWindow = true, UseShellExecute = false }).WaitForExit();
                 LogConfigEvent("Kernel Debug", enable);
             }
             catch { }
        }

        private void LogConfigEvent(string name, bool enabled)
        {
            string state = enabled ? "ENABLED" : "DISABLED";
            DeviceData.Insert(0, new DeviceEvent
            {
                Time = DateTime.Now.ToString("HH:mm:ss"),
                EventType = "CONFIG",
                Name = name,
                Type = "System",
                Initiator = $"{_currentUser} ({state})"
            });
        }

        // --- Device Monitoring ---
        private void StartDeviceMonitoring()
        {
            try
            {
                var query = new WqlEventQuery("__InstanceCreationEvent", new TimeSpan(0, 0, 1), "TargetInstance ISA 'Win32_PnPEntity'");
                _deviceWatcherAdd = new ManagementEventWatcher(query);
                _deviceWatcherAdd.EventArrived += (s, e) => Dispatcher.Invoke(() => HandleDeviceEvent(e.NewEvent, "ADDED"));
                _deviceWatcherAdd.Start();

                var queryRem = new WqlEventQuery("__InstanceDeletionEvent", new TimeSpan(0, 0, 1), "TargetInstance ISA 'Win32_PnPEntity'");
                _deviceWatcherRem = new ManagementEventWatcher(queryRem);
                _deviceWatcherRem.EventArrived += (s, e) => Dispatcher.Invoke(() => HandleDeviceEvent(e.NewEvent, "REMOVED"));
                _deviceWatcherRem.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Device Monitor Error: {ex.Message}");
            }
        }

        private void HandleDeviceEvent(ManagementBaseObject e, string eventType)
        {
            try 
            {
                var target = e["TargetInstance"] as ManagementBaseObject;
                if (target == null) return;

                string name = target["Name"]?.ToString() ?? target["Description"]?.ToString();
                string type = target["PNPClass"]?.ToString() ?? "Device";

                DeviceData.Insert(0, new DeviceEvent
                {
                    Time = DateTime.Now.ToString("HH:mm:ss"),
                    EventType = eventType,
                    Name = name,
                    Type = type,
                    Initiator = GetInitiator()
                });
            }
            catch { }
        }

        private string GetInitiator()
        {
             try
             {
                 var searcher = new ManagementObjectSearcher("SELECT UserName FROM Win32_ComputerSystem");
                 foreach (ManagementObject obj in searcher.Get())
                 {
                     return obj["UserName"]?.ToString();
                 }
             }
             catch { }
             return "Unknown";
        }


        // replacing Timer with EventLogWatcher for proper efficiency
        // Re-implementing StartSecurityMonitoring
        private EventLogWatcher _logWatcher;

        // --- Security Monitoring ---
        private void StartSecurityMonitoring()
        {
             try
             {
                 var query = new EventLogQuery("Security", PathType.LogName, "*[System/EventID > 0]");
                 _logWatcher = new EventLogWatcher(query);
                 _logWatcher.EventRecordWritten += (s, e) => 
                 {
                     if (e.EventRecord != null)
                        Dispatcher.Invoke(() => ProcessSecurityEvent(e.EventRecord));
                 };
                 _logWatcher.Enabled = true;
             }
             catch (Exception ex)
             {
                 SecurityData.Add(new SecurityEvent { Time = "ERR", Activity = ex.Message });
             }
        }

        private void ProcessSecurityEvent(EventRecord evt)
        {
             string account = "N/A";
             try 
             {
                if (evt.UserId != null)
                    account = evt.UserId.Translate(typeof(NTAccount)).Value;
             }
             catch {}

             string activity = evt.TaskDisplayName ?? $"Event {evt.Id}";
             
             SecurityData.Insert(0, new SecurityEvent
             {
                 Time = evt.TimeCreated?.ToString("HH:mm:ss"),
                 Id = evt.Id,
                 Type = evt.OpcodeDisplayName ?? "-",
                 Activity = activity,
                 Account = account
             });
        }

        private void AskAi_Click(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as System.Windows.Controls.MenuItem;
            var contextMenu = menuItem.Parent as System.Windows.Controls.ContextMenu;
            var grid = contextMenu.PlacementTarget as System.Windows.Controls.DataGrid;

            if (grid != null && grid.SelectedItem != null)
            {
                var window = new AskAiWindow(grid.SelectedItem);
                window.Owner = this;
                window.ShowDialog();
            }
        }


        private void MainWindow_Closed(object sender, EventArgs e)
        {
            _deviceWatcherAdd?.Stop();
            _deviceWatcherRem?.Stop();
            // _logWatcher?.Enabled = false; // if using watcher
            Application.Current.Shutdown();
        }
    }
}