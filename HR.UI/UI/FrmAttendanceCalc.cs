using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using HR.BLL;
using HR.DAL;
using HR.Entities;
using HR.Helpers;

namespace HR.UI.UI
{
    /// <summary>
    /// Attendance engine screen (rule 10): runs a recalculation pass over a
    /// date window (permission "Attendance.Edit", confirmed first, one
    /// transaction) and browses the resulting daily attendance rows. The
    /// calculation itself lives in HR.Services/AttendanceEngine and
    /// HR.BLL/AttendanceBLL — this form never computes and never queries.
    /// </summary>
    public class FrmAttendanceCalc : Form
    {
        private DateTimePicker _dtpFrom;
        private DateTimePicker _dtpTo;
        private Button _btnRun;
        private Button _btnBrowse;
        private TextBox _txtEmployee;
        private ComboBox _cboDepartment;
        private Label _lblRunResult;
        private DataGridView _grid;

        public FrmAttendanceCalc()
        {
            Theme.Apply(this);
            Text = "محرك الحضور";
            ClientSize = new Size(1050, 620);
            StartPosition = FormStartPosition.CenterParent;

            Panel header = new Panel();
            Label lblTitle = new Label();
            lblTitle.Text = "محرك حساب الحضور — إعادة احتساب الفترات من الجدول والبصمات";
            lblTitle.ForeColor = Theme.HeaderTextColor;
            lblTitle.Font = Theme.Font(12f);
            lblTitle.Dock = DockStyle.Fill;
            lblTitle.TextAlign = ContentAlignment.MiddleCenter;
            Theme.StyleHeader(header);
            header.Controls.Add(lblTitle);
            header.Dock = DockStyle.Top;

            FlowLayoutPanel runBar = new FlowLayoutPanel();
            runBar.Dock = DockStyle.Top;
            runBar.Height = 46;
            runBar.BackColor = Theme.ToolbarColor;
            runBar.FlowDirection = FlowDirection.RightToLeft;
            runBar.WrapContents = false;
            runBar.Padding = new Padding(6, 8, 6, 4);

            Label lblFrom = new Label();
            lblFrom.Text = "من:";
            lblFrom.AutoSize = true;
            lblFrom.Margin = new Padding(3, 10, 3, 0);

            _dtpFrom = new DateTimePicker();
            _dtpFrom.Format = DateTimePickerFormat.Short;
            _dtpFrom.Width = 110;
            _dtpFrom.Margin = new Padding(1, 6, 1, 0);

            Label lblTo = new Label();
            lblTo.Text = "إلى:";
            lblTo.AutoSize = true;
            lblTo.Margin = new Padding(10, 10, 3, 0);

            _dtpTo = new DateTimePicker();
            _dtpTo.Format = DateTimePickerFormat.Short;
            _dtpTo.Width = 110;
            _dtpTo.Margin = new Padding(1, 6, 1, 0);

            _btnRun = new Button();
            _btnRun.Text = "إعادة احتساب الفترة";
            _btnRun.Height = 28;
            _btnRun.Margin = new Padding(10, 5, 3, 0);
            _btnRun.Click += delegate { OnRunClick(); };

            _lblRunResult = new Label();
            _lblRunResult.Text = "";
            _lblRunResult.AutoSize = true;
            _lblRunResult.ForeColor = Theme.HeaderTextColor;
            _lblRunResult.Margin = new Padding(14, 10, 3, 0);

            runBar.Controls.Add(lblFrom);
            runBar.Controls.Add(_dtpFrom);
            runBar.Controls.Add(lblTo);
            runBar.Controls.Add(_dtpTo);
            runBar.Controls.Add(_btnRun);
            runBar.Controls.Add(_lblRunResult);

            FlowLayoutPanel browseBar = new FlowLayoutPanel();
            browseBar.Dock = DockStyle.Top;
            browseBar.Height = 46;
            browseBar.BackColor = Theme.ToolbarColor;
            browseBar.FlowDirection = FlowDirection.RightToLeft;
            browseBar.WrapContents = false;
            browseBar.Padding = new Padding(6, 4, 6, 4);

            _btnBrowse = new Button();
            _btnBrowse.Text = "عرض النتائج";
            _btnBrowse.Height = 28;
            _btnBrowse.Margin = new Padding(3, 5, 3, 0);
            _btnBrowse.Click += delegate { OnBrowseClick(); };

            Label lblEmployee = new Label();
            lblEmployee.Text = "كود/اسم الموظف:";
            lblEmployee.AutoSize = true;
            lblEmployee.Margin = new Padding(12, 10, 3, 0);

            _txtEmployee = new TextBox();
            _txtEmployee.Width = 170;
            _txtEmployee.Margin = new Padding(1, 6, 1, 0);

            Label lblDepartment = new Label();
            lblDepartment.Text = "القسم:";
            lblDepartment.AutoSize = true;
            lblDepartment.Margin = new Padding(12, 10, 3, 0);

            _cboDepartment = new ComboBox();
            _cboDepartment.DropDownStyle = ComboBoxStyle.DropDownList;
            _cboDepartment.Width = 160;
            _cboDepartment.Margin = new Padding(1, 6, 1, 0);
            FillDepartments();

            browseBar.Controls.Add(_btnBrowse);
            browseBar.Controls.Add(lblEmployee);
            browseBar.Controls.Add(_txtEmployee);
            browseBar.Controls.Add(lblDepartment);
            browseBar.Controls.Add(_cboDepartment);

            _grid = new DataGridView();
            Theme.StyleGrid(_grid);
            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            Controls.Add(_grid);
            Controls.Add(browseBar);
            Controls.Add(runBar);
            Controls.Add(header);

            DateTime today = DateTime.Today;
            _dtpFrom.Value = new DateTime(today.Year, today.Month, 1);
            _dtpTo.Value = today;
            _btnRun.Enabled = AppContext.HasPermission("Attendance.Edit");
            OnBrowseClick();
        }

        private void FillDepartments()
        {
            Dictionary<int, string> departments = new Dictionary<int, string>();
            departments[0] = "كل الأقسام";
            foreach (LookupRow row in OrganizationBLL.Rows(LookupTable.Departments, true))
            {
                departments[row.Id] = row.Name;
            }
            _cboDepartment.DataSource = new BindingSource(departments, null);
            _cboDepartment.DisplayMember = "Value";
            _cboDepartment.ValueMember = "Key";
        }

        private void OnRunClick()
        {
            try
            {
                DateTime fromDate = _dtpFrom.Value.Date;
                DateTime toDate = _dtpTo.Value.Date;
                if (!Theme.Confirm("إعادة احتساب الحضور من " + fromDate.ToString("yyyy-MM-dd") +
                    " إلى " + toDate.ToString("yyyy-MM-dd") + "؟\n" +
                    "تُستبدل نتائج المحرك السابقة داخل الفترة، أما الصفوف المحرَّرة يدوياً فلا تُمس، " +
                    "وتُكتب كل النتائج في معاملة واحدة."))
                {
                    return;
                }
                UseWaitCursor = true;
                RecalcSummary summary;
                try
                {
                    summary = AttendanceBLL.Recalculate(fromDate, toDate);
                }
                finally
                {
                    UseWaitCursor = false;
                }
                _lblRunResult.Text = "آخر تشغيل: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                Theme.ShowInfo(
                    "اكتملت إعادة الاحتساب:\n" +
                    "— أيام الموظفين المعالَجة: " + summary.DaysProcessed + "\n" +
                    "— أيام تخُطتها تصحيحات يدوية: " + summary.DaysSkippedManual + "\n" +
                    "— صفوف حضور مكتوبة: " + summary.AttendanceRows + "\n" +
                    "— حالات مرفوعة: " + summary.Exceptions + "\n" +
                    "— سجلات ساعات إضافية: " + summary.OvertimeRows + "\n" +
                    "— المدة: " + summary.DurationMs + " ms");
                OnBrowseClick();
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmAttendanceCalc.OnRunClick");
                Theme.ShowError("حدث خطأ أثناء إعادة احتساب الحضور.");
            }
        }

        private void OnBrowseClick()
        {
            try
            {
                DateTime fromDate = _dtpFrom.Value.Date;
                DateTime toDate = _dtpTo.Value.Date;
                int? employeeId = ResolveEmployeeFilter();
                int selectedDepartment = (int)_cboDepartment.SelectedValue;
                int? departmentId = selectedDepartment == 0 ? (int?)null : selectedDepartment;
                List<AttendanceView> rows = AttendanceBLL.Browse(fromDate, toDate, employeeId, departmentId);

                _grid.Columns.Clear();
                _grid.Rows.Clear();
                AddColumn("EmployeeCode", "كود الموظف", 70);
                AddColumn("EmployeeName", "اسم الموظف", 160);
                AddColumn("AttDate", "التاريخ", 90);
                AddColumn("ShiftCode", "الوردية", 60);
                AddColumn("StartTime", "الحضور الفعلي", 110);
                AddColumn("EndTime", "الانصراف الفعلي", 110);
                AddColumn("Status", "الحالة", 90);
                AddColumn("MinutesWorked", "دقائق العمل", 80);
                AddColumn("OvertimeMin", "دقائق إضافية", 80);
                AddColumn("IsManuallyChg", "تصحيح يدوي", 80);

                int lateCount = 0;
                int absentCount = 0;
                foreach (AttendanceView row in rows)
                {
                    int rowIndex = _grid.Rows.Add();
                    DataGridViewRow gridRow = _grid.Rows[rowIndex];
                    gridRow.Cells["EmployeeCode"].Value = row.EmployeeCode;
                    gridRow.Cells["EmployeeName"].Value = row.EmployeeName;
                    gridRow.Cells["AttDate"].Value = row.AttDate == null ? null :
                        (object)row.AttDate.Value.ToString("yyyy-MM-dd");
                    gridRow.Cells["ShiftCode"].Value = row.ShiftCode;
                    gridRow.Cells["StartTime"].Value = row.StartTime == null ? "-" :
                        (object)row.StartTime.Value.ToString("yyyy-MM-dd HH:mm");
                    gridRow.Cells["EndTime"].Value = row.EndTime == null ? "-" :
                        (object)row.EndTime.Value.ToString("yyyy-MM-dd HH:mm");
                    string status = TranslateStatus(row.Status);
                    gridRow.Cells["Status"].Value = status;
                    gridRow.Cells["MinutesWorked"].Value = row.MinutesWorked;
                    gridRow.Cells["OvertimeMin"].Value = row.OvertimeMin;
                    gridRow.Cells["IsManuallyChg"].Value = row.IsManuallyChg == true ? "نعم" : "لا";
                    if (row.IsManuallyChg == true)
                    {
                        gridRow.Cells["IsManuallyChg"].Style.BackColor = Theme.StatusWarn;
                    }
                    if (string.Equals(row.Status, "Late", StringComparison.Ordinal) ||
                        string.Equals(row.Status, "EarlyLeave", StringComparison.Ordinal) ||
                        string.Equals(row.Status, "MissingPunch", StringComparison.Ordinal))
                    {
                        lateCount++;
                        gridRow.Cells["Status"].Style.BackColor = Theme.StatusWarn;
                    }
                    if (string.Equals(row.Status, "Absent", StringComparison.Ordinal))
                    {
                        absentCount++;
                        gridRow.Cells["Status"].Style.BackColor = Theme.StatusBad;
                    }
                }

                Text = "محرك الحضور — النتائج: " + rows.Count + " صف (متأخر/مبكر/ناقص: " + lateCount +
                    ", غياب: " + absentCount + ")";
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmAttendanceCalc.OnBrowseClick");
                Theme.ShowError("حدث خطأ أثناء عرض نتائج الحضور.");
            }
        }

        /// <summary>Resolves the free-text filter to a single employee code match, or null when empty.</summary>
        private int? ResolveEmployeeFilter()
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
            return matches.Count > 0 ? matches[0].EmployeeId : (int?)null;
        }

        private static string TranslateStatus(string status)
        {
            if (string.Equals(status, "Present", StringComparison.Ordinal))
            {
                return "حاضر";
            }
            if (string.Equals(status, "Late", StringComparison.Ordinal))
            {
                return "متأخر";
            }
            if (string.Equals(status, "EarlyLeave", StringComparison.Ordinal))
            {
                return "انصراف مبكر";
            }
            if (string.Equals(status, "Absent", StringComparison.Ordinal))
            {
                return "غائب";
            }
            if (string.Equals(status, "MissingPunch", StringComparison.Ordinal))
            {
                return "بصمة ناقصة";
            }
            if (string.Equals(status, "Leave", StringComparison.Ordinal))
            {
                return "إجازة";
            }
            if (string.Equals(status, "Mission", StringComparison.Ordinal))
            {
                return "مأمورية";
            }
            if (string.Equals(status, "Holiday", StringComparison.Ordinal))
            {
                return "عطلة رسمية";
            }
            if (string.Equals(status, "WeeklyRest", StringComparison.Ordinal))
            {
                return "راحة أسبوعية";
            }
            return status;
        }

        private void AddColumn(string name, string header, int fillWeight)
        {
            DataGridViewTextBoxColumn column = new DataGridViewTextBoxColumn();
            column.Name = name;
            column.HeaderText = header;
            column.FillWeight = fillWeight;
            column.SortMode = DataGridViewColumnSortMode.NotSortable;
            _grid.Columns.Add(column);
        }
    }
}
