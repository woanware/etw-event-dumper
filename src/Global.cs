using Microsoft.Diagnostics.Tracing;

namespace etw_event_dumper
{
    internal class Global
    {
        public enum FilterType
        {
            None,
            Pid,
            ProcessName
        }

        public class EventRecord
        {
            public Guid ProviderGuid { get; set; }
            public string? ProviderName { get; set; }
            public string? EventName { get; set; }
            public TraceEventOpcode Opcode { get; set; }
            public string? OpcodeName { get; set; }
            public DateTime TimeStamp { get; set; }
            public int ThreadID { get; set; }
            public int ProcessID { get; set; }
            public string? ProcessName { get; set; }
            public Dictionary<string, string>? XmlEventData { get; set; }
        }
    }
}
