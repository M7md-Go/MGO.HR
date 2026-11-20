using System;
namespace HR.Entities
{
    /// <summary>Rendered row of the raw fingerprint log browser (joined view).</summary>
    public class FingerprintLogView
    {
        /// <summary>LogId.</summary>
        public int LogId { get; set; }

        /// <summary>Source device id.</summary>
        public int? DeviceId { get; set; }

        /// <summary>Source device name.</summary>
        public string DeviceName { get; set; }

        /// <summary>Resolved employee id (null while the enroll code is unmatched).</summary>
        public int? EmployeeId { get; set; }

        /// <summary>Raw enroll number exactly as delivered by the device.</summary>
        public string EmpDevCode { get; set; }

        /// <summary>Employee code (null when unmatched).</summary>
        public string EmployeeCode { get; set; }

        /// <summary>Employee Arabic name (null when unmatched).</summary>
        public string EmployeeName { get; set; }

        /// <summary>Punch time as recorded by the device.</summary>
        public DateTime? LogTime { get; set; }

        /// <summary>Raw in/out flag (0 = in, 1 = out on most models).</summary>
        public int? InOutFlag { get; set; }

        /// <summary>Import source when the log came from a file import; null for device downloads.</summary>
        public string SourceFile { get; set; }

        /// <summary>Row creation timestamp.</summary>
        public DateTime? CreatedAt { get; set; }
    }
}
