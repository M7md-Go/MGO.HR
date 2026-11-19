# Phase 2 — DAL + BLL (مكتملة)

## منجز
- `ModernTime.sln` (VS2012/.NET 4.0/Client Profile) + 4 مشاريع: HR.Entities, HR.Helpers, HR.DAL, HR.BLL.
- 38 كيانًا في `HR.Entities` (36 جدولًا + `EmployeeSearchFilter` + `LookupRow`).
- **Database/ModernTime_Schema.sql**: 36 جدولًا (DDL + FK + فهارس + بذور
  الورديات/الإجازات/الأدوار/20+1 صلاحية/21 إعدادًا).
- **HR.DAL**: `Db`، `DbInitializer` (ADOX + تنفيذ المخطط)، `EmployeeDAL`، `LookupDAL`
  (الهيكل التنظيمي مع قائمة جداول Whitelist)، `ShiftDAL`، `UserDAL` (دخول/قفل/صلاحيات)،
  `SettingsDAL`، `AuditDAL`، `EmployeeHistoryDAL`.
- **HR.BLL**: `EmployeeBLL`، `OrganizationBLL`، `ShiftBLL` (تحقق الأوقات وCross-Day
  وBR-06/09)، `UserBLL` (Login/Logout/ChangePassword/Unlock + Bootstrap admin أول مرة +
  صلاحيات فعالة Default-Deny)، `SettingsBLL`، `AuditBLL`، `EmployeeHistoryBLL`، `BusinessException`.
- **HR.Helpers**: `AppContext` (جلسة/صلاحيات/مسارات)، `Logger`، `TimeHelper`،
  `TextSerializer`، `PasswordHasher` (SHA-256 + Salt لكل مستخدم).

## تصحيحات أُثبتت خلال هذه الشريحة (قاعدة §33)
- إزالة `WITH DISALLOW NULL` من فهرس رقم البصمة (كانت تمنع إدخال موظف بلا رقم بصمة).
- إضافة صلاحية `Org.Manage` إلى البذور (شاشات الهيكل التنظيمي).
- `UpdateWithHistory`: التحديث والسجل التاريخي في نفس Transaction (BR-18) — أُزيل الاتصال المتداخل.
- توحيد Update SQL عبر `FillUpdateParameters` لتجنب التكرار.
- إصلاح مسار `FROMEmployees` واستبدال `.ToDictionary()` الوهمية وإضافة
  دوال `PDate/PSingle/PCurrency/PBool` المفقودة في `Db`.

## التالي (المرحلة 3)
شاشات الهيكل التنظيمي + الموظفين (WinForms) مع استدعاء
`DbInitializer.EnsureInitialized()` و`UserBLL.InitializeFirstAdmin()` عند الإقلاع.
