using System;
using System.Text.Json;
using System.Windows;
using DeviceMonitorCS.Models;

namespace DeviceMonitorCS
{
    public partial class AskAiWindow : Window
    {
        private GeminiClient _client;
        private string _context;

        public AskAiWindow(object contextItem)
        {
            InitializeComponent();
            
            string apiKey = "";
            try
            {
                var keyPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "apikey.txt");
                if (System.IO.File.Exists(keyPath))
                {
                    apiKey = System.IO.File.ReadAllText(keyPath).Trim();
                }
                else
                {
                    // Fallback: check project root if running from bin in dev mode (optional, but helpful)
                    var devPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "apikey.txt");
                    if (System.IO.File.Exists(devPath))
                    {
                         apiKey = System.IO.File.ReadAllText(devPath).Trim();
                    }
                }
            }
            catch {}

            if (string.IsNullOrEmpty(apiKey))
            {
                MessageBox.Show("API Key not found. Please create 'apikey.txt' in the application directory with your Gemini API key.", "Configuration Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                Close();
                return;
            }

            _client = new GeminiClient(apiKey);

            try
            {
                _context = JsonSerializer.Serialize(contextItem, new JsonSerializerOptions { WriteIndented = true });
            }
            catch
            {
                _context = contextItem.ToString();
            }

            ContextBox.Text = _context;
            QuestionBox.Focus();
            QuestionBox.SelectAll();

            AskBtn.Click += AskBtn_Click;
        }

        private async void AskBtn_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(QuestionBox.Text)) return;

            string question = QuestionBox.Text;
            string prompt = $"Here is a JSON object representing a system entity (e.g., a process, network connection, or event):\n\n{_context}\n\nUser Question: {question}\n\nPlease provide a concise explanation.";
            
            ResponseBox.Text = "Thinking...";
            AskBtn.IsEnabled = false;
            QuestionBox.IsEnabled = false;

            try
            {
                string answer = await _client.AskAsync(prompt);
                ResponseBox.Text = answer;
            }
            catch (Exception ex)
            {
                ResponseBox.Text = $"Error: {ex.Message}";
            }
            finally
            {
                AskBtn.IsEnabled = true;
                QuestionBox.IsEnabled = true;
                QuestionBox.Focus();
            }
        }
    }
}
