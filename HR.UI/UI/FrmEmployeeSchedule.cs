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
    /// Dedicated window (order aa2c7e45, requirement 4) to adjust ONE employee's
    /// schedule for specific days of one month — e.g. when an employee knows the
    /// shift that applies to the day of their leave request. Loads the employee's
    /// row from the same monthly schedule grid used by the attendance engine
    /// (ScheduleBLL.LoadMonth) and saves through ScheduleBLL.SaveMonth, so lock
    /// enforcement, BR-09 mixing rules, weekly-hours bounds and auditing all
    /// remain in force. Writing needs "Schedule.Edit"; locked months reject
    /// non-Admin saves with the standard Arabic message.
    /// </summary>
    public class FrmEmployeeSchedule : Form
    {
        private TextBox _txtEmployee;
        private Label _lblEmployeeName;
        private NumericUpDown _numYear;
        private ComboBox _cboMonth;
        private Button _btnLoad;
        private Button _btnSave;
        private Label _lblLockState;
        private DataGridView _grid;
        private Employee _employee;
        private MonthData _month;

        public FrmEmployeeSchedule()
        {
            Theme.Apply(this, false); // MDI child: mirrored layout is not applied — RTL flow comes from the containers
            Text = "تعديل جدول موظف لأيام محددة";
            ClientSize = new Size(760, 620);
            StartPosition = FormStartPosition.CenterParent;

            Panel header = new Panel();
            Label lblTitle = new Label();
            lblTitle.Text = "تعديل جدول موظف واحد لأيام محددة — مثال: تصحيح وردية يوم الإجازة. الحفظ يمر بقواعد الجداول وقفل الشهر وسجل العمليات";
            lblTitle.ForeColor = Theme.HeaderTextColor;
            lblTitle.Font = Theme.Font(12f);
            lblTitle.Dock = DockStyle.Fill;
            lblTitle.TextAlign = ContentAlignment.MiddleCenter;
            Theme.StyleHeader(header);
            header.Controls.Add(lblTitle);
            header.Dock = DockStyle.Top;

            FlowLayoutPanel toolbar = new FlowLayoutPanel();
            toolbar.Dock = DockStyle.Top;
            toolbar.Height = 46;
            toolbar.BackColor = Theme.ToolbarColor;
            // FlowLayoutPanel: RTL=Yes (inherited from the form) already reverses the flow;
            // an explicit RightToLeft cancelled that reversal and rendered the bar left-to-right.
            toolbar.FlowDirection = FlowDirection.LeftToRight;
            toolbar.WrapContents = false;
            toolbar.Padding = new Padding(6, 8, 6, 4);

            Label lblEmployee = new Label();
            lblEmployee.Text = "الموظف:";
            lblEmployee.AutoSize = true;
            lblEmployee.Margin = new Padding(3, 10, 3, 0);

            _txtEmployee = new TextBox();
            _txtEmployee.Width = 150;
            _txtEmployee.Margin = new Padding(3, 10, 3, 0);

            _lblEmployeeName = new Label();
            _lblEmployeeName.Text = string.Empty;
            _lblEmployeeName.AutoSize = true;
            _lblEmployeeName.ForeColor = Theme.HeaderTextColor;
            _lblEmployeeName.Margin = new Padding(3, 10, 3, 0);

            Label lblYear = new Label();
            lblYear.Text = "السنة:";
            lblYear.AutoSize = true;
            lblYear.Margin = new Padding(6, 10, 3, 0);

            _numYear = new NumericUpDown();
            _numYear.Minimum = DateTime.Now.Year - 3;
            _numYear.Maximum = DateTime.Now.Year + 1;
            _numYear.Value = DateTime.Now.Year;
            _numYear.Width = 70;
            _numYear.Margin = new Padding(3, 8, 3, 0);

            Label lblMonth = new Label();
            lblMonth.Text = "الشهر:";
            lblMonth.AutoSize = true;
            lblMonth.Margin = new Padding(6, 10, 3, 0);

            _cboMonth = new ComboBox();
            _cboMonth.DropDownStyle = ComboBoxStyle.DropDownList;
            _cboMonth.Width = 110;
            _cboMonth.Margin = new Padding(3, 8, 3, 0);
            for (int month = 1; month <= 12; month++)
            {
                _cboMonth.Items.Add(ArabicMonth(month));
            }
            _cboMonth.SelectedIndex = DateTime.Now.Month - 1;

            _btnLoad = new Button();
            _btnLoad.Text = "عرض";
            _btnLoad.Height = 30;
            _btnLoad.Click += delegate { Reload(); };

            _btnSave = new Button();
            _btnSave.Text = "حفظ التعديلات";
            _btnSave.Height = 30;
            _btnSave.Click += delegate { OnSaveClick(); };

            toolbar.Controls.Add(lblEmployee);
            toolbar.Controls.Add(_txtEmployee);
            toolbar.Controls.Add(_lblEmployeeName);
            toolbar.Controls.Add(lblYear);
            toolbar.Controls.Add(_numYear);
            toolbar.Controls.Add(lblMonth);
            toolbar.Controls.Add(_cboMonth);
            toolbar.Controls.Add(_btnLoad);
            toolbar.Controls.Add(_btnSave);

            _lblLockState = new Label();
            _lblLockState.Text = string.Empty;
            _lblLockState.Dock = DockStyle.Top;
            _lblLockState.Height = 24;
            _lblLockState.TextAlign = ContentAlignment.MiddleCenter;

            _grid = new DataGridView();
            Theme.StyleGrid(_grid);
            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = false;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            DataGridViewTextBoxColumn colDay = new DataGridViewTextBoxColumn();
            colDay.Name = "Day";
            colDay.HeaderText = "اليوم";
            colDay.ReadOnly = true;
            colDay.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            colDay.FillWeight = 35;
            _grid.Columns.Add(colDay);

            DataGridViewTextBoxColumn colDate = new DataGridViewTextBoxColumn();
            colDate.Name = "Date";
            colDate.HeaderText = "التاريخ";
            colDate.ReadOnly = true;
            colDate.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            colDate.FillWeight = 55;
            _grid.Columns.Add(colDate);

            DataGridViewTextBoxColumn colWeekday = new DataGridViewTextBoxColumn();
            colWeekday.Name = "Weekday";
            colWeekday.HeaderText = "اسم اليوم";
            colWeekday.ReadOnly = true;
            colWeekday.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            colWeekday.FillWeight = 55;
            _grid.Columns.Add(colWeekday);

            DataGridViewComboBoxColumn colShift = new DataGridViewComboBoxColumn();
            colShift.Name = "Shift";
            colShift.HeaderText = "الوردية";
            colShift.ReadOnly = false;
            colShift.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            colShift.FillWeight = 100;
            colShift.DisplayStyle = DataGridViewComboBoxDisplayStyle.ComboBox;
            _grid.Columns.Add(colShift);

            _grid.DataError += delegate(object sender, DataGridViewDataErrorEventArgs args)
            {
                args.ThrowException = false;
            };

            Controls.Add(_grid);
            Controls.Add(_lblLockState);
            Controls.Add(toolbar);
            Controls.Add(header);

            _btnSave.Enabled = AppContext.HasPermission("Schedule.Edit");
        }

        /// <summary>Arabic month name for the toolbar combo.</summary>
        private static string ArabicMonth(int month)
        {
            string[] names = new string[] { "يناير", "فبراير", "مارس", "أبريل", "مايو", "يونيو",
                "يوليو", "أغسطس", "سبتمبر", "أكتوبر", "نوفمبر", "ديسمبر" };
            return names[month - 1];
        }

        /// <summary>Arabic weekday name for the grid.</summary>
        private static string DayNameAr(DayOfWeek day)
        {
            string[] names = new string[] { "الأحد", "الاثنين", "الثلاثاء", "الأربعاء", "الخميس", "الجمعة", "السبت" };
            return names[(int)day];
        }

        /// <summary>Resolves the free-text box to one employee (code, name or id).</summary>
        private bool ResolveEmployee(out string error)
        {
            string text = (_txtEmployee.Text ?? string.Empty).Trim();
            if (text.Length == 0)
            {
                error = "اكتب رقم الموظف أو اسمه أولاً.";
                return false;
            }
            EmployeeSearchFilter filter = new EmployeeSearchFilter();
            filter.Text = text;
            filter.IncludeArchived = true;
            filter.PageSize = 1;
            List<Employee> matches = EmployeeBLL.Search(filter);
            if (matches.Count == 0)
            {
                error = "لم يتم العثور على موظف مطابق.";
                return false;
            }
            _employee = matches[0];
            _lblEmployeeName.Text = _employee.EmployeeCode + " - " + _employee.FullNameAr;
            error = null;
            return true;
        }

        private void Reload()
        {
            string resolveError;
            if (!ResolveEmployee(out resolveError))
            {
                Theme.ShowInfo(resolveError);
                return;
            }
            try
            {
                _employee = EmployeeBLL.GetById(_employee.EmployeeId);
                if (_employee == null)
                {
                    Theme.ShowInfo("لم يتم العثور على الموظف.");
                    return;
                }
                int year = (int)_numYear.Value;
                int month = _cboMonth.SelectedIndex + 1;
                _month = ScheduleBLL.LoadMonth(year, month, null, null);

                // lock feedback mirroring FrmMonthlySchedule
                if (_month.IsLocked)
                {
                    _lblLockState.Text = "الشهر مقفل — التعديل متاح لمسؤول النظام فقط (يتسجل في سجل العمليات).";
                    _lblLockState.ForeColor = Color.FromArgb(160, 60, 20);
                }
                else
                {
                    _lblLockState.Text = "الشهر مفتوح للتعديل.";
                    _lblLockState.ForeColor = Color.FromArgb(20, 110, 60);
                }

                Dictionary<int, int> employeeCells;
                if (_month.Cells == null || !_month.Cells.TryGetValue(_employee.EmployeeId, out employeeCells))
                {
                    employeeCells = new Dictionary<int, int>();
                }

                // refresh the shift dropdown including a "غير مخصص" empty option
                List<Shift> shiftList = new List<Shift>();
                Shift empty = new Shift();
                empty.ShiftId = 0;
                empty.ShiftCode = string.Empty;
                empty.ShiftName = "غير مخصص";
                shiftList.Add(empty);
                foreach (KeyValuePair<int, Shift> pair in _month.Shifts)
                {
                    shiftList.Add(pair.Value);
                }

                DataGridViewComboBoxColumn colShift = (DataGridViewComboBoxColumn)_grid.Columns["Shift"];
                colShift.DataSource = shiftList;
                colShift.DisplayMember = "ShiftName";
                colShift.ValueMember = "ShiftId";

                _grid.Rows.Clear();
                int daysInMonth = DateTime.DaysInMonth(year, month);
                for (int day = 1; day <= daysInMonth; day++)
                {
                    int rowIndex = _grid.Rows.Add();
                    DataGridViewRow row = _grid.Rows[rowIndex];
                    DateTime date = new DateTime(year, month, day);
                    row.Cells["Day"].Value = day;
                    row.Cells["Date"].Value = date.ToString("yyyy-MM-dd");
                    row.Cells["Weekday"].Value = DayNameAr(date.DayOfWeek);
                    int shiftId;
                    row.Cells["Shift"].Value = employeeCells.TryGetValue(day, out shiftId) ?
                        (object)shiftId : (object)null;
                }
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmEmployeeSchedule.Reload");
                Theme.ShowError("حدث خطأ أثناء تحميل جدول الموظف.");
            }
        }

        private void OnSaveClick()
        {
            if (_month == null || _employee == null)
            {
                Theme.ShowInfo("اعرض جدول الموظف أولاً.");
                return;
            }
            int year = (int)_numYear.Value;
            int month = _cboMonth.SelectedIndex + 1;
            Dictionary<int, Dictionary<int, int>> cells = new Dictionary<int, Dictionary<int, int>>();
            Dictionary<int, int> days = new Dictionary<int, int>();
            int changed = 0;
            for (int i = 0; i < _grid.Rows.Count; i++)
            {
                DataGridViewRow row = _grid.Rows[i];
                object cellValue = row.Cells["Shift"].Value;
                if (cellValue == null || !(cellValue is int))
                {
                    continue;
                }
                int shiftId = (int)cellValue;
                if (shiftId == 0)
                {
                    continue;
                }
                int day = Convert.ToInt32(row.Cells["Day"].Value);
                int oldShiftId;
                if (!_month.Cells.ContainsKey(_employee.EmployeeId) ||
                    !_month.Cells[_employee.EmployeeId].TryGetValue(day, out oldShiftId) ||
                    oldShiftId != shiftId)
                {
                    changed++;
                }
                days[day] = shiftId;
            }
            if (changed == 0)
            {
                Theme.ShowInfo("لا يوجد أي تغيير على الورديات للحفظ.");
                return;
            }
            if (!Theme.Confirm("سيتم تعديل وردية " + changed + " يومًا للموظف '" + _lblEmployeeName.Text +
                "' في " + year + "/" + month + ". متابعة؟"))
            {
                return;
            }
            try
            {
                cells[_employee.EmployeeId] = days;
                ScheduleBLL.SaveMonth(year, month, new List<Employee> { _employee }, cells, _month.Shifts);
                Theme.ShowInfo("تم حفظ تعديلات الجدول.");
                Reload();
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmEmployeeSchedule.OnSaveClick");
                Theme.ShowError("حدث خطأ أثناء حفظ تعديلات الجدول.");
            }
        }
    }
}
