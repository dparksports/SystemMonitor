using System.Windows;
using System.Windows.Controls;

namespace DeviceMonitorCS.Views
{
    public partial class SecurityMainView : UserControl
    {
        private Dictionary<Type, UserControl> _subViewCache = new Dictionary<Type, UserControl>();

        public SecurityMainView()
        {
            InitializeComponent();
            NavigateSub<OverviewView>(); // Default to Overview
        }

        private void OverviewBtn_Click(object sender, RoutedEventArgs e) => NavigateSub<OverviewView>();
        private void PrivacyBtn_Click(object sender, RoutedEventArgs e) => NavigateSub<PrivacyView>();
        private void DefenderBtn_Click(object sender, RoutedEventArgs e) => NavigateSub<WindowsDefenderView>();
        private void FirewallBtn_Click(object sender, RoutedEventArgs e) => NavigateSub<FirewallSettingsView>();
        private void EventsBtn_Click(object sender, RoutedEventArgs e) => NavigateSub<EventManagementView>();
        private void PanelBtn_Click(object sender, RoutedEventArgs e) => NavigateSub<CommandPanelView>();
        private void TasksBtn_Click(object sender, RoutedEventArgs e) => NavigateSub<TasksView>();
        private void ConnBtn_Click(object sender, RoutedEventArgs e) => NavigateSub<ConnectionsView>();

        private void NavigateSub<T>() where T : UserControl, new()
        {
            Type type = typeof(T);
            if (!_subViewCache.ContainsKey(type))
            {
                _subViewCache[type] = new T();
                
                // Initialize if needed
                if (_subViewCache[type] is OverviewView ov) _ = ov.LoadAllDataAsync();
            }
            SubContentArea.Content = _subViewCache[type];
        }
    }
}
