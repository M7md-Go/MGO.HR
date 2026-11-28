using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using HR.BLL;
using HR.Entities;
using HR.Helpers;

namespace HR.UI.UI
{
    /// <summary>
    /// Monthly schedule screen (rule 7): employees in rows, days of the month in
    /// columns, each cell a shift-combo. Copy row/day/week + previous month,
    /// automatic fill, undo against an in-memory snapshot, save through
    /// ScheduleBLL (which runs every rule-8 validation and audit), and month
    /// lock/unlock with a mandatory reason. All SQL lives in ScheduleDAL —
    /// nothing here touches the database directly.
    /// </summary>
    public class FrmMonthlySchedule : Form
    {
        private NumericUpDown _numYear;
        private ComboBox _cboMonth;
        private ComboBox _cboDepartment;
        private ComboBox _cboSection;
        private ComboBox _cboDefaultShift;
        private Button _btnLoad;
        private Button _btnSave;
        private Button _btnFill;
        private Button _btnCopyRow;
        private Button _btnPasteRow;
        private Button _btnCopyDay;
        private Button _btnPasteDay;
        private Button _btnCopyWeek;
        private Button _btnPasteWeek;
        private Button _btnCopyPrevMonth;
        private Button _btnUndo;
        private Button _btnLock;
        private Button _btnUnlock;
        private Label _lblState;
        private DataGridView _grid;
        private ComboBox _cboTemplate;
        private Button _btnRotate;
        private Button _btnCoverage;
        private Button _btnCsv;
        private Button _btnSubmit;
        private Button _btnApprove;
        private Button _btnReject;
        private Button _btnPrint;
        private Label _lblApproval;

        private MonthData _data;
        private List<DayOfWeek> _restDays;
        private Dictionary<int, Dictionary<int, int>> _cells;
        private Dictionary<int, Dictionary<int, int>> _snapshot;
        private Dictionary<int, int> _rowClipboard;
        private Dictionary<int, int> _dayClipboard;
        private Dictionary<int, int> _weekClipboard;
        private Dictionary<int, string> _departmentNames;
        private Dictionary<int, string> _sectionNames;
        private bool _loadingGrid;
        private HashSet<int> _holidayDays;

        public FrmMonthlySchedule()
        {
            Theme.Apply(this, false); // MDI child: mirrored layout is not applied — RTL flow comes from the containers
            Text = "الجدول الشهري";
            ClientSize = new Size(1240, 660);
            StartPosition = FormStartPosition.CenterParent;

            Panel header = new Panel();
            Label lblTitle = new Label();
            lblTitle.Text = "الجدول الشهري للورديات";
            lblTitle.ForeColor = Theme.HeaderTextColor;
            lblTitle.Font = Theme.Font(12f);
            lblTitle.Dock = DockStyle.Fill;
            lblTitle.TextAlign = ContentAlignment.MiddleCenter;
            Theme.StyleHeader(header);
            header.Controls.Add(lblTitle);
            header.Dock = DockStyle.Top;

            FlowLayoutPanel toolbar = new FlowLayoutPanel();
            toolbar.Dock = DockStyle.Top;
            toolbar.Height = 88;
            toolbar.BackColor = Theme.ToolbarColor;
            // FlowLayoutPanel: RTL=Yes (inherited from the form) already reverses the flow;
            // an explicit RightToLeft cancelled that reversal and rendered the bar left-to-right.
            toolbar.FlowDirection = FlowDirection.LeftToRight;
            toolbar.WrapContents = true;
            toolbar.Padding = new Padding(6, 8, 6, 4);

            _numYear = new NumericUpDown();
            _numYear.Minimum = 2000;
            _numYear.Maximum = 2100;
            _numYear.Value = DateTime.Today.Year;
            _numYear.Width = 72;
            _numYear.TextAlign = HorizontalAlignment.Center;

            _cboMonth = new ComboBox();
            _cboMonth.DropDownStyle = ComboBoxStyle.DropDownList;
            _cboMonth.Width = 110;
            for (int month = 1; month <= 12; month++)
            {
                _cboMonth.Items.Add(month + " - " + ArabicDates.MonthName(month));
            }
            _cboMonth.SelectedIndex = DateTime.Today.Month - 1;

            _cboDepartment = new ComboBox();
            _cboDepartment.DropDownStyle = ComboBoxStyle.DropDownList;
            _cboDepartment.Width = 150;
            _cboDepartment.SelectedIndexChanged += delegate { FillSections(); };

            _cboSection = new ComboBox();
            _cboSection.DropDownStyle = ComboBoxStyle.DropDownList;
            _cboSection.Width = 150;

            _cboDefaultShift = new ComboBox();
            _cboDefaultShift.DropDownStyle = ComboBoxStyle.DropDownList;
            _cboDefaultShift.Width = 120;

            _btnLoad = new Button();
            _btnLoad.Text = "تحميل";
            _btnLoad.Height = 30;
            _btnLoad.Click += delegate { OnLoadClick(); };

            _btnSave = new Button();
            _btnSave.Text = "حفظ";
            _btnSave.Height = 30;
            _btnSave.Click += delegate { OnSaveClick(); };

            _btnFill = new Button();
            _btnFill.Text = "ملء تلقائي";
            _btnFill.Height = 30;
            _btnFill.Click += delegate { OnFillClick(); };

            _btnCopyRow = new Button();
            _btnCopyRow.Text = "نسخ صف";
            _btnCopyRow.Height = 30;
            _btnCopyRow.Click += delegate { OnCopyRowClick(); };

            _btnPasteRow = new Button();
            _btnPasteRow.Text = "لصق صف";
            _btnPasteRow.Height = 30;
            _btnPasteRow.Click += delegate { OnPasteRowClick(); };

            _btnCopyDay = new Button();
            _btnCopyDay.Text = "نسخ يوم";
            _btnCopyDay.Height = 30;
            _btnCopyDay.Click += delegate { OnCopyDayClick(); };

            _btnPasteDay = new Button();
            _btnPasteDay.Text = "لصق يوم";
            _btnPasteDay.Height = 30;
            _btnPasteDay.Click += delegate { OnPasteDayClick(); };

            _btnCopyWeek = new Button();
            _btnCopyWeek.Text = "نسخ أسبوع";
            _btnCopyWeek.Height = 30;
            _btnCopyWeek.Click += delegate { OnCopyWeekClick(); };

            _btnPasteWeek = new Button();
            _btnPasteWeek.Text = "لصق أسبوع";
            _btnPasteWeek.Height = 30;
            _btnPasteWeek.Click += delegate { OnPasteWeekClick(); };

            _btnCopyPrevMonth = new Button();
            _btnCopyPrevMonth.Text = "نسخ الشهر السابق";
            _btnCopyPrevMonth.Height = 30;
            _btnCopyPrevMonth.Click += delegate { OnCopyPrevMonthClick(); };

            _btnUndo = new Button();
            _btnUndo.Text = "تراجع";
            _btnUndo.Height = 30;
            _btnUndo.Click += delegate { OnUndoClick(); };

            _btnLock = new Button();
            _btnLock.Text = "قفل الشهر";
            _btnLock.Height = 30;
            _btnLock.Click += delegate { OnLockClick(); };

            _btnUnlock = new Button();
            _btnUnlock.Text = "فتح الشهر";
            _btnUnlock.Height = 30;
            _btnUnlock.Click += delegate { OnUnlockClick(); };

            _cboTemplate = new ComboBox();
            _cboTemplate.DropDownStyle = ComboBoxStyle.DropDownList;
            _cboTemplate.Width = 150;
            foreach (string templateName in ScheduleBLL.RotationTemplateNames())
            {
                _cboTemplate.Items.Add(templateName);
            }
            if (_cboTemplate.Items.Count > 0)
            {
                _cboTemplate.SelectedIndex = 0;
            }

            _btnRotate = new Button();
            _btnRotate.Text = "تطبيق نمط دوري";
            _btnRotate.Height = 30;
            _btnRotate.Click += delegate { OnRotateClick(); };

            _btnCoverage = new Button();
            _btnCoverage.Text = "خريطة التغطية";
            _btnCoverage.Height = 30;
            _btnCoverage.Click += delegate { OnCoverageClick(); };

            _btnCsv = new Button();
            _btnCsv.Text = "تصدير CSV";
            _btnCsv.Height = 30;
            _btnCsv.Click += delegate { OnCsvClick(); };

            _btnSubmit = new Button();
            _btnSubmit.Text = "إرسال للاعتماد";
            _btnSubmit.Height = 30;
            _btnSubmit.Click += delegate { OnSubmitClick(); };

            _btnApprove = new Button();
            _btnApprove.Text = "اعتماد نهائي";
            _btnApprove.Height = 30;
            _btnApprove.Click += delegate { OnApproveFinalClick(); };

            _btnReject = new Button();
            _btnReject.Text = "رفض";
            _btnReject.Height = 30;
            _btnReject.Click += delegate { OnRejectFinalClick(); };

            _btnPrint = new Button();
            _btnPrint.Text = "طباعة A3 مع خانات الاعتماد";
            _btnPrint.Height = 30;
            _btnPrint.Click += delegate { OnPrintClick(); };

            _lblApproval = new Label();
            _lblApproval.AutoSize = true;
            _lblApproval.Margin = new Padding(6, 12, 6, 0);

            _lblState = new Label();
            _lblState.AutoSize = true;
            _lblState.Margin = new Padding(6, 12, 6, 0);

            Label lblYear = MakeLabel("السنة:");
            Label lblMonth = MakeLabel("الشهر:");
            Label lblDepartment = MakeLabel("القسم:");
            Label lblSection = MakeLabel("القطاع:");
            Label lblDefault = MakeLabel("وردية الملء:");

            toolbar.Controls.Add(lblYear);
            toolbar.Controls.Add(_numYear);
            toolbar.Controls.Add(lblMonth);
            toolbar.Controls.Add(_cboMonth);
            toolbar.Controls.Add(lblDepartment);
            toolbar.Controls.Add(_cboDepartment);
            toolbar.Controls.Add(lblSection);
            toolbar.Controls.Add(_cboSection);
            toolbar.Controls.Add(_btnLoad);
            toolbar.Controls.Add(_btnSave);
            toolbar.Controls.Add(_btnFill);
            toolbar.Controls.Add(lblDefault);
            toolbar.Controls.Add(_cboDefaultShift);
            toolbar.Controls.Add(_btnCopyRow);
            toolbar.Controls.Add(_btnPasteRow);
            toolbar.Controls.Add(_btnCopyDay);
            toolbar.Controls.Add(_btnPasteDay);
            toolbar.Controls.Add(_btnCopyWeek);
            toolbar.Controls.Add(_btnPasteWeek);
            toolbar.Controls.Add(_btnCopyPrevMonth);
            toolbar.Controls.Add(_btnUndo);
            toolbar.Controls.Add(_btnLock);
            toolbar.Controls.Add(_btnUnlock);
            toolbar.Controls.Add(_cboTemplate);
            toolbar.Controls.Add(_btnRotate);
            toolbar.Controls.Add(_btnCoverage);
            toolbar.Controls.Add(_btnCsv);
            toolbar.Controls.Add(_btnSubmit);
            toolbar.Controls.Add(_btnApprove);
            toolbar.Controls.Add(_btnReject);
            toolbar.Controls.Add(_btnPrint);
            toolbar.Controls.Add(_lblApproval);
            toolbar.Controls.Add(_lblState);

            _grid = new DataGridView();
            Theme.StyleGrid(_grid);
            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = false;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.AllowUserToResizeRows = false;
            _grid.RowHeadersVisible = true;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.EnableResizing;
            _grid.ColumnHeadersHeight = 44;
            _grid.EditMode = DataGridViewEditMode.EditOnEnter;
            _grid.SelectionMode = DataGridViewSelectionMode.CellSelect;
            _grid.CurrentCellDirtyStateChanged += delegate
            {
                if (_grid.IsCurrentCellDirty)
                {
                    _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
                }
            };
            _grid.CellValueChanged += OnGridCellValueChanged;
            _grid.DataError += delegate(object sender, DataGridViewDataErrorEventArgs e)
            {
                e.ThrowException = false;
            };

            Controls.Add(_grid);
            Controls.Add(toolbar);
            Controls.Add(header);

            FillDepartments();
            FillSections();
            ApplyPermissionButtons();
        }

        private static Label MakeLabel(string text)
        {
            Label label = new Label();
            label.Text = text;
            label.AutoSize = true;
            label.Margin = new Padding(6, 12, 2, 0);
            return label;
        }

        /// <summary>Toolbar buttons gated by permissions (rule 21).</summary>
        private void ApplyPermissionButtons()
        {
            bool canEdit = AppContext.HasPermission("Schedule.Edit");
            bool canApprove = AppContext.HasPermission("Schedule.Approve");
            bool canUnlock = AppContext.HasPermission("Schedule.Unlock");
            _btnSave.Enabled = canEdit;
            _btnFill.Enabled = canEdit;
            _btnCopyRow.Enabled = canEdit;
            _btnPasteRow.Enabled = canEdit;
            _btnCopyDay.Enabled = canEdit;
            _btnPasteDay.Enabled = canEdit;
            _btnCopyWeek.Enabled = canEdit;
            _btnPasteWeek.Enabled = canEdit;
            _btnCopyPrevMonth.Enabled = canEdit;
            _btnUndo.Enabled = canEdit;
            _btnLock.Enabled = canApprove;
            _btnUnlock.Enabled = canUnlock;
            _btnRotate.Enabled = canEdit;
            _btnCoverage.Enabled = true;
            _btnCsv.Enabled = true;
            _btnSubmit.Enabled = canEdit;
            _btnApprove.Enabled = canApprove;
            _btnReject.Enabled = canApprove;
            _btnPrint.Enabled = true;
        }

        // ---------------- filters ----------------

        private void FillDepartments()
        {
            _departmentNames = new Dictionary<int, string>();
            Dictionary<int, string> options = new Dictionary<int, string>();
            options[0] = "كل الأقسام";
            foreach (Department department in OrganizationBLL.Departments(true))
            {
                _departmentNames[department.DepartmentId] = department.DepartmentName;
                options[department.DepartmentId] = department.DepartmentName;
            }
            Theme.BindDictionary(_cboDepartment, options);
            _cboDepartment.SelectedValue = 0;
        }

        /// <summary>Sections of the selected department; disabled until a department is chosen.</summary>
        private void FillSections()
        {
            _sectionNames = new Dictionary<int, string>();
            Dictionary<int, string> options = new Dictionary<int, string>();
            options[0] = "كل القطاعات";
            int? departmentId = MatchId(_cboDepartment, _departmentNames);
            if (departmentId != null)
            {
                foreach (Section section in OrganizationBLL.Sections(departmentId, true))
                {
                    _sectionNames[section.SectionId] = section.SectionName;
                    options[section.SectionId] = section.SectionName;
                }
                _cboSection.Enabled = true;
            }
            else
            {
                _cboSection.DataSource = null;
                _cboSection.Items.Clear();
                _cboSection.Items.Add("كل القطاعات");
                _cboSection.SelectedIndex = 0;
                _cboSection.Enabled = false;
                return;
            }
            _cboSection.DataSource = new BindingSource(options, null);
            _cboSection.DisplayMember = "Value";
            _cboSection.ValueMember = "Key";
            _cboSection.SelectedValue = 0;
        }

        /// <summary>Real id behind a sentinel dictionary combo; 0 means (الكل).</summary>
        private static int? MatchId(ComboBox combo, Dictionary<int, string> names)
        {
            object value = combo.SelectedValue;
            if (!(value is int))
            {
                return null;
            }
            int id = (int)value;
            if (id == 0 || names == null || !names.ContainsKey(id))
            {
                return null;
            }
            return id;
        }

        // ---------------- load / bind ----------------

        private void OnLoadClick()
        {
            try
            {
                LoadCurrentMonth();
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmMonthlySchedule.OnLoadClick");
                Theme.ShowError("حدث خطأ أثناء تحميل الجدول الشهري.");
            }
        }

        private void LoadCurrentMonth()
        {
            int? departmentId = MatchId(_cboDepartment, _departmentNames);
            int? sectionId = departmentId != null ? MatchId(_cboSection, _sectionNames) : null;
            LoadMonthData((int)_numYear.Value, _cboMonth.SelectedIndex + 1, departmentId, sectionId);
        }

        private void LoadMonthData(int year, int month, int? departmentId, int? sectionId)
        {
            _data = ScheduleBLL.LoadMonth(year, month, departmentId, sectionId);
            _restDays = WeekRestDays.Parse(SettingsBLL.GetString(WeekRestDays.SettingKey, WeekRestDays.DefaultDays));
            _holidayDays = new HashSet<int>();
            try
            {
                // Phase 3 (R-02): shared holiday-day set (explicit + recurring).
                _holidayDays = PublicHolidayBLL.HolidayDays(year, month);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmMonthlySchedule.LoadMonthData.Holidays");
            }
            _cells = Clone(_data.Cells);
            _snapshot = Clone(_cells);
            _rowClipboard = null;
            _dayClipboard = null;
            _weekClipboard = null;
            LoadDefaultShifts();
            BindGrid();
            bool canEdit = !_data.IsLocked || AppContext.IsCurrentUserAdmin;
            _grid.ReadOnly = !canEdit;
            if (_data.IsLocked)
            {
                _lblState.Text = "الشهر " + year + "-" + month + " مقفل — التعديل لمسؤول النظام فقط (مُسجَّل في سجل العمليات).";
                _lblState.ForeColor = Theme.StatusBad;
            }
            else
            {
                _lblState.Text = "مسودة — الشهر " + year + "-" + month + " غير مقفل، التعديل والقفل متاحان حسب الصلاحيات.";
                _lblState.ForeColor = Theme.StatusOk;
            }
            UpdateApprovalLabel();
        }

        /// <summary>Non-rest shifts sorted by code, used as the autofill default.</summary>
        private void LoadDefaultShifts()
        {
            List<Shift> options = new List<Shift>();
            foreach (Shift shift in _data.Shifts.Values)
            {
                if (!CategoryRest(shift))
                {
                    options.Add(shift);
                }
            }
            options.Sort(delegate(Shift a, Shift b)
            {
                return string.Compare(a.ShiftCode, b.ShiftCode, StringComparison.Ordinal);
            });
            _cboDefaultShift.DataSource = options;
            _cboDefaultShift.DisplayMember = "ShiftCode";
            _cboDefaultShift.ValueMember = "ShiftId";
            if (options.Count > 0)
            {
                _cboDefaultShift.SelectedIndex = 0;
            }
            else
            {
                _cboDefaultShift.SelectedIndex = -1;
            }
        }

        /// <summary>Rebuilds all columns: code/name then one combo per day (rule 7).</summary>
        private void BindGrid()
        {
            _loadingGrid = true;
            try
            {
                _grid.Columns.Clear();
                _grid.Rows.Clear();

                DataGridViewTextBoxColumn codeColumn = new DataGridViewTextBoxColumn();
                codeColumn.Name = "code";
                codeColumn.HeaderText = "الكود";
                codeColumn.ReadOnly = true;
                codeColumn.Width = 70;
                codeColumn.Frozen = true;
                codeColumn.SortMode = DataGridViewColumnSortMode.NotSortable;
                _grid.Columns.Add(codeColumn);

                DataGridViewTextBoxColumn nameColumn = new DataGridViewTextBoxColumn();
                nameColumn.Name = "name";
                nameColumn.HeaderText = "اسم الموظف";
                nameColumn.ReadOnly = true;
                nameColumn.Width = 190;
                nameColumn.Frozen = true;
                nameColumn.SortMode = DataGridViewColumnSortMode.NotSortable;
                _grid.Columns.Add(nameColumn);

                int daysInMonth = DateTime.DaysInMonth(_data.Year, _data.Month);
                for (int day = 1; day <= daysInMonth; day++)
                {
                    List<Shift> options = new List<Shift>(_data.Shifts.Values);
                    options.Sort(delegate(Shift a, Shift b)
                    {
                        return string.Compare(a.ShiftCode, b.ShiftCode, StringComparison.Ordinal);
                    });
                    DataGridViewComboBoxColumn dayColumn = new DataGridViewComboBoxColumn();
                    dayColumn.Name = "day" + day;
                    dayColumn.HeaderText = day + "\n" + ArabicDates.DayName(new DateTime(_data.Year, _data.Month, day).DayOfWeek);
                    dayColumn.DataSource = options;
                    dayColumn.DisplayMember = "ShiftCode";
                    dayColumn.ValueMember = "ShiftId";
                    dayColumn.ValueType = typeof(int);
                    dayColumn.Width = 52;
                    dayColumn.SortMode = DataGridViewColumnSortMode.NotSortable;
                    dayColumn.HeaderCell.Style.WrapMode = DataGridViewTriState.True;
                    if (_restDays != null && _restDays.Contains(new DateTime(_data.Year, _data.Month, day).DayOfWeek))
                    {
                        dayColumn.HeaderCell.Style.BackColor = Theme.StatusWarn;
                    }
                    else if (_holidayDays != null && _holidayDays.Contains(day))
                    {
                        dayColumn.HeaderCell.Style.BackColor = Theme.StatusBad;
                    }
                    _grid.Columns.Add(dayColumn);
                }

                DataGridViewTextBoxColumn hoursColumn = new DataGridViewTextBoxColumn();
                hoursColumn.Name = "hours";
                hoursColumn.HeaderText = "ساعات الشهر";
                hoursColumn.ReadOnly = true;
                hoursColumn.Width = 90;
                hoursColumn.SortMode = DataGridViewColumnSortMode.NotSortable;
                _grid.Columns.Add(hoursColumn);

                foreach (Employee employee in _data.Employees)
                {
                    int rowIndex = _grid.Rows.Add();
                    DataGridViewRow row = _grid.Rows[rowIndex];
                    row.Tag = employee;
                    row.Cells["code"].Value = employee.EmployeeCode;
                    row.Cells["name"].Value = employee.FullNameAr;
                }
                RefreshGridValues();
            }
            finally
            {
                _loadingGrid = false;
            }
        }

        /// <summary>Shift id stored for an employee/day, or null when empty or unknown shift.</summary>
        private int? CellValue(int employeeId, int day)
        {
            Dictionary<int, int> days;
            int shiftId;
            if (_cells.TryGetValue(employeeId, out days) && days.TryGetValue(day, out shiftId)
                && _data.Shifts.ContainsKey(shiftId))
            {
                return shiftId;
            }
            return null;
        }

        /// <summary>Rewrites every day cell from the in-memory map (paste/fill/undo).</summary>
        private void RefreshGridValues()
        {
            _loadingGrid = true;
            try
            {
                int daysInMonth = DateTime.DaysInMonth(_data.Year, _data.Month);
                foreach (DataGridViewRow row in _grid.Rows)
                {
                    Employee employee = row.Tag as Employee;
                    if (employee == null)
                    {
                        continue;
                    }
                    for (int day = 1; day <= daysInMonth; day++)
                    {
                        int? shiftId = CellValue(employee.EmployeeId, day);
                        row.Cells["day" + day].Value = shiftId != null ? (object)shiftId.Value : null;
                    }
                    UpdateHoursCell(row, employee.EmployeeId);
                }
            }
            finally
            {
                _loadingGrid = false;
            }
        }

        /// <summary>Monthly-hours cell: red below target, yellow above, green match;
        /// plain (no tint) when the target rule is disabled (0).</summary>
        private void UpdateHoursCell(DataGridViewRow row, int employeeId)
        {
            int target = SettingsBLL.GetInt("Sch.TargetMonthlyHours", 0);
            int daysInMonth = DateTime.DaysInMonth(_data.Year, _data.Month);
            double total = 0;
            Dictionary<int, int> days;
            if (_cells.TryGetValue(employeeId, out days))
            {
                foreach (KeyValuePair<int, int> pair in days)
                {
                    Shift shift;
                    if (_data.Shifts.TryGetValue(pair.Value, out shift) && !CategoryRest(shift))
                    {
                        total += shift.DurationHrs ?? shift.NormalHours ?? 8f;
                    }
                }
            }
            DataGridViewCell cell = row.Cells["hours"];
            cell.Value = total.ToString("0.#") + (target > 0 ? " / " + target : "");
            if (target <= 0)
            {
                cell.Style.BackColor = Color.Empty;
                return;
            }
            if (total < target)
            {
                cell.Style.BackColor = Theme.StatusBad;
            }
            else if (total > target)
            {
                cell.Style.BackColor = Theme.StatusWarn;
            }
            else
            {
                cell.Style.BackColor = Theme.StatusOk;
            }
        }

        private void OnGridCellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (_loadingGrid || _data == null || e.RowIndex < 0 || e.ColumnIndex < 2)
            {
                return;
            }
            Employee employee = _grid.Rows[e.RowIndex].Tag as Employee;
            if (employee == null)
            {
                return;
            }
            int day;
            if (!int.TryParse(_grid.Columns[e.ColumnIndex].Name.Substring(3), out day))
            {
                return;
            }
            object raw = _grid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value;
            if (raw is int)
            {
                RowCells(employee.EmployeeId)[day] = (int)raw;
            }
            else
            {
                Dictionary<int, int> days;
                if (_cells.TryGetValue(employee.EmployeeId, out days) && days.ContainsKey(day))
                {
                    days.Remove(day);
                }
            }
            UpdateHoursCell(_grid.Rows[e.RowIndex], employee.EmployeeId);
        }

        // ---------------- helpers ----------------

        private Employee SelectedEmployee()
        {
            if (_grid.CurrentRow == null)
            {
                return null;
            }
            return _grid.CurrentRow.Tag as Employee;
        }

        private int? SelectedDay()
        {
            if (_grid.CurrentCell == null)
            {
                return null;
            }
            int columnIndex = _grid.CurrentCell.ColumnIndex;
            if (columnIndex < 2)
            {
                return null;
            }
            int day;
            if (!int.TryParse(_grid.Columns[columnIndex].Name.Substring(3), out day))
            {
                return null;
            }
            return day;
        }

        private static bool CategoryRest(Shift shift)
        {
            return string.Equals(shift.CategoryCode, "Rest", StringComparison.Ordinal) ||
                   string.Equals(shift.ShiftCode, "FR", StringComparison.OrdinalIgnoreCase);
        }

        private int? RestShiftId()
        {
            foreach (Shift shift in _data.Shifts.Values)
            {
                if (CategoryRest(shift))
                {
                    return shift.ShiftId;
                }
            }
            return null;
        }

        private int? DefaultShiftId()
        {
            object value = _cboDefaultShift.SelectedValue;
            if (value is int)
            {
                return (int)value;
            }
            return null;
        }

        private Dictionary<int, int> RowCells(int employeeId)
        {
            Dictionary<int, int> days;
            if (!_cells.TryGetValue(employeeId, out days))
            {
                days = new Dictionary<int, int>();
                _cells[employeeId] = days;
            }
            return days;
        }

        private static Dictionary<int, Dictionary<int, int>> Clone(Dictionary<int, Dictionary<int, int>> source)
        {
            Dictionary<int, Dictionary<int, int>> copy = new Dictionary<int, Dictionary<int, int>>();
            foreach (KeyValuePair<int, Dictionary<int, int>> pair in source)
            {
                copy[pair.Key] = new Dictionary<int, int>(pair.Value);
            }
            return copy;
        }

        /// <summary>Day number starting the operational week (Sch.RestDays-derived anchor)
        /// that contains the given day.</summary>
        private int WeekStartDay(int day)
        {
            DateTime date = new DateTime(_data.Year, _data.Month, day);
            List<DayOfWeek> restDays = _restDays != null && _restDays.Count > 0
                ? _restDays
                : WeekRestDays.Parse(null);
            int anchor = (int)WeekRestDays.WeekStartAfterRest(restDays);
            int offset = ((int)date.DayOfWeek - anchor + 7) % 7;
            return day - offset;
        }

        private void RequireLoadedMonth()
        {
            if (_data == null)
            {
                throw new BusinessException("حمّل الشهر أولًا.");
            }
        }

        // ---------------- actions ----------------

        private void OnSaveClick()
        {
            try
            {
                RequireLoadedMonth();
                List<string> overruns = ScheduleBLL.ContinuousOverruns(
                    _data.Year, _data.Month, _data.Employees, _cells, _data.Shifts);
                bool preApproved = false;
                if (overruns.Count > 0 && AppContext.HasPermission("Schedule.Unlock"))
                {
                    StringBuilder message = new StringBuilder(
                        "يحتوي الجدول على تعارض/عمل متواصل يتجاوز الحد المسموح:\n");
                    for (int i = 0; i < overruns.Count && i < 5; i++)
                    {
                        message.Append("- ").Append(overruns[i]).Append("\n");
                    }
                    message.Append("هل تملك موافقة مسبقة على هذا التجاوز؟ (سيُسجَّل في سجل العمليات)");
                    preApproved = Theme.Confirm(message.ToString());
                }
                ScheduleBLL.SaveMonth(_data.Year, _data.Month, _data.Employees, _cells, _data.Shifts, preApproved);
                _snapshot = Clone(_cells);
                Theme.ShowInfo("تم حفظ الجدول الشهري " + _data.Year + "-" + _data.Month + ".");
                LoadCurrentMonth();
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmMonthlySchedule.OnSaveClick");
                Theme.ShowError("حدث خطأ أثناء حفظ الجدول الشهري.");
            }
        }

        /// <summary>Autofill: default shift every day, configured Sch.RestDays rest shift.</summary>
        private void OnFillClick()
        {
            try
            {
                RequireLoadedMonth();
                Employee employee = SelectedEmployee();
                if (employee == null)
                {
                    Theme.ShowInfo("اختر موظفًا من الجدول أولًا.");
                    return;
                }
                int? defaultShiftId = DefaultShiftId();
                if (defaultShiftId == null)
                {
                    Theme.ShowError("اختر وردية الملء من الشريط أولًا.");
                    return;
                }
                int? restShiftId = RestShiftId();
                Dictionary<int, int> days = RowCells(employee.EmployeeId);
                days.Clear();
                int daysInMonth = DateTime.DaysInMonth(_data.Year, _data.Month);
                for (int day = 1; day <= daysInMonth; day++)
                {
                    if ((_restDays == null || _restDays.Contains(new DateTime(_data.Year, _data.Month, day).DayOfWeek)) && restShiftId != null)
                    {
                        days[day] = restShiftId.Value;
                    }
                    else
                    {
                        days[day] = defaultShiftId.Value;
                    }
                }
                RefreshGridValues();
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmMonthlySchedule.OnFillClick");
                Theme.ShowError("حدث خطأ أثناء الملء التلقائي.");
            }
        }

        private void OnCopyRowClick()
        {
            try
            {
                RequireLoadedMonth();
                Employee employee = SelectedEmployee();
                if (employee == null)
                {
                    Theme.ShowInfo("اختر موظفًا من الجدول أولًا.");
                    return;
                }
                Dictionary<int, int> days;
                if (!_cells.TryGetValue(employee.EmployeeId, out days) || days.Count == 0)
                {
                    Theme.ShowInfo("صف الموظف المحدد فارغ — لا يوجد ما يُنسخ.");
                    return;
                }
                _rowClipboard = new Dictionary<int, int>(days);
                Theme.ShowInfo("تم نسخ صف '" + employee.FullNameAr + "' (" + days.Count + " يوم).");
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmMonthlySchedule.OnCopyRowClick");
                Theme.ShowError("حدث خطأ أثناء نسخ الصف.");
            }
        }

        private void OnPasteRowClick()
        {
            try
            {
                RequireLoadedMonth();
                if (_rowClipboard == null || _rowClipboard.Count == 0)
                {
                    Theme.ShowInfo("حافظة الصفوف فارغة — انسخ صفًا أولًا.");
                    return;
                }
                Employee employee = SelectedEmployee();
                if (employee == null)
                {
                    Theme.ShowInfo("اختر الموظف الهدف من الجدول أولًا.");
                    return;
                }
                int daysInMonth = DateTime.DaysInMonth(_data.Year, _data.Month);
                Dictionary<int, int> days = RowCells(employee.EmployeeId);
                days.Clear();
                foreach (KeyValuePair<int, int> pair in _rowClipboard)
                {
                    if (pair.Key < 1 || pair.Key > daysInMonth)
                    {
                        continue;
                    }
                    if (!_data.Shifts.ContainsKey(pair.Value))
                    {
                        continue;
                    }
                    days[pair.Key] = pair.Value;
                }
                RefreshGridValues();
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmMonthlySchedule.OnPasteRowClick");
                Theme.ShowError("حدث خطأ أثناء لصق الصف.");
            }
        }

        private void OnCopyDayClick()
        {
            try
            {
                RequireLoadedMonth();
                int? day = SelectedDay();
                if (day == null)
                {
                    Theme.ShowInfo("اختر خانة يوم من الجدول أولًا.");
                    return;
                }
                _dayClipboard = new Dictionary<int, int>();
                foreach (DataGridViewRow row in _grid.Rows)
                {
                    Employee employee = row.Tag as Employee;
                    if (employee == null)
                    {
                        continue;
                    }
                    int? shiftId = CellValue(employee.EmployeeId, day.Value);
                    if (shiftId != null)
                    {
                        _dayClipboard[employee.EmployeeId] = shiftId.Value;
                    }
                }
                Theme.ShowInfo("تم نسخ يوم " + day.Value + " لعدد " + _dayClipboard.Count + " موظف.");
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmMonthlySchedule.OnCopyDayClick");
                Theme.ShowError("حدث خطأ أثناء نسخ اليوم.");
            }
        }

        private void OnPasteDayClick()
        {
            try
            {
                RequireLoadedMonth();
                if (_dayClipboard == null || _dayClipboard.Count == 0)
                {
                    Theme.ShowInfo("حافظة الأيام فارغة — انسخ يومًا أولًا.");
                    return;
                }
                int? day = SelectedDay();
                if (day == null)
                {
                    Theme.ShowInfo("اختر اليوم الهدف في الجدول أولًا.");
                    return;
                }
                Dictionary<int, DataGridViewRow> visible = new Dictionary<int, DataGridViewRow>();
                foreach (DataGridViewRow row in _grid.Rows)
                {
                    Employee employee = row.Tag as Employee;
                    if (employee != null)
                    {
                        visible[employee.EmployeeId] = row;
                    }
                }
                foreach (KeyValuePair<int, int> pair in _dayClipboard)
                {
                    if (!visible.ContainsKey(pair.Key))
                    {
                        continue;
                    }
                    if (!_data.Shifts.ContainsKey(pair.Value))
                    {
                        continue;
                    }
                    RowCells(pair.Key)[day.Value] = pair.Value;
                }
                RefreshGridValues();
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmMonthlySchedule.OnPasteDayClick");
                Theme.ShowError("حدث خطأ أثناء لصق اليوم.");
            }
        }

        private void OnCopyWeekClick()
        {
            try
            {
                RequireLoadedMonth();
                Employee employee = SelectedEmployee();
                if (employee == null)
                {
                    Theme.ShowInfo("اختر موظفًا من الجدول أولًا.");
                    return;
                }
                int? day = SelectedDay();
                if (day == null)
                {
                    Theme.ShowInfo("اختر خانة يوم ضمن الأسبوع المراد نسخه أولًا.");
                    return;
                }
                int weekStart = WeekStartDay(day.Value);
                int daysInMonth = DateTime.DaysInMonth(_data.Year, _data.Month);
                _weekClipboard = new Dictionary<int, int>();
                for (int offset = 0; offset < 7; offset++)
                {
                    int target = weekStart + offset;
                    if (target < 1 || target > daysInMonth)
                    {
                        continue;
                    }
                    int? shiftId = CellValue(employee.EmployeeId, target);
                    if (shiftId != null)
                    {
                        _weekClipboard[offset] = shiftId.Value;
                    }
                }
                Theme.ShowInfo("تم نسخ أسبوع الموظف '" + employee.FullNameAr + "' (" + _weekClipboard.Count + " يوم).");
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmMonthlySchedule.OnCopyWeekClick");
                Theme.ShowError("حدث خطأ أثناء نسخ الأسبوع.");
            }
        }

        private void OnPasteWeekClick()
        {
            try
            {
                RequireLoadedMonth();
                if (_weekClipboard == null || _weekClipboard.Count == 0)
                {
                    Theme.ShowInfo("حافظة الأسابيع فارغة — انسخ أسبوعًا أولًا.");
                    return;
                }
                Employee employee = SelectedEmployee();
                if (employee == null)
                {
                    Theme.ShowInfo("اختر الموظف الهدف من الجدول أولًا.");
                    return;
                }
                int? day = SelectedDay();
                if (day == null)
                {
                    Theme.ShowInfo("اختر يومًا ضمن الأسبوع الهدف في الجدول أولًا.");
                    return;
                }
                int weekStart = WeekStartDay(day.Value);
                int daysInMonth = DateTime.DaysInMonth(_data.Year, _data.Month);
                Dictionary<int, int> days = RowCells(employee.EmployeeId);
                foreach (KeyValuePair<int, int> pair in _weekClipboard)
                {
                    int target = weekStart + pair.Key;
                    if (target < 1 || target > daysInMonth)
                    {
                        continue;
                    }
                    if (!_data.Shifts.ContainsKey(pair.Value))
                    {
                        continue;
                    }
                    days[target] = pair.Value;
                }
                RefreshGridValues();
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmMonthlySchedule.OnPasteWeekClick");
                Theme.ShowError("حدث خطأ أثناء لصق الأسبوع.");
            }
        }

        /// <summary>Copies the previous month's grid by day number (not weekday).</summary>
        private void OnCopyPrevMonthClick()
        {
            try
            {
                RequireLoadedMonth();
                int prevMonth = _data.Month == 1 ? 12 : _data.Month - 1;
                int prevYear = _data.Month == 1 ? _data.Year - 1 : _data.Year;
                int? departmentId = MatchId(_cboDepartment, _departmentNames);
                int? sectionId = departmentId != null ? MatchId(_cboSection, _sectionNames) : null;
                MonthData previous = ScheduleBLL.LoadMonth(prevYear, prevMonth, departmentId, sectionId);

                int daysInMonth = DateTime.DaysInMonth(_data.Year, _data.Month);
                int prevDaysInMonth = DateTime.DaysInMonth(prevYear, prevMonth);
                int dayLimit = Math.Min(daysInMonth, prevDaysInMonth);
                int copied = 0;
                foreach (Employee employee in _data.Employees)
                {
                    Dictionary<int, int> previousDays;
                    if (!previous.Cells.TryGetValue(employee.EmployeeId, out previousDays))
                    {
                        continue;
                    }
                    Dictionary<int, int> days = RowCells(employee.EmployeeId);
                    for (int day = 1; day <= dayLimit; day++)
                    {
                        int shiftId;
                        if (!previousDays.TryGetValue(day, out shiftId))
                        {
                            continue;
                        }
                        if (!_data.Shifts.ContainsKey(shiftId))
                        {
                            continue;
                        }
                        days[day] = shiftId;
                        copied++;
                    }
                }
                RefreshGridValues();
                Theme.ShowInfo("تم نسخ جدول " + prevYear + "-" + prevMonth + " (" + copied + " خانة) بنفس أرقام الأيام — احفظ للإثبات.");
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmMonthlySchedule.OnCopyPrevMonthClick");
                Theme.ShowError("حدث خطأ أثناء نسخ الشهر السابق.");
            }
        }

        private void OnUndoClick()
        {
            try
            {
                RequireLoadedMonth();
                _cells = Clone(_snapshot);
                RefreshGridValues();
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmMonthlySchedule.OnUndoClick");
                Theme.ShowError("حدث خطأ أثناء التراجع.");
            }
        }

        private void OnLockClick()
        {
            try
            {
                RequireLoadedMonth();
                if (!Theme.Confirm("قفل شهر " + _data.Year + "-" + _data.Month +
                    " يمنع تعديل الجدول إلا بواسطة مسؤول النظام، وتُسجَّل العملية في سجل العمليات. هل تريد المتابعة؟"))
                {
                    return;
                }
                ScheduleBLL.LockMonth(_data.Year, _data.Month);
                Theme.ShowInfo("تم قفل الشهر " + _data.Year + "-" + _data.Month + ".");
                LoadCurrentMonth();
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmMonthlySchedule.OnLockClick");
                Theme.ShowError("حدث خطأ أثناء قفل الشهر.");
            }
        }

        private void OnUnlockClick()
        {
            try
            {
                RequireLoadedMonth();
                string reason = ReasonDialog.Show(this);
                if (reason == null)
                {
                    return;
                }
                ScheduleBLL.UnlockMonth(_data.Year, _data.Month, reason);
                Theme.ShowInfo("تم فتح الشهر " + _data.Year + "-" + _data.Month + ".");
                LoadCurrentMonth();
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmMonthlySchedule.OnUnlockClick");
                Theme.ShowError("حدث خطأ أثناء فتح الشهر.");
            }
        }

        // ---------------- RosterCore additions: rotation / coverage / CSV / approval ----------------

        private void OnRotateClick()
        {
            try
            {
                RequireLoadedMonth();
                Employee employee = SelectedEmployee();
                if (employee == null)
                {
                    Theme.ShowInfo("اختر موظفاً من الجدول أولاً.");
                    return;
                }
                string templateName = _cboTemplate.SelectedItem as string;
                if (string.IsNullOrWhiteSpace(templateName))
                {
                    Theme.ShowInfo("اختر نمطاً دورياً من القائمة أولاً.");
                    return;
                }
                List<KeyValuePair<string, int>> pattern = ScheduleBLL.RotationTemplateSpec(templateName);
                Dictionary<int, Dictionary<int, int>> generated = ScheduleBLL.ApplyRotation(
                    _data.Year, _data.Month, pattern, _data.Shifts, DefaultShiftId(), RestShiftId(), _holidayDays);
                Dictionary<int, int> days = RowCells(employee.EmployeeId);
                days.Clear();
                foreach (KeyValuePair<int, int> pair in generated[1])
                {
                    days[pair.Key] = pair.Value;
                }
                RefreshGridValues();
                Theme.ShowInfo("طُبِّق النمط الدوري '" + templateName + "' على " + employee.EmployeeCode + " — راجع ثم احفظ.");
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmMonthlySchedule.OnRotateClick");
                Theme.ShowError("حدث خطأ أثناء تطبيق النمط الدوري.");
            }
        }

        /// <summary>Daily coverage map: workers / rest / unscheduled / total hours (in-memory).</summary>
        private void OnCoverageClick()
        {
            try
            {
                RequireLoadedMonth();
                int daysInMonth = DateTime.DaysInMonth(_data.Year, _data.Month);
                DataGridView grid = new DataGridView();
                Theme.StyleGrid(grid);
                grid.Dock = DockStyle.Fill;
                grid.ReadOnly = true;
                grid.AllowUserToAddRows = false;
                grid.AllowUserToDeleteRows = false;
                grid.RowHeadersVisible = false;
                grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
                grid.Columns.Add("day", "اليوم");
                grid.Columns.Add("workers", "عاملون");
                grid.Columns.Add("rest", "راحة");
                grid.Columns.Add("unscheduled", "غير مجدول");
                grid.Columns.Add("hours", "إجمالي الساعات");
                int target = SettingsBLL.GetInt("Sch.TargetMonthlyHours", 0);
                double monthHours = 0;
                for (int day = 1; day <= daysInMonth; day++)
                {
                    int workers = 0;
                    int rest = 0;
                    int unscheduled = _data.Employees.Count;
                    double dayHours = 0;
                    foreach (Employee employee in _data.Employees)
                    {
                        int? shiftId = CellValue(employee.EmployeeId, day);
                        Shift shift;
                        if (shiftId == null || !_data.Shifts.TryGetValue(shiftId.Value, out shift))
                        {
                            continue;
                        }
                        unscheduled--;
                        if (CategoryRest(shift))
                        {
                            rest++;
                        }
                        else
                        {
                            workers++;
                            dayHours += shift.DurationHrs ?? shift.NormalHours ?? 8f;
                        }
                    }
                    monthHours += dayHours;
                    int rowIndex = grid.Rows.Add();
                    DataGridViewRow row = grid.Rows[rowIndex];
                    row.Cells[0].Value = day + " " + ArabicDates.DayName(new DateTime(_data.Year, _data.Month, day).DayOfWeek);
                    row.Cells[1].Value = workers;
                    row.Cells[2].Value = rest;
                    row.Cells[3].Value = unscheduled;
                    row.Cells[4].Value = dayHours.ToString("0.#");
                }
                Form dialog = new Form();
                Theme.Apply(dialog);
                dialog.Text = "خريطة التغطية اليومية — " + _data.Year + "-" + _data.Month +
                    " (إجمالي الشهر " + monthHours.ToString("0.#") + " ساعة" +
                    (target > 0 ? " — الهدف " + target : "") + ")";
                dialog.ClientSize = new Size(560, 620);
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.Controls.Add(grid);
                dialog.ShowDialog(this);
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmMonthlySchedule.OnCoverageClick");
                Theme.ShowError("حدث خطأ أثناء بناء خريطة التغطية.");
            }
        }

        /// <summary>CSV export with UTF-8 BOM so Excel opens Arabic correctly.</summary>
        private void OnCsvClick()
        {
            try
            {
                RequireLoadedMonth();
                using (SaveFileDialog dialog = new SaveFileDialog())
                {
                    dialog.Filter = "CSV (*.csv)|*.csv";
                    dialog.FileName = "Schedule_" + _data.Year + "-" + _data.Month.ToString("00") + ".csv";
                    if (dialog.ShowDialog(this) != DialogResult.OK)
                    {
                        return;
                    }
                    int daysInMonth = DateTime.DaysInMonth(_data.Year, _data.Month);
                    StringBuilder builder = new StringBuilder();
                    builder.Append("الكود,الاسم");
                    for (int day = 1; day <= daysInMonth; day++)
                    {
                        builder.Append(",").Append(day);
                    }
                    builder.Append("\r\n");
                    foreach (DataGridViewRow row in _grid.Rows)
                    {
                        Employee employee = row.Tag as Employee;
                        if (employee == null)
                        {
                            continue;
                        }
                        builder.Append(CsvField(employee.EmployeeCode)).Append(",");
                        builder.Append(CsvField(employee.FullNameAr));
                        for (int day = 1; day <= daysInMonth; day++)
                        {
                            int? shiftId = CellValue(employee.EmployeeId, day);
                            Shift shift;
                            string code = shiftId != null && _data.Shifts.TryGetValue(shiftId.Value, out shift)
                                ? shift.ShiftCode : "";
                            builder.Append(",").Append(CsvField(code));
                        }
                        builder.Append("\r\n");
                    }
                    System.IO.File.WriteAllText(dialog.FileName, builder.ToString(), new System.Text.UTF8Encoding(true));
                    Theme.ShowInfo("تم تصدير الجدول إلى:\n" + dialog.FileName);
                }
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmMonthlySchedule.OnCsvClick");
                Theme.ShowError("حدث خطأ أثناء تصدير CSV.");
            }
        }

        private static string CsvField(string value)
        {
            if (value == null)
            {
                return "";
            }
            if (value.IndexOf(',') >= 0 || value.IndexOf('"') >= 0 || value.IndexOf('\n') >= 0)
            {
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            }
            return value;
        }

        private void OnSubmitClick()
        {
            try
            {
                RequireLoadedMonth();
                if (!Theme.Confirm("إرسال جدول " + _data.Year + "-" + _data.Month + " للاعتماد؟" +
                    "\nستُفحص كل قواعد الجدول ولن يكون الإرسال ممكناً إلا لجدول صحيح."))
                {
                    return;
                }
                ScheduleBLL.SubmitForApproval(_data.Year, _data.Month);
                Theme.ShowInfo("أُرسل الجدول للاعتماد.");
                LoadCurrentMonth();
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmMonthlySchedule.OnSubmitClick");
                Theme.ShowError("حدث خطأ أثناء إرسال الجدول للاعتماد.");
            }
        }

        private void OnApproveFinalClick()
        {
            try
            {
                RequireLoadedMonth();
                if (!Theme.Confirm("اعتماد نهائي لجدول " + _data.Year + "-" + _data.Month + "؟" +
                    "\nسيُقفل الشهر وتصبح التعديلات اللاحقة عبر طلبات تعديل الورديات فقط."))
                {
                    return;
                }
                ScheduleBLL.ApproveMonth(_data.Year, _data.Month);
                Theme.ShowInfo("اعتُمد الجدول وقُفل الشهر.");
                LoadCurrentMonth();
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmMonthlySchedule.OnApproveFinalClick");
                Theme.ShowError("حدث خطأ أثناء اعتماد الجدول.");
            }
        }

        private void OnRejectFinalClick()
        {
            try
            {
                RequireLoadedMonth();
                string reason = ReasonDialog.Show(this, "سبب رفض الجدول",
                    "سبب الرفض (إلزامي — يُعرض على مُعدّ الجدول ويُسجَّل في سجل العمليات):", "رفض الجدول");
                if (reason == null)
                {
                    return;
                }
                ScheduleBLL.RejectMonth(_data.Year, _data.Month, reason);
                Theme.ShowInfo("رُفض الجدول وعاد إلى مسودة.");
                LoadCurrentMonth();
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmMonthlySchedule.OnRejectFinalClick");
                Theme.ShowError("حدث خطأ أثناء رفض الجدول.");
            }
        }

        /// <summary>A3 print of the grid with the approval-chain boxes on the last page.</summary>
        private void OnPrintClick()
        {
            try
            {
                RequireLoadedMonth();
                string subtitle = "جدول " + _data.Year + "-" + _data.Month;
                try
                {
                    ScheduleStatusView status = ScheduleBLL.ScheduleStatus(_data.Year, _data.Month);
                    subtitle += " — الحالة: " + status.StatusAr;
                    if (!string.IsNullOrEmpty(status.RejectReason))
                    {
                        subtitle += " (سبب الرفض: " + status.RejectReason + ")";
                    }
                }
                catch (Exception statusUnexpected)
                {
                    Logger.LogError(statusUnexpected, "FrmMonthlySchedule.OnPrintClick.Status");
                }
                GridPrintHelper.PrintWithApprovals(_grid, "الجدول الشهري للورديات", subtitle,
                    new string[] { "مُعدّ الجدول", "مراجعة الموارد البشرية", "مدير الشؤون الإدارية", "المدير العام" });
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmMonthlySchedule.OnPrintClick");
                Theme.ShowError("حدث خطأ أثناء تجهيز الطباعة.");
            }
        }

        /// <summary>Approval-state label text/color (never breaks the load flow).</summary>
        private void UpdateApprovalLabel()
        {
            try
            {
                ScheduleStatusView status = ScheduleBLL.ScheduleStatus(_data.Year, _data.Month);
                StringBuilder text = new StringBuilder("حالة الاعتماد: " + status.StatusAr);
                if (!string.IsNullOrEmpty(status.SubmittedByName) && status.SubmittedAt != null)
                {
                    text.Append(" — أرسله " + status.SubmittedByName + " في " + status.SubmittedAt.Value.ToString("yyyy-MM-dd HH:mm"));
                }
                if (!string.IsNullOrEmpty(status.ApprovedByName) && status.ApprovedAt != null)
                {
                    text.Append(" — اعتمده " + status.ApprovedByName + " في " + status.ApprovedAt.Value.ToString("yyyy-MM-dd HH:mm"));
                }
                if (!string.IsNullOrEmpty(status.RejectReason))
                {
                    text.Append(" — سبب الرفض: " + status.RejectReason);
                }
                _lblApproval.Text = text.ToString();
                if (status.Status == "Approved") { _lblApproval.ForeColor = Theme.StatusOk; }
                else if (status.Status == "Submitted") { _lblApproval.ForeColor = Theme.StatusWarn; }
                else if (status.Status == "Rejected") { _lblApproval.ForeColor = Theme.StatusBad; }
                else { _lblApproval.ForeColor = Theme.HeaderTextColor; }
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmMonthlySchedule.UpdateApprovalLabel");
                _lblApproval.Text = "";
            }
        }

    }
}
