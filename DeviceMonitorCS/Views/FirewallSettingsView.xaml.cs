using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DeviceMonitorCS.Views
{
    public partial class FirewallSettingsView : UserControl
    {
        public ObservableCollection<FirewallRule> InboundRules { get; set; } = new ObservableCollection<FirewallRule>();
        public ObservableCollection<FirewallRule> OutboundRules { get; set; } = new ObservableCollection<FirewallRule>();

        public FirewallSettingsView()
        {
            InitializeComponent();
            InboundGrid.ItemsSource = InboundRules;
            OutboundGrid.ItemsSource = OutboundRules;
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

                // Fetch rules with details (Filters)
                // Use Pipeline with ForEach-Object to ensure output is captured by ConvertTo-Json.
                // Wrapped in try/catch in PS to avoid crashing on individual rule errors.
                string script = @"
Get-NetFirewallRule | ForEach-Object {
    try {
        $r = $_
        $app = $r | Get-NetFirewallApplicationFilter -ErrorAction SilentlyContinue
        $port = $r | Get-NetFirewallPortFilter -ErrorAction SilentlyContinue
        $addr = $r | Get-NetFirewallAddressFilter -ErrorAction SilentlyContinue
        
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
            LocalPort = if ($port) { $port.LocalPort } else { '' }
            RemotePort = if ($port) { $port.RemotePort } else { '' }
            RemoteAddress = if ($addr) { $addr.RemoteAddress } else { '' }
        }
    } catch {}
} | ConvertTo-Json -Depth 2
";
                string json = await RunPowershellAsync(script);

                if (string.IsNullOrWhiteSpace(json)) 
                {
                    // If no JSON, something went wrong with the script execution.
                    // Check if run as admin, but we are admin.
                    // Fallback or alert? 
                    // Let's try to fetch basic rules if advanced fetch failed, or just alert.
                    // For now, let's alert to help debug if it persists.
                    MessageBox.Show("Failed to retrieve firewall rules. The data returned was empty.");
                    return;
                }

                // Handle single object vs array
                if (!json.TrimStart().StartsWith("[")) json = $"[{json}]";

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var rules = JsonSerializer.Deserialize<FirewallRule[]>(json, options);
                
                if (rules == null) return;

                foreach (var rule in rules)
                {
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

        private async Task ToggleRule(FirewallRule rule)
        {
            try
            {
                Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
                bool isEnabled = rule.Enabled != null && rule.Enabled.Equals("Yes", StringComparison.OrdinalIgnoreCase);
                string newState = isEnabled ? "False" : "True";

                await RunPowershellAsync($"Set-NetFirewallRule -Name '{rule.Name}' -Enabled {newState}");
                
                // Update Model locally for instant feedback logic
                rule.Enabled = isEnabled ? "No" : "Yes";
                
                // Refresh to be sure (optional, can be removed for speed if local update is trusted)
                // await LoadRules(); 
                // Let's force refresh for now to be safe as requested
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

    public class FirewallRule
    {
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string DisplayGroup { get; set; }
        public string Direction { get; set; }
        public string Action { get; set; }
        public string Profile { get; set; }
        
        // Detailed Columns
        public string Program { get; set; }
        public string Protocol { get; set; }
        public string LocalPort { get; set; }
        public string RemotePort { get; set; }
        public string RemoteAddress { get; set; }

        public string Enabled { get; set; } // "Yes" or "No"
        
        // Helper for UI triggers if needed, though we bind to Enabled string now
        public bool IsEnabledBool => Enabled != null && Enabled.Equals("Yes", StringComparison.OrdinalIgnoreCase); 
    }
}
