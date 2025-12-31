using System;
using System.Windows.Controls;

namespace DeviceMonitorCS.Views
{
    public partial class SettingsView : UserControl
    {
        public event Action<int> IntervalChanged;

        public SettingsView()
        {
            InitializeComponent();

            IntervalSlider.ValueChanged += (s, e) =>
            {
                int val = (int)e.NewValue;
                IntervalValueText.Text = $"{val} ms";
                IntervalChanged?.Invoke(val);
            };
        }

        public void SetCurrentInterval(int interval)
        {
            IntervalSlider.Value = interval;
            IntervalValueText.Text = $"{interval} ms";
        }
    }
}
