namespace HR.Entities
{
    /// <summary>Generic read view of one lookup row (code, name, parent) for change tracking.</summary>
    public class LookupRow
    {
        /// <summary>Row identifier.</summary>
        public int Id { get; set; }

        /// <summary>Code column value.</summary>
        public string Code { get; set; }

        /// <summary>Name column value.</summary>
        public string Name { get; set; }

        /// <summary>Parent column value (when the table has a parent), otherwise null.</summary>
        public int? ParentId { get; set; }
    }
}
