using System;

namespace HR.Entities
{
    /// <summary>One document attached to an employee file (stored as a relative app path).</summary>
    public class EmployeeFile
    {
        public int FileId { get; set; }
        public int EmployeeId { get; set; }
        public string FilePath { get; set; }
        public string Category { get; set; }
        public string Notes { get; set; }
        public DateTime? CreatedAt { get; set; }
        public int? CreatedBy { get; set; }
    }
}
