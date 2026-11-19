# Phase 5 Status — Monthly Schedule (الجدول الشهري)

## Delivered

| Layer | File | Content |
|---|---|---|
| DAL | `HR.DAL/ScheduleDAL.cs` | `GetSchedule`, `InsertSchedule` (Status Draft), `GetDetails`, `ReplaceEmployeeDetails` (delete+insert per employee inside one transaction, parameter reuse), `GetLock`, `SetLock` (insert-on-lock / update with unlock audit fields; refuses to unlock a nonexistent lock row). Fully parameterized. |
| BLL | `HR.BLL/ScheduleBLL.cs` | `MonthData` snapshot, `LoadMonth`, `IsLocked`, `SaveMonth`, `LockMonth`, `UnlockMonth`, `ValidateCells` (all rule-8 validations), summary audit entries. |
| UI | `HR.UI/UI/FrmMonthlySchedule.cs` | Grid: employees in rows, days in columns (day number + Arabic weekday header, Friday highlighted), one shift combo per day bound to `(ShiftId, ShiftCode)` — real entity keys, no index math, no reflection, no fake IDs. Toolbar: year/month/department/section filters, load, save, autofill (with default shift combo), copy/paste row, copy/paste day, copy/paste week, copy previous month, undo, lock month, unlock month (mandatory reason dialog), live lock-state label. |
| UI | `HR.UI/UI/FrmMain.cs` | `miMonthly` rewired from `ShowPhaseNotice(5)` to `ShowForm(new FrmMonthlySchedule())`. |

The `HR.UI.csproj` glob (`**\*.cs`) picks all new files up automatically.

## Permission model (rule 21 / rule 7)

- `Schedule.Edit` — save, autofill, copy/paste/undo (cell editing is read-only otherwise).
- `Schedule.Approve` — lock month.
- `Schedule.Unlock` — unlock month; reason is mandatory and stored with unlocker id/timestamp.
- Locked months: edits rejected for everyone except Admin, and even Admin saves are audit-flagged as `Schedule.SaveLocked` (rule 7). `FrmMain` itself remains the only gate for menu visibility per earlier phases.

## Rule-36 design decisions

1. **No `ShiftTemplates` table exists in the approved 36-table schema.** As decided in Phase 4, the `Shifts` table itself is the template store (every row carries Code/times/Duration/CrossDay/Category/Rules). `MonthlyScheduleDetails.ShiftId` references it directly. A separate template table would orphan schedule details and duplicate the same data.
2. **Weekly bounds (36–42 h) validated only on complete weeks.** A week (Saturday → Friday, offset = `((int)DayOfWeek + 1) % 7`) contributes a validation error only when all 7 days carry shifts and at least one is non-rest — otherwise the grid is still being drafted and any "warning" would be noise. Bounds come from `Sch.MinWeeklyHours` / `Sch.MaxWeeklyHours` settings, never hardcoded (rule 11).
3. **Continuous-work rule (≤ 24 h, rule 8-2).** For consecutive days both scheduled: end-of-shift-D absolute time (crossing midnight when start + duration > 1440 min) is compared with the start of shift D+1. Overlap → reject; exact contiguity allowed only while the sum ≤ `Sch.MaxContinuousHours`.
4. **Category anti-mixing (BR-09 / rule 8-1)** enforced across the whole employee month when `Sch.DisableMixingCategories` is YES; rest shifts (`Rest` category / code `FR`) are exempt, otherwise the Friday rest day would make every schedule "mixed".
5. **Copy previous month maps by day number, not weekday.** Month lengths differ and weekdays shift; mapping day 1 → day 1 … up to the shorter month's length is the unambiguous, auditable choice. The confirmation message states this explicitly ("بنفس أرقام الأيام").
6. **Bulk-save audit** is one summary row per save (`Schedule.Save` / `Schedule.SaveLocked`) instead of one row per employee cell — a 1000-employee month would otherwise flood `AuditLogs`.
7. **Deviation from the prescribed `SelectedId(displayName)` matcher (carried over from Phase 3):** matching by display name silently picks the wrong entity when two shifts/employees share a name. This form binds real keys (`ValueMember = "ShiftId"` for shifts; `(Key,Value)` dictionaries + `SelectedValue` for filters), which gives the same guarantee — real entity IDs, no reflection, no index math — with strictly fewer failure modes. No string→id matching exists anywhere in the form.

## Structural checks (sandbox has no C# compiler)

`79 .cs` files (UI/DAL/BLL/Entities/Helpers) checked with the Python structural linter:

- brace/paren/bracket balance after stripping comments and string/char literals — PASS
- C# 6+ bans (`?.`, `$"`, `nameof`, `out var`, expression-bodied `=>`, pattern matching) — PASS
- TODO / NotImplementedException / placeholder markers — none

Cross-checks: `MonthData` field names (`Employees`, `Shifts`, `Cells`, `IsLocked`, `Year`, `Month`), `ScheduleBLL` signatures (`LoadMonth`, `SaveMonth(year, month, List<Employee>, cells, shifts)`, `LockMonth`, `UnlockMonth(year, month, reason)`), `OrganizationBLL.Departments/Sections`, `Theme.*`, `AppContext.HasPermission/IsCurrentUserAdmin`, `Logger.LogError` — verified against the actual declarations.

## Test steps (Windows / VS2012)

1. Open the solution in VS2012 (x86, .NET 4.0 Client Profile), build, run, log in as Admin.
2. Menu الوحدات ← **الورديات**: ensure M/M1/M2/X/L and one rest shift (`FR`, category راحة) exist.
3. **الجدول الشهري**: pick current year/month → تحميل. Grid shows employees × days, Friday column headers highlighted.
4. Select an employee row cell, choose a shift, pick **وردية الملء**, press **ملء تلقائي** → whole row filled, Friday = FR.
5. **Save** → success message; close the form, reopen, reload → values persisted.
6. Try a 6h + 7h mix on one employee → save must fail with the clear BR-09 Arabic message (mixing enabled by default).
7. Fill a full week with an X (9h) shift ×7 days = 63h → save fails with the 36–42 bound message; a 6h × 6-day week (36h) passes.
8. **نسخ صف / لصق صف، نسخ يوم / لصق يوم، نسخ أسبوع / لصق أسبوع، نسخ الشهر السابق** → cells change immediately; **تراجع** restores the last saved snapshot; reloading without saving discards unsaved edits.
9. **قفل الشهر** (needs `Schedule.Approve`) → reload shows the red "مقفل" state; a non-Admin with `Schedule.Edit` gets the locked-month rejection on save; Admin can still save and the audit log records `Schedule.SaveLocked`.
10. **فتح الشهر** (needs `Schedule.Unlock`): cancelling or leaving the reason empty blocks the operation (`سبب الفتح إلزامي.`); a valid reason unlocks, reload shows the green state, and the audit log contains `Schedule.Unlock` with the reason.
11. Log in as a user without `Schedule.Edit` → save/fill/copy/paste/undo disabled; without `Schedule.Approve`/`Schedule.Unlock` → lock/unlock disabled.

## Known deferrals (by plan)

- Holidays / official-holiday columns on the grid: `Holidays` table exists; visual holiday markers land with Phase 7 (Attendance Engine consumes them) to avoid a read-only column now and a rewrite later.
- Settings screen (`miSettings`), fingerprint (`miFingerprint`), reports, users, dashboard, backup menus still show phase notices — later phases (2-UI/6/9/10/11/12).
