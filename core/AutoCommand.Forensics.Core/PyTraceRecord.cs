using System;
using System.Text.Json.Serialization;

namespace AutoCommand.Forensics.Core
{
    /// <summary>
    /// One audit-hook record from an instrumented python environment: the
    /// spawn API that fired, its arguments, and the python call stack at the
    /// moment it did — the "why" half of python spawn tracking.
    /// </summary>
    public class PyTraceRecord
    {
        [JsonPropertyName("ts")]
        public double Ts { get; set; }              // epoch seconds (time.time() in the child)

        [JsonPropertyName("pid")]
        public int Pid { get; set; }

        [JsonPropertyName("event")]
        public string Event { get; set; }           // e.g. "subprocess.Popen" or "os.system"

        [JsonPropertyName("args")]
        public string[] Args { get; set; }          // repr-safe (truncated) arguments

        [JsonPropertyName("stack")]
        public string Stack { get; set; }           // python stack at the moment the spawn API fired

        [JsonIgnore]
        public DateTime TimeUtc => DateTimeOffset.FromUnixTimeMilliseconds((long)(Ts * 1000)).UtcDateTime;

        [JsonIgnore]
        public string TimeDisplay => TimeUtc.ToLocalTime().ToString("HH:mm:ss.fff");

        [JsonIgnore]
        public string Header => $"{TimeDisplay}  {Event}  {string.Join(" ", Args ?? Array.Empty<string>())}";
    }
}
