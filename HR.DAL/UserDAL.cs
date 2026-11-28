using System;
using System.Collections.Generic;
using System.Data.OleDb;
using System.Text;
using HR.Entities;

namespace HR.DAL
{
    /// <summary>Users, Roles, Permissions and LoginHistory access.</summary>
    public static class UserDAL
    {
        private const string SelectColumns =
            "UserId, Username, PwdHash, Salt, FullName, RoleId, DeptId, IsDisabled, IsLocked, FailedLogins, LastLogin, CreatedAt, CreatedBy, MustChangePassword";

        private static User MapUser(OleDbDataReader reader)
        {
            User user = new User();
            user.UserId = reader.GetInt32(0);
            user.Username = reader.IsDBNull(1) ? null : reader.GetString(1);
            user.PwdHash = reader.IsDBNull(2) ? null : reader.GetString(2);
            user.Salt = reader.IsDBNull(3) ? null : reader.GetString(3);
            user.FullName = reader.IsDBNull(4) ? null : reader.GetString(4);
            user.RoleId = reader.IsDBNull(5) ? (int?)null : reader.GetInt32(5);
            user.DeptId = reader.IsDBNull(6) ? (int?)null : reader.GetInt32(6);
            user.IsDisabled = reader.IsDBNull(7) ? (bool?)null : reader.GetBoolean(7);
            user.IsLocked = reader.IsDBNull(8) ? (bool?)null : reader.GetBoolean(8);
            user.FailedLogins = reader.IsDBNull(9) ? (int?)null : reader.GetInt32(9);
            user.LastLogin = reader.IsDBNull(10) ? (DateTime?)null : reader.GetDateTime(10);
            user.CreatedAt = reader.IsDBNull(11) ? (DateTime?)null : reader.GetDateTime(11);
            user.CreatedBy = reader.IsDBNull(12) ? (int?)null : reader.GetInt32(12);
            user.MustChangePassword = reader.IsDBNull(13) ? (bool?)null : reader.GetBoolean(13);
            return user;
        }

        public static User GetByUsername(string username)
        {
            return Db.ReadOne("SELECT " + SelectColumns + " FROM Users WHERE Username = ?", MapUser, Db.P(username));
        }

        public static User GetById(int userId)
        {
            return Db.ReadOne("SELECT " + SelectColumns + " FROM Users WHERE UserId = ?", MapUser, Db.P(userId));
        }

        public static long CountUsers()
        {
            return Db.Count("SELECT COUNT(*) FROM Users");
        }

        public static int Insert(User user)
        {
            string sql = "INSERT INTO Users (Username, PwdHash, Salt, FullName, RoleId, DeptId, IsDisabled, IsLocked, FailedLogins, CreatedAt, CreatedBy, MustChangePassword) " +
                         "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)";
            return Db.ExecuteInTransaction(delegate(OleDbConnection connection, OleDbTransaction transaction)
            {
                using (OleDbCommand command = new OleDbCommand(sql, connection, transaction))
                {
                    command.Parameters.Add(Db.P(user.Username));
                    command.Parameters.Add(Db.P(user.PwdHash));
                    command.Parameters.Add(Db.P(user.Salt));
                    command.Parameters.Add(Db.P(user.FullName));
                    command.Parameters.Add(Db.PNullableId(user.RoleId));
                    command.Parameters.Add(Db.PNullableId(user.DeptId));
                    command.Parameters.Add(Db.PBool(user.IsDisabled));
                    command.Parameters.Add(Db.PBool(user.IsLocked));
                    command.Parameters.Add(Db.P(0));
                    command.Parameters.Add(Db.PDateRequired(DateTime.Now));
                    command.Parameters.Add(Db.PNullableId(user.CreatedBy));
                    command.Parameters.Add(Db.PBool(user.MustChangePassword));
                    command.ExecuteNonQuery();
                }
                using (OleDbCommand identity = new OleDbCommand("SELECT @@IDENTITY", connection, transaction))
                {
                    return Convert.ToInt32(identity.ExecuteScalar());
                }
            });
        }

        /// <summary>Username duplication check case-insensitive (Jet text compare).</summary>
        public static bool UsernameExists(string username, int? excludeUserId)
        {
            string sql = "SELECT COUNT(*) FROM Users WHERE Username = ?";
            if (excludeUserId != null)
            {
                sql = sql + " AND UserId <> ?";
                return Db.Count(sql, Db.P(username), Db.PNullableId(excludeUserId)) > 0;
            }
            return Db.Count(sql, Db.P(username)) > 0;
        }

        public static void RecordFailedLogin(int userId, int failedLogins, int maxAttempts)
        {
            bool lockAccount = failedLogins >= maxAttempts;
            string sql = "UPDATE Users SET FailedLogins = ?, IsLocked = ? WHERE UserId = ?";
            Db.ExecuteNonQuery(sql, Db.P(failedLogins), Db.P(lockAccount), Db.P(userId));
        }

        public static void RecordSuccessfulLogin(int userId)
        {
            string sql = "UPDATE Users SET FailedLogins = 0, LastLogin = ? WHERE UserId = ?";
            Db.ExecuteNonQuery(sql, Db.PDateRequired(DateTime.Now), Db.P(userId));
        }

        public static void UnlockUser(int userId)
        {
            Db.ExecuteNonQuery("UPDATE Users SET IsLocked = 0, FailedLogins = 0 WHERE UserId = ?", Db.P(userId));
        }

        public static void SetDisabled(int userId, bool disabled)
        {
            Db.ExecuteNonQuery("UPDATE Users SET IsDisabled = ? WHERE UserId = ?", Db.PBool(disabled), Db.P(userId));
        }

        public static void ChangePassword(int userId, string pwdHash, string salt)
        {
            // S-01: a completed password change clears the forced-change flag.
            string sql = "UPDATE Users SET PwdHash = ?, Salt = ?, FailedLogins = 0, IsLocked = 0, MustChangePassword = 0 WHERE UserId = ?";
            Db.ExecuteNonQuery(sql, Db.P(pwdHash), Db.P(salt), Db.P(userId));
        }

        /// <summary>S-01: sets/clears the forced first-login password-change flag.</summary>
        public static void SetMustChangePassword(int userId, bool mustChange)
        {
            Db.ExecuteNonQuery("UPDATE Users SET MustChangePassword = ? WHERE UserId = ?",
                Db.PBool(mustChange), Db.P(userId));
        }

        // ---------------- login history ----------------

        public static void InsertLoginHistory(int? userId, bool success, string machineName, string ipAddress)
        {
            string sql = "INSERT INTO LoginHistory (UserId, AttemptTime, Success, MachineName, IpAddress) VALUES (?, ?, ?, ?, ?)";
            Db.ExecuteNonQuery(sql,
                Db.PNullableId(userId),
                Db.PDateRequired(DateTime.Now),
                Db.PBool(success),
                Db.P(machineName),
                Db.P(ipAddress));
        }

        // ---------------- roles & permissions ----------------

        public static Role GetRole(int roleId)
        {
            return Db.ReadOne("SELECT RoleId, RoleName, IsAdmin FROM Roles WHERE RoleId = ?",
                delegate(OleDbDataReader reader)
                {
                    Role role = new Role();
                    role.RoleId = reader.GetInt32(0);
                    role.RoleName = reader.IsDBNull(1) ? null : reader.GetString(1);
                    role.IsAdmin = reader.IsDBNull(2) ? (bool?)null : reader.GetBoolean(2);
                    return role;
                },
                Db.P(roleId));
        }

        public static Role GetRoleByRoleName(string roleName)
        {
            return Db.ReadOne("SELECT RoleId, RoleName, IsAdmin FROM Roles WHERE RoleName = ?",
                delegate(OleDbDataReader reader)
                {
                    Role role = new Role();
                    role.RoleId = reader.GetInt32(0);
                    role.RoleName = reader.IsDBNull(1) ? null : reader.GetString(1);
                    role.IsAdmin = reader.IsDBNull(2) ? (bool?)null : reader.GetBoolean(2);
                    return role;
                },
                Db.P(roleName));
        }

        /// <summary>Number of accounts assigned to a role (roles with users cannot be deleted).</summary>
        public static long CountUsersInRole(int roleId)
        {
            return Db.Count("SELECT COUNT(*) FROM Users WHERE RoleId = ?", Db.P(roleId));
        }

        /// <summary>Creates a non-admin role with the given name (unique name is enforced by the UNIQUE index).</summary>
        public static int InsertRole(string roleName)
        {
            return Db.ExecuteInTransaction(delegate(OleDbConnection connection, System.Data.OleDb.OleDbTransaction transaction)
            {
                using (System.Data.OleDb.OleDbCommand command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText = "INSERT INTO Roles (RoleName, IsAdmin) VALUES (?, False)";
                    command.Parameters.Add(Db.P(roleName));
                    command.ExecuteNonQuery();
                    return Convert.ToInt32(Db.ExecuteScalarOn(connection, transaction, "SELECT @@IDENTITY"));
                }
            });
        }

        /// <summary>Renames a role.</summary>
        public static void UpdateRoleName(int roleId, string roleName)
        {
            Db.ExecuteNonQuery("UPDATE Roles SET RoleName = ? WHERE RoleId = ?", Db.P(roleName), Db.P(roleId));
        }

        /// <summary>Removes a role and its permission grants (only roleless-user-free roles reach here).</summary>
        public static void DeleteRole(int roleId)
        {
            Db.ExecuteInTransaction(delegate(OleDbConnection connection, System.Data.OleDb.OleDbTransaction transaction)
            {
                Db.ExecuteNonQueryOn(connection, transaction, "DELETE FROM RolePermissions WHERE RoleId = ?", Db.P(roleId));
                Db.ExecuteNonQueryOn(connection, transaction, "DELETE FROM Roles WHERE RoleId = ?", Db.P(roleId));
                return true;
            });
        }

        /// <summary>Permission keys granted to the user's role.</summary>
        public static List<string> GetRolePermissionKeys(int roleId)
        {
            string sql = "SELECT p.PermissionKey FROM RolePermissions rp INNER JOIN Permissions p ON rp.PermissionId = p.PermissionId WHERE rp.RoleId = ?";
            return Db.ReadList(sql, delegate(OleDbDataReader reader) { return reader.GetString(0); }, Db.P(roleId));
        }

        /// <summary>Per-user overrides: key + granted flag.</summary>
        public static List<KeyValuePair<string, bool>> GetUserOverrides(int userId)
        {
            string sql = "SELECT p.PermissionKey, up.IsGranted FROM UserPermissions up INNER JOIN Permissions p ON up.PermissionId = p.PermissionId WHERE up.UserId = ?";
            return Db.ReadList(sql,
                delegate(OleDbDataReader reader)
                {
                    return new KeyValuePair<string, bool>(reader.GetString(0), reader.GetBoolean(1));
                },
                Db.P(userId));
        }

        /// <summary>Grants a permission to a role (Users.Manage screen).</summary>
        public static void GrantToRole(int roleId, int permissionId)
        {
            string sql = "INSERT INTO RolePermissions (RoleId, PermissionId) VALUES (?, ?)";
            try
            {
                Db.ExecuteNonQuery(sql, Db.P(roleId), Db.P(permissionId));
            }
            catch (System.Data.OleDb.OleDbException)
            {
                // Duplicate (RoleId, PermissionId) via UQ_RolePerm — already granted.
            }
        }

        /// <summary>Revokes a permission from a role.</summary>
        public static void RevokeFromRole(int roleId, int permissionId)
        {
            Db.ExecuteNonQuery("DELETE FROM RolePermissions WHERE RoleId = ? AND PermissionId = ?", Db.P(roleId), Db.P(permissionId));
        }

        public static Permission GetPermissionByKey(string permissionKey)
        {
            return Db.ReadOne("SELECT PermissionId, PermissionKey, DescriptionAr FROM Permissions WHERE PermissionKey = ?",
                delegate(OleDbDataReader reader)
                {
                    Permission permission = new Permission();
                    permission.PermissionId = reader.GetInt32(0);
                    permission.PermissionKey = reader.GetString(1);
                    permission.DescriptionAr = reader.IsDBNull(2) ? null : reader.GetString(2);
                    return permission;
                },
                Db.P(permissionKey));
        }

        /// <summary>User + role + department join rows for management screens (never any hash).</summary>
        public static List<UserView> ListUsers()
        {
            string sql =
                "SELECT u.UserId, u.Username, u.FullName, u.RoleId, r.RoleName, u.DeptId, " +
                "dep.DepartmentName, u.IsDisabled, u.IsLocked, u.FailedLogins, u.LastLogin " +
                "FROM ((Users u INNER JOIN Roles r ON u.RoleId = r.RoleId) " +
                "LEFT JOIN Departments dep ON u.DeptId = dep.DepartmentId) " +
                "ORDER BY u.Username";
            return Db.ReadList(sql, delegate(OleDbDataReader reader)
            {
                UserView view = new UserView();
                view.UserId = reader.GetInt32(0);
                view.Username = reader.IsDBNull(1) ? null : reader.GetString(1);
                view.FullName = reader.IsDBNull(2) ? null : reader.GetString(2);
                view.RoleId = reader.IsDBNull(3) ? (int?)null : reader.GetInt32(3);
                view.RoleName = reader.IsDBNull(4) ? null : reader.GetString(4);
                view.DeptId = reader.IsDBNull(5) ? (int?)null : reader.GetInt32(5);
                view.DeptName = reader.IsDBNull(6) ? null : reader.GetString(6);
                view.IsDisabled = reader.IsDBNull(7) ? (bool?)null : reader.GetBoolean(7);
                view.IsLocked = reader.IsDBNull(8) ? (bool?)null : reader.GetBoolean(8);
                view.FailedLogins = reader.IsDBNull(9) ? (int?)null : reader.GetInt32(9);
                view.LastLogin = reader.IsDBNull(10) ? (DateTime?)null : reader.GetDateTime(10);
                return view;
            });
        }

        /// <summary>Updates username / full name / role / department (Profile only, never the password).</summary>
        public static void UpdateProfile(int userId, string username, string fullName, int roleId, int? deptId)
        {
            string sql = "UPDATE Users SET Username = ?, FullName = ?, RoleId = ?, DeptId = ? WHERE UserId = ?";
            Db.ExecuteNonQuery(sql, Db.P(username), Db.P(fullName), Db.P(roleId), Db.PNullableId(deptId), Db.P(userId));
        }

        /// <summary>All permission definitions for the matrix screen.</summary>
        public static List<Permission> GetAllPermissions()
        {
            return Db.ReadList("SELECT PermissionId, PermissionKey, DescriptionAr FROM Permissions ORDER BY PermissionKey",
                delegate(OleDbDataReader reader)
                {
                    Permission permission = new Permission();
                    permission.PermissionId = reader.GetInt32(0);
                    permission.PermissionKey = reader.IsDBNull(1) ? null : reader.GetString(1);
                    permission.DescriptionAr = reader.IsDBNull(2) ? null : reader.GetString(2);
                    return permission;
                });
        }

        /// <summary>All roles for the matrix / user edit combos.</summary>
        public static List<Role> GetAllRoles()
        {
            return Db.ReadList("SELECT RoleId, RoleName, IsAdmin FROM Roles ORDER BY RoleName",
                delegate(OleDbDataReader reader)
                {
                    Role role = new Role();
                    role.RoleId = reader.GetInt32(0);
                    role.RoleName = reader.IsDBNull(1) ? null : reader.GetString(1);
                    role.IsAdmin = reader.IsDBNull(2) ? (bool?)null : reader.GetBoolean(2);
                    return role;
                });
        }

        /// <summary>Active (enabled, unlocked) accounts whose role is an admin role.</summary>
        public static long CountActiveAdmins()
        {
            return Db.Count(
                "SELECT COUNT(*) FROM (Users u INNER JOIN Roles r ON u.RoleId = r.RoleId) " +
                "WHERE r.IsAdmin = true AND (u.IsDisabled = false OR u.IsDisabled IS NULL) " +
                "AND (u.IsLocked = false OR u.IsLocked IS NULL)");
        }

        /// <summary>Sets (or clears with granted = null) a per-user permission override.</summary>
        public static void SetUserOverride(int userId, int permissionId, bool? granted)
        {
            if (granted == null)
            {
                Db.ExecuteNonQuery("DELETE FROM UserPermissions WHERE UserId = ? AND PermissionId = ?",
                    Db.P(userId), Db.P(permissionId));
                return;
            }
            string update = "UPDATE UserPermissions SET IsGranted = ? WHERE UserId = ? AND PermissionId = ?";
            int affected = Db.ExecuteNonQuery(update, Db.PBool(granted), Db.P(userId), Db.P(permissionId));
            if (affected == 0)
            {
                try
                {
                    Db.ExecuteNonQuery("INSERT INTO UserPermissions (UserId, PermissionId, IsGranted) VALUES (?, ?, ?)",
                        Db.P(userId), Db.P(permissionId), Db.PBool(granted));
                }
                catch (OleDbException)
                {
                    // Duplicate (UserId, PermissionId) via UQ_UserPerm: a concurrent write already inserted the row.
                }
            }
        }

        /// <summary>Recent login attempts for the session-security view.</summary>
        public static List<LoginHistory> SearchLoginHistory(int? userId, int maxRows)
        {
            StringBuilder sql = new StringBuilder(
                "SELECT TOP " + maxRows + " LoginId, UserId, AttemptTime, Success, MachineName, IpAddress FROM LoginHistory");
            List<OleDbParameter> parameters = new List<OleDbParameter>();
            if (userId != null)
            {
                sql.Append(" WHERE UserId = ?");
                parameters.Add(Db.P(userId));
            }
            sql.Append(" ORDER BY AttemptTime DESC, LoginId DESC");
            return Db.ReadList(sql.ToString(), delegate(OleDbDataReader reader)
            {
                LoginHistory history = new LoginHistory();
                history.LoginId = reader.GetInt32(0);
                history.UserId = reader.IsDBNull(1) ? (int?)null : reader.GetInt32(1);
                history.AttemptTime = reader.IsDBNull(2) ? (DateTime?)null : reader.GetDateTime(2);
                history.Success = reader.IsDBNull(3) ? (bool?)null : reader.GetBoolean(3);
                history.MachineName = reader.IsDBNull(4) ? null : reader.GetString(4);
                history.IpAddress = reader.IsDBNull(5) ? null : reader.GetString(5);
                return history;
            }, parameters.ToArray());
        }
    }
}
