using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Xml.Linq;

namespace DeviceMonitorCS.Models
{
    public class DeviceHistoryItem
    {
        public string DeviceId { get; set; }          // DeviceInstanceId or fallback key
        public string FriendlyName { get; set; }      // Friendly name if available
        public int EventId { get; set; }              // 400, 410, 420, etc.
        public DateTime TimeCreated { get; set; }
        public string RawDescription { get; set; }
    }

    public class DeviceStatus
    {
        public string DeviceId { get; set; }
        public string FriendlyName { get; set; }
        public string Status { get; set; } // "Connected" or "Disconnected" or "Unknown"
        public DeviceHistoryItem LastEvent { get; set; }
        public string DeviceType { get; set; } // Keyboard, Mouse, Monitor, or Other
    }

    public static class PnpHistoryReader
    {
        /// <summary>
        /// Read recent Kernel-PnP events and return per-device history and status.
        /// Filters for Keyboards, Mice, and Monitors.
        /// </summary>
        public static (Dictionary<string, List<DeviceHistoryItem>> history, List<DeviceStatus> statuses) ReadDeviceHistoryAndStatus(
            int maxEvents = 1000,
            int maxDevicesToReturn = 200)
        {
            // Query for 400 (Configure), 410 (Start), 420 (Delete/Stop) events
            string query = "*[System[(EventID=400 or EventID=410 or EventID=420)]]";
            var elq = new EventLogQuery("Microsoft-Windows-Kernel-PnP/Configuration", PathType.LogName, query)
            {
                ReverseDirection = true // newest first
            };

            var perDevice = new Dictionary<string, List<DeviceHistoryItem>>(StringComparer.OrdinalIgnoreCase);
            int readCount = 0;

            using (var reader = new EventLogReader(elq))
            {
                EventRecord ev;
                while ((ev = reader.ReadEvent()) != null)
                {
                    try
                    {
                        if (++readCount > maxEvents) break;

                        string xml = ev.ToXml();
                        var x = XElement.Parse(xml);

                        // EventID
                        int eventId = ev.Id;

                        // TimeCreated
                        DateTime timeCreated = ev.TimeCreated ?? DateTime.MinValue;

                        // Raw description (may contain friendly name)
                        string rawDesc = ev.FormatDescription() ?? string.Empty;

                        // Parse EventData/Data elements to find stable identifiers
                        string deviceInstanceId = null;
                        string friendlyName = null;

                        var dataElements = x.Descendants().Where(n => n.Name.LocalName == "Data");
                        foreach (var d in dataElements)
                        {
                            var nameAttr = (string)d.Attribute("Name") ?? string.Empty;
                            var value = (string)d;
                            if (string.IsNullOrEmpty(value)) continue;

                            if (string.Equals(nameAttr, "DeviceInstanceId", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(nameAttr, "InstanceId", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(nameAttr, "DeviceId", StringComparison.OrdinalIgnoreCase))
                            {
                                if (string.IsNullOrEmpty(deviceInstanceId))
                                    deviceInstanceId = value.Trim();
                            }
                            else if (string.Equals(nameAttr, "FriendlyName", StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(nameAttr, "Name", StringComparison.OrdinalIgnoreCase))
                            {
                                if (string.IsNullOrEmpty(friendlyName))
                                    friendlyName = value.Trim();
                            }
                            else if (string.Equals(nameAttr, "ParentIdPrefix", StringComparison.OrdinalIgnoreCase))
                            {
                                // Use as fallback part of identity if needed
                                if (string.IsNullOrEmpty(deviceInstanceId))
                                    deviceInstanceId = value.Trim();
                            }
                        }

                        // If no structured DeviceInstanceId, try to extract from the description heuristically
                        if (string.IsNullOrEmpty(deviceInstanceId))
                        {
                            // Use a short fallback key (friendly name or first 80 chars of description)
                            deviceInstanceId = !string.IsNullOrEmpty(friendlyName)
                                ? friendlyName
                                : (rawDesc.Length > 80 ? rawDesc.Substring(0, 80) : rawDesc);
                        }

                        var item = new DeviceHistoryItem
                        {
                            DeviceId = deviceInstanceId,
                            FriendlyName = friendlyName ?? rawDesc,
                            EventId = eventId,
                            TimeCreated = timeCreated,
                            RawDescription = rawDesc
                        };

                        if (!perDevice.TryGetValue(deviceInstanceId, out var list))
                        {
                            list = new List<DeviceHistoryItem>();
                            perDevice[deviceInstanceId] = list;
                        }

                        list.Add(item);

                        if (perDevice.Count > maxDevicesToReturn * 2) // buffer for filtering later
                        {
                            break;
                        }
                    }
                    catch
                    {
                        // ignore malformed events
                    }
                }
            }

            // Build statuses by looking at the most recent event per device
            var statuses = new List<DeviceStatus>();
            foreach (var kvp in perDevice)
            {
                var deviceId = kvp.Key;
                var events = kvp.Value.OrderByDescending(e => e.TimeCreated).ToList();
                var last = events.FirstOrDefault();

                string status;
                if (last == null) status = "Unknown";
                else if (last.EventId == 420) status = "Disconnected";
                else if (last.EventId == 400 || last.EventId == 410) status = "Connected";
                else status = "Unknown";

                string deviceType = DetermineDeviceType(deviceId, last?.FriendlyName, last?.RawDescription);

                if (deviceType != "Other") // FILTER: Only include Keyboards, Mice, Monitors
                {
                    statuses.Add(new DeviceStatus
                    {
                        DeviceId = deviceId,
                        FriendlyName = last?.FriendlyName,
                        Status = status,
                        LastEvent = last,
                        DeviceType = deviceType
                    });
                }
            }

            // Refine the history dictionary to only include the relevant devices
            var acceptedDeviceIds = new HashSet<string>(statuses.Select(s => s.DeviceId));
            var filteredHistory = perDevice
                .Where(k => acceptedDeviceIds.Contains(k.Key))
                .ToDictionary(k => k.Key, k => k.Value);

            // Sort statuses by name
            statuses = statuses.OrderBy(s => s.FriendlyName ?? s.DeviceId).ToList();

            return (filteredHistory, statuses);
        }

        private static string DetermineDeviceType(string deviceId, string friendlyName, string rawDesc)
        {
            string combined = (deviceId + " " + friendlyName + " " + rawDesc).ToLowerInvariant();

            if (combined.Contains("keyboard") || combined.Contains("key board")) return "Keyboard";
            if (combined.Contains("mouse") || combined.Contains("trackpad") || combined.Contains("pointing")) return "Mouse";
            if (combined.Contains("monitor") || combined.Contains("display") || deviceId.StartsWith("DISPLAY", StringComparison.OrdinalIgnoreCase)) return "Monitor";

            // Heuristics based on IDs
            // HID often includes Keyboards and Mice
            if (deviceId.IndexOf("HID", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                // Can be tricky, could be other HID devices.
                // Generally relying on string match for "keyboard" or "mouse" is safer for simple filtering,
                // but if the name is generic "USB Input Device", it's hard.
                // Let's assume most users want things that explicitly say what they are.
                // However, we can look for specific ClassGUIDs if we had them, but we only have string data here usually.
                
                // Common HID usage pages:
                // If it contains "MI_00" often keyboard, "MI_01" often mouse on composite devices, but not guaranteed.
            }
            
            return "Other";
        }
    }
}
