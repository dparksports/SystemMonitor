using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using DeviceMonitorCS.Helpers;
using DeviceMonitorCS.Models;

namespace DeviceMonitorCS.Views
{
    public partial class TimelineView : UserControl
    {
        private List<TimelineEvent> _loadedEvents;

        public TimelineView()
        {
            InitializeComponent();
            EventDatePicker.SelectedDate = DateTime.Today;
            ActivityHistogram.TimeRangeSelected += OnHistogramTimeRangeSelected;
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

                _loadedEvents = await EventLogHelper.GetTimelineEventsAsync(filterDate);
                
                // Bind DataGrid
                EventsGrid.ItemsSource = _loadedEvents;
                
                // Load Histogram
                ActivityHistogram.LoadData(_loadedEvents, filterDate ?? DateTime.Today);

                StatusText.Text = $"Loaded {_loadedEvents.Count} events.";
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
        
        private void OnHistogramTimeRangeSelected(DateTime start, DateTime end)
        {
            // Filter the DataGrid
            if (_loadedEvents != null)
            {
                var filtered = _loadedEvents.Where(ev => ev.Timestamp >= start && ev.Timestamp < end).ToList();
                EventsGrid.ItemsSource = filtered;
                StatusText.Text = $"Showing {filtered.Count} events from {start:HH:mm} to {end:HH:mm}";
            }
        }

        private void EventsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (EventsGrid.SelectedItem is TimelineEvent evt)
            {
                DetailMetaPanel.Visibility = Visibility.Visible;
                DetailTime.Text = evt.Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
                DetailCategory.Text = $"{evt.Category} ({evt.EventId})";
                DetailCategory.Foreground = GetBrushForCategory(evt);
                
                DetailDescription.Text = $"Source: {evt.Source}\n\n{evt.Description}";
            }
            else
            {
                DetailMetaPanel.Visibility = Visibility.Hidden;
                DetailDescription.Text = "Select an event to view details.";
            }
        }
        
        private Brush GetBrushForCategory(TimelineEvent evt)
        {
             if (evt.Category.Contains("Error") || evt.Category.Contains("Fail")) return Brushes.Red;
             if (evt.Category.Contains("Warning") || evt.Category.Contains("Change")) return Brushes.Yellow;
             return Brushes.Cyan;
        }
    }
}
