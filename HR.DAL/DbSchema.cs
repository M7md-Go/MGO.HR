using System;
using System.Collections.Generic;
using System.Data;
using System.Data.OleDb;

namespace HR.DAL
{
    /// <summary>
    /// Read-only schema/catalog access for the database administration screen:
    /// tables list, per-table column structure, indexes, row counts and paged
    /// data browsing. There is deliberately no write path here — this is a
    /// monitoring catalogue (user tables are edited only through their own
    /// dedicated screens, never raw). Table names are always validated against
    /// the live catalog before being embedded in a statement.
    /// </summary>
    public static class DbSchema
    {
        public class ColumnInfo
        {
            public string Name { get; set; }
            public int Ordinal { get; set; }
            public string TypeName { get; set; }
            public int? MaxChars { get; set; }
            public bool Nullable { get; set; }
            public bool PrimaryKey { get; set; }
        }

        public class IndexInfo
        {
            public string IndexName { get; set; }
            public string ColumnName { get; set; }
            public bool Unique { get; set; }
            public bool Primary { get; set; }
        }

        /// <summary>Opens a connection and yields it to the work function.</summary>
        private static T WithConnection<T>(Func<OleDbConnection, T> work)
        {
            using (OleDbConnection connection = Db.CreateConnection())
            {
                return work(connection);
            }
        }

        /// <summary>User tables only (VIEW / SYSTEM TABLE / ACCESS TABLE excluded).</summary>
        public static List<string> ListTables()
        {
            return WithConnection(delegate(OleDbConnection connection)
            {
                List<string> names = new List<string>();
                DataTable schema = connection.GetOleDbSchemaTable(OleDbSchemaGuid.Tables, null);
                foreach (DataRow row in schema.Rows)
                {
                    string schemaType = row["TABLE_TYPE"] as string;
                    if (schemaType == "TABLE")
                    {
                        names.Add(row["TABLE_NAME"] as string);
                    }
                }
                names.Sort(StringComparer.OrdinalIgnoreCase);
                return names;
            });
        }

        /// <summary>Whitelist check: the name must exist in the live catalog (blocks raw-SQL injection).</summary>
        public static bool IsValidTable(string tableName)
        {
            if (string.IsNullOrEmpty(tableName))
            {
                return false;
            }
            foreach (string name in ListTables())
            {
                if (string.Equals(name, tableName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Row count of one validated table.</summary>
        public static long CountRows(string tableName)
        {
            RequireValidTable(tableName);
            return Db.Count("SELECT COUNT(*) FROM [" + tableName + "]");
        }

        private static void RequireValidTable(string tableName)
        {
            if (!IsValidTable(tableName))
            {
                throw new InvalidOperationException("اسم جدول غير معروف: " + tableName);
            }
        }

        /// <summary>Column structure of one validated table (Jet type names resolved).</summary>
        public static List<ColumnInfo> GetColumns(string tableName)
        {
            RequireValidTable(tableName);
            return WithConnection(delegate(OleDbConnection connection)
            {
                List<ColumnInfo> columns = new List<ColumnInfo>();
                DataTable schema = connection.GetOleDbSchemaTable(OleDbSchemaGuid.Columns,
                    new object[] { null, null, tableName, null });
                foreach (DataRow row in schema.Select("", "ORDINAL_POSITION ASC"))
                {
                    ColumnInfo column = new ColumnInfo();
                    column.Name = row["COLUMN_NAME"] as string;
                    column.Ordinal = row["ORDINAL_POSITION"] is DBNull ? 0 : Convert.ToInt32(row["ORDINAL_POSITION"]);
                    column.TypeName = JetTypeName(row["DATA_TYPE"], row["COLUMN_DEFAULT"]);
                    column.MaxChars = row["CHARACTER_MAXIMUM_LENGTH"] is DBNull ?
                        (int?)null : Convert.ToInt32(row["CHARACTER_MAXIMUM_LENGTH"]);
                    column.Nullable = string.Equals(row["IS_NULLABLE"] as string, "YES", StringComparison.OrdinalIgnoreCase);
                    columns.Add(column);
                }
                HashSet<string> primaryColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (IndexInfo index in GetIndexes(tableName))
                {
                    if (index.Primary)
                    {
                        primaryColumns.Add(index.ColumnName);
                    }
                }
                foreach (ColumnInfo column in columns)
                {
                    column.PrimaryKey = primaryColumns.Contains(column.Name);
                }
                columns.Sort(delegate(ColumnInfo left, ColumnInfo right) { return left.Ordinal.CompareTo(right.Ordinal); });
                return columns;
            });
        }

        /// <summary>Indexes of one validated table, read by column name (robust across providers).</summary>
        public static List<IndexInfo> GetIndexes(string tableName)
        {
            RequireValidTable(tableName);
            return WithConnection(delegate(OleDbConnection connection)
            {
                List<IndexInfo> indexes = new List<IndexInfo>();
                DataTable schema = connection.GetOleDbSchemaTable(OleDbSchemaGuid.Indexes,
                    new object[] { null, null, null, tableName });
                foreach (DataRow row in schema.Rows)
                {
                    IndexInfo index = new IndexInfo();
                    index.IndexName = row.Table.Columns.Contains("INDEX_NAME") &&
                        !(row["INDEX_NAME"] is DBNull) ? row["INDEX_NAME"] as string : "";
                    index.ColumnName = row.Table.Columns.Contains("COLUMN_NAME") &&
                        !(row["COLUMN_NAME"] is DBNull) ? row["COLUMN_NAME"] as string : "";
                    index.Unique = row.Table.Columns.Contains("UNIQUE") &&
                        !(row["UNIQUE"] is DBNull) && Convert.ToBoolean(row["UNIQUE"]);
                    index.Primary = row.Table.Columns.Contains("PRIMARY_KEY") &&
                        !(row["PRIMARY_KEY"] is DBNull) && Convert.ToBoolean(row["PRIMARY_KEY"]);
                    indexes.Add(index);
                }
                return indexes;
            });
        }

        /// <summary>Relationship names from MSysRelationships (best effort: Jet denies it by default).</summary>
        public static List<string> TryRelationships()
        {
            try
            {
                return Db.ReadList("SELECT szRelationship FROM MSysRelationships",
                    delegate(OleDbDataReader reader) { return reader.GetString(0); });
            }
            catch (Exception)
            {
                return null; // not available under default Jet permissions
            }
        }

        /// <summary>Browses up to maxRows of one validated table into a detached DataTable.</summary>
        public static DataTable ReadRows(string tableName, int maxRows)
        {
            RequireValidTable(tableName);
            return WithConnection(delegate(OleDbConnection connection)
            {
                using (OleDbDataAdapter adapter = new OleDbDataAdapter("SELECT TOP " + maxRows + " * FROM ["
                    + tableName + "]", connection))
                {
                    DataTable data = new DataTable(tableName);
                    adapter.Fill(data);
                    return data;
                }
            });
        }

        /// <summary>Opens and closes a plain connection: null = OK, otherwise the Arabic error.</summary>
        public static string TestConnection()
        {
            try
            {
                WithConnection(delegate(OleDbConnection connection) { return connection.ServerVersion; });
                return null;
            }
            catch (Exception failure)
            {
                return failure.Message;
            }
        }

        /// <summary>Maps OleDbType codes to the Jet names an administrator knows.</summary>
        private static string JetTypeName(object dataType, object columnDefault)
        {
            if (dataType is DBNull || dataType == null)
            {
                return "";
            }
            OleDbType type = (OleDbType)Convert.ToInt32(dataType);
            switch (type)
            {
                case OleDbType.SmallInt: return "Short (عدد قصير)";
                case OleDbType.Integer: return "Long (عدد طويل)";
                case OleDbType.Single: return "Single";
                case OleDbType.Double: return "Double";
                case OleDbType.Currency: return "Currency";
                case OleDbType.Date: return "DateTime";
                case OleDbType.Boolean: return "YesNo";
                case OleDbType.LongVarWChar: return "Memo (LONGCHAR)";
                case OleDbType.VarWChar: return "Text (VARCHAR)";
                case OleDbType.WChar: return "Text (CHAR)";
                case OleDbType.Binary: return "Binary";
                default: return type.ToString();
            }
        }
    }
}
