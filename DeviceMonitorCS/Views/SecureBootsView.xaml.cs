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

        private void FixBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://support.microsoft.com/en-us/topic/kb5012170-security-update-for-secure-boot-dbx-72ff5eed-25b4-47c7-be28-c42bd211bb15",
                    UseShellExecute = true
                });
            }
            catch { }
        }
    }
}
