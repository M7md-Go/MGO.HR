using System;
using System.Collections.Generic;
using System.Data.OleDb;
using System.IO;
using System.Text;
using ADOX;

namespace HR.DAL
{
    /// <summary>
    /// Creates the .mdb file and applies Database/ModernTime_Schema.sql exactly as approved
    /// in the Phase 1 design. Statements are split on ';' and executed sequentially.
    /// </summary>
    public static class DbInitializer
    {
        /// <summary>
        /// Ensures the database file and full schema exist.
        /// Safe to call at every application start; creation happens only once.
        /// </summary>
        public static void EnsureInitialized()
        {
            string databasePath = AppContext.DatabasePath;
            if (!CoreTablesExist())
            {
                CreateDatabaseFile(databasePath);
                ExecuteSchemaScript(databasePath);
            }
        }

        /// <summary>Creates an empty Access 2002-2003 (Jet 4.0) file via ADOX.</summary>
        public static void CreateDatabaseFile(string databasePath)
        {
            if (File.Exists(databasePath))
            {
                return;
            }
            string folder = Path.GetDirectoryName(databasePath);
            if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
            Catalog catalog = new CatalogClass();
            try
            {
                catalog.Create(Db.BuildConnectionString(databasePath) + ";Jet OLEDB:Engine Type=5");
            }
            finally
            {
                System.Runtime.InteropServices.Marshal.ReleaseComObject(catalog);
            }
        }

        /// <summary>Reads the approved schema file, strips comments, executes every statement.</summary>
        public static void ExecuteSchemaScript(string databasePath)
        {
            string schemaPath = AppContext.SchemaPath;
            if (!File.Exists(schemaPath))
            {
                throw new FileNotFoundException("ملف مخطط قاعدة البيانات غير موجود: " + schemaPath);
            }
            List<string> statements = SplitStatements(File.ReadAllText(schemaPath, Encoding.UTF8));
            using (OleDbConnection connection = new OleDbConnection(Db.BuildConnectionString(databasePath)))
            {
                connection.Open();
                foreach (string statement in statements)
                {
                    using (OleDbCommand command = new OleDbCommand(statement, connection))
                    {
                        command.ExecuteNonQuery();
                    }
                }
            }
        }

        /// <summary>Splits the DDL file into executable statements (drops '--' comments).</summary>
        public static List<string> SplitStatements(string script)
        {
            List<string> statements = new List<string>();
            if (string.IsNullOrWhiteSpace(script))
            {
                return statements;
            }
            StringBuilder current = new StringBuilder();
            string[] lines = script.Replace("\r\n", "\n").Split('\n');
            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();
                if (line.StartsWith("--"))
                {
                    continue;
                }
                current.AppendLine(line);
            }
            string[] parts = current.ToString().Split(';');
            foreach (string part in parts)
            {
                string statement = part.Trim();
                if (statement.Length > 0)
                {
                    statements.Add(statement);
                }
            }
            return statements;
        }
    }
}
