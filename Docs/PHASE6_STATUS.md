# Phase 6 — Fingerprint Integration (الأجهزة والسجلات الخام)

## النطاق المنفَّذ
- `HR.Services/ZkGateway.cs` — بوابة ZKTeco عبر **late binding** (`Type.GetTypeFromProgID("zkemkeeper.CZKEM")` + `InvokeMember`), تدعم SSR_* أولاً مع fallback تلقائي للـ legacy int APIs، وتغليف كل أخطاء COM برسائل عربية.
- `HR.DAL/DeviceDAL.cs`, `HR.DAL/FingerprintDAL.cs`, `HR.BLL/DeviceBLL.cs`, `HR.BLL/FingerprintBLL.cs` — إدارة سجل الأجهزة، تنزيل السجلات، منع التكرار، Audit.
- `HR.Entities/DeviceUserInfo.cs`, `HR.Entities/FingerprintLogView.cs`.
- `HR.UI` — `FrmDevices.cs` (سجل الأجهزة + اختبار اتصال + تنزيل + مستخدمو الجهاز), `FrmDeviceEdit.cs`, `FrmDeviceUsers.cs`, `FrmFingerprintLogs.cs` (السجلات الخام بفلترة جهاز/فترة).
- `FrmMain.cs` — قائمة البصمة موصولة بـ FrmDevices.
- `ModernTime.sln` — أُضيف مشروعا `HR.Services` و`HR.UI` (كانا غائبين). `HR.BLL.csproj` أُضيف له ProjectReference إلى HR.Services.
- Schema: seed `'Fp.MachineNumber' = '1'` في AppSettings (قناة `SettingsBLL.GetInt` تغطي قواعد بيانات قديمة بدون seed).

## قرارات (Rule 36)
1. **Late binding بدل COMReference**: مرجع COM وقت البناء يكسر بناء الحل كاملاً على أي جهاز بدون تسجيل ZKTeco SDK. الربط المتأخر يبني في كل مكان ويفشل برسالة عربية واضحة فقط لحظة استخدام الجهاز. ByRef/out تُكتب تلقائياً في مصفوفة الوسائط عبر IDispatch (لا يمكن التحقق منه إلا على Windows — انظر خطوات الاختبار).
2. **إعادة استخدام صلاحية `Devices.Manage`** الموجودة في seed الـ schema بدلاً من إضافة صلاحيات جديدة (تجنّب ترحيل قاعدة البيانات).
3. **MachineNumber من `AppSettings.Fp.MachineNumber`** بدلاً من عمود جديد في جدول Devices.
4. **`ShiftTemplates` غير موجودة** (قرار المرحلة 4): جدول `Shifts` نفسه مخزن القوالب.

## عيب مُكتشَف ومُصلَح (Rule 33)
- `EmployeeBLL.Search` كان يقصّ `PageSize > 500` إلى 100 بصمت — فكان `ScheduleBLL.LoadMonth` (يمرر 5000) يعرض أول 100 موظف فقط في الجدول الشهري. 
**الإصلاح**: سقف 10,000 بدون تقليص صامت. الملفات المتأثرة: `HR.BLL/EmployeeBLL.cs` (حصراً). **خطوة اختبار**: جهّز أكثر من 100 موظف وافتح الجدول الشهري — يجب ظهورهم جميعاً.

## منع التكرار وعدم فقد السجلات
- مفتاح الاستدلال: `FingerprintDAL.BuildKey(employeeId, enrollCode, logTime, flag)` = `empId|enroll|ticks|flag` (غير المطابَق يُفهرَس على `EmpDevCode` لأن Jet UNIQUE يسمح بـ NULL متكررة).
- إدخال دفعة واحدة داخل Transaction; عند فشل الدفعة → Rollback → إدخال صف-بصف بعد فحص وجود حرفي — **لا يسقط أي سجل وصل من الجهاز ولا يدخل مكرر ضمن نافذة UQ_Log**.
- الملاحظة المعروفة: إعادة ربط موظف بعد تنزيل مسبق غير مطابق قد تنتج شبه-تكرار (نفس اللحظة بكودين) — يُحل محرك المرحلة 7 عبر `Att.DuplicatePunchWindowMin` (فترة إهمال 1 دقيقة افتراضياً).

## تدفق جلسة الجهاز
`Connect_Net` → تحقق Serial (تسجيل فارغ = ربط تلقائي، عدم تطابق = رسالة صريحة) → `EnableDevice(false)` → قراءة كاملة في الذاكرة → `EnableDevice(true)` في `finally` → `Disconnect` → كتابة DB **خارج** جلسة الجهاز. `LastPingDelay` = زمن الجلسة ms، `LastStatus` ∈ Connected/Downloaded/Failed. الحدث: القراءة هنا read-only ولا تعطّل الجهاز.

## خطوات الاختبار على Windows
1. ثبّت **ZKTeco SDK (Stand Alone SDK) إصدار x86** وسجّل `zkemkeeper.dll` (`%windir%\SysWOW64\regsvr32 zkemkeeper.dll` على نظام 64 بت — الحل يعمل x86).
2. من دون SDK: افتح شاشة الأجهزة → **اختبار الاتصال** → يجب رسالة عربية واضحة عن عدم توفر مكتبة ZKTeco (لا انهيار).
3. أضف جهازاً (IP/Port 4370) → **اختبار الاتصال** → يظهر الرقم التسلسلي وآخر حالة = "متصل".
4. غيّر Serial في التعديل إلى قيمة خاطئة → التنزيل يجب أن يفشل برسالة عدم تطابق.
5. **تنزيل السجلات** مرتين متتاليتين: الأولى New = عدد السجلات، الثانية Duplicates = نفس العدد وNew = 0.
6. افحص `SELECT COUNT(*) FROM FingerprintLogs` وأنماط `UQ_Log` — لا تكرارات `(EmployeeId, LogTime, InOutFlag)`.
7. **مستخدمو الجهاز**: مطابقة الأكواد المسجلة في `Employees.DeviceEnrollNumber` تظهر عند الخلفية التحذيرية للغير مطابق.
8. **عرض السجلات الخام**: فترة افتراضية = الشهر الحالي، الصفوف غير المطابقة مميزة بلون.
9. Audit: `Fingerprint.Test` / `Fingerprint.Download` (بعدادات كاملة) / `Fingerprint.ReadUsers` في `AuditLogs`.

## الحالة
البناء البنيوي: PASS على 89 ملف (C# 5 صارم، بلا stubs). التحويل الفعلي يتطلب Visual Studio 2012 على Windows (بيئة الحاوية Linux).
