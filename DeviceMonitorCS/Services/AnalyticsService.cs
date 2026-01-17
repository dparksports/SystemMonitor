using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace DeviceMonitorCS.Services
{
    public class AnalyticsService
    {
        private static AnalyticsService _instance;
        public static AnalyticsService Instance => _instance ?? (_instance = new AnalyticsService());

        private const string MeasurementId = "G-XXXXXXXXXX"; // Replace with your GA4 Measurement ID
        private const string ApiSecret = "YOUR_API_SECRET"; // Replace with your GA4 API Secret
        private const string GaUrl = "https://www.google-analytics.com/mp/collect";
        
        private readonly HttpClient _httpClient;
        private string _clientId;
        private bool _isAnalyticsEnabled;

        public bool IsAnalyticsEnabled 
        { 
            get => _isAnalyticsEnabled;
            set
            {
                if (_isAnalyticsEnabled != value)
                {
                    _isAnalyticsEnabled = value;
                    SaveSettings();
                }
            }
        }

        public bool HasUserConsented => File.Exists(GetSettingsPath()); // Check if user has made ANY choice

        private AnalyticsService()
        {
            _httpClient = new HttpClient();
            LoadSettings();
            _clientId = GetOrGenerateClientId();
        }

        private string GetSettingsPath()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DeviceMonitorCS", "analytics_settings.txt");
        }

        private void LoadSettings()
        {
            try 
            {
                string path = GetSettingsPath();
                if (File.Exists(path))
                {
                    _isAnalyticsEnabled = bool.Parse(File.ReadAllText(path));
                }
                else
                {
                    _isAnalyticsEnabled = false; // Default to false until explicit opt-in
                }
            }
            catch { _isAnalyticsEnabled = false; }
        }

        private void SaveSettings()
        {
            try
            {
                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DeviceMonitorCS");
                Directory.CreateDirectory(folder);
                File.WriteAllText(GetSettingsPath(), _isAnalyticsEnabled.ToString());
            }
            catch {}
        }

        private string GetOrGenerateClientId()
         {
             var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DeviceMonitorCS");
             var path = Path.Combine(folder, "client_id.txt");
             if (File.Exists(path)) return File.ReadAllText(path);
             
             var newId = Guid.NewGuid().ToString();
             Directory.CreateDirectory(folder);
             File.WriteAllText(path, newId);
             return newId;
         }

        public async Task LogEventAsync(string eventName, Dictionary<string, string> parameters)
        {
            var objParams = parameters?.ToDictionary(k => k.Key, v => (object)v.Value);
            await LogEventAsync(eventName, (object)objParams);
        }

        public async Task LogEventAsync(string eventName, object parameters = null)
        {
            if (!IsAnalyticsEnabled) return;

            try
            {
                var dictParams = parameters as Dictionary<string, object>;
                if (dictParams == null && parameters != null)
                {
                    // Convert anonymous object to dictionary
                    var json = System.Text.Json.JsonSerializer.Serialize(parameters);
                    dictParams = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                }

                // PII Sanitization
                if (dictParams != null)
                {
                    var keys = dictParams.Keys.ToList();
                    foreach(var key in keys)
                    {
                        if (dictParams[key] is string val)
                        {
                            dictParams[key] = SanitizePii(val);
                        }
                    }
                }

                var payload = new
                {
                    client_id = _clientId,
                    user_properties = new
                    {
                        app_stage = new { value = "beta" } // Requirement 2: User Property
                    },
                    events = new[]
                    {
                        new
                        {
                            name = eventName,
                            @params = dictParams ?? new Dictionary<string, object>()
                        }
                    }
                };

                // Send Fire-and-Forget
                var content = new StringContent(System.Text.Json.JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json");
                await _httpClient.PostAsync($"{GaUrl}?measurement_id={MeasurementId}&api_secret={ApiSecret}", content);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Analytics Error: {ex.Message}");
            }
        }

        private string SanitizePii(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;

            // Email Regex
            string emailPattern = @"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}";
            if (System.Text.RegularExpressions.Regex.IsMatch(input, emailPattern)) return "REDACTED_EMAIL";

            // Credit Card (Simple Luhn check approximation - 13-19 digits)
            string ccPattern = @"\b(?:\d[ -]*?){13,16}\b"; 
            if (System.Text.RegularExpressions.Regex.IsMatch(input, ccPattern)) return "REDACTED_CC";

            return input;
        }
    }
}
