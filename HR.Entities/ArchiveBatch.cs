using System.Collections.Generic;

namespace HR.Entities
{
    /// <summary>Raw table export batch: column names plus rows of string cells.</summary>
    public class ArchiveBatch
    {
        /// <summary>Column names of the exported table.</summary>
        public List<string> Header { get; set; }

        /// <summary>Row cells, one list per row (InvariantCulture strings, nulls as empty).</summary>
        public List<List<string>> Rows { get; set; }

        public ArchiveBatch()
        {
            Header = new List<string>();
            Rows = new List<List<string>>();
        }
    }
}
