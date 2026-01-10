using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Diagnostics;
using System.Windows.Shapes;

namespace DeviceMonitorCS.Views
{
    public partial class DashboardView : UserControl
    {
        private DispatcherTimer _timer;
        private PerformanceCounter _cpuCounter;
        private PerformanceCounter _ramCounter;
        public ObservableCollection<TimelineItem> RecentEvents { get; set; }

        public DashboardView()
        {
            InitializeComponent();
            RecentEvents = new ObservableCollection<TimelineItem>();
            TimelineList.ItemsSource = RecentEvents;

            // Sample Data
            RecentEvents.Add(new TimelineItem { Title = "Firewall Rule Updated", Time = DateTime.Now.AddMinutes(-10), Color = "#00F0FF" });
            RecentEvents.Add(new TimelineItem { Title = "Scan Completed", Time = DateTime.Now.AddMinutes(-45), Color = "#00E676" });
            RecentEvents.Add(new TimelineItem { Title = "Device Connected", Time = DateTime.Now.AddHours(-1), Color = "#FFAE00" });
             RecentEvents.Add(new TimelineItem { Title = "Scan Completed", Time = DateTime.Now.AddHours(-2), Color = "#00E676" });
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                _ramCounter = new PerformanceCounter("Memory", "Available MBytes");
                _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                _timer.Tick += Timer_Tick;
                _timer.Start();
            }
            catch (Exception ex)
            {
                Console.WriteLine("PerfCounter Error: " + ex.Message);
            }
        }

        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            _timer?.Stop();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (_cpuCounter != null)
            {
                float cpu = _cpuCounter.NextValue();
                float ram = _ramCounter.NextValue();
                
                CpuText.Text = $"{cpu:0}%";
                MemText.Text = $"{ram:0} MB Free";

                // Simple simulated graph update (random point added for visual effect if we had a full chart control)
                // For Polyline, we would need a collection of Points.
                // Keeping it simple static for now or just text update as requested by "screenshot look".
            }
        }
    }

    public class TimelineItem
    {
        public string Title { get; set; }
        public DateTime Time { get; set; }
        public string Color { get; set; }
        public string TimeDisplay => Time.ToString("t") + ", " + Time.ToString("M");
        public Brush ColorBrush 
        {
            get 
            {
                try { return (Brush)new BrushConverter().ConvertFromString(Color); }
                catch { return Brushes.Gray; }
            }
        }
    }
}
