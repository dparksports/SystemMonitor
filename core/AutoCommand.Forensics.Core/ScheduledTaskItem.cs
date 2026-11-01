namespace AutoCommand.Models
{
    /// <summary>
    /// One row of the scheduled-tasks list (TaskHostw Spawns attribution).
    /// Extracted from Models\DataModels.cs in the phase 4 Forensics
    /// extraction because TaskSchedulerService — which builds these rows —
    /// now lives in that core.
    /// </summary>
    public class ScheduledTaskItem
    {
        public string TaskName { get; set; }
        public string TaskPath { get; set; }
        public string State { get; set; }
        public string Action { get; set; }
        public string User { get; set; }
    }
}
