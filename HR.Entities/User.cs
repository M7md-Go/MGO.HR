using System;

namespace HR.Entities
{
    /// <summary>Maps to table [Users].</summary>
    public class User
    {
        /// <summary>Primary key (AUTOINCREMENT: UserId).</summary>
        public int UserId { get; set; }
        /// <summary>Username.</summary>
        public string Username { get; set; }

        /// <summary>PwdHash.</summary>
        public string PwdHash { get; set; }

        /// <summary>Salt.</summary>
        public string Salt { get; set; }

        /// <summary>FullName.</summary>
        public string FullName { get; set; }

        /// <summary>RoleId.</summary>
        public int? RoleId { get; set; }

        /// <summary>DeptId.</summary>
        public int? DeptId { get; set; }

        /// <summary>IsDisabled.</summary>
        public bool? IsDisabled { get; set; }

        /// <summary>IsLocked.</summary>
        public bool? IsLocked { get; set; }

        /// <summary>FailedLogins.</summary>
        public int? FailedLogins { get; set; }

        /// <summary>LastLogin.</summary>
        public DateTime? LastLogin { get; set; }

        /// <summary>CreatedAt.</summary>
        public DateTime? CreatedAt { get; set; }

        /// <summary>CreatedBy.</summary>
        public int? CreatedBy { get; set; }
    }
}
