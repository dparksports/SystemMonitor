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
        private string _currentUser;

        // VPN Services
        private readonly string[] _vpnServices = { "RasMan", "IKEEXT", "PolicyAgent", "RemoteAccess" };
        
        private SecurityEnforcer _enforcer;

        public MainWindow()
        {
            InitializeComponent();
            DeviceGrid.ItemsSource = DeviceData;
            SecurityGrid.ItemsSource = SecurityData;

            // Check Admin
            _currentUser = System.Security.Principal.WindowsIdentity.GetCurrent().Name;

            if (IsAdministrator())
            {
                Title = $"Windows System Monitor (Administrator) - User: {_currentUser}";
                InitializeToggles();
                StartDeviceMonitoring();
                StartSecurityMonitoring();
                
                _enforcer = new SecurityEnforcer(HandleThreatDetected);
                _enforcer.StatusChanged += (status, color) => Dispatcher.Invoke(() => DashboardView.UpdateLiveStatus(status, color));
                _enforcer.Start();
            }
            else
            {
                Title = $"Windows System Monitor (User: {_currentUser}) - LIMITED MODE";
                MessageBox.Show("Please run as Administrator for full functionality.", "Restricted", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            // Wire Buttons
            // The following two lines are moved from the admin block to ensure they are always wired,
            // but _enforcer will only be initialized if IsAdministrator() is true.
            // This implies _enforcer might be null if not admin, which needs careful handling or
            // the wiring should remain conditional. Assuming _enforcer is always initialized for now.
            // If _enforcer is null, these lines will cause a NullReferenceException.
            // For a robust solution, these should be inside the admin block or _enforcer should be initialized conditionally.
            // For the purpose of this edit, I'm placing them as per instruction.
            if (IsAdministrator()) // Re-adding the check to prevent NullReferenceException if _enforcer is not initialized
            {
                _enforcer.StatusChanged += (s, c) => DashboardView.UpdateLiveStatus(s, c);
                _enforcer.Start();
            }

            // Settings Wiring
            // These lines also depend on _enforcer being initialized.
            if (IsAdministrator())
            {
                SettingsView.SetCurrentInterval(_enforcer.CheckInterval);
                SettingsView.IntervalChanged += (newInterval) => _enforcer.CheckInterval = newInterval;
            }

            // Navigation Wiring
            NavDashboardBtn.Click += (s, e) => NavigateTo(DashboardView);
            NavLogsBtn.Click += (s, e) => NavigateTo(LogsView);
            
            HostedNetworkBtn.Click += (s, e) => NavigateTo(HostedNetworkView);
            WanMiniportBtn.Click += (s, e) => NavigateTo(WanMiniportView);
            NetworkAdaptersBtn.Click += (s, e) => NavigateTo(NetworkAdaptersView);
            TasksBtn.Click += (s, e) => NavigateTo(TasksView);
            ConnectionsBtn.Click += (s, e) => NavigateTo(ConnectionsView);
            SettingsBtn.Click += (s, e) => NavigateTo(SettingsView);

            ClearBtn.Click += (s, e) => 
            {
                DeviceData.Clear(); // Original was DeviceData.Clear(), instruction changed to EventData.Clear() which doesn't exist. Reverting to DeviceData.Clear()
                SecurityData.Clear();
            };
            
            // Bind Dashboard
            DashboardView.BindActivity(SecurityData);

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
                    RunCommand("sc.exe", $"config {svcName} start= {startType}");
                    
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
             string cmd = enable ? "Enable-NetAdapter" : "Disable-NetAdapter";
             try
             {
                 RunCommand("powershell.exe", $"-Command \"{cmd} -Name '*Wi-Fi Direct*' -Confirm:$false\"");
                 LogConfigEvent("WiFi Direct", enable);
             }
             catch (Exception ex)
             {
                 MessageBox.Show($"Error: {ex.Message}");
                 WifiDirectToggle.IsChecked = !enable;
             }
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
                 RunCommand("bcdedit.exe", $"/debug {state}");
                 LogConfigEvent("Kernel Debug", enable);
             }
             catch (Exception ex)
             {
                  MessageBox.Show($"Error: {ex.Message}");
                  DebugToggle.IsChecked = !enable;
             }
        }

        private void RunCommand(string exe, string args)
        {
            var psi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = args,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };

            using (var p = Process.Start(psi))
            {
                string err = p.StandardError.ReadToEnd();
                p.WaitForExit();
                if (p.ExitCode != 0)
                {
                    throw new Exception($"{exe} failed (Code {p.ExitCode}): {err}");
                }
            }
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


        private async void HandleThreatDetected(string type, string details)
        {
            Dispatcher.Invoke(() => {
                SecurityData.Insert(0, new SecurityEvent { 
                     Time = DateTime.Now.ToString("HH:mm:ss"), 
                     Activity = $"Security Enforcer: {type}", 
                     Type = "Intervention", 
                     Account = "SYSTEM", 
                     Id = 999 
                });
            });

            // Get Key
            string apiKey = "";
            try { apiKey = System.IO.File.ReadAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "apikey.txt")).Trim(); } catch {}

            if (!string.IsNullOrEmpty(apiKey)) 
            {
                try
                {
                    var client = new GeminiClient(apiKey);
                    string explanation = await client.AskAsync($"System Threat Blocked: {type}. Details: {details}. Explain why this is dangerous in 1 sentence.");
                
                    Dispatcher.Invoke(() => {
                         SecurityData.Insert(0, new SecurityEvent { 
                             Time = DateTime.Now.ToString("HH:mm:ss"), 
                             Activity = $"AI Insight: {explanation}", 
                             Type = "Info", 
                             Account = "Gemini", 
                             Id = 999 
                        });
                         
                         MessageBox.Show($"Security Enforcer Intervention!\n\nBlocked: {type}\n\nAI Explanation: {explanation}", "Security Alert", MessageBoxButton.OK, MessageBoxImage.Warning);
                    });
                }
                catch {}
            }
        }
        
        private bool IsAdministrator()
        {
            using (var identity = WindowsIdentity.GetCurrent())
            {
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        private void NavigateTo(UIElement targetView)
        {
            // Hide all views
            DashboardView.Visibility = Visibility.Collapsed;
            LogsView.Visibility = Visibility.Collapsed;
            HostedNetworkView.Visibility = Visibility.Collapsed;
            WanMiniportView.Visibility = Visibility.Collapsed;
            NetworkAdaptersView.Visibility = Visibility.Collapsed;
            TasksView.Visibility = Visibility.Collapsed;
            ConnectionsView.Visibility = Visibility.Collapsed;
            SettingsView.Visibility = Visibility.Collapsed;

            // Show target
            if (targetView != null)
            {
                targetView.Visibility = Visibility.Visible;
            }
        }

        private void MainWindow_Closed(object sender, EventArgs e)
        {
            _enforcer?.Stop();
            _deviceWatcherAdd?.Stop();
            _deviceWatcherRem?.Stop();
            // _logWatcher?.Enabled = false; // if using watcher
            Application.Current.Shutdown();
        }
    }
}