using System;
using System.Collections.Generic;
using System.Data.OleDb;
using HR.Entities;

namespace HR.DAL
{
    /// <summary>Users, Roles, Permissions and LoginHistory access.</summary>
    public static class UserDAL
    {
        private const string SelectColumns =
            "UserId, Username, PwdHash, Salt, FullName, RoleId, DeptId, IsDisabled, IsLocked, FailedLogins, LastLogin, CreatedAt, CreatedBy";

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
            string sql = "INSERT INTO Users (Username, PwdHash, Salt, FullName, RoleId, DeptId, IsDisabled, IsLocked, FailedLogins, CreatedAt, CreatedBy) " +
                         "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)";
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
            string sql = "UPDATE Users SET PwdHash = ?, Salt = ?, FailedLogins = 0, IsLocked = 0 WHERE UserId = ?";
            Db.ExecuteNonQuery(sql, Db.P(pwdHash), Db.P(salt), Db.P(userId));
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
    }
}
