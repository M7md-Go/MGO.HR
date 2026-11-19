using System;

namespace HR.Entities
{
    /// <summary>Maps to table [Roles].</summary>
    public class Role
    {
        /// <summary>Primary key (AUTOINCREMENT: RoleId).</summary>
        public int RoleId { get; set; }
        /// <summary>RoleName.</summary>
        public string RoleName { get; set; }

        /// <summary>IsAdmin.</summary>
        public bool? IsAdmin { get; set; }
    }
}
