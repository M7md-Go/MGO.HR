using System;

namespace HR.Entities
{
    /// <summary>
    /// Join row for user management screens: Users + Roles + Departments.
    /// Never exposes PwdHash or Salt.
    /// </summary>
    public class UserView
    {
        /// <summary>Primary key (UserId).</summary>
        public int UserId { get; set; }
        /// <summary>Username.</summary>
        public string Username { get; set; }
        /// <summary>Full display name.</summary>
        public string FullName { get; set; }
        /// <summary>Role id.</summary>
        public int? RoleId { get; set; }
        /// <summary>Role name from the join.</summary>
        public string RoleName { get; set; }
        /// <summary>Department id.</summary>
        public int? DeptId { get; set; }
        /// <summary>Department name from the join.</summary>
        public string DeptName { get; set; }
        /// <summary>Account disabled flag.</summary>
        public bool? IsDisabled { get; set; }
        /// <summary>Account locked by failed logins flag.</summary>
        public bool? IsLocked { get; set; }
        /// <summary>Consecutive failed attempts.</summary>
        public int? FailedLogins { get; set; }
        /// <summary>Last successful login time.</summary>
        public DateTime? LastLogin { get; set; }
    }
}
