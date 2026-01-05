using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using DeviceMonitorCS.Helpers;
using DeviceMonitorCS.Models;

namespace DeviceMonitorCS.Views
{
    public partial class TimelineView : UserControl
    {
        public TimelineView()
        {
            InitializeComponent();
            EventDatePicker.SelectedDate = DateTime.Today;
        }

        private async void RefreshBtn_Click(object sender, RoutedEventArgs e)
        {
            await LoadEvents();
        }

        private async Task LoadEvents()
        {
            try
            {
                StatusText.Text = "Loading events... This may take a moment.";
                LoadingBar.Visibility = Visibility.Visible;
                RefreshBtn.IsEnabled = false;

                DateTime? filterDate = null;
                if (ShowAllDates.IsChecked == false)
                {
                    filterDate = EventDatePicker.SelectedDate ?? DateTime.Today;
                }

                var events = await EventLogHelper.GetTimelineEventsAsync(filterDate);
                
                EventsGrid.ItemsSource = events;
                StatusText.Text = $"Loaded {events.Count} events.";
            }
            catch (Exception ex)
            {
                StatusText.Text = "Error loading events.";
                MessageBox.Show($"Error querying Event Log: {ex.Message}\nNote: Some logs require Administrator privileges.");
            }
            finally
            {
                LoadingBar.Visibility = Visibility.Collapsed;
                RefreshBtn.IsEnabled = true;
            }
        }

        private void ShowAllDates_Click(object sender, RoutedEventArgs e)
        {
            EventDatePicker.IsEnabled = ShowAllDates.IsChecked == false;
        }
    }
}
