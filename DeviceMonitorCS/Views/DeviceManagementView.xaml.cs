using System;
using System.Collections.ObjectModel;
using System.Diagnostics.Eventing.Reader;
using System.Management;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Linq;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Data;

namespace DeviceMonitorCS.Views
{
    public partial class DeviceManagementView : UserControl
    {
        public ObservableCollection<DeviceInventoryItem> InventoryList { get; set; } = new ObservableCollection<DeviceInventoryItem>();
        public ObservableCollection<DeviceInventoryItem> InputDevicesList { get; set; } = new ObservableCollection<DeviceInventoryItem>();
        public ObservableCollection<DeviceInventoryItem> UnconnectedInputList { get; set; } = new ObservableCollection<DeviceInventoryItem>();
        public ObservableCollection<DeviceHistoryItem> HistoryList { get; set; } = new ObservableCollection<DeviceHistoryItem>();
        
        // Use ICollectionView for grouping logic in code-behind
        public ICollectionView GroupedInventoryView { get; set; }

        // Cache full history to avoid re-querying log constantly
        private List<DeviceHistoryItem> _fullHistoryCache = new List<DeviceHistoryItem>(); 

        public DeviceManagementView()
        {
            InitializeComponent();
            
            // Setup Grouping
            GroupedInventoryView = CollectionViewSource.GetDefaultView(InventoryList);
            GroupedInventoryView.GroupDescriptions.Add(new PropertyGroupDescription("Category"));

            InventoryGrid.ItemsSource = GroupedInventoryView; // Bind Full Inventory Tab in Code-Behind to ensure grouping
            
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
             if (sender is DataGrid dg && dg.SelectedItem is DeviceInventoryItem item)
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
            InputDevicesList.Clear();
            UnconnectedInputList.Clear();

            int inputDeviceCount = 0;
            int activeCount = 0;

            await Task.Run(() =>
            {
                try
                {
                    // Fetch ALL PnP Devices
                    using (var searcher = new ManagementObjectSearcher("SELECT Name, Manufacturer, DeviceID, PNPClass, Present, Status FROM Win32_PnPEntity"))
                    {
                        foreach (var device in searcher.Get())
                        {
                            string pnpClass = device["PNPClass"]?.ToString();
                            if (string.IsNullOrEmpty(pnpClass)) pnpClass = "Uncategorized";

                            bool present = (bool)(device["Present"] ?? false);
                            
                            // Clean Name
                            string name = device["Name"]?.ToString();
                            if (string.IsNullOrEmpty(name)) name = "Unknown Device";

                            var item = new DeviceInventoryItem
                            {
                                Category = pnpClass, // Use the PnP Class as Category
                                Name = name,
                                Manufacturer = device["Manufacturer"]?.ToString(),
                                Status = present ? "Active" : "Inactive",
                                DeviceId = device["DeviceID"]?.ToString()
                            };

                            Dispatcher.Invoke(() =>
                            {
                                InventoryList.Add(item);
                                
                                // Filter for Input Devices (Keyboard, Mouse, Monitor)
                                // Standard classes: Keyboard, Mouse, Monitor, Display
                                bool isInput = pnpClass.Contains("Keyboard", StringComparison.OrdinalIgnoreCase) ||
                                               pnpClass.Contains("Mouse", StringComparison.OrdinalIgnoreCase) ||
                                               pnpClass.Contains("Monitor", StringComparison.OrdinalIgnoreCase) ||
                                               pnpClass.Contains("Display", StringComparison.OrdinalIgnoreCase);

                                if (isInput)
                                {
                                    if (present)
                                    {
                                        InputDevicesList.Add(item);
                                        inputDeviceCount++; // Only count Connected Input Devices
                                    }
                                    else
                                    {
                                        UnconnectedInputList.Add(item);
                                    }
                                }
                            });
                            
                            if (present) activeCount++;
                        }
                    }
                }
                catch (Exception ex) 
                {
                    Dispatcher.Invoke(() => InventoryList.Add(new DeviceInventoryItem { Name = "Error", Manufacturer = ex.Message }));
                }
            });

            TotalDeviceCount.Text = inputDeviceCount.ToString();
            ActiveDeviceCount.Text = activeCount.ToString();
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
                            
                            // Broader filter: Include ALL PnP events since we track all devices now.
                            // The Inventory selection will handle specific filtering.
                            
                            var item = new DeviceHistoryItem
                            {
                                Time = eventInstance.TimeCreated?.ToString("yyyy-MM-dd HH:mm:ss"),
                                EventName = eventInstance.Id == 400 ? "Configured" : (eventInstance.Id == 410 ? "Started" : $"Event {eventInstance.Id}"),
                                DeviceName = desc
                            };
                            
                            _fullHistoryCache.Add(item);

                            if (_fullHistoryCache.Count > 500) break; // Increased limit for full inventory
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
