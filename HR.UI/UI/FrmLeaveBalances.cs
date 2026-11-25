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
    /// Leave balances viewer (rule 15) — two ledgers side by side:
    ///
    /// 1) Annual balances: quota-managed types (الاعتيادية/العارضة by default,
    ///    configurable via "Leave.BalanceTypes") showing this year's
    ///    quota/used/remaining, the carry-over pool of previous years
    ///    ("Leave.AllowCarryOver") and the total available. Quota editing
    ///    (needs "Leaves.Approve") goes through LeaveBLL.SaveEmployeeQuota so
    ///    UsedDays stays recomputed from approved requests.
    ///
    /// 2) Special leaves: all other types (المرضية، الأمومية، الحج، الدراسية…)
    ///    tracked by actual approved usage per year and over all years instead
    ///    of a pre-consumed fixed quota.
    /// </summary>
    public class FrmLeaveBalances : Form
    {
        private int _employeeId;
        private TextBox _txtEmployee;
        private Label _lblEmployeeName;
        private NumericUpDown _numYear;
        private Label _lblFiscalRange;
        private Button _btnLoad;
        private DataGridView _gridBalance;
        private DataGridView _gridSpecial;
        private List<LeaveBalanceView> _balanceRows;
        private List<LeaveTypeUsageView> _specialRows;
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
            int workingHeight = Screen.PrimaryScreen.WorkingArea.Height;
            ClientSize = new Size(720, Math.Min(620, workingHeight - 80));

            Label lblEmployee = new Label();
            lblEmployee.Text = "الموظف:";
            lblEmployee.Location = new Point(20, 22);
            lblEmployee.Size = new Size(130, 22);

            _txtEmployee = new TextBox();
            _txtEmployee.Location = new Point(155, 18);
            _txtEmployee.Size = new Size(180, 26);

            _lblEmployeeName = new Label();
            _lblEmployeeName.Location = new Point(345, 22);
            _lblEmployeeName.Size = new Size(340, 22);
            _lblEmployeeName.ForeColor = Theme.HeaderTextColor;

            Label lblYear = new Label();
            lblYear.Text = "السنة المالية:";
            lblYear.Location = new Point(20, 57);
            lblYear.Size = new Size(130, 22);

            _numYear = new NumericUpDown();
            _numYear.Location = new Point(155, 53);
            _numYear.Size = new Size(100, 26);
            _numYear.Minimum = 2000;
            _numYear.Maximum = 2100;
            _numYear.Value = LeaveBLL.BalanceYearFor(DateTime.Now);

            _btnLoad = new Button();
            _btnLoad.Text = "عرض الأرصدة";
            _btnLoad.Location = new Point(270, 51);
            _btnLoad.Size = new Size(130, 30);
            _btnLoad.Click += delegate { LoadBalances(); };

            _lblFiscalRange = new Label();
            _lblFiscalRange.Text = string.Empty;
            _lblFiscalRange.Location = new Point(415, 57);
            _lblFiscalRange.Size = new Size(330, 22);
            _lblFiscalRange.ForeColor = Theme.HeaderTextColor;

            // ---------- annual balances grid ----------
            Label lblBalanceTitle = new Label();
            lblBalanceTitle.Text = "الأرصدة السنوية — حصص الإجازات الاعتيادية والعارضة + المرحّل من أعوام سابقة:";
            lblBalanceTitle.Location = new Point(20, 95);
            lblBalanceTitle.Size = new Size(660, 22);
            lblBalanceTitle.ForeColor = Theme.HeaderTextColor;

            _gridBalance = new DataGridView();
            StyleGrid(_gridBalance);
            _gridBalance.Location = new Point(20, 120);
            _gridBalance.Size = new Size(675, 160);
            _gridBalance.SelectionChanged += delegate { OnSelectionChanged(); };

            // ---------- special usage grid ----------
            Label lblSpecialTitle = new Label();
            lblSpecialTitle.Text = "الإجازات من نوع خاص — تُرصد بالاستخدام الفعلي (مرضية، أمومية، حج، دراسية، رعاية طفل…):";
            lblSpecialTitle.Location = new Point(20, 292);
            lblSpecialTitle.Size = new Size(660, 22);
            lblSpecialTitle.ForeColor = Theme.HeaderTextColor;

            _gridSpecial = new DataGridView();
            StyleGrid(_gridSpecial);
            _gridSpecial.Location = new Point(20, 317);
            _gridSpecial.Size = new Size(675, 160);

            // ---------- quota editor (annual grid only) ----------
            _lblQuota = new Label();
            _lblQuota.Text = "الحصة للنوع المحدد:";
            _lblQuota.Location = new Point(20, 492);
            _lblQuota.Size = new Size(130, 22);

            _numQuota = new NumericUpDown();
            _numQuota.Location = new Point(155, 488);
            _numQuota.Size = new Size(90, 26);
            _numQuota.Minimum = 0;
            _numQuota.Maximum = 365;

            _btnSaveQuota = new Button();
            _btnSaveQuota.Text = "حفظ الحصة";
            _btnSaveQuota.Location = new Point(260, 486);
            _btnSaveQuota.Size = new Size(110, 30);
            _btnSaveQuota.Click += delegate { OnSaveQuotaClick(); };

            Button btnClose = new Button();
            btnClose.Text = "إغلاق";
            btnClose.Location = new Point(580, 555);
            btnClose.Size = new Size(110, 32);
            btnClose.DialogResult = DialogResult.Cancel;

            Controls.AddRange(new Control[]
            {
                lblEmployee, _txtEmployee, _lblEmployeeName, lblYear, _numYear, _btnLoad, _lblFiscalRange,
                lblBalanceTitle, _gridBalance, lblSpecialTitle, _gridSpecial,
                _lblQuota, _numQuota, _btnSaveQuota, btnClose
            });

            CancelButton = btnClose;
            _btnSaveQuota.Enabled = AppContext.HasAnyPermission(new string[] { "Leaves.Approve", "Leaves.Balances" });

            _gridBalance.Columns.Add("TypeName", "النوع");
            _gridBalance.Columns.Add("TypeCode", "الكود");
            _gridBalance.Columns.Add("QuotaDays", "حصة السنة المالية");
            _gridBalance.Columns.Add("UsedDays", "المستهلك");
            _gridBalance.Columns.Add("YearRemaining", "متبقي السنة المالية");
            _gridBalance.Columns.Add("CarryOverDays", "منقول من أعوام سابقة");
            _gridBalance.Columns.Add("TotalAvailable", "الإجمالي المتاح");

            _gridSpecial.Columns.Add("TypeName", "النوع");
            _gridSpecial.Columns.Add("TypeCode", "الكود");
            _gridSpecial.Columns.Add("ApprovedRequests", "عدد الطلبات المعتمدة");
            _gridSpecial.Columns.Add("UsedDays", "المستهلك في السنة المالية");
            _gridSpecial.Columns.Add("LifetimeDays", "الاستهلاك الكلي (كل السنوات)");
            _gridSpecial.Columns.Add("AnnualDays", "الحد الاسترشادي");
            _gridSpecial.Columns.Add("RequiresDoc", "متطلب مستند");

            if (workingHeight < 700)
            {
                // short screens: keep both grids reachable by scrolling
                AutoScroll = true;
            }

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

        private static void StyleGrid(DataGridView grid)
        {
            Theme.StyleGrid(grid);
            grid.ReadOnly = true;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
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
                _gridBalance.Rows.Clear();
                _gridSpecial.Rows.Clear();
                _balanceRows = null;
                _specialRows = null;
                if (_employeeId <= 0)
                {
                    return;
                }
                int year = (int)_numYear.Value;
                _lblFiscalRange.Text = "الفترة: من " + LeaveBLL.FiscalStart(year).ToString("yyyy-MM-dd") +
                    " إلى " + LeaveBLL.FiscalStart(year + 1).AddDays(-1).ToString("yyyy-MM-dd");
                _balanceRows = LeaveBLL.BalanceGrid(_employeeId, year);
                foreach (LeaveBalanceView view in _balanceRows)
                {
                    int rowIndex = _gridBalance.Rows.Add(view.TypeName, view.TypeCode,
                        view.QuotaDays, view.UsedDays, view.YearRemaining,
                        view.CarryOverDays, view.TotalAvailable);
                    _gridBalance.Rows[rowIndex].Tag = view;
                }
                _specialRows = LeaveBLL.SpecialUsage(_employeeId, year);
                foreach (LeaveTypeUsageView view in _specialRows)
                {
                    int rowIndex = _gridSpecial.Rows.Add(view.TypeName, view.TypeCode,
                        view.ApprovedRequests, view.UsedDays, view.LifetimeDays,
                        view.AnnualDays == null ? "غير محدد" : view.AnnualDays.Value.ToString(),
                        view.RequiresDoc ? "نعم" : "لا");
                    _gridSpecial.Rows[rowIndex].Tag = view;
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

        private void OnSelectionChanged()
        {
            if (_gridBalance.CurrentRow == null || _balanceRows == null ||
                _gridBalance.CurrentRow.Index >= _balanceRows.Count)
            {
                return;
            }
            LeaveBalanceView view = _balanceRows[_gridBalance.CurrentRow.Index];
            _numQuota.Value = Math.Max(_numQuota.Minimum,
                Math.Min(_numQuota.Maximum, view.QuotaDays));
        }

        private void OnSaveQuotaClick()
        {
            if (_gridBalance.CurrentRow == null || _balanceRows == null ||
                _gridBalance.CurrentRow.Index >= _balanceRows.Count)
            {
                Theme.ShowInfo("اختر نوعاً من جدول الأرصدة السنوية أولاً.");
                return;
            }
            LeaveBalanceView view = _balanceRows[_gridBalance.CurrentRow.Index];
            try
            {
                LeaveBLL.SaveEmployeeQuota(_employeeId, (int)_numYear.Value,
                    view.LeaveTypeId, (int)_numQuota.Value);
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
