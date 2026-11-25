using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace HR.Helpers
{
    /// <summary>
    /// Application-wide context: current user, permissions, paths and machine info.
    /// Must be initialized at login (SetSession) before any BLL operation.
    /// </summary>
    public static class AppContext
    {
        private static HR.Entities.User _currentUser;
        private static HashSet<string> _permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Currently logged-in user, or null before login.</summary>
        public static HR.Entities.User CurrentUser
        {
            get { return _currentUser; }
        }

        /// <summary>True after a successful login.</summary>
        public static bool IsAuthenticated
        {
            get { return _currentUser != null; }
        }

        /// <summary>Machine name of the workstation running the application.</summary>
        public static string MachineName
        {
            get { return Environment.MachineName; }
        }

        /// <summary>Best-effort local IPv4 address; empty string when unavailable.</summary>
        public static string LocalIpAddress
        {
            get
            {
                try
                {
                    string hostName = Dns.GetHostName();
                    IPHostEntry entry = Dns.GetHostEntry(hostName);
                    foreach (IPAddress address in entry.AddressList)
                    {
                        if (address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            return address.ToString();
                        }
                    }
                }
                catch (Exception)
                {
                    // Network lookup failure is not fatal; caller receives an empty value.
                }
                return string.Empty;
            }
        }

        /// <summary>
        /// Absolute path of the application data folder.
        /// Override in production deployment when data lives on a network share.
        /// </summary>
        public static string DataFolder
        {
            get
            {
                string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
                return baseDirectory;
            }
        }

        /// <summary>Full path of the main database file.</summary>
        public static string DatabasePath
        {
            get
            {
                string configured = System.Configuration.ConfigurationManager.AppSettings["DatabasePath"];
                if (!string.IsNullOrWhiteSpace(configured))
                {
                    if (System.IO.Path.IsPathRooted(configured))
                    {
                        return configured;
                    }
                    return System.IO.Path.Combine(DataFolder, configured);
                }
                return System.IO.Path.Combine(DataFolder, "Database", "ModernTime.mdb");
            }
        }

        /// <summary>Full path of the schema DDL used only when creating a new database file.</summary>
        public static string SchemaPath
        {
            get
            {
                string configured = System.Configuration.ConfigurationManager.AppSettings["SchemaPath"];
                if (!string.IsNullOrWhiteSpace(configured))
                {
                    if (System.IO.Path.IsPathRooted(configured))
                    {
                        return configured;
                    }
                    return System.IO.Path.Combine(DataFolder, configured);
                }
                return System.IO.Path.Combine(DataFolder, "Database", "ModernTime_Schema.sql");
            }
        }

        /// <summary>Directory where log files are written.</summary>
        public static string LogsFolder
        {
            get
            {
                string folder = System.IO.Path.Combine(DataFolder, "Logs");
                if (!System.IO.Directory.Exists(folder))
                {
                    System.IO.Directory.CreateDirectory(folder);
                }
                return folder;
            }
        }

        /// <summary>
        /// Registers an authenticated session. Called only by the Login BLL.
        /// </summary>
        public static void SetSession(HR.Entities.User user, bool isAdmin, IEnumerable<string> permissionKeys)
        {
            if (user == null)
            {
                throw new ArgumentNullException("user");
            }
            _currentUser = user;
            _permissions = new HashSet<string>(permissionKeys ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            IsCurrentUserAdmin = isAdmin;
        }

        /// <summary>Clears the session on logout.</summary>
        public static void ClearSession()
        {
            _currentUser = null;
            _permissions.Clear();
            IsCurrentUserAdmin = false;
        }

        /// <summary>True when the current user belongs to an administrator role.</summary>
        public static bool IsCurrentUserAdmin { get; private set; }

        /// <summary>
        /// Checks a required permission key (for example "Employees.Edit").
        /// Administrators always pass.
        /// </summary>
        public static bool HasPermission(string permissionKey)
        {
            if (!IsAuthenticated)
            {
                return false;
            }
            if (IsCurrentUserAdmin)
            {
                return true;
            }
            return _permissions.Contains(permissionKey);
        }

        /// <summary>
        /// Guards a BLL operation; throws UnauthorizedAccessException with an Arabic
        /// message when the current session lacks the required permission.
        /// </summary>
        public static void RequirePermission(string permissionKey)
        {
            if (!HasPermission(permissionKey))
            {
                throw new System.Security.SecurityException("ليس لديك الصلاحية لتنفيذ هذه العملية: " + permissionKey);
            }
        }

        /// <summary>
        /// True when the current session holds ANY of the given permission keys.
        /// Administrators always pass; an empty list denies (fail-safe).
        /// </summary>
        public static bool HasAnyPermission(string[] permissionKeys)
        {
            if (permissionKeys == null || permissionKeys.Length == 0)
            {
                return false;
            }
            if (IsCurrentUserAdmin)
            {
                return true;
            }
            for (int i = 0; i < permissionKeys.Length; i++)
            {
                if (_permissions.Contains(permissionKeys[i]))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Guards a BLL operation that may be unlocked by several permission keys
        /// (fine-grained matrix: any one of them is sufficient).
        /// </summary>
        public static void RequireAnyPermission(string[] permissionKeys)
        {
            if (!HasAnyPermission(permissionKeys))
            {
                throw new System.Security.SecurityException("ليس لديك الصلاحية لتنفيذ هذه العملية.");
            }
        }
    }
}
