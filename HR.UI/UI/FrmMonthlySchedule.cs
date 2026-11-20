using System;
using System.Collections.Generic;
using System.Drawing;
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
        private static readonly string[] MonthNames = new string[]
        {
            "يناير", "فبراير", "مارس", "أبريل", "مايو", "يونيو",
            "يوليو", "أغسطس", "سبتمبر", "أكتوبر", "نوفمبر", "ديسمبر"
        };

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

        private MonthData _data;
        private Dictionary<int, Dictionary<int, int>> _cells;
        private Dictionary<int, Dictionary<int, int>> _snapshot;
        private Dictionary<int, int> _rowClipboard;
        private Dictionary<int, int> _dayClipboard;
        private Dictionary<int, int> _weekClipboard;
        private Dictionary<int, string> _departmentNames;
        private Dictionary<int, string> _sectionNames;
        private bool _loadingGrid;

        public FrmMonthlySchedule()
        {
            Theme.Apply(this);
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
            toolbar.FlowDirection = FlowDirection.RightToLeft;
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
                _cboMonth.Items.Add(month + " - " + MonthNames[month - 1]);
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
            _cboDepartment.DataSource = new BindingSource(options, null);
            _cboDepartment.DisplayMember = "Value";
            _cboDepartment.ValueMember = "Key";
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
                    dayColumn.HeaderText = day + "\n" + WeekdayAr(new DateTime(_data.Year, _data.Month, day).DayOfWeek);
                    dayColumn.DataSource = options;
                    dayColumn.DisplayMember = "ShiftCode";
                    dayColumn.ValueMember = "ShiftId";
                    dayColumn.ValueType = typeof(int);
                    dayColumn.Width = 52;
                    dayColumn.SortMode = DataGridViewColumnSortMode.NotSortable;
                    dayColumn.HeaderCell.Style.WrapMode = DataGridViewTriState.True;
                    if (new DateTime(_data.Year, _data.Month, day).DayOfWeek == DayOfWeek.Friday)
                    {
                        dayColumn.HeaderCell.Style.BackColor = Theme.StatusWarn;
                    }
                    _grid.Columns.Add(dayColumn);
                }

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
                }
            }
            finally
            {
                _loadingGrid = false;
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

        /// <summary>Saturday to Friday labels (rule 8-3: week starts Saturday).</summary>
        private static string WeekdayAr(DayOfWeek dayOfWeek)
        {
            switch (dayOfWeek)
            {
                case DayOfWeek.Saturday:
                    return "السبت";
                case DayOfWeek.Sunday:
                    return "الأحد";
                case DayOfWeek.Monday:
                    return "الاثنين";
                case DayOfWeek.Tuesday:
                    return "الثلاثاء";
                case DayOfWeek.Wednesday:
                    return "الأربعاء";
                case DayOfWeek.Thursday:
                    return "الخميس";
                default:
                    return "الجمعة";
            }
        }

        /// <summary>Day number of the Saturday starting the week containing the given day.</summary>
        private int WeekStartDay(int day)
        {
            DateTime date = new DateTime(_data.Year, _data.Month, day);
            int offset = ((int)date.DayOfWeek + 1) % 7;
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
                ScheduleBLL.SaveMonth(_data.Year, _data.Month, _data.Employees, _cells, _data.Shifts);
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

        /// <summary>Autofill: default shift every day, Friday rest when a rest shift exists.</summary>
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
                    if (new DateTime(_data.Year, _data.Month, day).DayOfWeek == DayOfWeek.Friday && restShiftId != null)
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

        /// <summary>Modal dialog collecting the mandatory unlock reason (rule 22).</summary>
        private class ReasonDialog : Form
        {
            private TextBox _txtReason;
            private bool _confirmed;

            private ReasonDialog()
            {
                Theme.Apply(this);
                Text = "سبب فتح الشهر المقفل";
                ClientSize = new Size(440, 155);
                StartPosition = FormStartPosition.CenterParent;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MinimizeBox = false;
                MaximizeBox = false;
                ShowInTaskbar = false;

                Label lblReason = new Label();
                lblReason.Text = "سبب فتح الشهر المقفل (إلزامي، يُسجَّل في سجل العمليات):";
                lblReason.Location = new Point(16, 14);
                lblReason.Size = new Size(408, 22);

                _txtReason = new TextBox();
                _txtReason.Location = new Point(16, 44);
                _txtReason.Size = new Size(408, 26);
                _txtReason.MaxLength = 300;

                Button btnOk = new Button();
                btnOk.Text = "فتح الشهر";
                btnOk.Location = new Point(294, 90);
                btnOk.Size = new Size(130, 32);
                btnOk.DialogResult = DialogResult.None;
                btnOk.Click += delegate
                {
                    if (string.IsNullOrWhiteSpace(_txtReason.Text))
                    {
                        Theme.ShowError("سبب الفتح إلزامي.");
                        return;
                    }
                    _confirmed = true;
                    Close();
                };

                Button btnCancel = new Button();
                btnCancel.Text = "إلغاء";
                btnCancel.Location = new Point(16, 90);
                btnCancel.Size = new Size(130, 32);
                btnCancel.DialogResult = DialogResult.Cancel;
                btnCancel.Click += delegate { Close(); };

                Controls.Add(lblReason);
                Controls.Add(_txtReason);
                Controls.Add(btnOk);
                Controls.Add(btnCancel);
                AcceptButton = btnOk;
                CancelButton = btnCancel;
            }

            /// <summary>Returns the trimmed reason, or null when cancelled.</summary>
            public static string Show(IWin32Window owner)
            {
                using (ReasonDialog dialog = new ReasonDialog())
                {
                    dialog.ShowDialog(owner);
                    if (!dialog._confirmed)
                    {
                        return null;
                    }
                    return dialog._txtReason.Text.Trim();
                }
            }
        }
    }
}
