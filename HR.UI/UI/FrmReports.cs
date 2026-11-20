using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using HR.BLL;
using HR.Entities;
using HR.Helpers;
using HR.Services;

namespace HR.UI.UI
{
    /// <summary>
    /// Reports browser (rule 19): selects one of the nine reports, applies the
    /// shared filters, previews the ReportData grid and exports to Excel.
    /// This form is read-only; it only calls HR.Services.ReportEngine which
    /// composes read-only DAL queries (no writes, no permission required to
    /// view, per design decision D5/Phase 9). Export is SpreadsheetML 2003
    /// through HR.Services.ExcelWriter (UTF-8 + RTL, Excel 2007 compatible).
    /// </summary>
    public class FrmReports : Form
    {
        private static readonly string[] ReportCodes = new string[]
        {
            "DailyAttendance", "MonthlySummary", "DepartmentSummary",
            "Leaves", "Missions", "Penalties", "Overtime",
            "RawFingerprint", "Employees"
        };

        private static readonly string[] ReportNames = new string[]
        {
            "الحضور اليومي",
            "ملخص الحضور الشهري",
            "ملخص الأقسام (شهري)",
            "الإجازات",
            "المأموريات",
            "الجزاءات",
            "الساعات الإضافية",
            "البصمة الخام",
            "دليل الموظفين"
        };

        private static readonly string[] FilterModes = new string[]
        {
            "None", "None", "None",
            "Status", "Status", "Type", "Status",
            "Device", "Archived"
        };

        private static readonly string[] StatusValues = new string[]
        {
            "", "Pending", "Approved", "Rejected"
        };

        private static readonly string[] StatusLabels = new string[]
        {
            "كل الحالات", "معلّق", "معتمد", "مرفوض"
        };

        private static readonly string[] TypeValues = new string[]
        {
            "", "Warning", "Deduction", "Penalty", "Absence", "Violation"
        };

        private static readonly string[] TypeLabels = new string[]
        {
            "كل الأنواع", "إنذار", "خصم", "جزاء", "غياب", "مخالفة"
        };

        private ComboBox _cboReport;
        private ComboBox _cboDepartment;
        private TextBox _txtEmployee;
        private ComboBox _cboFilter;
        private Label _lblFilter;
        private DateTimePicker _dtpFrom;
        private DateTimePicker _dtpTo;
        private Label _lblHint;
        private Button _btnRefresh;
        private Button _btnExport;
        private DataGridView _grid;
        private ReportData _report;

        public FrmReports()
        {
            Theme.Apply(this);
            Text = "التقارير";
            ClientSize = new Size(1150, 640);
            StartPosition = FormStartPosition.CenterParent;

            Panel header = new Panel();
            Label lblTitle = new Label();
            lblTitle.Text = "التقارير — معاينة على الشاشة وتصدير إلى Excel (UTF-8 / RTL)";
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

            Label lblReport = new Label();
            lblReport.Text = "التقرير:";
            lblReport.AutoSize = true;
            lblReport.Margin = new Padding(3, 10, 3, 0);

            _cboReport = new ComboBox();
            _cboReport.DropDownStyle = ComboBoxStyle.DropDownList;
            _cboReport.Width = 140;
            foreach (string name in ReportNames)
            {
                _cboReport.Items.Add(name);
            }
            _cboReport.SelectedIndex = 0;
            _cboReport.SelectedIndexChanged += delegate { UpdateFilterItems(); UpdateHint(); };
            _cboReport.Margin = new Padding(3, 8, 3, 0);

            Label lblDepartment = new Label();
            lblDepartment.Text = "القسم:";
            lblDepartment.AutoSize = true;
            lblDepartment.Margin = new Padding(3, 10, 3, 0);

            _cboDepartment = new ComboBox();
            _cboDepartment.DropDownStyle = ComboBoxStyle.DropDownList;
            _cboDepartment.Width = 140;
            _cboDepartment.Margin = new Padding(3, 8, 3, 0);

            Label lblEmployee = new Label();
            lblEmployee.Text = "الموظف:";
            lblEmployee.AutoSize = true;
            lblEmployee.Margin = new Padding(3, 10, 3, 0);

            _txtEmployee = new TextBox();
            _txtEmployee.Width = 110;
            _txtEmployee.Margin = new Padding(3, 8, 3, 0);

            _lblFilter = new Label();
            _lblFilter.Text = "تصفية:";
            _lblFilter.AutoSize = true;
            _lblFilter.Margin = new Padding(3, 10, 3, 0);

            _cboFilter = new ComboBox();
            _cboFilter.DropDownStyle = ComboBoxStyle.DropDownList;
            _cboFilter.Width = 110;
            _cboFilter.Margin = new Padding(3, 8, 3, 0);

            Label lblFrom = new Label();
            lblFrom.Text = "من:";
            lblFrom.AutoSize = true;
            lblFrom.Margin = new Padding(3, 10, 3, 0);

            _dtpFrom = new DateTimePicker();
            _dtpFrom.Format = DateTimePickerFormat.Short;
            _dtpFrom.Width = 100;
            _dtpFrom.ShowCheckBox = true;
            _dtpFrom.Checked = true;
            _dtpFrom.Value = DateTime.Now.Date.AddDays(-DateTime.Now.Day + 1);
            _dtpFrom.Margin = new Padding(3, 8, 3, 0);

            Label lblTo = new Label();
            lblTo.Text = "إلى:";
            lblTo.AutoSize = true;
            lblTo.Margin = new Padding(3, 10, 3, 0);

            _dtpTo = new DateTimePicker();
            _dtpTo.Format = DateTimePickerFormat.Short;
            _dtpTo.Width = 100;
            _dtpTo.ShowCheckBox = true;
            _dtpTo.Checked = true;
            _dtpTo.Value = DateTime.Now.Date;
            _dtpTo.Margin = new Padding(3, 8, 3, 0);

            _lblHint = new Label();
            _lblHint.Text = "";
            _lblHint.AutoSize = true;
            _lblHint.ForeColor = Theme.StatusWarn;
            _lblHint.Margin = new Padding(3, 12, 3, 0);

            _btnRefresh = new Button();
            _btnRefresh.Text = "عرض";
            _btnRefresh.Height = 30;
            _btnRefresh.Click += delegate { Reload(); };

            _btnExport = new Button();
            _btnExport.Text = "تصدير إلى Excel";
            _btnExport.Height = 30;
            _btnExport.Enabled = false;
            _btnExport.Click += delegate { OnExportClick(); };

            toolbar.Controls.Add(lblReport);
            toolbar.Controls.Add(_cboReport);
            toolbar.Controls.Add(lblDepartment);
            toolbar.Controls.Add(_cboDepartment);
            toolbar.Controls.Add(lblEmployee);
            toolbar.Controls.Add(_txtEmployee);
            toolbar.Controls.Add(_lblFilter);
            toolbar.Controls.Add(_cboFilter);
            toolbar.Controls.Add(lblFrom);
            toolbar.Controls.Add(_dtpFrom);
            toolbar.Controls.Add(lblTo);
            toolbar.Controls.Add(_dtpTo);
            toolbar.Controls.Add(_lblHint);
            toolbar.Controls.Add(_btnRefresh);
            toolbar.Controls.Add(_btnExport);

            _grid = new DataGridView();
            Theme.StyleGrid(_grid);
            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            Controls.Add(_grid);
            Controls.Add(toolbar);
            Controls.Add(header);

            LoadDepartments();
            UpdateFilterItems();
            UpdateHint();
        }

        private void LoadDepartments()
        {
            try
            {
                List<LookupRow> departments = OrganizationBLL.Rows(LookupTable.Departments, true);
                Dictionary<int, string> items = new Dictionary<int, string>();
                items.Add(0, "كل الأقسام");
                foreach (LookupRow row in departments)
                {
                    items.Add(row.Id, row.Name);
                }
                _cboDepartment.DataSource = new BindingSource(items, null);
                _cboDepartment.DisplayMember = "Value";
                _cboDepartment.ValueMember = "Key";
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmReports.LoadDepartments");
                Theme.ShowError("حدث خطأ أثناء تحميل قائمة الأقسام.");
            }
        }

        /// <summary>
        /// Refills the dynamic filter combo according to the selected report:
        /// request statuses for leaves/missions/overtime, penalty types,
        /// device list for the raw fingerprint report (sentinel 0 = all) and
        /// an archived-scope chooser for the employee directory.
        /// </summary>
        private void UpdateFilterItems()
        {
            string mode = FilterModes[_cboReport.SelectedIndex >= 0 ? _cboReport.SelectedIndex : 0];
            _cboFilter.Items.Clear();
            if (mode == "Status")
            {
                _lblFilter.Text = "الحالة:";
                foreach (string label in StatusLabels)
                {
                    _cboFilter.Items.Add(label);
                }
            }
            else if (mode == "Type")
            {
                _lblFilter.Text = "النوع:";
                foreach (string label in TypeLabels)
                {
                    _cboFilter.Items.Add(label);
                }
            }
            else if (mode == "Device")
            {
                _lblFilter.Text = "الجهاز:";
                try
                {
                    List<Device> devices = DeviceBLL.GetAll(true);
                    _cboFilter.Items.Add("كل الأجهزة");
                    foreach (Device device in devices)
                    {
                        _cboFilter.Items.Add(device.DeviceName);
                    }
                }
                catch (Exception unexpected)
                {
                    Logger.LogError(unexpected, "FrmReports.UpdateFilterItems");
                    _cboFilter.Items.Add("كل الأجهزة");
                }
            }
            else if (mode == "Archived")
            {
                _lblFilter.Text = "النطاق:";
                _cboFilter.Items.Add("النشطون فقط");
                _cboFilter.Items.Add("يشمل المؤرشفين");
            }
            else
            {
                _lblFilter.Text = "";
                _cboFilter.Items.Add("—");
            }
            _cboFilter.SelectedIndex = 0;
        }

        /// <summary>
        /// Returns the filter value of the dynamic combo: status / type code
        /// for the request reports, device id for raw fingerprint, or null
        /// when the selected report has no filter.
        /// </summary>
        private string FilterTextValue()
        {
            string mode = FilterModes[_cboReport.SelectedIndex >= 0 ? _cboReport.SelectedIndex : 0];
            int index = _cboFilter.SelectedIndex >= 0 ? _cboFilter.SelectedIndex : 0;
            if (mode == "Status")
            {
                return StatusValues[index];
            }
            if (mode == "Type")
            {
                return TypeValues[index];
            }
            return null;
        }

        private int? FilterDeviceId()
        {
            int index = _cboFilter.SelectedIndex >= 0 ? _cboFilter.SelectedIndex : 0;
            if (index == 0)
            {
                return null;
            }
            try
            {
                List<Device> devices = DeviceBLL.GetAll(true);
                if (index - 1 < devices.Count)
                {
                    return devices[index - 1].DeviceId;
                }
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmReports.FilterDeviceId");
            }
            return null;
        }

        private bool FilterIncludeArchived()
        {
            return _cboFilter.SelectedIndex == 1;
        }

        private void UpdateHint()
        {
            string code = ReportCodes[_cboReport.SelectedIndex >= 0 ? _cboReport.SelectedIndex : 0];
            if (code == "MonthlySummary")
            {
                _lblHint.Text = "الشهر يُؤخذ من تاريخ البداية";
            }
            else if (code == "DepartmentSummary")
            {
                _lblHint.Text = "ملخص كل الأقسام — الشهر من تاريخ البداية";
            }
            else if (code == "Employees")
            {
                _lblHint.Text = "لا تنطبق فلاتر التاريخ أو القسم على هذا التقرير";
            }
            else
            {
                _lblHint.Text = "";
            }
        }

        private int? ResolveEmployee()
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

        /// <summary>Builds the selected report from the shared filters.</summary>
        private void Reload()
        {
            try
            {
                string code = ReportCodes[_cboReport.SelectedIndex >= 0 ? _cboReport.SelectedIndex : 0];
                int? departmentId = _cboDepartment.SelectedValue is int ? (int)_cboDepartment.SelectedValue : 0;
                departmentId = departmentId == 0 ? (int?)null : departmentId;
                int? employeeId = ResolveEmployee();
                DateTime fromDate = _dtpFrom.Value.Date;
                DateTime toDate = _dtpTo.Value.Date;
                if (toDate < fromDate)
                {
                    DateTime temp = fromDate;
                    fromDate = toDate;
                    toDate = temp;
                }

                if (code == "DailyAttendance")
                {
                    if (!_dtpFrom.Checked || !_dtpTo.Checked)
                    {
                        Theme.ShowInfo("تقرير الحضور اليومي يتطلب تحديد فترة (من / إلى).");
                        return;
                    }
                    _report = ReportEngine.DailyAttendance(fromDate, toDate, departmentId, employeeId);
                }
                else if (code == "MonthlySummary")
                {
                    _report = ReportEngine.MonthlyAttendanceSummary(
                        fromDate.Year, fromDate.Month, departmentId, employeeId);
                }
                else if (code == "DepartmentSummary")
                {
                    _report = ReportEngine.DepartmentMonthlySummary(fromDate.Year, fromDate.Month);
                }
                else if (code == "Leaves")
                {
                    _report = ReportEngine.Leaves(fromDate, toDate, FilterTextValue(), employeeId);
                }
                else if (code == "Missions")
                {
                    _report = ReportEngine.Missions(fromDate, toDate, FilterTextValue(), employeeId);
                }
                else if (code == "Penalties")
                {
                    _report = ReportEngine.Penalties(fromDate, toDate, FilterTextValue(), employeeId);
                }
                else if (code == "Overtime")
                {
                    _report = ReportEngine.Overtime(fromDate, toDate, FilterTextValue(), employeeId);
                }
                else if (code == "RawFingerprint")
                {
                    DateTime fromTime = _dtpFrom.Checked ? fromDate : DateTime.Now.Date.AddDays(-30);
                    DateTime toTime = (_dtpTo.Checked ? toDate : DateTime.Now.Date).AddDays(1).AddSeconds(-1);
                    _report = ReportEngine.RawFingerprint(fromTime, toTime,
                        FilterDeviceId(), employeeId);
                }
                else
                {
                    _report = ReportEngine.EmployeeDirectory(FilterIncludeArchived());
                }

                RenderReport();
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmReports.Reload");
                Theme.ShowError("حدث خطأ أثناء بناء التقرير.");
            }
        }

        /// <summary>
        /// Renders the materialized ReportData into the grid; the totals row
        /// (when present) is appended in bold.
        /// </summary>
        private void RenderReport()
        {
            _grid.Columns.Clear();
            _grid.Rows.Clear();
            if (_report == null)
            {
                return;
            }
            foreach (ReportColumn column in _report.Columns)
            {
                AddColumn(column.Name, column.Header, column.Width);
            }
            foreach (List<string> cells in _report.Rows)
            {
                int rowIndex = _grid.Rows.Add();
                DataGridViewRow row = _grid.Rows[rowIndex];
                for (int i = 0; i < cells.Count && i < _report.Columns.Count; i++)
                {
                    row.Cells[i].Value = cells[i];
                }
            }
            if (_report.TotalsRow != null)
            {
                int rowIndex = _grid.Rows.Add();
                DataGridViewRow row = _grid.Rows[rowIndex];
                DataGridViewCellStyle bold = new DataGridViewCellStyle();
                bold.Font = new Font(Theme.Font(10f), FontStyle.Bold);
                row.DefaultCellStyle = bold;
                for (int i = 0; i < _report.TotalsRow.Count && i < _report.Columns.Count; i++)
                {
                    row.Cells[i].Value = _report.TotalsRow[i];
                }
            }
            _btnExport.Enabled = true;
        }

        /// <summary>
        /// Exports the last previewed report with SaveFileDialog + ExcelWriter
        /// (SpreadsheetML 2003, UTF-8 BOM, RTL) — Excel 2007 compatible.
        /// </summary>
        private void OnExportClick()
        {
            if (_report == null)
            {
                Theme.ShowInfo("اعرض التقرير أولاً قبل التصدير.");
                return;
            }
            SaveFileDialog dialog = new SaveFileDialog();
            dialog.Filter = "Microsoft Excel SpreadsheetML (*.xml)|*.xml";
            dialog.FileName = "تقرير.xml";
            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }
            try
            {
                string path = ExcelWriter.Export(_report, dialog.FileName);
                Theme.ShowInfo("تم تصدير التقرير بنجاح:\n" + path);
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmReports.OnExportClick");
                Theme.ShowError("حدث خطأ أثناء تصدير التقرير إلى Excel.");
            }
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
