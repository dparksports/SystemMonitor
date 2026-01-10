using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace DeviceMonitorCS.Services
{
    public class FirebaseTelemetryService
    {

        private readonly string _clientIdFile;
        private FirebaseConfig _config;
        private string _clientId;
        private static readonly HttpClient _httpClient = new HttpClient();

        public FirebaseTelemetryService()
        {
            // Use LocalApplicationData for runtime writes (single-file friendly)
            var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AutoCommand");
            Directory.CreateDirectory(appData);
            _clientIdFile = Path.Combine(appData, "client_id.txt");
            
            LoadConfig();
            LoadOrGenerateClientId();
        }

        private void LoadConfig()
        {
            try
            {
                // Prioritize local file for Portable Directory mode (v3.9.0+)
                string localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "firebase_config.json");
                if (File.Exists(localPath))
                {
                    string json = File.ReadAllText(localPath);
                    _config = JsonSerializer.Deserialize<FirebaseConfig>(json);
                    return;
                }

                // Fallback to Embedded Resource
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                var resourceName = "DeviceMonitorCS.firebase_config.json";

                using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream != null)
                    {
                        using (StreamReader reader = new StreamReader(stream))
                        {
                            string json = reader.ReadToEnd();
                            _config = JsonSerializer.Deserialize<FirebaseConfig>(json);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading Firebase config: {ex.Message}");
            }
        }

        private void LoadOrGenerateClientId()
        {
            try
            {
                if (File.Exists(_clientIdFile))
                {
                    _clientId = File.ReadAllText(_clientIdFile).Trim();
                }

                if (string.IsNullOrEmpty(_clientId))
                {
                    _clientId = Guid.NewGuid().ToString();
                    File.WriteAllText(_clientIdFile, _clientId);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error managing Client ID: {ex.Message}");
                _clientId = Guid.NewGuid().ToString(); // Fallback
            }
        }

        public async Task SendEventAsync(string eventName, Dictionary<string, object> parameters = null)
        {
            if (_config == null || string.IsNullOrEmpty(_config.MeasurementId) || string.IsNullOrEmpty(_config.ApiKey))
            {
                Debug.WriteLine("[TELEMETRY] Cannot send event: Firebase configuration is missing or invalid.");
                return;
            }

            try
            {
                var payload = new
                {
                    client_id = _clientId,
                    events = new[]
                    {
                        new
                        {
                            name = eventName,
                            @params = parameters ?? new Dictionary<string, object>()
                        }
                    }
                };

                // Google Analytics Measurement Protocol URL
                // Note: api_secret is the API Secret from GA4 Admin > Data Streams > [Stream] > Measurement Protocol API secrets
                string url = $"https://www.google-analytics.com/mp/collect?measurement_id={_config.MeasurementId}&api_secret={_config.ApiKey}";

                string jsonPayload = JsonSerializer.Serialize(payload);
                var content = new StringContent(jsonPayload, System.Text.Encoding.UTF8, "application/json");

                // Send the event
                var response = await _httpClient.PostAsync(url, content);
                
                if (!response.IsSuccessStatusCode)
                {
                    string errorDetails = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"[TELEMETRY] Firebase Event Failed: {response.StatusCode} - {errorDetails}");
                }
                else 
                {
                     Debug.WriteLine($"[TELEMETRY] Firebase Event Sent: {eventName}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TELEMETRY] Error sending telemetry: {ex.Message}");
            }
        }
    }

    public class FirebaseConfig
    {
        public string apiKey { get; set; }
        public string measurementId { get; set; }
        
        // Helper properties to handle case sensitivity from JSON if needed, 
        // but System.Text.Json is case-sensitive by default.
        // We map the user provided keys.
        public string ApiKey => apiKey; 
        public string MeasurementId => measurementId;
    }
}
