namespace HR.Entities
{
    /// <summary>A user registered inside a fingerprint device (live device read).</summary>
    public class DeviceUserInfo
    {
        /// <summary>Enroll number on the device.</summary>
        public string EnrollCode { get; set; }

        /// <summary>User name stored on the device.</summary>
        public string FullName { get; set; }

        /// <summary>Device privilege value (0 = user, 2 = manager on most models).</summary>
        public int? Privilege { get; set; }

        /// <summary>Whether the user is enabled on the device.</summary>
        public bool? Enabled { get; set; }

        /// <summary>Matched employee code (null when the enroll code has no employee).</summary>
        public string EmployeeCode { get; set; }

        /// <summary>Matched employee Arabic name (null when unmatched).</summary>
        public string EmployeeName { get; set; }

        /// <summary>True when the enroll code maps to an employee record.</summary>
        public bool Matched { get; set; }
    }
}
