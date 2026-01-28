using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Diagnostics;
using DeviceMonitorCS.Services;

namespace DeviceMonitorCS.Views
{
    public partial class SecureBootsView : UserControl
    {
        private readonly UefiService _uefiService;
        private List<DbxEntry> _allDbxEntries = new List<DbxEntry>();

        public SecureBootsView()
        {
            InitializeComponent();
            _uefiService = new UefiService();
        }

        public async Task InitializeAndLoad()
        {
            LoadingOverlay.Visibility = Visibility.Visible;
            
            try
            {
                await Task.Run(() =>
                {
                    // 1. Get DB Entries
                    var dbEntries = _uefiService.GetDbEntries();

                    // 2. Get DBX Entries
                    var dbxInfo = _uefiService.GetDbxInfo();
                    _allDbxEntries = dbxInfo.Entries;

                    // Update UI on UI Thread
                    Dispatcher.Invoke(() =>
                    {
                        DbGrid.ItemsSource = dbEntries;
                        DbCountText.Text = $"Allowed (db): {dbEntries.Count}";
                        
                        DbxCountText.Text = $"Revocations (dbx): {dbxInfo.Count}";

                        // 3. BlackLotus Status
                        bool isPatched = _uefiService.IsBlackLotusMitigated();
                        if (isPatched)
                        {
                            BlackLotusStatusText.Text = "ACTIVE";
                            BlackLotusStatusText.Foreground = System.Windows.Media.Brushes.LimeGreen;
                            BlackLotusBadge.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x22, 0x00, 0xFF, 0x00));
                            VulnerabilityInfoPanel.Visibility = Visibility.Collapsed;
                        }
                        else
                        {
                            BlackLotusStatusText.Text = "VULNERABLE";
                            BlackLotusStatusText.Foreground = System.Windows.Media.Brushes.Red;
                            BlackLotusBadge.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x22, 0xFF, 0x00, 0x00));
                            VulnerabilityInfoPanel.Visibility = Visibility.Visible;
                        }
                        
                        // Show first 100 dbx entries initially to avoid lag if list is huge
                        DbxList.ItemsSource = _allDbxEntries.Take(100); 
                    });
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => MessageBox.Show($"Error reading UEFI variables: {ex.Message}"));
            }
            finally
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private void SearchBtn_Click(object sender, RoutedEventArgs e)
        {
            string query = SearchBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(query))
            {
                DbxList.ItemsSource = _allDbxEntries.Take(100);
            }
            else
            {
                // Simple containment search, case insensitive
                var results = _allDbxEntries
                    .Where(x => x.Hash.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                    .Take(100) // Limit results for performance
                    .ToList();
                DbxList.ItemsSource = results;
            }
        }

        private async void FixBtn_Click(object sender, RoutedEventArgs e)
        {
            LoadingOverlay.Visibility = Visibility.Visible;

            // 1. Download and get checksum
            var result = await DeviceMonitorCS.Helpers.DbxRemediator.DownloadUpdateAsync();
            
            LoadingOverlay.Visibility = Visibility.Collapsed;

            if (string.IsNullOrEmpty(result.Path))
            {
                 MessageBox.Show("Failed to download the update file from uefi.org.", "Download Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                 return;
            }

            // 2. Show Confirmation with Checksum
            string message = $"Downloaded UEFI DBX Update (KB5012170).\n\n" +
                             $"Source: uefi.org\n" +
                             $"SHA256: {result.Checksum}\n\n" +
                             "This file is digitally signed by Microsoft (KEK).\n" +
                             "The firmware will REJECT it if the signature is invalid.\n\n" +
                             "Proceed with installation? (Requires Admin / Restart)";

            if (MessageBox.Show(message, "Verify & Install", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                LoadingOverlay.Visibility = Visibility.Visible;
                bool success = await DeviceMonitorCS.Helpers.DbxRemediator.InstallUpdateAsync(result.Path);
                LoadingOverlay.Visibility = Visibility.Collapsed;

                if (success)
                {
                    MessageBox.Show("Update command executed successfully.\n\nPlease RESTART your computer for the changes to take effect.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    await InitializeAndLoad();
                }
                else
                {
                    MessageBox.Show("Update failed to install.\nPossible reasons: Admin denied, or Firmware rejected the file.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            
            // Cleanup
            try { if (System.IO.File.Exists(result.Path)) System.IO.File.Delete(result.Path); } catch { }
        }
    }
}
