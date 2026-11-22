using System;

namespace HR.Entities
{
    /// <summary>One aggregated operational alert shown in the alerts center.</summary>
    public class AlertItem
    {
        public string Category { get; set; }
        public string Message { get; set; }
        public string Severity { get; set; } // "warn" = needs attention, "bad" = urgent
        public string ModuleId { get; set; }
    }

    /// <summary>Result snapshot: alerts with the total count for the status bar.</summary>
    public class AlertSummary
    {
        public System.Collections.Generic.List<AlertItem> Items { get; set; }
        public int Count { get; set; }
    }
}
