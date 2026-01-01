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


        
        private SecurityEnforcer _enforcer;
        private PerformanceMonitor _perfMonitor;

        public MainWindow()
        {
            InitializeComponent();



            // Check Admin
            _currentUser = System.Security.Principal.WindowsIdentity.GetCurrent().Name;

            if (IsAdministrator())
            {
                Title = $"Windows System Monitor (Administrator) - User: {_currentUser}";
                // InitializeToggles(); // Moved to PrivacyView
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
            PerformanceBtn.Click += (s, e) => NavigateTo(PerformanceView);
            PrivacyBtn.Click += (s, e) => NavigateTo(PrivacyView);
            FirmwareSettingsBtn.Click += (s, e) => NavigateTo(FirmwareSettingsView);
            FirewallSettingsBtn.Click += (s, e) => NavigateTo(FirewallSettingsView);
            
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
            
            // Bind Dashboard (New Unified Grid)
            DashboardView.BindData(SecurityData, DeviceData);

            // Start Monitoring

            Closed += MainWindow_Closed;
            
            // Performance Monitor Init
            _perfMonitor = new PerformanceMonitor();
            var perfTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            perfTimer.Tick += (s, e) => 
            {
                if (PerformanceView.Visibility == Visibility.Visible)
                {
                     PerformanceView.UpdateMetrics(_perfMonitor.GetMetrics());
                }
            };
            perfTimer.Start();
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
             string source = evt.ProviderName ?? "System";
             string details = "";
             
             // Properties to extract
             string accountName = "";
             string accountDomain = "";
             string logonId = "";
             string securityId = "";
             string privileges = "";
             string logonType = "";
             string processName = "";
             string workstationName = "";
             string ipAddress = "";
             string ipPort = "";
             string restrictedAdminMode = "";
             string remoteCredentialGuard = "";
             string virtualAccount = "";
             string groupName = "";
             string groupDomain = "";
             string groupSecurityId = "";

             try 
             {
                 details = evt.FormatDescription();
                 if (!string.IsNullOrEmpty(details))
                 {
                     // Convert newlines to spaces for uniform regex parsing
                     details = details.Replace("\r", " ").Replace("\n", " ");

                     // Dictionary mapping Regex Pattern -> Ref Variable (conceptually)
                     // Since we can't pass refs easily in a loop with different variables, we'll do it sequentially
                     // but use a helper to extract and remove.
                     
                     // Helper Func: Extract value, assign to var, remove from details
                     // We match "Key: Value" where Value is until the next "Key:" or end of string.
                     // The lookahead checks for " TwoWords:" or " OneWord:" pattern.
                     
                     string Extract(string keyPattern, ref string targetVar)
                     {
                         // Pattern: Key followed by colon, optional space, then convert the capture group
                         // We use a non-greedy match for content (.*?) until valid lookahead
                         var regex = new System.Text.RegularExpressions.Regex(keyPattern + @":\s*(.*?)(?=\s+[A-Za-z][A-Za-z ]+:|$)");
                         var match = regex.Match(details);
                         if (match.Success)
                         {
                             targetVar = match.Groups[1].Value.Trim();
                             details = details.Replace(match.Value, " "); // Replace with space to prevent sticking words together
                             return match.Value;
                         }
                         return null;
                     }

                     // 1. Account Info
                     Extract("Account Name", ref accountName);
                     Extract("Account Domain", ref accountDomain);
                     Extract("Logon ID", ref logonId);
                     Extract("Security ID", ref securityId);
                     
                     // 2. Network / System
                     Extract("Logon Type", ref logonType);
                     Extract("Workstation Name", ref workstationName);
                     Extract("Source Network Address", ref ipAddress);
                     Extract("Source Port", ref ipPort);
                     Extract("Process Name", ref processName);
                     Extract("New Process Name", ref processName); // sometimes "New Process Name"

                     // 3. Extended Info
                     Extract("Restricted Admin Mode", ref restrictedAdminMode);
                     Extract("Remote Credential Guard", ref remoteCredentialGuard);
                     Extract("Virtual Account", ref virtualAccount);

                     // 4. Group Info
                     Extract("Group Name", ref groupName);
                     Extract("Group Domain", ref groupDomain);
                     // If there's a second Security ID (e.g. for Group), extracting 'Security ID' again will pick it up
                     // because the first one was removed from 'details' by the first Extract call.
                     Extract("Security ID", ref groupSecurityId);

                     // 5. Privileges - Specific handling
                     // Privileges often is the last item or long list. 
                     // The generic extraction might fail if it contains keys inside (unlikely for privileges).
                     // But strictly speaking, Privileges might be: "SeTcbPrivilege\nSeChangeNotify..."
                     Extract("Privileges", ref privileges);

                     // Clean up multiple spaces
                     details = System.Text.RegularExpressions.Regex.Replace(details, @"\s+", " ").Trim();
                     if (details.Length > 150) details = details.Substring(0, 147) + "...";
                 }
             }
             catch { details = evt.OpcodeDisplayName ?? "Info"; }

             if (string.IsNullOrEmpty(details)) details = evt.OpcodeDisplayName ?? "Info";

             string activity = evt.TaskDisplayName ?? $"Event {evt.Id}";
             
             SecurityData.Insert(0, new SecurityEvent
             {
                 Time = evt.TimeCreated?.ToString("HH:mm:ss"),
                 Id = evt.Id,
                 Type = details,
                 Activity = activity,
                 Account = source,
                 AccountName = accountName,
                 AccountDomain = accountDomain,
                 LogonId = logonId,
                 SecurityId = securityId,
                 Privileges = privileges,
                 LogonType = logonType,
                 ProcessName = processName,
                 WorkstationName = workstationName,
                 IpAddress = ipAddress,
                 IpPort = ipPort,
                 RestrictedAdminMode = restrictedAdminMode,
                 RemoteCredentialGuard = remoteCredentialGuard,
                 VirtualAccount = virtualAccount,
                 GroupName = groupName,
                 GroupDomain = groupDomain,
                 GroupSecurityId = groupSecurityId,
                 // Set Privilege Flags based on substrings
                 SeAssignPrimaryTokenPrivilege = privileges.Contains("SeAssignPrimaryTokenPrivilege"),
                 SeTcbPrivilege = privileges.Contains("SeTcbPrivilege"),
                 SeSecurityPrivilege = privileges.Contains("SeSecurityPrivilege"),
                 SeTakeOwnershipPrivilege = privileges.Contains("SeTakeOwnershipPrivilege"),
                 SeLoadDriverPrivilege = privileges.Contains("SeLoadDriverPrivilege"),
                 SeBackupPrivilege = privileges.Contains("SeBackupPrivilege"),
                 SeRestorePrivilege = privileges.Contains("SeRestorePrivilege"),
                 SeDebugPrivilege = privileges.Contains("SeDebugPrivilege"),
                 SeAuditPrivilege = privileges.Contains("SeAuditPrivilege"),
                 SeSystemEnvironmentPrivilege = privileges.Contains("SeSystemEnvironmentPrivilege"),
                 SeImpersonatePrivilege = privileges.Contains("SeImpersonatePrivilege")
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
                     Activity = $"Blocked: {type}", 
                     Type = details, // Details Column
                     Account = "Security Enforcer", // Source Column
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
                             Activity = "Threat Analysis", 
                             Type = explanation, // Details Column
                             Account = "Gemini AI", // Source Column
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
            if (targetView == null) return;

            // Hide all
            DashboardView.Visibility = Visibility.Collapsed;
            PerformanceView.Visibility = Visibility.Collapsed;
            PrivacyView.Visibility = Visibility.Collapsed;
            HostedNetworkView.Visibility = Visibility.Collapsed;
            WanMiniportView.Visibility = Visibility.Collapsed;
            NetworkAdaptersView.Visibility = Visibility.Collapsed;
            TasksView.Visibility = Visibility.Collapsed;
            ConnectionsView.Visibility = Visibility.Collapsed;
            SettingsView.Visibility = Visibility.Collapsed;
            FirmwareSettingsView.Visibility = Visibility.Collapsed;
            FirewallSettingsView.Visibility = Visibility.Collapsed;

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