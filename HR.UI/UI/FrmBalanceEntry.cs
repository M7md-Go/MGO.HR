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
    /// Batch balance-entry window (order aa2c7e45, requirement 3). Authorized
    /// staff (any of "Leaves.Balances" / "Leaves.Approve") picks a fiscal year
    /// and a leave type, enters the quota (fractions allowed: 0.5 steps), loads
    /// the employee list, then ticks employees individually, in ranges through
    /// "تحديد الكل" or a filtered subset, and saves the whole batch with one
    /// click. Every quota still goes through LeaveBLL.SaveEmployeeQuota so the
    /// values land in LeaveBalances and are audit-logged row by row.
    /// </summary>
    public class FrmBalanceEntry : Form
    {
        private NumericUpDown _numYear;
        private ComboBox _cboType;
        private NumericUpDown _numQuota;
        private TextBox _txtFilter;
        private Button _btnLoad;
        private Button _btnCheckAll;
        private Button _btnClearAll;
        private Button _btnSave;
        private DataGridView _grid;
        private List<LeaveType> _types;
        private List<Employee> _rows;

        public FrmBalanceEntry()
        {
            Theme.Apply(this, false); // MDI child: mirrored layout is not applied — RTL flow comes from the containers
            Text = "إدخال أرصدة الموظفين (فردي أو جماعي)";
            ClientSize = new Size(900, 580);
            StartPosition = FormStartPosition.CenterParent;

            Panel header = new Panel();
            Label lblTitle = new Label();
            lblTitle.Text = "إدخال أرصدة الإجازات الاعتيادية والعارضة — تتجدد مع بداية كل عام مالي، ويُدخلها موظف مختص لكل موظف أو مجموعة دفعة واحدة";
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

            Label lblYear = new Label();
            lblYear.Text = "العام المالي:";
            lblYear.AutoSize = true;
            lblYear.Margin = new Padding(3, 10, 3, 0);

            _numYear = new NumericUpDown();
            _numYear.Minimum = 2000;
            _numYear.Maximum = 2100;
            _numYear.Width = 70;
            _numYear.Value = LeaveBLL.BalanceYearFor(DateTime.Now);
            _numYear.Margin = new Padding(3, 8, 3, 0);

            Label lblType = new Label();
            lblType.Text = "نوع الإجازة:";
            lblType.AutoSize = true;
            lblType.Margin = new Padding(6, 10, 3, 0);

            _cboType = new ComboBox();
            _cboType.DropDownStyle = ComboBoxStyle.DropDownList;
            _cboType.Width = 170;
            _cboType.Margin = new Padding(3, 8, 3, 0);

            Label lblQuota = new Label();
            lblQuota.Text = "الرصيد:";
            lblQuota.AutoSize = true;
            lblQuota.Margin = new Padding(6, 10, 3, 0);

            _numQuota = new NumericUpDown();
            _numQuota.DecimalPlaces = 1;
            _numQuota.Increment = 0.5m;
            _numQuota.Minimum = 0;
            _numQuota.Maximum = 365;
            _numQuota.Width = 80;
            _numQuota.Margin = new Padding(3, 8, 3, 0);

            _txtFilter = new TextBox();
            _txtFilter.Width = 150;
            _txtFilter.Margin = new Padding(6, 10, 3, 0);

            _btnLoad = new Button();
            _btnLoad.Text = "تحميل الموظفين";
            _btnLoad.Height = 30;
            _btnLoad.Click += delegate { LoadEmployees(); };

            _btnCheckAll = new Button();
            _btnCheckAll.Text = "تحديد الكل";
            _btnCheckAll.Height = 30;
            _btnCheckAll.Click += delegate { SetAllChecks(true); };

            _btnClearAll = new Button();
            _btnClearAll.Text = "إلغاء التحديد";
            _btnClearAll.Height = 30;
            _btnClearAll.Click += delegate { SetAllChecks(false); };

            _btnSave = new Button();
            _btnSave.Text = "حفظ الدفعة";
            _btnSave.Height = 30;
            _btnSave.Click += delegate { OnSaveClick(); };

            toolbar.Controls.Add(lblYear);
            toolbar.Controls.Add(_numYear);
            toolbar.Controls.Add(lblType);
            toolbar.Controls.Add(_cboType);
            toolbar.Controls.Add(lblQuota);
            toolbar.Controls.Add(_numQuota);
            toolbar.Controls.Add(_txtFilter);
            toolbar.Controls.Add(_btnLoad);
            toolbar.Controls.Add(_btnCheckAll);
            toolbar.Controls.Add(_btnClearAll);
            toolbar.Controls.Add(_btnSave);

            _grid = new DataGridView();
            Theme.StyleGrid(_grid);
            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = false;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _grid.CellDoubleClick += delegate {
                DataGridViewCell cell = _grid.CurrentCell;
                if (cell != null && cell.OwningColumn != null && cell.OwningColumn.Name == "Send")
                {
                    cell.Value = (cell.Value is bool && (bool)cell.Value) ? false : (object)true;
                }
            };

            DataGridViewCheckBoxColumn colSend = new DataGridViewCheckBoxColumn();
            colSend.Name = "Send";
            colSend.HeaderText = "إرسال";
            colSend.ReadOnly = false;
            colSend.Width = 55;
            _grid.Columns.Add(colSend);
            AddTextColumn("Code", "الرقم الوظيفي", 90);
            AddTextColumn("Name", "اسم الموظف", 220);
            AddTextColumn("Dept", "القسم", 140);
            AddTextColumn("Status", "الحالة", 90);

            Controls.Add(_grid);
            Controls.Add(toolbar);
            Controls.Add(header);

            _cboType.SelectedIndexChanged += delegate { OnTypeChanged(); };
            _types = LeaveBLL.Types();
            foreach (LeaveType type in _types)
            {
                _cboType.Items.Add(type);
            }
            _cboType.DisplayMember = "TypeName";

            bool canSave = AppContext.HasAnyPermission(new string[] { "Leaves.Balances", "Leaves.Approve" });
            _btnSave.Enabled = canSave;
            _btnCheckAll.Enabled = canSave;
            _btnClearAll.Enabled = canSave;
            if (_cboType.Items.Count > 0)
            {
                _cboType.SelectedIndex = 0;
            }
        }

        private void AddTextColumn(string name, string header, int width)
        {
            DataGridViewTextBoxColumn column = new DataGridViewTextBoxColumn();
            column.Name = name;
            column.HeaderText = header;
            column.ReadOnly = true;
            column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            column.FillWeight = width;
            _grid.Columns.Add(column);
        }

        private LeaveType SelectedType()
        {
            return _cboType.SelectedItem as LeaveType;
        }

        private void OnTypeChanged()
        {
            LeaveType type = SelectedType();
            if (type != null && type.AnnualDays != null)
            {
                decimal quota = type.AnnualDays.Value;
                if (quota < _numQuota.Minimum)
                {
                    quota = _numQuota.Minimum;
                }
                if (quota > _numQuota.Maximum)
                {
                    quota = _numQuota.Maximum;
                }
                _numQuota.Value = quota;
            }
        }

        private void LoadEmployees()
        {
            try
            {
                EmployeeSearchFilter filter = new EmployeeSearchFilter();
                filter.Text = (_txtFilter.Text ?? string.Empty).Trim();
                filter.IncludeArchived = false;
                filter.PageSize = 10000;
                _rows = EmployeeBLL.Search(filter);

                Dictionary<int, string> deptNames = new Dictionary<int, string>();
                foreach (Department department in OrganizationBLL.Departments(false))
                {
                    deptNames[department.DepartmentId] = department.DepartmentName;
                }

                _grid.Rows.Clear();
                foreach (Employee employee in _rows)
                {
                    string dept;
                    if (employee.DepartmentId == null || !deptNames.TryGetValue(employee.DepartmentId.Value, out dept))
                    {
                        dept = "—";
                    }
                    _grid.Rows.Add(false, employee.EmployeeCode,
                        employee.FullNameAr, dept, employee.Status);
                }
                if (_rows.Count == 0)
                {
                    Theme.ShowInfo("لا يوجد موظفون مطابقون لبحثك.");
                }
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmBalanceEntry.LoadEmployees");
                Theme.ShowError("حدث خطأ أثناء تحميل الموظفين.");
            }
        }

        private void SetAllChecks(bool isChecked)
        {
            foreach (DataGridViewRow row in _grid.Rows)
            {
                row.Cells["Send"].Value = isChecked;
            }
        }

        private void OnSaveClick()
        {
            LeaveType type = SelectedType();
            if (type == null)
            {
                Theme.ShowInfo("اختر نوع الإجازة أولاً.");
                return;
            }
            if (_rows == null || _rows.Count == 0)
            {
                Theme.ShowInfo("حمّل الموظفين أولاً.");
                return;
            }
            List<int> selectedIds = new List<int>();
            for (int i = 0; i < _grid.Rows.Count && i < _rows.Count; i++)
            {
                object value = _grid.Rows[i].Cells["Send"].Value;
                if (value is bool && (bool)value)
                {
                    selectedIds.Add(_rows[i].EmployeeId);
                }
            }
            if (selectedIds.Count == 0)
            {
                Theme.ShowInfo("حدّد موظفاً واحداً على الأقل (عمود إرسال).");
                return;
            }
            if (!Theme.Confirm("سيتم حفظ الرصيد " + _numQuota.Value + " يومًا للنوع '" + type.TypeName +
                "' لعدد " + selectedIds.Count + " موظفًا عن العام المالي " + (int)_numYear.Value + ". متابعة؟"))
            {
                return;
            }
            int year = (int)_numYear.Value;
            decimal quota = _numQuota.Value;
            int saved = 0;
            int failed = 0;
            string firstError = null;
            foreach (int employeeId in selectedIds)
            {
                try
                {
                    LeaveBLL.SaveEmployeeQuota(employeeId, year, type.LeaveTypeId, quota);
                    saved++;
                }
                catch (Exception error)
                {
                    failed++;
                    if (!(error is BusinessException))
                    {
                        Logger.LogError(error, "FrmBalanceEntry.OnSaveClick");
                    }
                    if (firstError == null)
                    {
                        firstError = error.Message;
                    }
                }
            }
            if (failed == 0)
            {
                Theme.ShowInfo("تم حفظ الرصيد لعدد " + saved + " موظفًا بنجاح.");
            }
            else
            {
                Theme.ShowInfo("تم الحفظ لـ " + saved + " موظفًا، وفشل " + failed + " موظفًا.\n" +
                    (firstError ?? string.Empty));
            }
        }
    }
}
