using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace HR.Helpers
{
    /// <summary>
    /// Centralized file logger. Developer-facing details (stack traces) are written
    /// to the Logs folder only; the UI must show Arabic user-friendly messages (BR-23).
    /// </summary>
    public static class Logger
    {
        private static readonly object SyncRoot = new object();

        /// <summary>Logs an exception with full details for developers.</summary>
        public static void LogError(Exception ex, string context)
        {
            if (ex == null)
            {
                return;
            }
            StringBuilder message = new StringBuilder();
            message.AppendLine("=== ERROR ===");
            message.AppendLine("Time   : " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture));
            message.AppendLine("User   : " + (AppContext.CurrentUser != null ? AppContext.CurrentUser.Username : "(not logged in)"));
            message.AppendLine("Machine: " + AppContext.MachineName);
            message.AppendLine("Context: " + (context ?? string.Empty));
            Exception current = ex;
            while (current != null)
            {
                message.AppendLine("Type   : " + current.GetType().FullName);
                message.AppendLine("Message: " + current.Message);
                message.AppendLine(current.StackTrace ?? "(no stack trace)");
                current = current.InnerException;
            }
            message.AppendLine();
            Write(message.ToString());
        }

        /// <summary>Logs an informational entry.</summary>
        public static void LogInfo(string context, string message)
        {
            StringBuilder line = new StringBuilder();
            line.AppendLine("=== INFO ===");
            line.AppendLine("Time   : " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture));
            line.AppendLine("User   : " + (AppContext.CurrentUser != null ? AppContext.CurrentUser.Username : "(not logged in)"));
            line.AppendLine("Context: " + (context ?? string.Empty));
            line.AppendLine("Message: " + (message ?? string.Empty));
            line.AppendLine();
            Write(line.ToString());
        }

        private static void Write(string content)
        {
            try
            {
                string fileName = Path.Combine(AppContext.LogsFolder,
                    DateTime.Now.ToString("yyyy-MM", CultureInfo.InvariantCulture) + ".log");
                lock (SyncRoot)
                {
                    File.AppendAllText(fileName, content, Encoding.UTF8);
                }
            }
            catch (Exception)
            {
                // Logging must never crash the application; disk problems are silently dropped.
            }
        }
    }
}
