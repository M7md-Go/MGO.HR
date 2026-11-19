using System;

namespace HR.Entities
{
    /// <summary>Maps to table [UserPermissions].</summary>
    public class UserPermission
    {
        /// <summary>Primary key (AUTOINCREMENT: UserPermissionId).</summary>
        public int UserPermissionId { get; set; }
        /// <summary>UserId.</summary>
        public int? UserId { get; set; }

        /// <summary>PermissionId.</summary>
        public int? PermissionId { get; set; }

        /// <summary>IsGranted.</summary>
        public bool? IsGranted { get; set; }
    }
}
