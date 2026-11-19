# Phase 2 — DAL + BLL (قيد التنفيذ)

## منجز
- `ModernTime.sln` (VS2012/.NET 4.0/Client Profile/x86) + 4 مشاريع: HR.Entities, HR.Helpers, HR.DAL, HR.BLL.
- 36 كيان POCO في `HR.Entities` (مرآة 1:1 للجداول) + `EmployeeSearchFilter`.
- `HR.DAL`: `Db` (اتصال Jet4.0، Parameters، Transactions، Record-Level Locking)، `DbInitializer`
  (إنشاء mdb عبر ADOX + تنفيذ `Database/ModernTime_Schema.sql`)، `EmployeeDAL` (بحث بترقيم صفحات
  Keyset + CRUD + SoftDelete + حراس الارتباط)، `SettingsDAL` (Cache)، `AuditDAL`، `EmployeeHistoryDAL`.
- `HR.BLL`: `BusinessException` برسائل عربية، `EmployeeBLL` (تحقق كامل، تعديل مع سجل تغييرات الحقول،
  إيقاف/تفعيل/أرشفة، حذف محمي)، `SettingsBLL` (كل العتبات من AppSettings — لا hardcoding)،
  `AuditBLL`، `EmployeeHistoryBLL`.
- `HR.Helpers`: `AppContext` (جلسة + صلاحيات Default-Deny BR-19)، `Logger` (BR-23)، `TimeHelper`، `TextSerializer`.

## متبقٍ من المرحلة 2
- `OrganizationDAL/BLL` (نفس نمط Employees) — المرحلة 3.
- `ScheduleDAL/BLL` + `MonthlyShiftLocks` — المرحلة 4/5.
- `UserDAL/BLL` + PasswordHasher (SHA-256+Salt) — المرحلة 10.

## التحقق
- فحص بنيوي آلي (توازن الأقواس ومنع صياغات C#6+ مثل interpolation/lambda-expressions
  ومنع TODO/Placeholders) — نظيف.
- البناء النهائي في Visual Studio 2012 على جهاز التشغيل (لا يوجد مترجم C# في بيئة التطوير الحالية).
