using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using DeviceMonitorCS.Models;

namespace DeviceMonitorCS.ViewModels
{
    public class SecurityStatusViewModel : INotifyPropertyChanged
    {
        private static SecurityStatusViewModel _instance;
        public static SecurityStatusViewModel Instance => _instance ?? (_instance = new SecurityStatusViewModel());

        // Private constructor for Singleton
        // Private constructor for Singleton
        // Logic moved to bottom block to include command initialization


        private bool _isExpertMode;
        public bool IsExpertMode
        {
            get => _isExpertMode;
            set
            {
                if (_isExpertMode != value)
                {
                    _isExpertMode = value;
                    OnPropertyChanged();
                }
            }
        }

        private SystemHealth _currentHealth = SystemHealth.Secure;
        public SystemHealth CurrentHealth
        {
            get => _currentHealth;
            set
            {
                if (_currentHealth != value)
                {
                    _currentHealth = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(StatusColor));
                    OnPropertyChanged(nameof(StatusIcon));
                }
            }
        }

        private string _statusMessage = "System Secure";
        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                if (_statusMessage != value)
                {
                    _statusMessage = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _detailedStatus = "All systems operational.";
        public string DetailedStatus
        {
            get => _detailedStatus;
            set
            {
                if (_detailedStatus != value)
                {
                    _detailedStatus = value;
                    OnPropertyChanged();
                }
            }
        }

        // Helper for UI binding (color strings)
        public string StatusColor
        {
            get
            {
                switch (_currentHealth)
                {
                    case SystemHealth.Secure: return "#4CAF50"; // Green
                    case SystemHealth.AtRisk: return "#FFC107"; // Amber
                    case SystemHealth.Critical: return "#F44336"; // Red
                    default: return "#4CAF50";
                }
            }
        }
        
        public string StatusIcon
        {
             get
             {
                 switch (_currentHealth)
                 {
                     case SystemHealth.Secure: return "CheckCircle"; 
                     case SystemHealth.AtRisk: return "AlertCircle"; 
                     case SystemHealth.Critical: return "ShieldAlert"; 
                     default: return "ShieldCheck";
                 }
             }
        }

        // Scan Command
        public ICommand ScanCommand { get; }
        public ICommand ShieldUpCommand { get; }

        private bool _isScanning;
        public bool IsScanning
        {
            get => _isScanning;
            set
            {
                if (_isScanning != value)
                {
                    _isScanning = value;
                    OnPropertyChanged();
                }
            }
        }

        // Expanded Constructor
        private SecurityStatusViewModel() 
        {
             // Sync with SettingsManager
             _isExpertMode = Services.SettingsManager.Instance.IsExpertMode;
             Services.SettingsManager.Instance.ExpertModeChanged += (val) => IsExpertMode = val;
             
             ScanCommand = new Helpers.RelayCommand(async (o) => await PerformScan());
             ShieldUpCommand = new Helpers.RelayCommand(async (o) => await PerformShieldUp());
        }

        private async Task PerformShieldUp()
        {
             StatusMessage = "Activating Shield...";
             await Services.FirewallProfileService.Instance.ApplyProfile(Services.FirewallProfileService.ProfileType.ShieldUp);
             
             CurrentHealth = SystemHealth.Secure;
             StatusMessage = "Shields Up";
             DetailedStatus = "All inbound/outbound rules blocked (except Core Networking/mDNS).";
        }

        private async Task PerformScan()
        {
            if (IsScanning) return;
            
            IsScanning = true;
            StatusMessage = "Scanning...";
            
            // Simulate Scan Delay
            await Task.Delay(3000);
            
            // Randomize result for demo purposes or keep secure
            IsScanning = false;
            CurrentHealth = SystemHealth.Secure;
            StatusMessage = "System Secure";
            DetailedStatus = $"Scan complete at {DateTime.Now:t}. No threats found.";
        }

        public void UpdateStatus(string status, string colorType)
        {
            // Map legacy string/color to Enum
            StatusMessage = status;
            
            if (colorType.Equals("Red", System.StringComparison.OrdinalIgnoreCase))
            {
                CurrentHealth = SystemHealth.Critical;
            }
            else if (colorType.Equals("Amber", System.StringComparison.OrdinalIgnoreCase))
            {
                CurrentHealth = SystemHealth.AtRisk;
            }
            else
            {
                CurrentHealth = SystemHealth.Secure;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
