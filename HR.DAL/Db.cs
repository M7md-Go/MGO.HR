using System;
using System.Collections.Generic;
using System.Data;
using System.Data.OleDb;
using System.IO;
using HR.Entities;

namespace HR.DAL
{
    /// <summary>
    /// Central database access helper for Microsoft Access (Jet 4.0).
    /// All SQL in the application is parameterized through this class (rule: no SQL in Forms).
    /// </summary>
    public static class Db
    {
        private static string _connectionString;

        /// <summary>Jet 4.0 connection string built from AppContext.DatabasePath.</summary>
        public static string ConnectionString
        {
            get
            {
                if (_connectionString == null)
                {
                    _connectionString = BuildConnectionString(AppContext.DatabasePath);
                }
                return _connectionString;
            }
        }

        /// <summary>Re-reads the connection string (used after Backup/Restore changes the path).</summary>
        public static void ResetConnectionString()
        {
            _connectionString = null;
        }

        /// <summary>Builds a Jet connection string with record-level locking.</summary>
        public static string BuildConnectionString(string databasePath)
        {
            if (string.IsNullOrWhiteSpace(databasePath))
            {
                throw new InvalidOperationException("مسار قاعدة البيانات غير محدد.");
            }
            OleDbConnectionStringBuilder builder = new OleDbConnectionStringBuilder();
            builder.Provider = "Microsoft.Jet.OLEDB.4.0";
            builder.DataSource = databasePath;
            builder["Jet OLEDB:Database Locking Mode"] = 1;
            builder["Persist Security Info"] = false;
            return builder.ConnectionString;
        }

        /// <summary>Creates a new open connection (caller is responsible for disposing).</summary>
        public static OleDbConnection CreateConnection()
        {
            OleDbConnection connection = new OleDbConnection(ConnectionString);
            connection.Open();
            return connection;
        }

        /// <summary>Creates an OleDbType parameter; null values are converted to DBNull.</summary>
        public static OleDbParameter P(object value)
        {
            if (value == null)
            {
                return new OleDbParameter { Value = DBNull.Value, OleDbType = OleDbType.VarChar };
            }
            if (value is int)
            {
                return new OleDbParameter { OleDbType = OleDbType.Integer, Value = value };
            }
            if (value is DateTime)
            {
                return new OleDbParameter { OleDbType = OleDbType.Date, Value = value };
            }
            if (value is bool)
            {
                return new OleDbParameter { OleDbType = OleDbType.Boolean, Value = value };
            }
            if (value is float)
            {
                return new OleDbParameter { OleDbType = OleDbType.Single, Value = value };
            }
            if (value is double)
            {
                return new OleDbParameter { OleDbType = OleDbType.Double, Value = value };
            }
            if (value is decimal)
            {
                return new OleDbParameter { OleDbType = OleDbType.Currency, Value = value };
            }
            if (value is long || value is short || value is byte)
            {
                return new OleDbParameter { OleDbType = OleDbType.Integer, Value = Convert.ToInt32(value) };
            }
            return new OleDbParameter { OleDbType = OleDbType.VarChar, Value = value };
        }

        /// <summary>Packs nullable int (foreign key) into a parameter.</summary>
        public static OleDbParameter PNullableId(int? value)
        {
            if (value == null)
            {
                return new OleDbParameter { OleDbType = OleDbType.Integer, Value = DBNull.Value };
            }
            return new OleDbParameter { OleDbType = OleDbType.Integer, Value = value.Value };
        }

        /// <summary>Executes a parameterized statement and returns affected rows.</summary>
        public static int ExecuteNonQuery(string sql, params OleDbParameter[] parameters)
        {
            using (OleDbConnection connection = CreateConnection())
            using (OleDbCommand command = new OleDbCommand(sql, connection))
            {
                AddParameters(command, parameters);
                return command.ExecuteNonQuery();
            }
        }

        /// <summary>Executes a scalar query and returns the object value or null.</summary>
        public static object ExecuteScalar(string sql, params OleDbParameter[] parameters)
        {
            using (OleDbConnection connection = CreateConnection())
            using (OleDbCommand command = new OleDbCommand(sql, connection))
            {
                AddParameters(command, parameters);
                object result = command.ExecuteScalar();
                return result == DBNull.Value ? null : result;
            }
        }

        /// <summary>Executes a SELECT and maps each row with the supplied mapper.</summary>
        public static List<T> ReadList<T>(string sql, Func<OleDbDataReader, T> map, params OleDbParameter[] parameters)
        {
            List<T> results = new List<T>();
            using (OleDbConnection connection = CreateConnection())
            using (OleDbCommand command = new OleDbCommand(sql, connection))
            {
                AddParameters(command, parameters);
                using (OleDbDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        results.Add(map(reader));
                    }
                }
            }
            return results;
        }

        /// <summary>Reads a single row or returns default(T) when not found.</summary>
        public static T ReadOne<T>(string sql, Func<OleDbDataReader, T> map, params OleDbParameter[] parameters)
        {
            List<T> rows = ReadList(sql, map, parameters);
            if (rows.Count == 0)
            {
                return default(T);
            }
            return rows[0];
        }

        /// <summary>Counts rows of a filtered query.</summary>
        public static long Count(string sql, params OleDbParameter[] parameters)
        {
            object result = ExecuteScalar(sql, parameters);
            return result == null ? 0 : Convert.ToInt64(result);
        }

        /// <summary>Typed nullable DATE parameter (Jet DATETIME columns).</summary>
        public static OleDbParameter PDate(DateTime? value)
        {
            if (value == null)
            {
                return new OleDbParameter { OleDbType = OleDbType.Date, Value = DBNull.Value };
            }
            return new OleDbParameter { OleDbType = OleDbType.Date, Value = value.Value };
        }

        /// <summary>Typed required DATE parameter; throws when null (a NOT NULL column).</summary>
        public static OleDbParameter PDateRequired(DateTime? value)
        {
            if (value == null)
            {
                throw new InvalidOperationException("قيمة تاريخ إلزامية مفقودة.");
            }
            return new OleDbParameter { OleDbType = OleDbType.Date, Value = value.Value };
        }

        /// <summary>Typed nullable SINGLE parameter (Jet Single columns).</summary>
        public static OleDbParameter PSingle(float? value)
        {
            if (value == null)
            {
                return new OleDbParameter { OleDbType = OleDbType.Single, Value = DBNull.Value };
            }
            return new OleDbParameter { OleDbType = OleDbType.Single, Value = value.Value };
        }

        /// <summary>Typed nullable CURRENCY parameter (Jet Currency columns).</summary>
        public static OleDbParameter PCurrency(decimal? value)
        {
            if (value == null)
            {
                return new OleDbParameter { OleDbType = OleDbType.Currency, Value = DBNull.Value };
            }
            return new OleDbParameter { OleDbType = OleDbType.Currency, Value = value.Value };
        }

        /// <summary>Typed nullable BOOLEAN parameter (Jet YESNO columns).</summary>
        public static OleDbParameter PBool(bool? value)
        {
            if (value == null)
            {
                return new OleDbParameter { OleDbType = OleDbType.Boolean, Value = DBNull.Value };
            }
            return new OleDbParameter { OleDbType = OleDbType.Boolean, Value = value.Value };
        }

        /// <summary>
        /// Runs work inside one Jet transaction (BR-20).
        /// The work function receives the connection and the transaction.
        /// </summary>
        /// <summary>Runs a scalar query inside an open transaction.</summary>
        public static object ExecuteScalarOn(OleDbConnection connection, OleDbTransaction transaction,
            string sql, params OleDbParameter[] parameters)
        {
            using (OleDbCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = sql;
                if (parameters != null)
                {
                    command.Parameters.AddRange(parameters);
                }
                return command.ExecuteScalar();
            }
        }

        /// <summary>Runs a statement inside an open transaction and returns affected rows.</summary>
        public static int ExecuteNonQueryOn(OleDbConnection connection, OleDbTransaction transaction,
            string sql, params OleDbParameter[] parameters)
        {
            using (OleDbCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = sql;
                if (parameters != null)
                {
                    command.Parameters.AddRange(parameters);
                }
                return command.ExecuteNonQuery();
            }
        }

        public static T ExecuteInTransaction<T>(Func<OleDbConnection, OleDbTransaction, T> work)
        {
            using (OleDbConnection connection = CreateConnection())
            {
                OleDbTransaction transaction = connection.BeginTransaction();
                try
                {
                    T result = work(connection, transaction);
                    transaction.Commit();
                    return result;
                }
                catch (Exception)
                {
                    try
                    {
                        transaction.Rollback();
                    }
                    catch (Exception)
                    {
                        // Rollback of an already-broken Jet connection cannot be salvaged.
                    }
                    throw;
                }
            }
        }

        /// <summary>Returns true when the database file physically exists.</summary>
        public static bool DatabaseFileExists()
        {
            return File.Exists(AppContext.DatabasePath);
        }

        /// <summary>Returns true when all core tables exist (used by DbInitializer).</summary>
        public static bool CoreTablesExist()
        {
            if (!DatabaseFileExists())
            {
                return false;
            }
            List<string> required = new List<string>();
            required.Add("Employees");
            required.Add("Attendance");
            required.Add("Shifts");
            required.Add("AppSettings");
            try
            {
                using (OleDbConnection connection = CreateConnection())
                {
                    DataTable tables = connection.GetOleDbSchemaTable(
                        OleDbSchemaGuid.Tables, new object[] { null, null, null, "TABLE" });
                    if (tables == null)
                    {
                        return false;
                    }
                    foreach (string tableName in required)
                    {
                        bool found = false;
                        foreach (DataRow row in tables.Rows)
                        {
                            if (string.Equals(Convert.ToString(row["TABLE_NAME"]), tableName, StringComparison.OrdinalIgnoreCase))
                            {
                                found = true;
                                break;
                            }
                        }
                        if (!found)
                        {
                            return false;
                        }
                    }
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void AddParameters(OleDbCommand command, OleDbParameter[] parameters)
        {
            if (parameters == null)
            {
                return;
            }
            foreach (OleDbParameter parameter in parameters)
            {
                command.Parameters.Add(parameter);
            }
        }
    }
}
