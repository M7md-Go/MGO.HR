# TECHNICAL_AUDIT.md — ModernTime 2.0 Enterprise
## المرحلة 1: الفحص الشامل (Technical Audit) — v1.5.0 → 2.0

> **منهجية**: لا مشكلة مسجَّلة هنا دون دليل من الكود (ملف:سطر) أو من سلوك موثَّق.
> التقييمات التنفيذية (لم تُختبر تشغيليًا) موسومة صراحة. البيئة الحالية للتطوير
> لا تحتوي mono/msbuild — الإثبات الوحيد المتاح للترجمة هو GitHub Actions
> (مُصرِّف حقيقي)، والاختبار التشغيلي يتطلب جهاز Windows فعلي.

---

## 1. خريطة المشروع

### 1.1 المكونات والحجم الفعلي (مُقاس)

| المشروع | ملفات .cs | أسطر | المسؤولية |
|---|---|---|---|
| HR.UI | 70 | 21,740 | WinForms فقط — **صفر SQL** (تحقق آلي: لا OleDb*/SELECT في HR.UI) |
| HR.BLL | 31 | 6,950 | قواعد العمل + التحقق + الصلاحيات (RequirePermission) + Audit |
| HR.DAL | 29 | 5,357 | ADO.NET/Jet — Db مركزي (P/PCurrency/ExecuteInTransaction/QueryMonitor) |
| HR.Entities | 67 | 2,303 | كيانات + DTOs + Filters |
| HR.Services | 6 | 2,540 | AttendanceEngine (حاسِب نقي بلا DB)، ReportEngine، ExcelWriter، DashboardEngine، ZkGateway |
| HR.Helpers | 7 | 602 | Logger، AppContext، PasswordHasher، TimeHelper، WeekRestDays |
| **المجموع** | **210** | **39,492** | |

### 1.2 الإعدادات الفعلية (متحقَّق منها من csproj)
- TargetFrameworkVersion = **v4.0** في المشاريع الستة ✓
- `TargetFrameworkProfile=Client` (Client Profile) في HR.UI ✓
- PlatformTarget = **x86** في HR.UI ✓ (Jet OLEDB 4.0 لا يعمل إلا 32-bit)
- csproj تستخدم `Compile Include="**\*.cs"` (wildcard) → إضافة ملفات جديدة لا تتطلب تعديل csproj.

### 1.3 الملفات الأكبر (مرشحة للتقسيم لاحقًا)
| الملف | الأسطر | ملاحظة |
|---|---|---|
| HR.UI/UI/FrmMonthlySchedule.cs | 1,722 | أكبر نموذج؛ تم تفرخه جزئيًا (FrmEmployeeSchedule/FrmScheduleChanges) في v1.4/v1.5 |
| HR.Services/ReportEngine.cs | 1,404 | محرك تقارير مبني على string-building |
| HR.BLL/DemoDataBLL.cs | 928 | بيانات تجريبية فقط — غير في المسار الإنتاجي |
| HR.UI/UI/FrmEmployeeEdit.cs | 852 | |
| HR.BLL/ScheduleBLL.cs | 782 | |

### 1.4 الملفات غير المستخدمة
- لم يُعثر على ملفات ميتة: `BarChartPanel`, `FrmPreviewHost`, `QueryMonitor`, `DbSchema`, `ZkGateway` كلها مستخدمة (تحقق grep بالاسم المرمزي).
- كيان واحد له using غير مستخدم (`ScheduleChangeRequest.cs` — `System.Collections.Generic`) — P3.

---

## 2. المشكلات المكتشفة

### 🔴 P0 — حرجة (بيانات/عمليات أساسية)

#### A-01: اختلاط أرقام الأيام في إعادة احتساب فترة متعددة الأشهر
- **الموقع**: `HR.BLL/AttendanceBLL.cs:61-87` (المنتج) + `HR.Services/AttendanceEngine.cs:103` (المستهلك) + `HR.Entities/EngineModels.cs:42`.
- **السبب الجذري**: `Recalculate` يدمج خلايا الجدول لعدة أشهر في قاموس واحد
  `Dictionary<int employeeId, Dictionary<int dayOfMonth, int shiftId>>`،
  والمحرك يحل الوردية بـ `cells[date.Day]`. عند فترة تمتد لأكثر من شهر
  (النظام يدعم حتى 93 يومًا) تتداخل أرقام الأيام: خلايا الشهر الأحدث
  **تستبدل** خلايا الشهر الأقدم لنفس رقم اليوم، ثم يُحسب **كل** تاريخ في
  النافذة بورديات الشهر الأخير.
- **السيناريو المؤكد**: إعادة حساب «1 يناير → 31 مارس» → كل أيام يناير وفبراير
  تُحسب بورديات مارس (لأن مفتاح 1..31 عُوِّض بآخر شهر). نتيجة: حالات حضور/
  غياب/إضافات/استثناءات **خاطئة** تُكتب في قاعدة البيانات.
- **حالة الإصلاح**: **أُصلح في هذا الفرع** — المفتاح أصبح التاريخ الكامل
  (`DateTime.Ticks` = مفهوم EmployeeId + FullDate المطلوب) في الملفات الثلاثة.
- **طريقة الاختبار**: (1) إنشاء جدولين بورديات مختلفة لشهرين متتاليين؛
  (2) إعادة حساب الفترة كاملة؛ (3) التحقق أن `Attendance.ShiftId` لكل تاريخ
  يطابق جدول شهره — يتطلب جهاز Windows فعلي (CI = تجميع فقط).

### 🟠 P1 — مهمة (صحة/أمان النظام)

#### S-01: كلمة مرور admin الافتراضية غير مُفروض تغييرها
- **الدليل**: `HR.BLL/UserBLL.cs:117-142` — تعليق الدالة يقول
  "must be changed at first login" لكن **لا يوجد حقل MustChangePassword ولا
  آلية إجبار** (grep شامل: صفر نتائج). كلمة `Admin@123` الافتراضية تبقى صالحة
  إلى ما لا نهاية إذا لم يغيّرها المستخدم طوعًا.
- **التأثير**: حساب بصلاحيات كاملة بكلمة مرور معروفة ومكتوبة في الكود.
- **الحل المقترح** (يتطلب موافقة + ترحيل مخطط): عمود `Users.MustChangePassword`
  (YESNO) عبر EnsureColumn + إجبار نافذة تغيير عند الدخول + منع إعادة تعيين
  كلمة المرور الافتراضية عبر ValidatePassword.
- **الاختبار**: دخول أول → إجبار التغيير؛ رفض كلمة مرور افتراضية عند إنشاء مستخدم.

#### S-02: تجزئة كلمات المرور بـ SHA-256 مرة واحدة
- **الدليل**: `HR.Helpers/PasswordHasher.cs:41` — SHA-256 واحد على salt+password.
- **التقييم**: السق←salt عشوائي 32B + مقارنة ثابتة الزمن (جيد)، لكن التكرار
  الواحد يجعل الهجوم دون اتصال سريعًا جدًا إذا سُرِب ملف MDB.
- **الحل** (المرحلة 8): `Rfc2898DeriveBytes` (PBKDF2 — متوفر في .NET 4.0) مع
  عدد تكرارات + إصدار خوارزمية مخزنان، وترقية تدريجية عند أول دخول ناجح
  بصيغة SHA-256 القديمة (بدون إجبار تغيير كلمات المرور الحالية).

#### B-01: نسخ احتياطي لملف MDB حي عبر File.Copy
- **الدليل**: `HR.BLL/BackupBLL.cs:78` (`File.Copy(databasePath, targetPath)`)
  و`:210` (الاستعادة). Jet يدعم تعدد المستخدمين؛ نسخ ملف أثناء كتابة متزامنة
  (مستخدم آخر على الشبكة أو مؤشر ترابط) قد يعطي نسخة تالفة. يوجد
  `VerifyBackupFile` (:142) لكن **لا يُستدعى بعد النسخ** — فقط قبل الاستعادة.
- **الحل المقترح**: (أ) فتح اتصال Jet بوضع حصري قبل النسخ لضمان السكون، أو على
  الأقل (ب) التحقق الفوري من النسخة بعد إنشائها وتسجيل الحالة "Verified/Failed"
  في BackupLogs بدل "Completed" الأعمى.
- **الاختبار**: نسخة مع محاكاة كتابة متزامنة → التحقق من كشف التلف.

### 🟡 P2 — معمارية/أداء/صيانة (موثقة بأدلة، الإصلاح لاحقًا بجدولة)

| # | المشكلة | الدليل | الأثر | الحل المقترح |
|---|---|---|---|---|
| R-01 | حلّ موظف يدوي مكرر بمنطق أضعف من المشترك | `FrmEmployeeSchedule.cs` (PageSize=1) و`FrmScheduleChanges.cs` (CreateDialog) بدل `EmployeeResolve.FromText` | أول مطابقة تُقبل رغم الغموض؛ ازدواجية صيانة | توحيد على EmployeeResolve |
| R-02 | حلقة تلوين العطلات الرسمية مكررة | `FrmMonthlySchedule.cs` + `FrmEmployeeSchedule.cs` (نفس كتلة PublicHolidayBLL.List + recurring) | ازدواجية منطق عطلة الشهر | مساعد مشترك `ScheduleUiHelper.HolidaysInMonth(year,month)` |
| R-03 | مصفوفات أسماء الأشهر/الأيام العربية مكررة | `FrmMonthlySchedule.MonthNames/WeekdayAr` + `FrmEmployeeSchedule.ArabicMonth/DayNameAr` | صيانة نصوص موزعة | نقلها إلى Theme أو Helper واحد |
| R-04 | نافذتا سبب إلزامي شبه متطابقتين | `ReasonDialog` (FrmMonthlySchedule) و`DecideDialog` (FrmScheduleChanges) | ازدواجية | مكوّن حوار مشترك |
| A-02 | كلاسات ضخمة | FrmMonthlySchedule (1,722)، ReportEngine (1,404)، ScheduleBLL (782) | صعوبة اختبار وصيانة | تقسيم تدريجي **بعد** رصده (لا تحريك أعمى) |
| E-01 | حذف استثناءات أيام التعديل اليدوي عند إعادة الحساب | `AttendanceDAL.WriteEngineResults:204` يحذف الاستثناءات غير اليدوية لكل processedKeys بما فيها أيام SkipKeys ثم لا يعيد توليدها | فقد استثناءات محرك تاريخية لأيام عدلها مستخدم يدويًا — سلوك محتمل القبول لكنه غير موثق للمستخدم | قرار: إما استثناء أيام SkipKeys من الحذف أو توثيق السلوك في الواجهة |
| E-02 | `CalculateRestDayWork` لا يلتقط بصمات ما بعد منتصف الليل لليوم المرتاح | `AttendanceEngine.cs` (`dayEnd = date.Date.AddDays(1)`) | ساعات عمل يوم راحة تمتد لما بعد 00:00 تُنسب لليوم التالي | مراجعة مع حالة اختبار وردية راحة ليلية |
| D-01 | التاريخ ممثَّل بصيغتين (AttDate تاريخ فقط + StartTime/EndTimestamp كامل) | المخطط + `AttendanceDAL` | قابل للالتباس في التقارير | توحيد الاصطلاح (تاريخ فقط للتواريخ، timestamp للبطاقات) + توثيق |

### 🟢 P3 — تحسينات شكلية/تجربة مستخدم
- Using غير مستخدم في `ScheduleChangeRequest.cs`.
- FrmMonthlySchedule يبني 33 عمود Combo لكل صف (1000 موظف) — يظل مستجيبًا
  عادةً؛ VirtualMode خيار مستقبلي إن ظهرت بطء (قياس أولًا — لم يُثبت بطء).
- رسائل الأخطاء موحّدة عبر Theme.ShowError/ShowInfo — جيدة؛ تبقى مراجعة تجربة
  مستخدم شاملة لمرحلة 6.

---

## 3. نقاط القوة المؤكدة (حتى لا تُعالَج كمشكلات)
1. **صفر SQL في الواجهات** (تحقق آلي شامل HR.UI) — قاعدة المعمارية محترمة.
2. الصلاحيات تُفحص في BLL (`RequirePermission`) وليس إخفاء أزرار فقط.
3. Logger مركزي + Audit لكل عملية كتابة حساسة + QueryMonitor للاستعلامات.
4. محرك الحضور **حاسِب نقي** (لا DB) — قابل للاختبار بالعزل.
5. الحماية اليدوية: `SkipKeys` من `IsManuallyChg=True` قبل الحساب، و`WriteEngineResults` idempotent (حذف استثناءات غير يدوية + upsert حسب المفتاح + حماية إضافات Auto/Pending).
6. كلمات المرور: SHA-256 + salt فردي + مقارنة ثابتة الزمن + قفل بعد محاولات فاشلة + سجل دخول.
7. الاستعادة ترفض نسخة غير صالحة برسالة عربية صريحة وتصنع نسخة أمان قبلها.
8. 20 فهرسًا + 10 قيود UNIQUE تشمل الجداول الساخنة (Attendance EmployeeId+AttDate مضمون بقيد UQ_Att، FingerprintLogs EmployeeId+LogTime) — التفصيل في DATABASE_REFACTORING.md.

## 4. توزيع العمل على المراحل (خطة التنفيذ المرتبطة بالملفات)

| المرحلة | المحتوى | الملفات |
|---|---|---|
| 2 (فورية) | A-01 ✓ (منجز)، S-01 إجبار تغيير كلمة المرور، B-01 تحقق ما بعد النسخ، E-01 قرار موثق | AttendanceBLL/EngineModels/AttendanceEngine، UserBLL/DbInitializer/DbSchema/FrmLogin، BackupBLL |
| 3 | R-01..R-04 التكرار، تقسيم الأكبر الكلاسات حيث يثبت الحاجة | UI + BLL |
| 4 | قاعدة البيانات والأداء (DATABASE_REFACTORING.md) | DbInitializer + Schema.sql |
| 5 | محرك الحضور E-02 + استيراد Excel (معطَّل افتراضيًا) | AttendanceEngine + وحدة جديدة في BLL |
| 6 | UI/UX موحد | UI |
| 7 | اختبارات (TEST_REPORT.md) — قيد بيئي موثق | مشروع اختبار جديد |
| 8 | PBKDF2 (S-02) + إصدار 2.0 | PasswordHasher/UserBLL |
