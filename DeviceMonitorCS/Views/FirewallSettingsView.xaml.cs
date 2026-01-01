using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace DeviceMonitorCS.Views
{
    public partial class FirewallSettingsView : UserControl, INotifyPropertyChanged
    {
        public ObservableCollection<FirewallRule> InboundRules { get; set; } = new ObservableCollection<FirewallRule>();
        public ObservableCollection<FirewallRule> OutboundRules { get; set; } = new ObservableCollection<FirewallRule>();

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public FirewallSettingsView()
        {
            InitializeComponent();
            // DataContext is set to this for bindings to work
            // Ideally we should use a proper ViewModel, but for this refactor we keep code-behind pattern
            // However, the UserControl itself doesn't need to be DataContext if we use RelativeSource in XAML (which we did).
            
            Loaded += async (s, e) => await LoadRules();
        }

        private async void RefreshBtn_Click(object sender, RoutedEventArgs e)
        {
            await LoadRules();
        }

        private async Task LoadRules()
        {
            try
            {
                Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
                InboundRules.Clear();
                OutboundRules.Clear();

                // OPTIMIZED: Bulk fetch all data first, then join in PowerShell.
                string script = @"
$rules = Get-NetFirewallRule
$appFilters = Get-NetFirewallApplicationFilter
$portFilters = Get-NetFirewallPortFilter
$addrFilters = Get-NetFirewallAddressFilter

# Create hashtables for fast lookup by InstanceID
$appHash = @{}; $appFilters | ForEach-Object { $appHash[$_.InstanceID] = $_ }
$portHash = @{}; $portFilters | ForEach-Object { $portHash[$_.InstanceID] = $_ }
$addrHash = @{}; $addrFilters | ForEach-Object { $addrHash[$_.InstanceID] = $_ }

$rules | ForEach-Object {
    $r = $_
    $app = $appHash[$r.Name]
    $port = $portHash[$r.Name]
    $addr = $addrHash[$r.Name]
    
    [PSCustomObject]@{
        Name = $r.Name
        DisplayName = $r.DisplayName
        DisplayGroup = $r.DisplayGroup
        Direction = $r.Direction.ToString()
        Enabled = if ($r.Enabled -eq 1 -or $r.Enabled -eq 'True') {'Yes'} else {'No'}
        Action = $r.Action.ToString()
        Profile = $r.Profile.ToString()
        Program = if ($app) { $app.Program } else { '' }
        Protocol = if ($port) { $port.Protocol } else { '' }
        LocalPort = if ($port -and $port.LocalPort) { ($port.LocalPort -join ',') } else { '' }
        RemotePort = if ($port -and $port.RemotePort) { ($port.RemotePort -join ',') } else { '' }
        RemoteAddress = if ($addr -and $addr.RemoteAddress) { ($addr.RemoteAddress -join ',') } else { '' }
    }
} | ConvertTo-Json -Depth 2
";
                string json = await RunPowershellAsync(script);

                if (string.IsNullOrWhiteSpace(json)) 
                {
                    MessageBox.Show("Failed to retrieve firewall rules. The data returned was empty.");
                    return;
                }

                if (!json.TrimStart().StartsWith("[")) json = $"[{json}]";

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var rules = JsonSerializer.Deserialize<FirewallRule[]>(json, options);
                
                if (rules == null) return;

                foreach (var rule in rules)
                {
                    if (string.IsNullOrEmpty(rule.DisplayGroup))
                        rule.DisplayGroup = "(Ungrouped)";

                    if (rule.Direction == "Inbound") InboundRules.Add(rule);
                    else OutboundRules.Add(rule);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading rules: {ex.Message}");
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        private async void ToggleRule_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is FirewallRule rule)
            {
                await ToggleRule(rule);
            }
        }

        private async void ToggleRuleCtx_Click(object sender, RoutedEventArgs e)
        {
            var grid = InboundGrid.IsMouseOver ? InboundGrid : OutboundGrid;
            if (grid.SelectedItem is FirewallRule rule)
            {
                await ToggleRule(rule);
            }
        }

        private async void GroupToggle_Click(object sender, RoutedEventArgs e)
        {
             if (sender is CheckBox cb && cb.Tag is string groupName)
             {
                 // Determine context (Inbound or Outbound)
                 // This is tricky because the sender is inside a template.
                 // However, we can infer from which list contains the group, or pass more info.
                 // A simpler way: Check both lists for this group name. It's possible same group name exists in both.
                 // XAML structure separates them, so let's try to detect the parent DataGrid or just process for all rules in that group across the currently visible tab context?
                 // Or better, just update rules in the visible lists that match the group.
                 
                 // Let's check which Tab is selected by checking visibility or just process both? 
                 // Firewall groups are usually global (same group name might span inbound/outbound), so toggling "Core Networking" usually implies both.
                 // But the UI splits them. The user clicked a checkbox in *one* of the grids.
                 // Let's try to find the rules in the collections.
                 
                 bool? isChecked = cb.IsChecked;
                 string targetState = isChecked == true ? "True" : "False"; // PowerShell expects True/False string or bool
                 string targetStateUi = isChecked == true ? "Yes" : "No";

                 Mouse.OverrideCursor = Cursors.Wait;
                 try
                 {
                     // Find all rules in this group in both lists (to be consistent with "Toggle Group" meaning)
                     // or just the list the user clicked? 
                     // Typically if I'm in "Inbound" tab and click "Group A", I expect Inbound Group A to toggle.
                     // But if I want to be safe, I'll update all rules with that Group Name in the collections.
                     
                     var rulesToUpdate = new List<FirewallRule>();
                     rulesToUpdate.AddRange(InboundRules.Where(r => r.DisplayGroup == groupName));
                     rulesToUpdate.AddRange(OutboundRules.Where(r => r.DisplayGroup == groupName));

                     if (rulesToUpdate.Count == 0) return;

                     // Run PowerShell to update all at once
                     await RunPowershellAsync($"Set-NetFirewallRule -DisplayGroup '{groupName}' -Enabled {targetState}");

                     // Update UI
                     foreach (var r in rulesToUpdate)
                     {
                         r.Enabled = targetStateUi;
                     }
                     
                     // Force refresh of the group header binding if it doesn't auto-update
                     // Since bindings are OneWay + Converter, we might need to trigger a refresh 
                     // But if the rules update their property, the converter should re-evaluate if we bound to the collection?
                     // Actually `Binding Items` in GroupStyle passes the CollectionViewGroup.Items, which is observable.
                 }
                 catch (Exception ex)
                 {
                     MessageBox.Show($"Error toggling group: {ex.Message}");
                 }
                 finally
                 {
                     Mouse.OverrideCursor = null;
                 }
             }
        }

        private async Task ToggleRule(FirewallRule rule)
        {
            try
            {
                Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
                bool isEnabled = rule.Enabled != null && rule.Enabled.Equals("Yes", StringComparison.OrdinalIgnoreCase);
                string newState = isEnabled ? "False" : "True";

                await RunPowershellAsync($"Set-NetFirewallRule -Name '{rule.Name}' -Enabled {newState}");
                
                // Update Model locally
                rule.Enabled = isEnabled ? "No" : "Yes";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error updating rule: {ex.Message}");
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        private async void ResetBtn_Click(object sender, RoutedEventArgs e)
        {
            var res = MessageBox.Show("Are you sure you want to reset Windows Firewall configuration to defaults? This may affect network connectivity.", "Confirm Reset", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (res == MessageBoxResult.Yes)
            {
                try
                {
                    Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
                    var psi = new ProcessStartInfo
                    {
                        FileName = "netsh",
                        Arguments = "advfirewall reset",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true
                    };
                    var p = Process.Start(psi);
                    await p.WaitForExitAsync();
                    
                    MessageBox.Show("Firewall reset command executed. Reloading rules...", "Success");
                    await LoadRules();
                }
                catch (Exception ex)
                {
                     MessageBox.Show($"Error resetting firewall: {ex.Message}");
                }
                finally
                {
                    Mouse.OverrideCursor = null;
                }
            }
        }

        private Task<string> RunPowershellAsync(string script)
        {
            return Task.Run(() =>
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-Command \"{script}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };
                    using var p = Process.Start(psi);
                    if (p == null) return "";
                    
                    var output = p.StandardOutput.ReadToEnd();
                    p.WaitForExit();
                    return output;
                }
                catch { return ""; }
            });
        }
    }

    public class FirewallRule : INotifyPropertyChanged
    {
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string DisplayGroup { get; set; }
        public string Direction { get; set; }
        public string Action { get; set; }
        public string Profile { get; set; }
        public string Program { get; set; }
        public string Protocol { get; set; }
        public string LocalPort { get; set; }
        public string RemotePort { get; set; }
        public string RemoteAddress { get; set; }

        private string _enabled;
        public string Enabled 
        { 
            get => _enabled; 
            set 
            {
                if (_enabled != value)
                {
                    _enabled = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsEnabledBool));
                }
            } 
        }

        public bool IsEnabledBool => Enabled != null && Enabled.Equals("Yes", StringComparison.OrdinalIgnoreCase);

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public class GroupToEnabledStateConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // value is ReadOnlyObservableCollection<object> (the Items in the group)
            if (value is IList<object> items)
            {
                var rules = items.OfType<FirewallRule>().ToList();
                if (rules.Count == 0) return false;

                bool allEnabled = rules.All(r => r.IsEnabledBool);
                bool allDisabled = rules.All(r => !r.IsEnabledBool);

                if (allEnabled) return true;
                if (allDisabled) return false;
                return null; // Indeterminate
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
