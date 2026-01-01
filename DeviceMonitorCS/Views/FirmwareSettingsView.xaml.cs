using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Management;
using System.Windows;
using System.Windows.Controls;
using DeviceMonitorCS.Models;

namespace DeviceMonitorCS.Views
{
    public partial class FirmwareSettingsView : UserControl
    {
        public ObservableCollection<FirmwareTableItem> FirmwareTables { get; set; } = new ObservableCollection<FirmwareTableItem>();
        public ObservableCollection<BcdEntry> BcdEntries { get; set; } = new ObservableCollection<BcdEntry>();

        public FirmwareSettingsView()
        {
            InitializeComponent();
            FirmwareGrid.ItemsSource = FirmwareTables;
            BcdGrid.ItemsSource = BcdEntries;
            Loaded += FirmwareSettingsView_Loaded;
        }

        private void FirmwareSettingsView_Loaded(object sender, RoutedEventArgs e)
        {
            if (FirmwareTables.Count == 0) LoadFirmwareTables();
            if (BcdEntries.Count == 0) LoadBcdInfo();
        }

        private void LoadFirmwareTables()
        {
            FirmwareTables.Clear();
            try
            {
                ManagementObjectSearcher searcher = new ManagementObjectSearcher("root\\wmi", "SELECT * FROM MS_SystemFirmwareTable");
                foreach (ManagementObject queryObj in searcher.Get())
                {
                    string tName = "N/A";
                    try { tName = queryObj["TableName"]?.ToString(); } catch { } 
                    if (string.IsNullOrEmpty(tName)) try { tName = queryObj["Name"]?.ToString(); } catch {}

                    string tId = "N/A";
                    try { tId = queryObj["TableID"]?.ToString(); } catch { }

                    string tLen = "N/A";
                    try { tLen = queryObj["TableLength"]?.ToString(); } catch { }

                    FirmwareTables.Add(new FirmwareTableItem
                    {
                        Name = tName,
                        TableID = tId,
                        Length = tLen
                    });
                }
            }
            catch (Exception ex)
            {
                FirmwareTables.Add(new FirmwareTableItem { Name = "Error/Restricted", TableID = "-", Length = ex.Message });
            }
        }

        private async void LoadBcdInfo()
        {
            try
            {
                // Clear existing
                BcdEntries.Clear();

                // Run bcdedit
                string output = await System.Threading.Tasks.Task.Run(() =>
                {
                    try 
                    {
                        ProcessStartInfo psi = new ProcessStartInfo("bcdedit", "/enum all");
                        psi.RedirectStandardOutput = true;
                        psi.UseShellExecute = false;
                        psi.CreateNoWindow = true;
                        // psi.StandardOutputEncoding = System.Text.Encoding.GetEncoding(437); // CP437 causes error if provider not registered

                        using (Process p = Process.Start(psi))
                        {
                            string result = p.StandardOutput.ReadToEnd();
                            p.WaitForExit();
                            return result;
                        }
                    }
                    catch (Exception ex)
                    {
                        return "Error running bcdedit: " + ex.Message;
                    }
                });

                // Parse Output
                ParseBcdOutput(output);
            }
            catch (Exception ex)
            {
                BcdEntries.Add(new BcdEntry { Type = "Error", Description = ex.Message });
            }
        }

        private void ParseBcdOutput(string rawOutput)
        {
            if (string.IsNullOrWhiteSpace(rawOutput) || rawOutput.StartsWith("Error"))
            {
                 BcdEntries.Add(new BcdEntry { Type = "Error", Description = rawOutput });
                 return;
            }

            var lines = rawOutput.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            string currentType = "";
            BcdEntry currentEntry = null;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].TrimEnd(); // Keep leading spaces for potential multi-line values, but we mainly care about structure
                
                if (string.IsNullOrWhiteSpace(line)) continue;

                // Check for Section Header (Text followed by separator line)
                if (i + 1 < lines.Length && lines[i + 1].Trim().StartsWith("---"))
                {
                    currentType = line.Trim();
                    i++; // Skip separator
                    continue;
                }

                // Check for Key-Value pair
                // Format: "identifier              {current}"
                // We'll split by first whitespace run
                var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 1)
                {
                    string key = parts[0].ToLower();
                    
                    // If parsing a new identifier, it's a new entry
                    if (key == "identifier")
                    {
                         if (currentEntry != null)
                         {
                             BcdEntries.Add(currentEntry);
                         }
                         currentEntry = new BcdEntry { Type = currentType };
                    }
                    
                    if (currentEntry != null && parts.Length >= 2)
                    {
                         // Join the rest as value in case value has spaces
                         // But simple split remove empty entries destroys spaces in value if any?
                         // Better: Find first space index
                         int spaceIndex = line.IndexOfAny(new[] { ' ', '\t' });
                         if (spaceIndex > 0)
                         {
                             string val = line.Substring(spaceIndex).Trim();

                             switch (key)
                             {
                                 case "identifier": currentEntry.Identifier = val; break;
                                 case "description": currentEntry.Description = val; break;
                                 case "device": currentEntry.Device = val; break;
                                 case "path": currentEntry.Path = val; break;
                             }
                         }
                    }
                }
            }
            
            // Add last entry
            if (currentEntry != null)
            {
                BcdEntries.Add(currentEntry);
            }
        }
    }
}
