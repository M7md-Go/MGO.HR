using System;

namespace HR.Entities
{
    /// <summary>Maps to table [RolePermissions].</summary>
    public class RolePermission
    {
        /// <summary>Primary key (AUTOINCREMENT: RolePermissionId).</summary>
        public int RolePermissionId { get; set; }
        /// <summary>RoleId.</summary>
        public int? RoleId { get; set; }

        /// <summary>PermissionId.</summary>
        public int? PermissionId { get; set; }
    }
}
