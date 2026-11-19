using System;

namespace HR.Entities
{
    /// <summary>Maps to table [Qualifications].</summary>
    public class Qualification
    {
        /// <summary>Primary key (AUTOINCREMENT: QualificationId).</summary>
        public int QualificationId { get; set; }
        /// <summary>QualCode.</summary>
        public string QualCode { get; set; }

        /// <summary>QualName.</summary>
        public string QualName { get; set; }
    }
}
