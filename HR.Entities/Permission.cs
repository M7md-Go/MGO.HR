using System;

namespace HR.Entities
{
    /// <summary>Maps to table [Permissions].</summary>
    public class Permission
    {
        /// <summary>Primary key (AUTOINCREMENT: PermissionId).</summary>
        public int PermissionId { get; set; }
        /// <summary>PermissionKey.</summary>
        public string PermissionKey { get; set; }

        /// <summary>DescriptionAr.</summary>
        public string DescriptionAr { get; set; }
    }
}
