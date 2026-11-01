namespace AutoCommand.Models
{
    /// <summary>
    /// One row of the firewall rules list (Firewall Settings view). Extracted
    /// from Models\DataModels.cs in the phase 3 Contain extraction because
    /// FirewallService — which builds these rows — now lives in this core;
    /// the type itself is plain INotifyPropertyChanged, no WPF.
    /// </summary>
    public class FirewallRuleItem : System.ComponentModel.INotifyPropertyChanged
    {
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string DisplayGroup { get; set; }
        public string Direction { get; set; }
        public string Action { get; set; }
        public string Profile { get; set; }
        public string Program { get; set; }
        public string Protocol { get; set; }
        public string LocalPort { get; set; }
        public string RemotePort { get; set; }
        public string RemoteAddress { get; set; }

        private bool _enabled;
        public bool Enabled
        {
            get => _enabled;
            set
            {
                if (_enabled != value)
                {
                    _enabled = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(EnabledDisplay));
                }
            }
        }

        public string EnabledDisplay => Enabled ? "Yes" : "No";

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
        }
    }
}
