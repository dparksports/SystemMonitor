using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Diagnostics;
using System.Windows.Shapes;
using System.Collections.Generic;

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

        // Graph Data History (Last 60 seconds)
        private readonly int _historyLength = 60;
        private List<double> _cpuHistory = new List<double>();
        private List<double> _ramHistory = new List<double>();

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (_cpuCounter != null)
            {
                float cpu = _cpuCounter.NextValue();
                float ram = _ramCounter.NextValue();
                
                CpuText.Text = $"{cpu:0}%";
                MemText.Text = $"{ram:0} MB Free";

                UpdateGraph(_cpuHistory, cpu, CpuGraph, 100, false);
                UpdateGraph(_ramHistory, ram, MemGraph, 32000, true); // Assuming 32GB max for scale relative
            }
        }

        private void UpdateGraph(List<double> history, double newValue, Shape shape, double maxY, bool fillArea)
        {
            history.Add(newValue);
            if (history.Count > _historyLength) history.RemoveAt(0);

            double width = shape.ActualWidth;
            double height = shape.ActualHeight;
            if (width == 0 || height == 0) return;

            double xStep = width / (_historyLength - 1);
            
            var points = new PointCollection();

            if (fillArea)
            {
                 points.Add(new Point(0, height)); // Bottom Left Anchor
            }

            for (int i = 0; i < history.Count; i++)
            {
                double val = history[i];
                // Scale Y: 0 at Bottom (Height), Max at Top (0)
                // val=0 -> y=Height; val=Max -> y=0
                double y = height - ((val / maxY) * height);
                if (y < 0) y = 0;
                if (y > height) y = height;

                points.Add(new Point(i * xStep, y));
            }

            if (fillArea)
            {
                points.Add(new Point((history.Count - 1) * xStep, height)); // Bottom Right Anchor
            }

            if (shape is Polyline line) line.Points = points;
            else if (shape is Polygon poly) poly.Points = points;
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
