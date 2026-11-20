# Phase 9 — Reports + Excel (Status)

## Files

| File | Lines | Purpose |
|---|---|---|
| `Database/ModernTime_Schema.sql` | mod | `Attendance` gains `LateMinutes LONG`, `EarlyLeaveMinutes LONG` |
| `HR.Entities/Attendance.cs` | mod | `LateMinutes`, `EarlyLeaveMinutes` |
| `HR.Entities/AttendanceView.cs` | mod | + LateMinutes/EarlyLeaveMinutes/DepartmentId/DepartmentName |
| `HR.Entities/ReportModels.cs` | 64 | `ReportColumn`, `ReportData` (chainable) |
| `HR.Entities/OvertimeView.cs` | 31 | Overtime join row |
| `HR.DAL/AttendanceDAL.cs` | mod | Browse joins Departments; INSERT/UPDATE (13 cols) |
| `HR.DAL/OvertimeDAL.cs` | 70 | Search / SetStatus / MapView |
| `HR.Services/AttendanceEngine.cs` | mod | `AddAttendance(..., lateMinutes, earlyMinutes)`; 11 call sites |
| `HR.Services/ReportEngine.cs` | 616 | 9 report builders + helpers (MaxRows=100000) |
| `HR.Services/ExcelWriter.cs` | 190 | SpreadsheetML 2003, UTF-8 BOM, RTL, frozen header |
| `HR.BLL/OvertimeBLL.cs` | 78 | Search + Approve/Reject (perm `Attendance.Edit`, audit) |
| `HR.UI/UI/FrmAttendanceCalc.cs` | mod | late/early minutes columns |
| `HR.UI/UI/FrmReports.cs` | 554 | 9 reports browser + shared filters + export |
| `HR.UI/UI/FrmMain.cs` | mod | «التقارير» opens `FrmReports` |
| `HR.Services/HR.Services.csproj` | mod | `<ProjectReference>` to HR.DAL |

## Rule 33 — root-cause fix (missing late/early storage)

- **Problem:** the engine computed `LateMinutes`/`EarlyLeaveMinutes` but the `Attendance` table never stored them; rule 19 reports and rule 18 dashboard require them. They were reconstructible only indirectly via `AttendanceExceptions.ExcValue`.
- **Fix (end-to-end):** schema + entity + engine (`AddAttendance` signature, all 11 call sites) + `AttendanceDAL` Browse/INSERT/UPDATE (parameterized, single transaction preserved) + `FrmAttendanceCalc` display columns.
- **Migration note (existing MDB):** for databases created before this phase, run `ALTER TABLE Attendance ADD COLUMN LateMinutes LONG;` and `ALTER TABLE Attendance ADD COLUMN EarlyLeaveMinutes LONG;` then recalculate the required period from FrmAttendanceCalc to backfill.

## Rule 36 — architectural decision (project cycle)

`HR.BLL` already references `HR.Services` (Phase 6 transcription services). A `ReportEngine` living in `HR.Services` therefore **must not** reference `HR.BLL` — that would be a project cycle disallowed by VS 2012. Resolution: `ReportEngine` composes **read-only DAL queries directly** (`AttendanceDAL`, `LeaveDAL`, `MissionDAL`, `PenaltyDAL`, `OvertimeDAL`, `FingerprintDAL`, `LookupDAL`) and maps statuses/labels itself. `HR.Services.csproj` gained a `ProjectReference` to `HR.DAL` (References: Entities ← DAL ← Services ← BLL ← UI; no cycle). `HR.Reports/` remains empty by design — D5 assigns reports to `HR.Services/ReportEngine`.

## Additional decisions

1. **Excel format:** SpreadsheetML 2003 XML (design doc D5) — no external libraries, opens in Excel 2007, `UTF-8` with BOM, `<DisplayRightToLeft/>`, frozen header pane, styled title/header/totals rows, column widths from `ReportColumn.Width`.
2. **`Overtime` report source:** new `OvertimeDAL.Search` (INNER JOIN Employees, dynamic parameterized filters, ORDER BY date desc); approval/rejection primitives exposed through `OvertimeBLL` gated by `Attendance.Edit` — the report itself is read-only.
3. **Report cap:** `ReportEngine.MaxRows = 100000` protects against unbounded exports (rule 27); Raises no silent truncation messaging because DAL layers already respect `maxRows` — totals state record count.
4. **FrmReports filters:** one dynamic filter combo refills per report (statuses / penalty types / device list with sentinel 0 / archived-scope for the directory); monthly reports take the month from the «من» date; department filter uses `LookupTable.Departments` + sentinel 0.
5. **Permissions:** reports are view-only (no write), so no permission gate on view/export; every sensitive operation inside the report module (overtime approval) stays in `OvertimeBLL` behind `Attendance.Edit`.
6. **Grid totals row** rendered bold; export button stays disabled until the firstSuccessful preview.

## Rule-19 coverage (9 reports)

| Report | Engine builder | Rule 19 item |
|---|---|---|
| الحضور اليومي | `DailyAttendance` | تقرير الحضور اليومي |
| ملخص الحضور الشهري | `MonthlyAttendanceSummary` | تقرير الحضور الشهري |
| الجدول الشهري | `FrmMonthlySchedule` (Phase 5) | تقرير الجدول الشهري |
| دليل الموظفين | `EmployeeDirectory` | تقارير الموظفين |
| الإجازات | `Leaves` | تقارير الإجازات |
| المأموريات | `Missions` | تقارير المأموريات |
| الجزاءات | `Penalties` | تقارير الجزاءات |
| الساعات الإضافية | `Overtime` | تقارير الساعات الإضافية |
| البصمة الخام | `RawFingerprint` | تقرير البصمة الخام |
| ملخص الأقسام | `DepartmentMonthlySummary` | تقرير ملخص القسم |
| المخالفات | `Penalties` (type=Violation) | تقرير المخالفات |

## Verification (sandbox)

- `python3 /tmp/check_structure.py` → `PASS: 115 files checked` (brace balance, C# 5 only, no TODO/NotImplemented).
- Phase-9 symbol cross-check (calls ↔ engine signatures, ReportModels members, OvertimeDAL/BLL surface, csproj HR.DAL reference, entity/DAL new columns) → OK.

## Windows test steps

1. Open `ModernTime.sln` in VS 2012 → build (must compile x86, no errors).
2. Run → login → attendance engine on a month with punches → «التقارير».
3. الحضور اليومي: confirm "دقائق التأخير"/"دقائق الانصراف المبكر" columns show engine values. Test scenario: grace 30 min → arrival 08:25 vs 08:31.
4. ملخص الحضور الشهري + ملخص الأقسام: totals per employee / per department.
5. الإجازات / المأموريات / الجزاءات / الساعات الإضافية: status + type filters, totals row.
6. البصمة الخام: device filter (sentinel 0 = all).
7. دليل الموظفين: النشطون فقط / يشمل المؤرشفين.
8. تصدير إلى Excel for each report; open each `.xml` in Excel 2007: Arabic readable (UTF-8 BOM), sheet RTL, header frozen after row 6, column widths, totals row present.
9. If using a pre-existing MDB, apply the 2 `ALTER TABLE` statements above then recalc.

Next: Phase 10 (Users + Permissions + Audit).
