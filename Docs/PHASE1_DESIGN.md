# Modern Time – HR & Attendance Management System

نظام إدارة الموظفين والحضور والانصراف والورديات — تصميم معتمد ومخرجاته التنفيذية.

## البيئة الإلزامية
- C# 5.0 / .NET Framework 4.0 (Client Profile) / Visual Studio 2012 / x86.
- قاعدة بيانات: Microsoft Access MDB (Jet 4.0)، بدون كلمة مرور، UTF-8.
- لا EF ولا ORM؛ Access فقط عبر `System.Data.OleDb` مع SQL معامل إلزامي.
- أجهزة البصمة: ZKTeco `zkemkeeper.dll`.
- Excel عبر SpreadsheetML 2003 (بدون مكتبات خارجية).

## المعمارية (اتجاه واحد صارم)
`HR.UI → HR.BLL → HR.DAL → HR.Entities` مع `HR.Services` و`HR.Helpers` و`HR.Reports`.
كل SQL في `HR.DAL` حصراً. كل قواعد العمل في `HR.BLL/HR.Services` حصراً.

## قرارات التصميم المعتمدة (D1–D7)
- **D1** الحدود الأسبوعية (36/42) تُفحص على الساعات الطبيعية المتعاقد عليها (`Shifts.NormalHours`) وليس مدة الوردية الكلية؛ ما زاد = Overtime.
- **D2** فتح التأخير 30 دقيقة والانصراف المبكر 15 دقيقة من `AppSettings`؛ وضعية احتساب التأخير `Att.LateCountMode`.
- **D3** منع خلط أصناف الورديات عبر `Shifts.CategoryCode` + إعداد `Sch.DisableMixingCategories`.
- **D4** ADO.NET OleDb + ADOX (لإنشاء ملف MDB) + JRO (لـ Compact).
- **D5** Excel = SpreadsheetML 2003 UTF-8.
- **D6** كلمات المرور SHA-256 + Salt لكل مستخدم + قفل الحساب بعد `Sec.MaxFailedLogins` + `LoginHistory`.
- **D7** لا حقول صور ثنائية داخل MDB — مسارات ملفات فقط.

## قاعدة البيانات
36 جدولاً — المصدر الرسمي: `Database/ModernTime_Schema.sql`
(DDL + علاقات + فهارس + بذور بيانات: الورديات، أنواع الإجازات، الأدوار، الصلاحيات، الإعدادات).

أبرز القيود: `UQ_Att(EmployeeId, AttDate)`، `UQ_Log(EmployeeId, LogTime, InOutFlag)`، `UQ_SchedDet`،
`UQ_LevBal`، `UQ_OvT`، قفل الشهر `MonthlyShiftLocks` فريد لكل (سنة، شهر).

## قواعد العمل (BR)
| # | القاعدة | الإعداد |
|---|---------|---------|
| BR-01 | كود الموظف فريد وغير قابل للتعديل | DB |
| BR-02 | Soft Delete + أرشفة؛ الحذف الفعلي فقط لمن بلا أي ارتباط | AppSettings |
| BR-03 | تأخير: Grace 30 | `Att.GraceMinutesLate` |
| BR-04 | انصراف مبكر: Grace 15 | `Att.GraceMinutesEarlyLeave` |
| BR-05 | Cross-Day Shift: نافذة تجميع ±4 ساعات حول الوردية | كود |
| BR-06 | أقصى 24 ساعة متواصلة → خطأ يمنع الحفظ | `Sch.MaxContinuousHours` |
| BR-07 | أسبوع السبت→الجمعة، الجمعة راحة افتراضية | قوالب Shifts |
| BR-08 | أسبوعي < 36 تحذير، > 42 خطأ (على الساعات الطبيعية) | `Sch.Min/MaxWeeklyHours` |
| BR-09 | منع خلط أصناف الورديات | `Sch.DisableMixingCategories` |
| BR-10 | OT يتطلب اعتماداً حين `Sch.OTRequiresApprove=YES` | `Sch.OTMinThresholdMinutes` |
| BR-11 | إجازة بلا رصيد تُرفض؛ الخصم عند الاعتماد فقط | كود |
| BR-12 | حج مرة واحدة فقط | كود |
| BR-13 | المأمورية المعتمدة = حضور كامل | كود |
| BR-14 | عطلة رسمية بلا عمل = Holiday؛ عملها = OT بمضاعف | `Att.HolidayOTRate` |
| BR-15 | بصمة مكررة خلال نافذة تُهمل | `Att.DuplicatePunchWindowMin` |
| BR-16 | Missing Punch يفتح Exception ولا يُغلق تلقائياً | كود |
| BR-17 | قفل الشهر يمنع التعديل؛ فتحه للـ Admin + Audit | `MonthlyShiftLocks` |
| BR-18 | أي تعديل حضور يدوي عبر AttendanceAdjustments + Audit | كود |
| BR-19 | الصلاحيات Default-Deny | Roles/Permissions |
| BR-20 | العمليات المجمعة داخل Transactions | DAL |
| BR-21 | نسخة قبل Restore/أرشفة/أي عملية حذف حساسة | BackupService |
| BR-22 | أرشفة أقدم من 3 سنوات إلى ملف الأرشيف | `Arch.ActivePeriodDays` |
| BR-23 | رسائل عربية للمستخدم؛ التفاصيل للمطور في Logs | Logger |

## Workflows
- **W1 الجدول الشهري**: Draft → ملء/نسخ → فحص BR-06/08/09 → اعتماد → قفل.
- **W2 اليومية**: تنزيل بصمة تدريجي (لا فقد) → `AttendanceEngine.Recalculate` → Exceptions → تصحيحات معتمدة → إعادة احتساب اليوم فقط.
- **W3 الإجازات**: طلب → اعتماد → خصم الرصيد → تغذية المحرك.
- **W4 الدخول**: SHA-256+Salt، قفل بعد محاولات، `AppContext.SetSession`.

## محرك الحضور (HR.Services/AttendanceEngine)
يستقبل (الموظف، التاريخ، الوردية، البصمات، الإجازات، المأموريات، العطلات) ويحدد:
Present/Absent/Late/EarlyLeave/MissingPunch/Mission/Leave/Holiday/WeeklyRest/Overtime وفق BR-03…BR-16،
ويكتب النتائج في Transactions على `Attendance` + `AttendanceExceptions` + `Overtime(Pending)`.

## الشاشات (33) / الخدمات (13) / التقارير (12)
القوائم الكاملة في وثيقة الاعتماد؛ مصدرها `HR.Services/ReportEngine` ويُصدَّر SpreadsheetML مع RTL وUTF-8.

## سيناريوهات الاختبار (15)
حالة 1..15 (حضر في الموعد…تجاوز 24 ساعة متواصلة) — تتحقق منها المرحلة 13 على المحرك مباشرة.

## حدود ومخاطر مُوثّقة
Access 2GB → تقسيم ملفات _att/_arch من اليوم الأول؛
30 مستخدماً → Record-Level Locking + `Jet OLEDB:Database Locking Mode=1` + Transactions قصيرة
+ إعادة محاولة عند انشغال الملف. CHECK constraints قصيرة في Jet — لذا BR-06/08/09 في ScheduleValidator.
