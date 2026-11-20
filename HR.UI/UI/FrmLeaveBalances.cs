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
    /// Leave balances viewer (rule 15): all leave types with quota / used /
    /// remaining for one employee and year. Quota editing (needs
    /// "Leaves.Approve") goes through LeaveBLL.SaveEmployeeQuota so UsedDays
    /// stays recomputed from approved requests.
    /// </summary>
    public class FrmLeaveBalances : Form
    {
        private int _employeeId;
        private TextBox _txtEmployee;
        private Label _lblEmployeeName;
        private NumericUpDown _numYear;
        private Button _btnLoad;
        private DataGridView _grid;
        private List<LeaveBalance> _rows;
        private Label _lblQuota;
        private NumericUpDown _numQuota;
        private Button _btnSaveQuota;

        public FrmLeaveBalances(int? employeeId)
        {
            _employeeId = employeeId ?? 0;
            Theme.Apply(this);
            Text = "أرصدة الإجازات";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(560, 480);

            Label lblEmployee = new Label();
            lblEmployee.Text = "الموظف:";
            lblEmployee.Location = new Point(20, 22);
            lblEmployee.Size = new Size(130, 22);

            _txtEmployee = new TextBox();
            _txtEmployee.Location = new Point(155, 18);
            _txtEmployee.Size = new Size(180, 26);

            _lblEmployeeName = new Label();
            _lblEmployeeName.Location = new Point(345, 22);
            _lblEmployeeName.Size = new Size(190, 22);
            _lblEmployeeName.ForeColor = Theme.HeaderTextColor;

            Label lblYear = new Label();
            lblYear.Text = "السنة:";
            lblYear.Location = new Point(20, 57);
            lblYear.Size = new Size(130, 22);

            _numYear = new NumericUpDown();
            _numYear.Location = new Point(155, 53);
            _numYear.Size = new Size(100, 26);
            _numYear.Minimum = 2000;
            _numYear.Maximum = 2100;
            _numYear.Value = DateTime.Now.Year;

            _btnLoad = new Button();
            _btnLoad.Text = "عرض الأرصدة";
            _btnLoad.Location = new Point(270, 51);
            _btnLoad.Size = new Size(130, 30);
            _btnLoad.Click += delegate { LoadBalances(); };

            _grid = new DataGridView();
            Theme.StyleGrid(_grid);
            _grid.Location = new Point(20, 95);
            _grid.Size = new Size(515, 250);
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _grid.SelectionChanged += delegate { OnSelectionChanged(); };

            _lblQuota = new Label();
            _lblQuota.Text = "الحصة للنوع المحدد:";
            _lblQuota.Location = new Point(20, 365);
            _lblQuota.Size = new Size(130, 22);

            _numQuota = new NumericUpDown();
            _numQuota.Location = new Point(155, 361);
            _numQuota.Size = new Size(90, 26);
            _numQuota.Minimum = 0;
            _numQuota.Maximum = 365;

            _btnSaveQuota = new Button();
            _btnSaveQuota.Text = "حفظ الحصة";
            _btnSaveQuota.Location = new Point(260, 359);
            _btnSaveQuota.Size = new Size(110, 30);
            _btnSaveQuota.Click += delegate { OnSaveQuotaClick(); };

            Button btnClose = new Button();
            btnClose.Text = "إغلاق";
            btnClose.Location = new Point(425, 425);
            btnClose.Size = new Size(110, 32);
            btnClose.DialogResult = DialogResult.Cancel;

            Controls.Add(lblEmployee);
            Controls.Add(_txtEmployee);
            Controls.Add(_lblEmployeeName);
            Controls.Add(lblYear);
            Controls.Add(_numYear);
            Controls.Add(_btnLoad);
            Controls.Add(_grid);
            Controls.Add(_lblQuota);
            Controls.Add(_numQuota);
            Controls.Add(_btnSaveQuota);
            Controls.Add(btnClose);

            CancelButton = btnClose;
            _btnSaveQuota.Enabled = AppContext.HasPermission("Leaves.Approve");

            _grid.Columns.Add("TypeName", "النوع");
            _grid.Columns.Add("TypeNameCode", "الكود");
            _grid.Columns.Add("QuotaDays", "الحصة");
            _grid.Columns.Add("UsedDays", "المستهلك");
            _grid.Columns.Add("Remaining", "المتبقي");

            if (_employeeId > 0)
            {
                EmployeeSearchFilter filter = new EmployeeSearchFilter();
                List<Employee> matches = new List<Employee>();
                try
                {
                    filter.PageSize = 1;
                    filter.IncludeArchived = true;
                    filter.Text = _employeeId.ToString();
                    matches = EmployeeBLL.Search(filter);
                }
                catch (Exception unexpected)
                {
                    Logger.LogError(unexpected, "FrmLeaveBalances.resolve");
                }
                if (matches.Count > 0)
                {
                    _txtEmployee.Text = matches[0].EmployeeCode;
                    _lblEmployeeName.Text = matches[0].FullNameAr;
                }
                LoadBalances();
            }
        }

        private Employee ResolveEmployee()
        {
            string text = (_txtEmployee.Text ?? string.Empty).Trim();
            if (text.Length == 0)
            {
                return null;
            }
            EmployeeSearchFilter filter = new EmployeeSearchFilter();
            filter.Text = text;
            filter.IncludeArchived = true;
            filter.PageSize = 1;
            List<Employee> matches = EmployeeBLL.Search(filter);
            return matches.Count > 0 ? matches[0] : null;
        }

        private void LoadBalances()
        {
            try
            {
                if (_txtEmployee.Text.Trim().Length > 0)
                {
                    Employee employee = ResolveEmployee();
                    if (employee != null)
                    {
                        _employeeId = employee.EmployeeId;
                        _lblEmployeeName.Text = employee.FullNameAr;
                    }
                    else
                    {
                        _employeeId = 0;
                        _lblEmployeeName.Text = "لم يتم العثور على الموظف";
                    }
                }
                _grid.Rows.Clear();
                _rows = null;
                if (_employeeId <= 0)
                {
                    return;
                }
                _rows = LeaveBLL.Balances(_employeeId, (int)_numYear.Value);
                Dictionary<int, string> names = LoadTypeNames();
                foreach (LeaveBalance balance in _rows)
                {
                    string typeName = "";
                    string typeCode = "";
                    if (names.ContainsKey(balance.LeaveTypeId ?? 0))
                    {
                        string combined = names[balance.LeaveTypeId ?? 0];
                        int open = combined.LastIndexOf(" (");
                        if (open > 0)
                        {
                            typeName = combined.Substring(0, open);
                            typeCode = combined.Substring(open + 2).TrimEnd(')');
                        }
                        else
                        {
                            typeName = combined;
                        }
                    }
                    _grid.Rows.Add(typeName, typeCode, balance.QuotaDays ?? 0,
                        balance.UsedDays ?? 0, balance.Remaining ?? 0);
                }
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmLeaveBalances.LoadBalances");
                Theme.ShowError("حدث خطأ أثناء تحميل الأرصدة.");
            }
        }

        private Dictionary<int, string> LoadTypeNames()
        {
            Dictionary<int, string> names = new Dictionary<int, string>();
            foreach (LeaveType type in LeaveBLL.Types())
            {
                names[type.LeaveTypeId] = type.TypeName + " (" + type.LeaveTypeCode + ")";
            }
            return names;
        }

        private void OnSelectionChanged()
        {
            if (_grid.CurrentRow == null || _rows == null ||
                _grid.CurrentRow.Index >= _rows.Count)
            {
                return;
            }
            LeaveBalance balance = _rows[_grid.CurrentRow.Index];
            _numQuota.Value = balance.QuotaDays ?? 0;
        }

        private void OnSaveQuotaClick()
        {
            if (_grid.CurrentRow == null || _rows == null ||
                _grid.CurrentRow.Index >= _rows.Count)
            {
                Theme.ShowInfo("اختر نوعًا من الجدول أولًا.");
                return;
            }
            LeaveBalance balance = _rows[_grid.CurrentRow.Index];
            try
            {
                LeaveBLL.SaveEmployeeQuota(_employeeId, (int)_numYear.Value,
                    balance.LeaveTypeId ?? 0, (int)_numQuota.Value);
                Theme.ShowInfo("تم حفظ الحصة.");
                LoadBalances();
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmLeaveBalances.OnSaveQuotaClick");
                Theme.ShowError("حدث خطأ أثناء حفظ الحصة.");
            }
        }
    }
}
