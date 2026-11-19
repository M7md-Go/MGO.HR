using System;

namespace HR.Entities
{
    /// <summary>Maps to table [AttendanceAdjustments].</summary>
    public class AttendanceAdjustment
    {
        /// <summary>Primary key (AUTOINCREMENT: AdjustmentId).</summary>
        public int AdjustmentId { get; set; }
        /// <summary>EmployeeId.</summary>
        public int? EmployeeId { get; set; }

        /// <summary>AttDate.</summary>
        public DateTime? AttDate { get; set; }

        /// <summary>AdjustmentType.</summary>
        public string AdjustmentType { get; set; }

        /// <summary>NewInTime.</summary>
        public string NewInTime { get; set; }

        /// <summary>NewOutTime.</summary>
        public string NewOutTime { get; set; }

        /// <summary>NewStatus.</summary>
        public string NewStatus { get; set; }

        /// <summary>Reason.</summary>
        public string Reason { get; set; }

        /// <summary>Status.</summary>
        public string Status { get; set; }

        /// <summary>CreatedBy.</summary>
        public int? CreatedBy { get; set; }

        /// <summary>CreatedAt.</summary>
        public DateTime? CreatedAt { get; set; }

        /// <summary>ApprovedBy.</summary>
        public int? ApprovedBy { get; set; }

        /// <summary>ApprovedAt.</summary>
        public DateTime? ApprovedAt { get; set; }
    }
}
