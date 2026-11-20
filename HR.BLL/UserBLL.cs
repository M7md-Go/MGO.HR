using System;
using System.Collections.Generic;
using HR.DAL;
using HR.Entities;
using HR.Helpers;

namespace HR.BLL
{
    /// <summary>
    /// Authentication and user management (Phase 1 W4 / rule 21, decision D6):
    /// SHA-256 + salt, failed-attempt lockout from AppSettings, effective
    /// permissions Default-Deny (BR-19) loaded into AppContext at login.
    /// </summary>
    public static class UserBLL
    {
        private const string DefaultAdminUsername = "admin";
        private const string DefaultAdminPassword = "Admin@123";

        /// <summary>Login flow: verification, lockout, permission load, session, audit.</summary>
        public static User Login(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                throw new BusinessException("يرجى إدخال اسم المستخدم وكلمة المرور.");
            }
            int maxAttempts = SettingsBLL.GetInt("Sec.MaxFailedLogins", 5);
            username = username.Trim();

            User user = UserDAL.GetByUsername(username);
            if (user == null)
            {
                RecordLogin(null, false);
                throw new BusinessException("اسم المستخدم أو كلمة المرور غير صحيحة.");
            }
            if (user.IsDisabled == true)
            {
                RecordLogin(user.UserId, false);
                throw new BusinessException("الحساب موقوف. تواصل مع مسؤول النظام.");
            }
            if (user.IsLocked == true)
            {
                RecordLogin(user.UserId, false);
                throw new BusinessException("الحساب مقفل بسبب محاولات دخول فاشلة. يرجى التواصل مع مسؤول النظام لإعادة التفعيل.");
            }
            if (!PasswordHasher.Verify(password, user.Salt, user.PwdHash))
            {
                int failedLogins = (user.FailedLogins ?? 0) + 1;
                UserDAL.RecordFailedLogin(user.UserId, failedLogins, maxAttempts);
                RecordLogin(user.UserId, false);
                if (failedLogins >= maxAttempts)
                {
                    throw new BusinessException("تم قفل الحساب بعد " + failedLogins + " محاولات فاشلة. تواصل مع مسؤول النظام.");
                }
                throw new BusinessException("اسم المستخدم أو كلمة المرور غير صحيحة. المحاولات المتبقية: " + (maxAttempts - failedLogins));
            }

            UserDAL.RecordSuccessfulLogin(user.UserId);
            RecordLogin(user.UserId, true);

            bool isAdmin = false;
            if (user.RoleId != null)
            {
                Role role = UserDAL.GetRole(user.RoleId.Value);
                isAdmin = role != null && role.IsAdmin == true;
            }
            List<string> permissions = GetEffectivePermissions(user);
            AppContext.SetSession(user, isAdmin, permissions);
            Logger.LogInfo("UserBLL.Login", "user=" + username);
            return user;
        }

        /// <summary>Logs out the current session with an audit entry.</summary>
        public static void Logout()
        {
            if (AppContext.IsAuthenticated)
            {
                AuditBLL.Log("Users.Logout", "Users", AppContext.CurrentUser.UserId.ToString(), null, null);
            }
            AppContext.ClearSession();
        }

        /// <summary>Effective permissions: role grants adjusted by user overrides (BR-19 Default-Deny).</summary>
        public static List<string> GetEffectivePermissions(User user)
        {
            List<string> effective = new List<string>();
            if (user == null || user.RoleId == null)
            {
                return effective;
            }
            List<string> roleKeys = UserDAL.GetRolePermissionKeys(user.RoleId.Value);
            Dictionary<string, bool> overrides = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, bool> pair in UserDAL.GetUserOverrides(user.UserId))
            {
                overrides[pair.Key] = pair.Value;
            }
            foreach (string roleKey in roleKeys)
            {
                if (!overrides.ContainsKey(roleKey))
                {
                    effective.Add(roleKey);
                }
            }
            foreach (KeyValuePair<string, bool> pair in overrides)
            {
                if (!effective.Contains(pair.Key))
                {
                    effective.Add(pair.Key);
                }
            }
            return effective;
        }

        // ---------------- first admin bootstrap ----------------

        /// <summary>
        /// Creates the initial admin account only when the Users table is empty.
        /// Initial credentials: admin / Admin@123 — must be changed at first login.
        /// </summary>
        public static bool InitializeFirstAdmin()
        {
            if (UserDAL.CountUsers() > 0)
            {
                return false;
            }
            Role adminRole = UserDAL.GetRoleByRoleName("Admin");
            if (adminRole == null)
            {
                throw new BusinessException("لم يتم العثور على دور Admin في قاعدة البيانات.");
            }
            string salt = PasswordHasher.NewSalt();
            User admin = new User();
            admin.Username = DefaultAdminUsername;
            admin.PwdHash = PasswordHasher.Hash(DefaultAdminPassword, salt);
            admin.Salt = salt;
            admin.FullName = "مسؤول النظام";
            admin.RoleId = adminRole.RoleId;
            admin.IsDisabled = false;
            admin.IsLocked = false;
            admin.CreatedAt = DateTime.Now;
            int userId = UserDAL.Insert(admin);
            Logger.LogInfo("UserBLL.InitializeFirstAdmin", "bootstrap admin created id=" + userId);
            return true;
        }

        // ---------------- user management ----------------

        /// <summary>Creates a user account (permission "Users.Manage").</summary>
        public static int CreateUser(string username, string fullName, int roleId, string password, int? deptId)
        {
            AppContext.RequirePermission("Users.Manage");
            if (string.IsNullOrWhiteSpace(username))
            {
                throw new BusinessException("اسم المستخدم إلزامي.");
            }
            username = username.Trim();
            if (UserDAL.UsernameExists(username, null))
            {
                throw new BusinessException("اسم المستخدم '" + username + "' مستخدم بالفعل.");
            }
            ValidatePassword(password);
            string salt = PasswordHasher.NewSalt();
            User user = new User();
            user.Username = username;
            user.PwdHash = PasswordHasher.Hash(password, salt);
            user.Salt = salt;
            user.FullName = fullName;
            user.RoleId = roleId;
            user.DeptId = deptId;
            user.IsDisabled = false;
            user.IsLocked = false;
            user.CreatedBy = AppContext.CurrentUser != null ? (int?)AppContext.CurrentUser.UserId : null;
            int newId = UserDAL.Insert(user);
            AuditBLL.Log("Users.Add", "Users", newId.ToString(), null, username);
            return newId;
        }

        /// <summary>Changes the password of the current session user (or another user by admins).</summary>
        public static void ChangePassword(int userId, string currentPassword, string newPassword)
        {
            if (!AppContext.IsAuthenticated)
            {
                throw new BusinessException("الجلسة غير مسجلة الدخول.");
            }
            if (AppContext.CurrentUser.UserId != userId && AppContext.HasPermission("Users.Manage") == false)
            {
                throw new BusinessException("لا يمكنك تغيير كلمة مرور مستخدم آخر.");
            }
            User user = UserDAL.GetById(userId);
            if (user == null)
            {
                throw new BusinessException("المستخدم غير موجود.");
            }
            if (!PasswordHasher.Verify(currentPassword, user.Salt, user.PwdHash))
            {
                throw new BusinessException("كلمة المرور الحالية غير صحيحة.");
            }
            ValidatePassword(newPassword);
            string salt = PasswordHasher.NewSalt();
            UserDAL.ChangePassword(userId, PasswordHasher.Hash(newPassword, salt), salt);
            User afterChange = UserDAL.GetById(userId);
            afterChange.PwdHash = null;
            afterChange.Salt = null;
            AuditBLL.Log("Users.ChangePassword", "Users", userId.ToString(), null, afterChange.Username);
        }

        /// <summary>Re-enables a locked account (permission "Users.Manage").</summary>
        public static void Unlock(int userId)
        {
            AppContext.RequirePermission("Users.Manage");
            UserDAL.UnlockUser(userId);
            AuditBLL.Log("Users.Unlock", "Users", userId.ToString(), "Locked", "Unlocked");
        }

        public static void SetDisabled(int userId, bool disabled)
        {
            AppContext.RequirePermission("Users.Manage");
            if (AppContext.CurrentUser != null && AppContext.CurrentUser.UserId == userId && disabled)
            {
                throw new BusinessException("لا يمكنك إيقاف حسابك الحالي.");
            }
            if (disabled)
            {
                User target = UserDAL.GetById(userId);
                if (target != null && target.RoleId != null)
                {
                    Role targetRole = UserDAL.GetRole(target.RoleId.Value);
                    if (targetRole != null && targetRole.IsAdmin == true && UserDAL.CountActiveAdmins() <= 1)
                    {
                        throw new BusinessException("لا يمكن إيقاف آخر مسؤول نشط في النظام.");
                    }
                }
            }
            UserDAL.SetDisabled(userId, disabled);
            AuditBLL.Log("Users.SetDisabled", "Users", userId.ToString(), null, disabled ? "Disabled" : "Enabled");
        }

        // ---------------- Phase 10: browse + profile + security ----------------

        /// <summary>User list for the management screen (never any hash) — permission "Users.Manage".</summary>
        public static List<UserView> ListUsers()
        {
            AppContext.RequirePermission("Users.Manage");
            return UserDAL.ListUsers();
        }

        /// <summary>Raw permission keys granted to a role (permission "Users.Manage").</summary>
        public static List<string> GetRoleKeys(int roleId)
        {
            AppContext.RequirePermission("Users.Manage");
            return UserDAL.GetRolePermissionKeys(roleId);
        }

        /// <summary>Per-user overrides for the matrix screen (permission "Users.Manage").</summary>
        public static List<KeyValuePair<string, bool>> GetUserOverrides(int userId)
        {
            AppContext.RequirePermission("Users.Manage");
            return UserDAL.GetUserOverrides(userId);
        }

        /// <summary>Permission catalog for the matrix screen — permission "Users.Manage".</summary>
        public static List<Permission> GetPermissionsCatalog()
        {
            AppContext.RequirePermission("Users.Manage");
            return UserDAL.GetAllPermissions();
        }

        /// <summary>Recent login attempts — permission "Audit.View" (security trail).</summary>
        public static List<LoginHistory> GetLoginHistory(int? userId, int maxRows)
        {
            AppContext.RequirePermission("Audit.View");
            return UserDAL.SearchLoginHistory(userId, maxRows);
        }

        /// <summary>Role list for combos — permission "Users.Manage".</summary>
        public static List<Role> GetRoles()
        {
            AppContext.RequirePermission("Users.Manage");
            return UserDAL.GetAllRoles();
        }

        /// <summary>
        /// Updates username / full name / role / department (rule 21 + BR-20):
        /// unique username, a user cannot change his own role, and the last
        /// active admin account cannot be demoted (audit "Users.Update").
        /// </summary>
        public static void UpdateUser(int userId, string username, string fullName, int roleId, int? deptId)
        {
            AppContext.RequirePermission("Users.Manage");
            if (string.IsNullOrWhiteSpace(username))
            {
                throw new BusinessException("اسم المستخدم إلزامي.");
            }
            username = username.Trim();
            if (UserDAL.UsernameExists(username, userId))
            {
                throw new BusinessException("اسم المستخدم '" + username + "' مستخدم بالفعل.");
            }
            User user = UserDAL.GetById(userId);
            if (user == null)
            {
                throw new BusinessException("المستخدم غير موجود.");
            }
            if (AppContext.CurrentUser != null && AppContext.CurrentUser.UserId == userId && user.RoleId != roleId)
            {
                throw new BusinessException("لا يمكن تغيير دور حسابك الحالي.");
            }
            if (user.RoleId != null && user.RoleId != roleId)
            {
                Role currentRole = UserDAL.GetRole(user.RoleId.Value);
                if (currentRole != null && currentRole.IsAdmin == true && UserDAL.CountActiveAdmins() <= 1)
                {
                    throw new BusinessException("لا يمكن تنزيل دور آخر مسؤول نشط في النظام.");
                }
            }
            UserDAL.UpdateProfile(userId, username, fullName, roleId, deptId);
            AuditBLL.Log("Users.Update", "Users", userId.ToString(), user.Username, username);
        }

        /// <summary>Admin password reset (permission "Users.Manage") — audit without any secret value.</summary>
        public static void ResetPassword(int userId, string newPassword)
        {
            AppContext.RequirePermission("Users.Manage");
            User user = UserDAL.GetById(userId);
            if (user == null)
            {
                throw new BusinessException("المستخدم غير موجود.");
            }
            ValidatePassword(newPassword);
            string salt = PasswordHasher.NewSalt();
            UserDAL.ChangePassword(userId, PasswordHasher.Hash(newPassword, salt), salt);
            UserDAL.UnlockUser(userId);
            AuditBLL.Log("Users.ResetPassword", "Users", userId.ToString(), null, user.Username);
        }

        /// <summary>
        /// Grants / revokes a permission on a role (permission "Users.Manage").
        /// The Admin role is intentionally not editable: it is IsAdmin and has
        /// every permission by definition (rule 36 decision).
        /// </summary>
        public static void SetRolePermission(int roleId, int permissionId, bool granted)
        {
            AppContext.RequirePermission("Users.Manage");
            Role role = UserDAL.GetRole(roleId);
            if (role == null)
            {
                throw new BusinessException("الدور غير موجود.");
            }
            if (role.IsAdmin == true)
            {
                throw new BusinessException("دور Admin يملك جميع الصلاحيات ولا يجوز تعديل مصفوفته.");
            }
            if (granted)
            {
                UserDAL.GrantToRole(roleId, permissionId);
                AuditBLL.Log("Permissions.Grant", "RolePermissions", roleId + ":" + permissionId, null, "Granted");
            }
            else
            {
                UserDAL.RevokeFromRole(roleId, permissionId);
                AuditBLL.Log("Permissions.Revoke", "RolePermissions", roleId + ":" + permissionId, "Granted", null);
            }
        }

        /// <summary>
        /// Sets (or clears with granted = null) a per-user override on top of
        /// the role matrix (permission "Users.Manage").
        /// </summary>
        public static void SetUserOverride(int userId, int permissionId, bool? granted)
        {
            AppContext.RequirePermission("Users.Manage");
            User user = UserDAL.GetById(userId);
            if (user == null)
            {
                throw new BusinessException("المستخدم غير موجود.");
            }
            if (user.RoleId != null)
            {
                Role role = UserDAL.GetRole(user.RoleId.Value);
                if (role != null && role.IsAdmin == true)
                {
                    throw new BusinessException("طالما الحساب بدور Admin فلا حاجة لتجاوزات صلاحيات.");
                }
            }
            UserDAL.SetUserOverride(userId, permissionId, granted);
            AuditBLL.Log("Permissions.UserOverride", "UserPermissions",
                userId + ":" + permissionId, null,
                granted == null ? "Default" : (granted.Value ? "Granted" : "Revoked"));
        }

        /// <summary>Password policy: at least 8 characters, letters and digits (rule 21).</summary>
        private static void ValidatePassword(string password)
        {
            if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
            {
                throw new BusinessException("كلمة المرور يجب أن تكون 8 أحرف على الأقل.");
            }
            bool hasLetter = false;
            bool hasDigit = false;
            foreach (char character in password)
            {
                if (char.IsLetter(character))
                {
                    hasLetter = true;
                }
                if (char.IsDigit(character))
                {
                    hasDigit = true;
                }
            }
            if (!hasLetter || !hasDigit)
            {
                throw new BusinessException("كلمة المرور يجب أن تحتوي على أحرف وأرقام معًا.");
            }
        }

        private static void RecordLogin(int? userId, bool success)
        {
            try
            {
                UserDAL.InsertLoginHistory(userId, success, AppContext.MachineName, AppContext.LocalIpAddress);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "UserBLL.RecordLogin failed");
            }
        }
    }
}
