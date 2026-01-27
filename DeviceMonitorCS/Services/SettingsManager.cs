using System;
using System.IO;

namespace DeviceMonitorCS.Services
{
    public class SettingsManager
    {
        private static SettingsManager _instance;
        public static SettingsManager Instance => _instance ?? (_instance = new SettingsManager());

        private bool _isFirstRun;
        public bool IsFirstRun
        {
            get => _isFirstRun;
            set
            {
                if (_isFirstRun != value)
                {
                    _isFirstRun = value;
                    SaveSettings();
                }
            }
        }

        private bool _isExpertMode;
        public bool IsExpertMode
        {
            get => _isExpertMode;
            set
            {
                if (_isExpertMode != value)
                {
                    _isExpertMode = value;
                    ExpertModeChanged?.Invoke(_isExpertMode);
                    SaveSettings();
                }
            }
        }

        public event Action<bool> ExpertModeChanged;

        private SettingsManager()
        {
            LoadSettings();
        }

        private string GetSettingsPath()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DeviceMonitorCS", "general_settings.txt");
        }

        private void LoadSettings()
        {
            try
            {
                string path = GetSettingsPath();
                if (File.Exists(path))
                {
                    string content = File.ReadAllText(path);
                    // Simple format: Key=Value lines, or just single boolean for now since it's the only setting here.
                    // For robustness let's just assume it stores IsExpertMode bool for now.
                    // Future: JSON or Key-Value parsing.
                    // Temp Hack: IsFirstRun is line 2 (optional)
                    var lines = File.ReadAllLines(path);
                    bool.TryParse(lines[0], out _isExpertMode);
                    if (lines.Length > 1) bool.TryParse(lines[1], out _isFirstRun);
                    else _isFirstRun = true; // Default to true if not present (new version)
                }
                else
                {
                    _isExpertMode = false; // Default to Novice mode
                    _isFirstRun = true;
                }
            }
            catch
            {
                _isExpertMode = false;
                _isFirstRun = true;
            }
        }

        private void SaveSettings()
        {
            try
            {
                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DeviceMonitorCS");
                Directory.CreateDirectory(folder);
                File.WriteAllLines(GetSettingsPath(), new[] { _isExpertMode.ToString(), _isFirstRun.ToString() });
            }
            catch { }
        }
    }
}
