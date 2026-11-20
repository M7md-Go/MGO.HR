# المرحلة الثامنة — الإجازات + المأموريات + الجزاءات

## الملفات المُسلَّمة

| الطبقة | الملف | الوصف |
|---|---|---|
| HR.Entities | `LeaveRequestView.cs` (35) | صف عرض طلبات الإجازة (Join: كود/اسم الموظف + اسم النوع) |
| HR.Entities | `MissionView.cs` (35) | صف عرض المأموريات (Join: كود/اسم الموظف) |
| HR.Entities | `PenaltyView.cs` (31) | صف عرض الجزاءات (Join: كود/اسم الموظف) |
| HR.DAL | `LeaveDAL.cs` (225) | أنواع، طلبات، تداخل، SUM(مستهلك)، رصيد GetBalance/UpsertBalance، تحديث حصص الأنواع |
| HR.DAL | `MissionDAL.cs` (103) | بحث/إدراج/تحديث/حالة للمأموريات |
| HR.DAL | `PenaltyDAL.cs` (93) | بحث/إدراج/تحديث/حذف للجزاءات |
| HR.BLL | `LeaveBLL.cs` (~300) | دورة حياة الطلب + فحص الرصيد + الحج مرة واحدة + المادية MaterializeBalance |
| HR.BLL | `MissionBLL.cs` (146) | دورة حياة المأمورية + صيغة HH:mm |
| HR.BLL | `PenaltyBLL.cs` (119) | CRUD + الأنواع الخمسة + التسمية العربية |
| HR.UI | `FrmLeaves.cs` (377) | متصفح الإجازات: فلترة (حالة/موظف/فترة) + اعتماد/رفض + أرصدة |
| HR.UI | `FrmLeaveEdit.cs` (284) | حوار طلب إجازة + قراءة رصيد حيّة |
| HR.UI | `FrmLeaveBalances.cs` (273) | أرصدة موظف/سنة لكل الأنواع + تعديل حصة |
| HR.UI | `FrmMissions.cs` (358) | متصفح المأموريات + اعتماد/رفض |
| HR.UI | `FrmMissionEdit.cs` (269) | حوار مأمورية + إعفاء من الحضور |
| HR.UI | `FrmPenalties.cs` (286) | متصفح الجزاءات (CRUD بالصلاحية) |
| HR.UI | `FrmPenaltyEdit.cs` (285) | حوار جزاء (الأنواع الخمسة، قيمة، سبب) |
| HR.UI | `FrmMain.cs` | توصيل miLeaves/miMissions/miPenalties بعد البصمة وقبل التقارير |

## قرارات (قاعدة 36)

1. **إعادة بناء الرصيد دائمًا من SUM(الطلبات المعتمدة)** — `LeaveBLL.MaterializeBalance` يعيد الحساب عند كل قراءة/تغيير؛ لا انحراف (drift) بين `LeaveBalances.UsedDays` و `LeaveRequests`. الحصة (QuotaDays) الواردة في صف الرصيد تُحترم إن وُجد، وإلا تُورَّث من `LeaveTypes.AnnualDays`.
2. **الحج مرة واحدة في العمر** — `LeaveDAL.UsedDaysAllYears` (SUM عبر كل السنوات) > 0 ⇒ رفض أي طلب حج جديد، حتى لو القيمة قابلة للتعديل من الشاشة.
3. **منع تعديل طلب معتمد** — الأمان أولًا: يلغي المسؤول الأعلى المستوى. يعاد فحص الرصيد عند كل اعتماد (ربما استُهلك الرصيد بعد إنشاء الطلب المعلّق).
4. **`LeaveBLL.SaveEmployeeQuota`** — تعديل حصة موظف/سنة (صلاحية `Leaves.Approve`) مع حفظ `UsedDays` محلوب من الاعتماد؛ بينما `SaveTypeQuota` يعدل `LeaveTypes` (إعدادات الأنواع).
5. **حذف الجزاءات حذفًا فعليًا** — جدول `Penalties` مستقل بلا FK أو تبعيات محرك؛ البصمة التاريخية محفوظة في Audit Log (`Penalty.Delete`) بينما الحضور التاريخي لا يُمس.
6. **الصلاحيات**: إنشاء/تعديل الإجازات والمأموريات لأي مستخدم مصادق؛ الاعتماد/الرفض يتطلب `Leaves.Approve` / `Missions.Approve`؛ كل عمليات الجزاء تتطلب `Penalties.Add`. تُفحص في UI (تعطيل الأزرار) **وفي BLL** (`RequirePermission`) — دفاع مزدوج.
7. **التكامل مع محرك الحضور** — المحرك يقرأ تلقائيًا المعتمد فقط عبر `AttendanceDAL.LoadApprovedLeaves/LoadApprovedMissions` (أسبقية اليوم: مأمورية > إجازة > عطلة > راحة > يوم عمل). لا تغيير في المحرك.
8. **الحالات**: Pending → Approved/Rejected فقط (لا رجوع خلفي)؛ تعديل معلّق مسموح، معتمد ممنوع.

## خطوات الاختبار على Windows

1. افتح البرنامج وادخل بحساب Admin.
2. **إنشاء طلب**: القائمة «الإجازات» → «طلب إجازة» → أدخل كود موظف → اختر «سنوية» → حدد فترة 5 أيام → لاحظ سطر الرصيد (21/0/21) → حفظ ⇒ يظهر الطلب «معلّق» (أخضر فاتح/Warn).
3. **الاعتماد**: زر «اعتماد» ⇒ رسالة نجاح ⇒ الحالة «معتمد» (خلفية خضراء) ⇒ إعادة فتح الأرصدة: المستخدم 5 والمتبقي 16.
4. **تجاوز الرصيد**: أنشئ طلبًا آخر 30 يومًا سنوية ⇒ رسالة «الرصيد لا يكفي…».
5. **الحج مرة واحدة**: بعد اعتماد طلب حج، طلب حج ثانٍ (أي سنة) ⇒ رفض برسالة «مرة واحدة في العمر».
6. **التداخل**: طلبان متداخلان لنفس الموظف ⇒ رفض برسالة التداخل.
7. **تعديل معتمد**: محاولة تعديل طلب معتمد ⇒ رسالة منع (UI و BLL).
8. **المأمورية**: أنشئ مأمورية معفية لليوم نفسه، اعتمدها، ثم أعد حساب الحضور من «محرك الحضور» ⇒ يوم الـ مأمورية Status='Mission' بلا تأخير/غياب.
9. **الجزاءات**: مستخدم بلا صلاحية `Penalties.Add` ⇒ الأزرار معطلة؛ Admin يضيف «خصم 250» ⇒ يظهر في الجدول؛ حذفه يعمل مع تسجيل العملية في `AuditLogs` (Action=Penalty.Delete). حملة عمل (AuditLog viewer يأتي في المرحلة 10) أو استعلام مباشر:
   `SELECT TOP 10 ActionName, TableName, NewValue FROM AuditLogs ORDER BY AuditId DESC`
10. **Audit Log المتوقع**: Leave.Create / Leave.Update / Leave.Approve / Leave.Reject / Leave.EmployeeQuota / Leave.TypeUpdate / Mission.Create / Mission.Update / Mission.Approve / Mission.Reject / Penalty.Create / Penalty.Update / Penalty.Delete.
11. **الأداء**: مع 1000 موظف، كل الاستعلامات معاملات (Parameters) ومفهرسة على (EmployeeId, Status, StartDate) حسب مخطط 36 جدولًا.

## الحالة

- الفحص الهيكلي: `PASS: 110 files checked` (توازن أقواس/أدلة، لا TODO، لا NotImplementedException).
- مطابقة الرموز UI→BLL→DAL: `OK` (سكربت `/tmp/symcheck_phase8.py`).
- C# 5 صارم: لا `?.` ولا `$""` ولا `nameof` ولا expression-bodied في الملفات الجديدة.
