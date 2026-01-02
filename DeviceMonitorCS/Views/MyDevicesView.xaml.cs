using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using DeviceMonitorCS.Models;

namespace DeviceMonitorCS.Views
{
    public partial class MyDevicesView : UserControl
    {
        public MyDevicesView()
        {
            InitializeComponent();
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadDataAsync();
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            await LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            try
            {
                // Run on background thread to keep UI responsive
                var result = await Task.Run(() => PnpHistoryReader.ReadDeviceHistoryAndStatus(maxEvents: 2000, maxDevicesToReturn: 100));
                
                DevicesGrid.ItemsSource = result.statuses;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading device history: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
