# Phase 10 — Users + Permissions + Audit (Status)

## Files

| File | Lines | Purpose |
|---|---|---|
| `HR.Entities/UserView.cs` | 41 | Users+Roles+Departments join row (never PwdHash/Salt) |
| `HR.Entities/AuditLog.cs` | mod | + `Username` (JOIN-resolved, not a column) |
| `HR.DAL/UserDAL.cs` | 334 | + ListUsers / UpdateProfile / GetAllPermissions / GetAllRoles / CountActiveAdmins / SetUserOverride / SearchLoginHistory |
| `HR.DAL/AuditDAL.cs` | mod | + `using System.Text;` (fix) + Search joins Users for Username |
| `HR.BLL/UserBLL.cs` | ~390 | + ListUsers / GetPermissionsCatalog / GetRoles / GetRoleKeys / GetUserOverrides / UpdateUser / ResetPassword / SetRolePermission / SetUserOverride / last-admin guards |
| `HR.UI/UI/FrmUsers.cs` | 356 | Browser: add / edit / disable-activate / unlock / password reset / open matrix |
| `HR.UI/UI/FrmUserEdit.cs` | 238 | Add (username + role + dept + password confirm) / edit profile |
| `HR.UI/UI/FrmPermissions.cs` | ~490 | Role permission matrix (checkbox per PermissionKey) + per-user overrides (Default/Granted/Revoked) |
| `HR.UI/UI/FrmAuditLog.cs` | 187 | Audit browser: action/table/date filters, latest 1000 rows |
| `HR.UI/UI/FrmMain.cs` | mod | miUsers → FrmUsers; new «سجل العمليات» → FrmAuditLog |

## Rule 33 — defect found and fixed

- `HR.DAL/AuditDAL.cs` used `StringBuilder` **without** `using System.Text;` — a compile-breaking latent defect. Fixed.

## Rule 36 — decisions

1. **Admin role matrix is read-only.** `Roles.IsAdmin=true` means "all permissions by definition" (AppContext short-circuits); editing its matrix would be misleading. Both `SetRolePermission` (BLL) and the UI block it with a clear Arabic message.
2. **Last-admin protection:** disabling an admin account or demoting its role is refused when `CountActiveAdmins() <= 1` (SQL over enabled+unlocked accounts with admin roles). A user also can never change his own role nor disable his own account (`SetDisabled` kept from Phase 1 + new guard in `UpdateUser`).
3. **Per-user overrides (UserPermissions) exposed** as a three-state grid (افتراضي/منح/سحب); "افتراضي" deletes the override row so the role matrix rules again. Effective permissions = role grants + overrides (BR-19 Default-Deny, computed at login by `GetEffectivePermissions`).
4. **Password reset** (admin): new password validated by the same policy (8+ chars, letters+digits), SHA-256 + fresh salt, unblocks the account (IsLocked=0, FailedLogins=0) and audits `Users.ResetPassword` — never writes a password/secret value to the audit log.
5. **Audit permission:** `AuditBLL.Search` requires `Audit.View`; the menu entry is visible to all but non-granted users get the Arabic permission message.

## Rule 21/22 coverage

- Login/logout/lockout/password change existed from Phase 1-2 (`Sec.MaxFailedLogins = 5` from AppSettings).
- Added: user CRUD without any hard delete (soft disable only), matrix + overrides, login history data access, audit `Username` resolution, guards, audit entries: `Users.Add/Update/Unlock/SetDisabled/ChangePassword/ResetPassword/Logout`, `Permissions.Grant/Revoke/UserOverride`.

## Verification (sandbox)

- `python3 /tmp/check_structure.py` → `PASS: 120 files checked`.
- Phase-10 symbol cross-check (UI→BLL→DAL signatures, permission gates, guards, mojibake scan, FrmMain wiring) → OK.

## Windows test steps

1. Build in VS 2012 (x86) → run → login as `admin` / current password.
2. الإدارة → المستخدمون والصلاحيات: create user `test1` (role User) → appears as نشط.
3. تسجيل خروج then login as `test1` → الإدارة menu → المستخدمون: buttons disabled (no `Users.Manage`); opening سجل العمليات shows the Arabic permission message (no `Audit.View`).
4. Login as admin: wrong password 5 times on `test1` → الصفحة "مقفول"; فتح الحساب → unlocked, FailedLogins reset.
5. إعادة تعيين كلمة المرور for `test1` → policy message if weak; with valid one → test1 logs in with the new password.
6. مصفوفة الصلاحيات: select role User → check `Reports.View` → apply → relogin as test1 → reports open. Uncheck → apply → test1 gets the permission message again.
7. تجاوزات: choose `test1`, set `Employees.Edit` = منح, apply, relogin as test1 → employee edit enabled; set back to افتراضي → disabled.
8. Try to disable the only active admin (via its own account) → "لا يمكن إيقاف آخر مسؤول نشط في النظام."
9. سجل العمليات: rows show user name, action, table, record, machine, IP, old/new values; resize columns to read serialized snapshots.

Next: Phase 11 (Dashboard).
