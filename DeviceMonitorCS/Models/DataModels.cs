using System;

namespace DeviceMonitorCS.Models
{
    public class DeviceEvent
    {
        public string Time { get; set; }
        public string EventType { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public string Initiator { get; set; }
    }

    public class SecurityEvent
    {
        public string Time { get; set; }
        public int? Id { get; set; } // Nullable because sometimes we might want to put "-"
        public string Type { get; set; }
        public string Activity { get; set; }
        public string Account { get; set; }
    }

    public class ScheduledTaskItem
    {
        public string TaskName { get; set; }
        public string TaskPath { get; set; }
        public string State { get; set; }
        public string Action { get; set; }
        public string User { get; set; }
    }

    public class NetworkAdapterItem
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Status { get; set; }
        public string MacAddress { get; set; }
        public string InterfaceType { get; set; }
        public string DeviceID { get; set; }
    }
}
