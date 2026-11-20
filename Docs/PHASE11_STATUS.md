# Phase 11 — Dashboard (Status)

## Files

| File | Lines | Purpose |
|---|---|---|
| `HR.Entities/DashboardModels.cs` | 93 | `DashboardData` (13 KPIs + 4 lists), `DepartmentCount`, `DepartmentStat` |
| `HR.Services/DashboardEngine.cs` | 132 | `Build(from, to, departmentId)` — read-only composition over DAL |
| `HR.DAL/EmployeeDAL.cs` | mod | + `CountByDepartment()` (GROUP BY, active employees) |
| `HR.UI/UI/FrmDashboard.cs` | 435 | KPI cards (10), 3 grids (departments / newest punches / devices), period selector |
| `HR.UI/UI/FrmMain.cs` | mod | «لوحة التحكم» → `FrmDashboard` (نفس النمط: MDI child) |

## Rule 36 — architecture (same as Phase 9)

`HR.BLL` → `HR.Services` reference already exists, so `DashboardEngine` (in HR.Services) reads the required tables via `HR.DAL` directly (AttendanceDAL, EmployeeDAL, LeaveDAL, MissionDAL, OvertimeDAL, FingerprintDAL, DeviceDAL). Verified no project cycle (`HR.Services.csproj` has no HR.BLL reference).

## What the dashboard shows (rule 18)

- **KPI cards (10):** إجمالي الموظفين، أيام الحضور (+ ساعات الحسوبة)، أيام الغياب، متأخرون/دقائق، انصراف مبكر/دقائق، بصمات ناقصة، ساعات إضافية محتسبة H:mm، إجازات معتمدة (طلب/يوم)، مأموريات معتمدة، أيام عمل محسوبة H:mm. The late/early KPIs consume the Phase-9 `LateMinutes`/`EarlyLeaveMinutes` columns (Phase-9 root-cause fix pays off here).
- **Period selector (قابل للتخصيص):** اليوم / هذا الأسبوع (السبت→الجمعة, قاعدة 3) / هذا الشهر / فترة مخصصة من-إلى + فلتر قسم + زر تحديث + تحديث تلقائي كل دقيقتين (Timer).
- **إحصائيات الأقسام:** عدد الموظفين لكل قسم (شامل الأقسام بلا حضور) + أيام حضور/غياب من السجلات.
- **آخر عمليات البصمة:** آخر 12 بصمة بترتيب تنازلي (LogTime DESC في DAL).
- **حالة أجهزة البصمة:** الجهاز، IP، آخر مزامنة، آخر حالة (Connected/Downloaded = أخضر، Failed ≠ ذلك برتقالي).

## Semantics

- Counts over the period are **man-day counts** (one row per employee-day), not distinct employees — the period text and grid headers say so.
- Overtime KPI = SUM(EffectiveMinutes) from `Overtime` (the approved/pending effective minutes), distinct from raw minutes beyond shift in `Attendance.OvertimeMin`.
- Guard rails: `MaxAttendanceRows = 200000` per load; leaves/missions capped at 5000 requests; overtime 20000 rows (rule 27).

## Verification (sandbox)

- `python3 /tmp/check_structure.py` → `PASS: 122 files checked` (C# 5 only, braces, no TODO).
- Phase-11 symbol cross-check (models, engine, EmployeeDAL helper, FrmDashboard↔engine, FrmMain wiring, no HR.BLL reference in HR.Services) → OK.

## Windows test steps

1. Build (VS 2012 x86) → run → login admin → وحدات → لوحة التحكم.
2. «اليوم»: punch in/out a test employee through engine, verify KPI numbers (حضور/تأخير بالدقائق).
3. «هذا الأسبوع»: verify span = Saturday→Friday (7 days).
4. «فترة مخصصة»: full month → compare أيام الغياب/الحضور with تقرير ملخص الحضور الشهري from FrmReports (numbers must match — both read AttendanceDAL).
5. فلتر قسم → KPIs change; grid still shows all departments.
6. Latest prints grid shows the newest 12; devices grid shows last sync/status (download once via الفرع البصمة to see Downloaded).

Next: Phase 12 (Backup + Restore), then Phase 13 (Testing + Optimization).
