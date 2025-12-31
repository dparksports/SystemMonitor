using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;
using DeviceMonitorCS.Models;

namespace DeviceMonitorCS
{
    public partial class DashboardView : UserControl
    {
        public DashboardView()
        {
            InitializeComponent();
        }

        public void BindActivity(ObservableCollection<SecurityEvent> events)
        {
            ActivityList.ItemsSource = events;
        }

        public void UpdateLiveStatus(string status, string colorType)
        {
            Dispatcher.Invoke(async () => 
            {
                StatusText.Text = status;
                
                if (colorType == "Red")
                {
                    StatusText.Foreground = Brushes.Red;
                    NetworkText.Text = "Intervention";
                    NetworkText.Foreground = Brushes.Red;
                }
                else if (colorType == "Amber")
                {
                    StatusText.Foreground = Brushes.Orange;
                    TaskText.Text = "Alert";
                    TaskText.Foreground = Brushes.Orange;
                }
                else
                {
                    StatusText.Foreground = Brushes.Lime;
                }

                // Auto-revert after 5 seconds if it was an alert
                if (colorType != "Green")
                {
                    await Task.Delay(5000);
                    StatusText.Text = "Protected";
                    StatusText.Foreground = Brushes.Lime;
                    NetworkText.Text = "Active";
                    NetworkText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00BFFF"));
                    TaskText.Text = "Scanning";
                    TaskText.Foreground = Brushes.Gold;
                }
            });
        }
    }
}
