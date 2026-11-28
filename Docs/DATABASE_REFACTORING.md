# إعادة هندسة قاعدة البيانات — ModernTime 2.0 Enterprise

> تاريخ الإعداد: مرحلة الفحص الشامل (Phase 1) للأمر 4e4c03e8.
> المصدر المرجعي: `Database/ModernTime_Schema.sql` (أحدث حالة على master 580a80b)
> و`HR.DAL/DbInitializer.cs` (آلية الترقية التلقائية).
> القاعدة الملزمة: لا حذف لأي جدول أو حقل قبل إثبات عدم استخدامه، ولا تغيير
> مخطط بدون Migration Script + شرط نسخة احتياطية + تحقق قبل/بعد + خطة رجوع.

---

## 1. جرد المخطط الحالي (مُقاس، ليس تقديريًا)

- **42 جدولًا** (CREATE TABLE في المخطط المرجعي).
- **20 فهرسًا** (CREATE INDEX/CREATE UNIQUE INDEX) + **10 قيود UNIQUE** داخل
  تعريفات الجداول + قيود FK (~40+) في نهاية الملف.
- قيود UNIQUE الموجودة (دليل: أسطر المخطط):
  - `UQ_Sched` MonthlySchedules (YearVal, MonthVal) — سطر 135.
  - `UQ_SchedDet` ScheduleDetails (ScheduleId, EmployeeId, DayNumber) — سطر 144.
  - `UQ_Log` FingerprintLogs (EmployeeId, LogTime, InOutFlag) — سطر 169.
  - `UQ_Att` Attendance (EmployeeId, AttDate) — سطر 188.
  - `UQ_OvT` Overtime (EmployeeId, OtDate) — سطر 235.
  - `UQ_LevBal` LeaveBalances (EmployeeId, YearVal, LeaveTypeId) — سطر 270.
  - `UQ_Lock` MonthlyShiftLocks (YearVal, MonthVal) — سطر 319.
  - `UQ_RolePerm` / `UQ_UserPerm` — سطرا 338/362.
  - `IX_Emp_Enroll` UNIQUE على Employees(DeviceEnrollNumber) — سطر 494.

### الفهارس الساخنة (مسارات القراءة الأكثر تكرارًا عند 1000+ موظف)

| الفهرس | الجدول | الاستخدام |
|--------|--------|-----------|
| `IX_LogEmpDate` (EmployeeId, LogTime) | FingerprintLogs | استعلام بصمات موظف/فترة — الأثقل حجمًا |
| `IX_LogTime` (LogTime) | FingerprintLogs | التحميل الدوري من الأجهزة |
| `IX_Att_Date` (AttDate) | Attendance | إعادة الاحتساب والتقارير بالفترة |
| `IX_Exc_EmpDate` (EmployeeId, AttDate) | AttendanceExceptions | محرك الاستثناءات |
| `IX_PenaltyEmp` / `IX_OT_Date` | Penalties / Overtime | حسابات الجزاءات والإضافي |
| `IX_Audit_Time` / `IX_Audit_Table` / `IX_Audit_User` | AuditLogs | شاشة التدقيق |
| `IX_Login_User` | LoginHistory | أمن الدخول |
| `IX_SchedAppr_Sched` / `IX_SchedChg_Status` / `IX_SchedChgItem_Req` | جداول v1.5.0 | سلسلة الاعتماد وطلبات التعديل |

### العلاقات (FK)

موجودة وبشكل كامل للمسارات الحرجة: Employees→كل المراجع، ScheduleDetails→
(المخطط/الموظف/الوردية)، FingerprintLogs→(الموظف/الجهاز)، Attendance→الموظف،
الاستثناءات/التعديلات/الإضافي/الإجازات/الأرصدة→الموظف (+ النوع للإجازات)،
وجداول v1.5.0: `FK_SAppr_Sched`، `FK_SChgIt_Req`، `FK_SChgIt_Emp`،
`FK_SChgIt_NewSh` (أسطر 783–786).

---

## 2. النتائج المؤكدة (بالدليل)

### DB-01 (P2، أُصلح مع هذا التقرير) — المخطط المرجعي ينقصه فهارس جداول v1.5.0

**الدليل:** `DbInitializer.cs:247/259/269` ينشئ `IX_SchedAppr_Sched` و
`IX_SchedChg_Status` و`IX_SchedChgItem_Req` عند الترقية، لكن
`Database/ModernTime_Schema.sql` (قائمة CREATE INDEX أعلاه، أسطر 490–772)
لا يحتويها — أي أن قاعدة **جديدة** منشأة من المخطط المرجعي تُنشأ بدونها،
بينما قاعدة **مُرقَّاة** تحصل عليها. انفصام بين مسارَي الإنشاء.

**الإصلاح (نُفِّذ مع هذا التقرير):** إضافة أوامر الفهارس الثلاثة للمخطط
المرجعي بعد جداولها. التغيير إضافي بحت (إنشاء فهرس لا يعدل بيانات)،
ويُنفَّذ فقط على القواعد الجديدة من المخطط — وللموجودة يغطيها DbInitializer
بالفعل، فلا حاجة لسكربت ترحيل. خطة الرجوع: `DROP INDEX IX_xxx ON Table`.

### DB-02 (P2) — تمثيل مزدوج للتاريخ: تاريخ محض مقابل طابع زمني

**الدليل:** `Attendance.AttDate DATETIME` يُخزَّن تاريخًا محضًا (منطق المحرك
يقارن بـ `.Date`) بينما `Attendance.StartTime/EndTime` طوابع زمنية كاملة؛
ونفس الثنائية في Overtime (OtDate مقابل ساعات محسوبة). هذا قرار تصميمي
موجود منذ البداية وليس خطأ — لكنه يجب أن يُوثّق كقاعدة صريحة: **أي حقل باسم
`*Date` = تاريخ محض عند منتصف الليل، وأي حقل `*Time` = طابع كامل**، وكل
كود جديد يلتزم به. لا ترحيل بيانات مطلوب؛ الخطر فقط في كود جديد يخلط بينهما
(مثل خطأ A-01 الذي كان يفتح أول مفتاح يوم من الشهر الأخير — أُصلح بالمفاتيح
الكاملة Ticks).

### DB-03 (P3، أُصلح في Phase 4) — فهارس الجداول القديمة على قواعد مُرقَّاة

**الدليل الجزئي:** أصلاً `DbInitializer` ينشئ الفهارس فقط للجداول المضافة لاحقًا
(EmployeeFiles :209، LongLeaves :228-232، جداول v1.5.0) ولا يعيد إنشاء
فهارس الجداول الأساسية (IX_Att_Date وغيرها) لأن قاعدة 1.0 الأصلية صُنعت
من مخطط يحويها. أي قاعدة عتيقة جدًا (قبل إضافة الفهارس) قد تنقصها فهارس.

**الإصلاح (نُفِّذ في Phase 4) :** ترقية تلقائية عند بدء التشغيل — ليست أداة يدوية في شاشة الأدمن كما اقترح التقرير الأصلي، لأن التلقائية أمثل (لا تتطلب تدخلًا من مسؤول قاعدة البيانات). التفاصيل في Docs/PERFORMANCE_REPORT.md (P-01):
- `DbInitializer.EnsureIndex` يتحقق عبر `GetOleDbSchemaTable(OleDbSchemaGuid.Indexes)` من وجود كل فهرس
  غير فريد من المخطط (22 فهرسًا) على القواعد المُرقَّاة وينشئ الناقص. Idempotent
  + إضافي بحت + الرجوع = `DROP INDEX`. الفهارس الفريدة مستثناة عمدًا
  (IX_Emp_Enroll قد يفشل إنشاؤها على بيانات قديمة فيها تكرار — يبقى ضمن قالب الترحيل الإلزامي).
- **الحالة:** مجمَّع فقط (CI = دليل تجميع) — يحتاج تحققًا على نسخة قاعدة حقيقية مُرقَّاة على جهاز فعلي.

### DB-04 (ملاحظة تصميمية) — Integrity عند الحذف

Jet FK في هذا المشروع بلا CASCADE: محاولة حذف موظف له سجلات حضور تُفشل —
وهذا مقصود (حماية البيانات التاريخية، قاعدة 6 من أمر المرحلة الخامسة)؛
النظام يستخدم أرشفة منطقية (`IsDeleted`) بدل الحذف. يُوثق هنا كقرار.

---

## 3. قائمة تحقق المرحلة الخامسة (حالة كل بند)

| البند المطلوب في الأمر | الحالة |
|------------------------|--------|
| Primary Keys | سليمة — AUTOINCREMENT على كل جدول (جرى التحقق من العينة أعلاه) |
| Foreign Keys | مكتملة للمسارات الحرجة + v1.5.0؛ بقية الجداول المرجعية (AppSettings/BackupLogs) بلا FK بحكم طبيعتها |
| Unique Constraints | 10 قيود + فهرس فريد للبصمة؛ الأهم UQ_Att وUQ_SchedDet موجودة (يرجى الانتباه: فرضية «غياب UNIQUE على Attendance» التي وردت في فحص مبكر **سقطت** عند التحقق — القيد موجود سطر 188) |
| Indexes | 20 + ثغرة DB-01 (أُصلحت) + DB-03 أُصلح في Phase 4 (EnsureIndex لـ 22 فهرسًا على القواعد المُرقَّاة) |
| Data Types | تحويل CURRENCY للإجازات العشرية تم في v1.4.0 (أسطر 253/267-269) |
| Nullable Fields | منطقية (SubmittedBy/ApprovedBy nullable طبيعي) |
| بيانات مكررة | أداة الكشف (UQ_Att/UQ_Log) لم تُنفَّذ في Phase 4 — مؤجَّلة: تتطلب قاعدة بيانات حقيقية للفحص ونطاقها موثَّق في Docs/PERFORMANCE_REPORT.md (القسم 5) |
| علاقات ناقصة | لم يُعثر على ناقصة حرجة |
| حقول غير مستخدمة | لا حذف قبل إثبات — يُغطى في Phase 13 بأداة بحث استخدام لكل عمود |
| جداول متشابهة | لا يوجد زوج متشابه بدرجة تبرر الدمج (MonthlySchedules/MonthlyShiftLocks مختلفا الغرض) |

---

## 4. قالب الترحيل الإلزامي (يُستخدم لأي تغيير مخطط لاحق)

كل تغيير مخطط قادم (مثل فهرس UNIQUE جديد أو عمود MustChangePassword)
يُوثق بهذا القالب في ملف ترحيل تحت `Database/Migrations/`:

1. **Migration Script** — أوامر SQL/Jet الدقيقة كما ينفذها DbInitializer.
2. **Backup Requirement** — نسخة احتياطية متحقق منها قبل التشغيل (ارتباط
   بإصلاح B-01: لا ترحيل قبل نسخة Verified).
3. **Pre-Migration Validation** — عدّ السجلات المستهدفة/الكشف عن تعارض
   (مثلًا: مكررات قبل فهرس UNIQUE).
4. **Post-Migration Validation** — إعادة العدّ والتأكد من القيد/العمود.
5. **Rollback Strategy** — أمر الرجوع الدقيق أو الاستعادة من النسخة.

لأن DbInitializer ينفذ الترقية عند بدء التشغيل تلقائيًا، أي سكربت ترحيل
يُدرج في DbInitializer بنفس النمط المُجرَّب (EnsureColumn/ExecuteUpgrade
مع فحص وجود مسبق) — وحدات idempotent بطبيعتها.
