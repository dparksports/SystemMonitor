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

                // Fetch rules via PowerShell JSON output for reliable parsing
                // Fix: Force string conversion for Enums (Direction, Action, Profile) to prevent JSON deserialization errors (converting Int to String)
                string script = "Get-NetFirewallRule | Select-Object Name, DisplayName, DisplayGroup, @{Name='Direction';Expression={$_.Direction.ToString()}}, Enabled, @{Name='Action';Expression={$_.Action.ToString()}}, @{Name='Profile';Expression={$_.Profile.ToString()}}, Program | ConvertTo-Json -Depth 1";
                string json = await RunPowershellAsync(script);

                if (string.IsNullOrWhiteSpace(json)) return;

                // Handle single object vs array
                if (!json.TrimStart().StartsWith("[")) json = $"[{json}]";

                var rules = JsonSerializer.Deserialize<FirewallRule[]>(json);
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
                bool newState = !rule.IsEnabled;
                string stateStr = newState ? "True" : "False";

                await RunPowershellAsync($"Set-NetFirewallRule -Name '{rule.Name}' -Enabled {stateStr}");
                
                // Update Model
                rule.Enabled = newState.ToString(); // Helper handles boolean parsing
                
                // Visual feedback (refresh helps confirm, but verify specific item update first)
                // For simplicity, just refreshing local binding, but true verification is reloading.
                // Let's reload to be sure.
                await LoadRules();
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
        public string Program { get; set; }
        
        // Handling PowerShell boolean/string weirdness in JSON
        public object Enabled { get; set; } 

        public bool IsEnabled 
        { 
            get 
            {
                if (Enabled is JsonElement je) return je.ValueKind == JsonValueKind.True || (je.ValueKind == JsonValueKind.Number && je.GetInt32() != 0);
                if (Enabled is bool b) return b;
                if (Enabled is string s) return bool.TryParse(s, out var res) && res;
                return false;
            }
        }
    }
}
