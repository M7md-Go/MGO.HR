# تحليل التكرار — Phase 3 (التحليل) — ModernTime 2.0 Enterprise

> تاريخ الإعداد: مرحلة الفحص الشامل (Phase 1) للأمر 4e4c03e8.
> المنهجية: كل بند أدناه مبني على دليل فعلي من الكود (اسم الملف + رقم السطر) عبر بحث
> `grep` على الشجرة الحالية (master 580a80b + إصلاح A-01). لا بند دون دليل.
> القاعدة الملزمة: لا دمج وظيفتين لمجرد تشابه الاسم — لكل بند تقييم للاختلافات
> في قواعد العمل قبل تنفيذ الدمج في مرحلة التنفيذ الفعلية.

---

## 1. ملخص تنفيذي

| المعرّف | نوع التكرار | الملفات | الخطورة | القرار |
|---------|-------------|---------|---------|--------|
| R-01 | تحديد الموظف من نص حر | FrmEmployeeSchedule.cs:200، FrmScheduleChanges.cs:647 مقابل EmployeeResolve.cs (المُوحَّد المستخدم في 6 نوافذ) | P2 (خطر اختيار موظف خاطئ بصمت) | توحيد على `EmployeeResolve.FromText` |
| R-02 | حلقة تلوين العطل الرسمية | FrmMonthlySchedule.cs:453–470، FrmEmployeeSchedule.cs:262–285 (متطابقة حرفيًا تقريبًا) | P3 | مساعد مشترك `PublicHolidayBLL.HolidayDays(first, last)` |
| R-03 | أسماء الأشهر/الأيام العربية | FrmMonthlySchedule.cs:22، FrmEmployeeSchedule.cs:185+193، ReportEngine.cs:1353 | P3 | مساعد واحد (Helpers) |
| R-04 | نافذة «سبب إلزامي» | ReasonDialog (FrmMonthlySchedule.cs:1638، ولها overload معمم :1705) مقابل DecideDialog (FrmScheduleChanges.cs:391) | P3 | إعادة استخدام ReasonDialog المعمّم |
| N-01 | دوال الـ Map في DAL | جميع ملفات DAL | مقبول | **لا يُلمس** (انظر §3) |

لا يوجد تكرار في: اتصالات قاعدة البيانات (نقطة واحدة `Db.`)، تسجيل العمليات
(`AuditBLL.Log` نقطة واحدة)، التحقق من الصلاحيات (`AppContext.RequirePermission`
نقطة واحدة)، محرك الحساب (منتج/مستهلك واحد لكل بنية — تم التحقق أثناء إصلاح A-01).

---

## 2. البنود المؤكدة (بالدليل)

### R-01 — تحديد الموظف من نص حر: ثلاثة سلوكيات مختلفة لنفس المهمة

**الدليل:**

1. النسخة المُوحَّدة الصحيحة `HR.UI/UI/EmployeeResolve.cs` (internal static):
   - مطابقة الكود الرقمي أولاً عبر `EmployeeBLL.GetByCode(candidate, true)`.
   - ثم مطابقة الكود داخل نتائج البحث (OrdinalIgnoreCase).
   - قبول مطابقة الاسم **فقط إذا كانت فريدة** (matches.Count == 1).
   - عند التعدد: رسالة غموض صريحة تحدد عدد الموظفين المطابقين وتطلب الرقم الكامل.
   - مستخدمة فعليًا في 6 نوافذ: FrmLongLeaveEdit.cs:280، FrmLeaveEdit.cs:245،
     FrmAdjustmentEdit.cs:260، FrmPenaltyEdit.cs:228، FrmMissionEdit.cs:212،
     FrmEmployeeHistory.cs:102.

2. النسخة المنحرفة رقم 1 — `FrmEmployeeSchedule.cs:200-224`:
   - `EmployeeSearchFilter { Text, IncludeArchived = true, PageSize = 1 }` ثم
     **أخذ أول نتيجة دون أي فحص للتعدد** (`matches[0]` مباشرة).
   - السيناريو الخطر: موظفان باسم متشابه (شائع مع 1000+ موظف) → يفتح جدول
     الموظف الخطأ **بصمت** ويُحفظ جدول شهر لشخص آخر. هذا خرق مباشر لقاعدة
     «أول مطابقة تفوز» التي أصلحناها سابقًا في النوافذ الست أعلاه.

3. النسخة المنحرفة رقم 2 — `FrmScheduleChanges.cs:647-680`:
   - نفس نمط PageSize = 1 مع `IncludeArchived = false` (اختلاف قواعد عمل —
     عمدًا؟ غير موثق) وأيضًا أول نتيجة دون فحص التعدد.

**القرار (للتنفيذ في Phase 3):** استبدال كلا النسختين بـ
`EmployeeResolve.FromText(text, out error)` — سلوك الكود-أولاً + الغموض الصريح.
بخصوص `IncludeArchived=false` في FrmScheduleChanges: الموظف المؤرشف لا يُعدل
جدوله عمليًا، و`FromText` يمرر `IncludeArchived = true` — سيتم قصر النتيجة على
غير المؤرشفين بفحص `Status/IsDeleted` بعد الحل (سطر واحد) مع تعليق يوثق السبب،
حتى لا تُغيَّر دلالات `EmployeeResolve` المشتركة.

### R-02 — حلقة «أيام العطل الرسمية لهذا الشهر» مكررة حرفيًا

**الدليل:** الكود التالي موجود مرتين بنفس البنية (بناء `HashSet<int>` من
`PublicHolidayBLL.List(first, last)` مع فرعين: تاريخ صريح داخل الشهر، أو
`IsRecurring == true && Month مطابق && Day <= DaysInMonth`):

- `HR.UI/UI/FrmMonthlySchedule.cs:453-470` (تعبئة `_holidayDays`).
- `HR.UI/UI/FrmEmployeeSchedule.cs:262-285` (تعبئة `holidayDays` محليًا).

أي تعديل لقاعدة العطل المتكررة (مثلًا: تعطيل العطل المتكررة لسنة محددة) يجب
أن يُطبق في مكانين — وهذا خطأ كلاسيكي مؤجل.

**القرار:** إضافة توقيع واحد في طبقة الأعمال (المنطق ليس عرضًا):
`PublicHolidayBLL.HolidayDays(DateTime first, DateTime last)` تُعيد
`HashSet<int>` لأرقام الأيام (day-of-month) بنفس دلالات الفرعين أعلاه،
وكلتا النافذتين تستدعيانها. `List()` تبقى كما هي لغير هذا الاستخدام.
ملاحظة: نُفِّذ هذا المنطق مرتين حديثًا في v1.5.0 — الدليل على أن غياب
المساعد المشترك يولّد تكرارًا فورًا.

### R-03 — أسماء الأشهر والأيام العربية في ثلاثة مواضع

**الدليل:**

- `HR.UI/UI/FrmMonthlySchedule.cs:22` — مصفوفة `MonthNames` (12 اسمًا).
- `HR.UI/UI/FrmEmployeeSchedule.cs:185` — `ArabicMonth(int)` + `:193`
  `DayNameAr(DayOfWeek)`.
- `HR.Services/ReportEngine.cs:1353` — `DayNameAr(DateTime)` ثالثة.

**القرار:** مساعد واحد (مقترح: `HR.Helpers/ArabicDates.cs` أو ضمّه لمساعد
التواريخ القائم إن وُجد) بدالتين: `MonthName(int)` و`DayName(DayOfWeek)`؛
الاستدعاءات الثلاثة تُوجَّه إليه. لا تغيير في النصوص نفسها (نفس الترتيب
والإملاء — يجب مقارنة المصفوفات حرفيًا قبل الدمج والتأكد من عدم اختلاف
إملائي بين المواضع الثلاثة).

### R-04 — نافذتا «سبب إلزامي» متشابهتان

**الدليل:**

- `FrmMonthlySchedule.cs:1638` `private class ReasonDialog : Form` — وقد
  عمّمتها v1.5.0 أصلاً: `Show(owner, title, labelText, okText)` في السطر :1705.
- `FrmScheduleChanges.cs:391` `private class DecideDialog : Form` — نفس
  المسؤولية (إدخال سبب إلزامي مع منع الفراغ) بتنفيذ مستقل.

**القرار:** رفع `ReasonDialog` من نافذة FrmMonthlySchedule إلى مكوّن مشترك
(ملف مستقل في HR.UI/UI، internal) بحيث تستدعيه النافذتان — مع الاحتفاظ بسلوك
«السبب إلزامي» في كليهما (لا تغيير قواعد عمل). إذا ثبت أن DecideDialog تضيف
سلوكًا خاصًا (عرض مبلغ/أرصدة مثلًا) يبقى الفارق فيها ويُوثق هنا.

### N-01 — دوال الـ Map في DAL (قرار عدم الدمج — مقصود)

**الدليل:** كل جدول له دالة `MapXxx(IDataRecord)` خاصة (نمط `Db.ReadList(sql,
map, params...)`). عدّها عشرات الدوال المتشابهة شكليًا.

**القرار: عدم الدمج.** هذا هو النمط المعتمد في المشروع كله (ADO.NET صريح،
بدون ORM وبدون Reflection mapper). استبداله بمُعمِّم عام (Reflection أو
expression trees) يعني: انعكاس أداء عند 1000+ موظف، خسارة التحقق الصريح
لترتيب الأعمدة، وخرق قيد «لا Abstractions غير ضرورية» (قاعدة 18 من أمر
التطوير، وقاعدة «لا God Classes»). يُوثق هنا كقرار نهائي حتى لا يُعاد طرحه.

### N-02 — سلاسل overloads في ScheduleBLL (قرار عدم الدمج)

`SaveMonth` و`ValidateCells` فيهما سلاسل overloads تُفوِّض لبعضها مع قيمة
`allowContinuousOverrun`. هذا تكوين (composition) مقصود وليس تكرارًا —
كل مستوى يضيف سلوكًا واحدًا (الأمان الافتراضي false، والتحكم الصريح true).

---

## 3. ما لم يُعثر عليه (نتائج سلبية موثقة)

| الفحص | النتيجة | الدليل |
|-------|---------|--------|
| SQL/OleDb داخل HR.UI | صفر | grep على HR.UI كاملًا — لا `OleDb` ولا SQL نصي |
| اتصال قاعدة بيانات خارج `Db.` | صفر | ماعدا `DbInitializer` (مشروع ومبرر — إنشاء/ترقية المخطط) |
| تسجيل تدقيق خارج `AuditBLL` | صفر | نقطة واحدة |
| تكرار منتج/مستهلك لـ ScheduleCells | منتج واحد (AttendanceBLL) ومستهلك واحد (AttendanceEngine) | تم التحقق أثناء إصلاح A-01 |
| كلاسات ميتة مرشحة بالاسم (BarChartPanel، FrmPreviewHost، QueryMonitor، DbSchema، ZkGateway) | كلها مستخدمة | grep على كل الاسم في الشجرة — لا حذف في هذه المرحلة |

---

## 4. خطة التنفيذ (تُنفذ في Phase 3 الفعلية — لا في هذه المرحلة)

1. R-01: تعديل `FrmEmployeeSchedule.ResolveEmployee` و
   `FrmScheduleChanges.CreateDialog.ResolveEmployee` للتفويض إلى
   `EmployeeResolve.FromText` — مع اختبار يدوي (جهاز فعلي) لحالة الاسم
   المتعدد في كل نافذة.
2. R-02: `PublicHolidayBLL.HolidayDays` + استبدال الحلقتين.
3. R-03: `ArabicDates` (Helpers) + توجيه الاستدعاءات الثلاثة.
4. R-04: رفع `ReasonDialog` لمكوّن مشترك واستخدامه في FrmScheduleChanges.
5. كل بند يُثبت بإلغاء الكود المكرر فعليًا (ليس بمجرد إضافة المساعد)، ويُقاس
   انخفاض الأسطر، ويُوثق في CHANGELOG.
