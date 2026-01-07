using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace DeviceMonitorCS.Views
{
    public partial class SubscriptionView : UserControl
    {
        // TODO: Replace with actual URLs from User
        private const string StripePaymentLink = "https://buy.stripe.com/test_3cIaEX2MT30ka4F8bKa3u00"; 
        private const string StripePortalLink = "https://billing.stripe.com/p/login/test_3cIaEX2MT30ka4F8bKa3u00";

        private const string RegistryKeyPath = @"Software\DeviceMonitorCS";
        private const string RegistryValueName = "LicenseKey";

        public SubscriptionView()
        {
            InitializeComponent();
            CheckLicenseStatus();
        }

        private void CheckLicenseStatus()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath))
                {
                    if (key != null)
                    {
                        string savedKey = key.GetValue(RegistryValueName) as string;
                        if (!string.IsNullOrEmpty(savedKey))
                        {
                            SetProStatus();
                            LicenseKeyInput.Text = savedKey;
                            return;
                        }
                    }
                }
            }
            catch { }

            SetFreeStatus();
        }

        private void SetProStatus()
        {
            CurrentPlanText.Text = "Pro Plan";
            StatusBadge.Text = "ACTIVE";
            StatusBadge.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#059669")); // Green
            ((Border)StatusBadge.Parent).Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ECFDF5"));
            
            LicenseKeyInput.IsEnabled = false;
        }

        private void SetFreeStatus()
        {
            CurrentPlanText.Text = "Free Tier";
            StatusBadge.Text = "INACTIVE";
            StatusBadge.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280")); // Gray
            ((Border)StatusBadge.Parent).Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F3F4F6"));
            
            LicenseKeyInput.IsEnabled = true;
        }

        private void SubscribeButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = StripePaymentLink,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open browser: {ex.Message}", "Error");
            }
        }

        private void ManageSubscription_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = StripePortalLink,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open browser: {ex.Message}", "Error");
            }
        }

        private void ActivateButton_Click(object sender, RoutedEventArgs e)
        {
            string key = LicenseKeyInput.Text.Trim();

            if (string.IsNullOrWhiteSpace(key))
            {
                ActivationMessage.Text = "Please enter a license key.";
                ActivationMessage.Foreground = Brushes.Red;
                return;
            }

            // Basic Validation (Guid format check)
            if (Guid.TryParse(key, out _))
            {
                try
                {
                    // Save Key to Registry
                    using (RegistryKey regKey = Registry.CurrentUser.CreateSubKey(RegistryKeyPath))
                    {
                        regKey.SetValue(RegistryValueName, key);
                    }

                    ActivationMessage.Text = "License activated successfully!";
                    ActivationMessage.Foreground = Brushes.Green;
                    
                    SetProStatus();
                }
                catch (Exception ex)
                {
                    ActivationMessage.Text = $"Error saving license: {ex.Message}";
                    ActivationMessage.Foreground = Brushes.Red;
                }
            }
            else
            {
                ActivationMessage.Text = "Invalid key format. Please check your key.";
                ActivationMessage.Foreground = Brushes.Red;
            }
        }
    }
}
