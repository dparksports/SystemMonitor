using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using DeviceMonitorCS.Models;

namespace DeviceMonitorCS
{
    public partial class TasksWindow : Window
    {
        public ObservableCollection<ScheduledTaskItem> TasksData { get; set; } = new ObservableCollection<ScheduledTaskItem>();

        public TasksWindow()
        {
            InitializeComponent();
            TasksGrid.ItemsSource = TasksData;

            // Wire up buttons
            RefreshBtn.Click += (s, e) => LoadTasks();
            DisableBtn.Click += DisableBtn_Click;
            StopBtn.Click += StopBtn_Click;
            StartBtn.Click += StartBtn_Click;
            DeleteBtn.Click += DeleteBtn_Click;

            LoadTasks();
        }

        private void LoadTasks()
        {
            try
            {
                TasksData.Clear();
                // Use schtasks /query /FO CSV /V to get details
                var startInfo = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = "/query /FO CSV /V",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8 // Ensure encoding matches
                };

                using (var process = Process.Start(startInfo))
                {
                    // Basic CSV parsing
                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();

                    var lines = output.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                    
                    // Skip header
                    if (lines.Length > 1)
                    {
                        var headers = ParseCsvLine(lines[0]);
                        // Find indices
                        int idxTaskName = Array.IndexOf(headers, "TaskName");
                        int idxStatus = Array.IndexOf(headers, "Status");
                        int idxAction = Array.IndexOf(headers, "Task To Run");
                        int idxUser = Array.IndexOf(headers, "Run As User");

                        if (idxTaskName == -1) idxTaskName = 0; // Fallback

                        for (int i = 1; i < lines.Length; i++)
                        {
                            var cols = ParseCsvLine(lines[i]);
                            if (cols.Length < 2) continue;

                            string taskName = GetCol(cols, idxTaskName);
                            // Filter for "High" run level is hard with just CSV output without checking XML, 
                            // but usually Admin tasks run as SYSTEM or specific users. 
                            // PowerShell script filtered by RunLevel 'Highest'. 
                            // schtasks /query /V includes "Level" column usually.
                           
                            // Let's just list all for now or filter by implicit admin heuristics if needed.
                            // But usually users want to see important tasks. 
                            
                            TasksData.Add(new ScheduledTaskItem
                            {
                                TaskName = taskName.Trim('"'),
                                State = GetCol(cols, idxStatus),
                                Action = GetCol(cols, idxAction),
                                User = GetCol(cols, idxUser)
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load tasks: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string GetCol(string[] cols, int index)
        {
            if (index >= 0 && index < cols.Length) return cols[index];
            return "";
        }

        // Simple CSV split handling quotes
        private string[] ParseCsvLine(string line)
        {
            // This is a naive parser but usually sufficient for schtasks output
            // schtasks quotes all fields in CSV
            return line.Split(new[] { "\",\"" }, StringSplitOptions.None)
                       .Select(s => s.Trim('"'))
                       .ToArray();
        }

        private void RunSchTasks(string args, string successMsg)
        {
            var selected = TasksGrid.SelectedItem as ScheduledTaskItem;
            if (selected == null)
            {
                MessageBox.Show("Please select a task first.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = $"/TN \"{selected.TaskName}\" {args}",
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(startInfo))
                {
                    string error = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    if (process.ExitCode == 0)
                    {
                        MessageBox.Show(successMsg, "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                        LoadTasks();
                    }
                    else
                    {
                        if (error.Contains("Access is denied"))
                        {
                            MessageBox.Show($"Access Denied: This task is protected by the system or requires additional permissions.\n\nTask: {selected.TaskName}", "Permission Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                        else
                        {
                            MessageBox.Show($"Operation failed: {error}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DisableBtn_Click(object sender, RoutedEventArgs e)
        {
            RunSchTasks("/Change /DISABLE", $"Task disabled successfully.");
        }

        private void StopBtn_Click(object sender, RoutedEventArgs e)
        {
            RunSchTasks("/End", $"Task stopped successfully.");
        }

        private void StartBtn_Click(object sender, RoutedEventArgs e)
        {
            RunSchTasks("/Run", $"Task started successfully.");
        }

        private void DeleteBtn_Click(object sender, RoutedEventArgs e)
        {
             var selected = TasksGrid.SelectedItem as ScheduledTaskItem;
            if (selected != null)
            {
                var res = MessageBox.Show($"Are you sure you want to delete task '{selected.TaskName}'?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (res == MessageBoxResult.Yes)
                {
                    RunSchTasks("/Delete /F", $"Task deleted successfully.");
                }
            }
        }
    }
}
