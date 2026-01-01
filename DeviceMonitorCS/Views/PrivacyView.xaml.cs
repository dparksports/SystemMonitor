using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DeviceMonitorCS.Models;

namespace DeviceMonitorCS.Views
{
    public partial class PrivacyView : UserControl
    {
        private GeminiClient _gemini;

        public PrivacyView()
        {
            InitializeComponent();
            InitializeGemini();
            Loaded += PrivacyView_Loaded;
        }

        private void PrivacyView_Loaded(object sender, RoutedEventArgs e)
        {
            CheckStatus();
            LoadAiExplanation();
        }

        private void InitializeGemini()
        {
            try
            {
                string keyPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "apikey.txt");
                if (File.Exists(keyPath))
                {
                    string key = File.ReadAllText(keyPath).Trim();
                    if (!string.IsNullOrEmpty(key))
                    {
                        _gemini = new GeminiClient(key);
                    }
                }
            }
            catch { }
        }

        private void CheckStatus()
        {
             bool enabled = IsUsageDataEnabled();
             UpdateUi(enabled);
        }

        private bool IsUsageDataEnabled()
        {
             try
             {
                 var psi = new ProcessStartInfo 
                 { 
                     FileName = "powershell.exe", 
                     Arguments = "-Command \"(Get-ScheduledTask -TaskName 'UsageDataReceiver' -TaskPath '\\Microsoft\\Windows\\Flighting\\FeatureConfig\\').State\"", 
                     RedirectStandardOutput = true, 
                     UseShellExecute = false, 
                     CreateNoWindow = true 
                 };
                 using (var p = Process.Start(psi))
                 {
                     string outStr = p.StandardOutput.ReadToEnd();
                     p.WaitForExit();
                     return !string.IsNullOrWhiteSpace(outStr) && !outStr.Trim().Equals("Disabled", StringComparison.OrdinalIgnoreCase);
                 }
             }
             catch { return false; }
        }

        private void UpdateUi(bool enabled)
        {
            if (enabled)
            {
                UsageStatusDetailsText.Text = "ENABLED (Collecting Data)";
                UsageStatusDetailsText.Foreground = Brushes.Red;
                ToggleUsageDataBtn.Content = "Disable Usage Data";
                ToggleUsageDataBtn.Background = new SolidColorBrush(Color.FromRgb(200, 50, 50)); // Red-ish
            }
            else
            {
                UsageStatusDetailsText.Text = "DISABLED (Private)";
                UsageStatusDetailsText.Foreground = Brushes.LightGreen;
                ToggleUsageDataBtn.Content = "Enable Usage Data";
                ToggleUsageDataBtn.Background = new SolidColorBrush(Color.FromRgb(0, 122, 204)); // Blue
            }
        }

        private void ToggleUsageDataBtn_Click(object sender, RoutedEventArgs e)
        {
             bool currentlyEnabled = IsUsageDataEnabled();
             bool targetState = !currentlyEnabled; // Toggle
             
             string cmd = targetState ? "Enable-ScheduledTask" : "Disable-ScheduledTask";
             try
             {
                 var psi = new ProcessStartInfo
                 {
                    FileName = "powershell.exe",
                    Arguments = $"-Command \"{cmd} -TaskName 'UsageDataReceiver' -TaskPath '\\Microsoft\\Windows\\Flighting\\FeatureConfig\\'\"",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                 };

                 using (var p = Process.Start(psi))
                 {
                     p.WaitForExit();
                     if (p.ExitCode != 0)
                     {
                         string err = p.StandardError.ReadToEnd();
                         MessageBox.Show($"Failed to toggle: {err}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                     }
                     else
                     {
                         CheckStatus(); // Refresh UI
                     }
                 }
             }
             catch (Exception ex)
             {
                  MessageBox.Show($"Error: {ex.Message}");
             }
        }

        private async void LoadAiExplanation()
        {
            if (AiExplanationText.Text != "Loading explanation...") return; // Already loaded?

            if (_gemini == null)
            {
                AiExplanationText.Text = "API Key not found. Please add apikey.txt to the application folder to enable AI insights.";
                return;
            }

            try
            {
                AiExplanationText.Text = "Asking Gemini...";
                string prompt = "Explain what the Windows Scheduled Task 'UsageDataReceiver' (in \\Microsoft\\Windows\\Flighting\\FeatureConfig\\) does in simple terms. Is it related to privacy?";
                string response = await _gemini.AskAsync(prompt);
                AiExplanationText.Text = response;
            }
            catch (Exception ex)
            {
                AiExplanationText.Text = $"Error: {ex.Message}";
            }
        }

        private void RefreshAiBtn_Click(object sender, RoutedEventArgs e)
        {
            LoadAiExplanation();
        }
    }
}
