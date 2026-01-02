using System;
using System.Collections.ObjectModel;
using System.Diagnostics.Eventing.Reader;
using System.Management;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Linq;
using System.Collections.Generic;

namespace DeviceMonitorCS.Views
{
    public partial class DeviceManagementView : UserControl
    {
        public ObservableCollection<DeviceInventoryItem> InventoryList { get; set; } = new ObservableCollection<DeviceInventoryItem>();
        public ObservableCollection<DeviceHistoryItem> HistoryList { get; set; } = new ObservableCollection<DeviceHistoryItem>();
        
        // Cache full history to avoid re-querying log constantly
        private List<DeviceHistoryItem> _fullHistoryCache = new List<DeviceHistoryItem>(); 

        public DeviceManagementView()
        {
            InitializeComponent();
            InventoryGrid.ItemsSource = InventoryList;
            HistoryGrid.ItemsSource = HistoryList;

            Loaded += (s, e) => RefreshData();
        }

        private void RefreshBtn_Click(object sender, RoutedEventArgs e)
        {
            RefreshData();
        }

        private async void RefreshData()
        {
            await LoadInventory();
            await LoadHistory(); // Loads full history once
            
            // Trigger selection update if item selected
            if (InventoryGrid.SelectedItem != null)
                FilterHistory(InventoryGrid.SelectedItem as DeviceInventoryItem);
            else
                FilterHistory(null);
        }

        private void InventoryGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
             if (InventoryGrid.SelectedItem is DeviceInventoryItem item)
             {
                 FilterHistory(item);
             }
             else
             {
                 FilterHistory(null);
             }
        }

        private void FilterHistory(DeviceInventoryItem device)
        {
            HistoryList.Clear();

            if (device == null)
            {
                SelectedDeviceText.Text = "(All Events)";
                foreach (var h in _fullHistoryCache) HistoryList.Add(h);
                return;
            }

            SelectedDeviceText.Text = $"(Events for {device.Name})";
            
            // Filter Logic: Check if Event DeviceName contains parts of the Inventory Device Name or ID
            // Often the log name is friendly, sometimes it is the Device ID.
            // We'll try to match loosely.
            
            var relevant = _fullHistoryCache.Where(h => 
                h.DeviceName.Contains(device.Name, StringComparison.OrdinalIgnoreCase) || 
                (device.DeviceId != null && h.DeviceName.Contains(device.DeviceId, StringComparison.OrdinalIgnoreCase)) ||
                (device.Manufacturer != null && h.DeviceName.Contains(device.Manufacturer, StringComparison.OrdinalIgnoreCase))
            ).ToList();

            foreach (var r in relevant)
            {
                HistoryList.Add(r);
            }
        }

        private async Task LoadInventory()
        {
            InventoryList.Clear();
            int kbdCount = 0;
            int mouseCount = 0;
            int monCount = 0;

            await Task.Run(() =>
            {
                // Use Win32_PnPEntity to find Everything (Active & Inactive)
                // Filter by Service or Class
                // Keyboards: ClassGuid = {4d36e96b-e325-11ce-bfc1-08002be10318}
                // Mice: ClassGuid = {4d36e96f-e325-11ce-bfc1-08002be10318}
                // Monitors: ClassGuid = {4d36e96e-e325-11ce-bfc1-08002be10318}

                string[] targetClasses = new[] { 
                    "{4d36e96b-e325-11ce-bfc1-08002be10318}", // Keyboard
                    "{4d36e96f-e325-11ce-bfc1-08002be10318}", // Mouse
                    "{4d36e96e-e325-11ce-bfc1-08002be10318}"  // Monitor
                };

                try
                {
                    // "Present" property tells us if it is currently connected.
                    using (var searcher = new ManagementObjectSearcher("SELECT Name, Manufacturer, DeviceID, ClassGuid, Present, Service FROM Win32_PnPEntity"))
                    {
                        foreach (var device in searcher.Get())
                        {
                            string classGuid = device["ClassGuid"]?.ToString()?.ToLower();
                            if (string.IsNullOrEmpty(classGuid)) continue;

                            string category = "Unknown";
                            if (classGuid.Contains("4d36e96b")) category = "Keyboard";
                            else if (classGuid.Contains("4d36e96f")) category = "Mouse";
                            else if (classGuid.Contains("4d36e96e")) category = "Monitor";
                            else continue; // Skip non-matching

                            bool present = (bool)(device["Present"] ?? false);
                            
                            // Stats (Only count active for the top cards?) -> User asked to track inactive too, but typically stats show active. 
                            // Let's count Active for stats, but list all.
                            if (present)
                            {
                                if (category == "Keyboard") kbdCount++;
                                if (category == "Mouse") mouseCount++;
                                if (category == "Monitor") monCount++;
                            }

                            // Clean Name
                            string name = device["Name"]?.ToString();
                            if (string.IsNullOrEmpty(name)) name = "Generic Device";

                            Dispatcher.Invoke(() =>
                            {
                                InventoryList.Add(new DeviceInventoryItem
                                {
                                    Category = category,
                                    Name = name,
                                    Manufacturer = device["Manufacturer"]?.ToString(),
                                    Status = present ? "Active" : "Inactive",
                                    DeviceId = device["DeviceID"]?.ToString()
                                });
                            });
                        }
                    }
                }
                catch (Exception ex) 
                {
                    Dispatcher.Invoke(() => InventoryList.Add(new DeviceInventoryItem { Name = "Error", Manufacturer = ex.Message }));
                }
            });

            KeyboardCountFormatted.Text = kbdCount.ToString();
            MouseCountFormatted.Text = mouseCount.ToString();
            MonitorCountFormatted.Text = monCount.ToString();
        }

        private async Task LoadHistory()
        {
            _fullHistoryCache.Clear();

            await Task.Run(() =>
            {
                try
                {
                    // Kernel-PnP Event IDs 400, 410, 420
                    string query = "*[System[(EventID=400 or EventID=410)]]";
                    
                    var elq = new EventLogQuery("Microsoft-Windows-Kernel-PnP/Configuration", PathType.LogName, query) { ReverseDirection = true };
                    using (var reader = new EventLogReader(elq))
                    {
                        EventRecord eventInstance;
                        while ((eventInstance = reader.ReadEvent()) != null)
                        {
                            string desc = eventInstance.FormatDescription();
                            
                            // Broader filter since we want history for potentially "Inactive" items that match our categories
                            bool relevant = desc.Contains("Keyboard", StringComparison.OrdinalIgnoreCase) ||
                                            desc.Contains("Mouse", StringComparison.OrdinalIgnoreCase) ||
                                            desc.Contains("Monitor", StringComparison.OrdinalIgnoreCase) ||
                                            desc.Contains("Display", StringComparison.OrdinalIgnoreCase) ||
                                            desc.Contains("HID", StringComparison.OrdinalIgnoreCase) ||
                                            desc.Contains("USB", StringComparison.OrdinalIgnoreCase);

                            if (relevant)
                            {
                                var item = new DeviceHistoryItem
                                {
                                    Time = eventInstance.TimeCreated?.ToString("yyyy-MM-dd HH:mm:ss"),
                                    EventName = eventInstance.Id == 400 ? "Configured" : (eventInstance.Id == 410 ? "Started" : $"Event {eventInstance.Id}"),
                                    DeviceName = desc
                                };
                                
                                _fullHistoryCache.Add(item);
                            }

                            if (_fullHistoryCache.Count > 300) break; // Increased limit
                        }
                    }
                }
                catch (Exception ex)
                {
                     _fullHistoryCache.Add(new DeviceHistoryItem { EventName = "Error", DeviceName = ex.Message });
                }
            });
        }
    }

    public class DeviceInventoryItem
    {
        public string Category { get; set; }
        public string Name { get; set; }
        public string Manufacturer { get; set; }
        public string Status { get; set; }
        public string DeviceId { get; set; }
    }

    public class DeviceHistoryItem
    {
        public string Time { get; set; }
        public string EventName { get; set; }
        public string DeviceName { get; set; }
    }
}
