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
    /// Dashboard (rule 18): KPI cards over a selectable period (today /
    /// current week / current month / custom), department statistics, the
    /// newest fingerprint punches and device sync status. Read-only: it only
    /// consumes HR.Services.DashboardEngine (no writes, no permission gate,
    /// consistent with the reports module).
    /// </summary>
    public class FrmDashboard : Form
    {
        private ComboBox _cboDepartment;
        private DateTimePicker _dtpFrom;
        private DateTimePicker _dtpTo;
        private Label _lblPeriod;
        private Label _lblTotal;
        private Label _lblPresent;
        private Label _lblAbsent;
        private Label _lblLate;
        private Label _lblEarly;
        private Label _lblMissing;
        private Label _lblOvertime;
        private Label _lblLeaves;
        private Label _lblMissions;
        private Label _lblWorked;
        private DataGridView _gridDepartments;
        private DataGridView _gridFingerprints;
        private DataGridView _gridDevices;
        private Timer _refreshTimer;

        public FrmDashboard()
        {
            Theme.Apply(this);
            Text = "لوحة التحكم";
            ClientSize = new Size(1150, 740);
            StartPosition = FormStartPosition.CenterParent;

            Panel header = new Panel();
            Label lblTitle = new Label();
            lblTitle.Text = "لوحة التحكم — مؤشرات الحضور والغياب والبصمة والأجهزة";
            lblTitle.ForeColor = Theme.HeaderTextColor;
            lblTitle.Font = Theme.Font(12f);
            lblTitle.Dock = DockStyle.Fill;
            lblTitle.TextAlign = ContentAlignment.MiddleCenter;
            Theme.StyleHeader(header);
            header.Controls.Add(lblTitle);
            header.Dock = DockStyle.Top;

            FlowLayoutPanel toolbar = new FlowLayoutPanel();
            toolbar.Dock = DockStyle.Top;
            toolbar.Height = 52;
            toolbar.BackColor = Theme.ToolbarColor;
            toolbar.FlowDirection = FlowDirection.RightToLeft;
            toolbar.WrapContents = true;
            toolbar.Padding = new Padding(6, 8, 6, 4);

            Button btnToday = new Button();
            btnToday.Text = "اليوم";
            btnToday.Height = 30;
            btnToday.Click += delegate
            {
                _dtpFrom.Value = DateTime.Now.Date;
                _dtpTo.Value = DateTime.Now.Date;
                _dtpFrom.Checked = true;
                _dtpTo.Checked = true;
                Reload();
            };

            Button btnWeek = new Button();
            btnWeek.Text = "هذا الأسبوع";
            btnWeek.Height = 30;
            btnWeek.Click += delegate
            {
                _dtpFrom.Value = StartOfWeek(DateTime.Now.Date);
                _dtpTo.Value = _dtpFrom.Value.AddDays(6);
                _dtpFrom.Checked = true;
                _dtpTo.Checked = true;
                Reload();
            };

            Button btnMonth = new Button();
            btnMonth.Text = "هذا الشهر";
            btnMonth.Height = 30;
            btnMonth.Click += delegate
            {
                _dtpFrom.Value = DateTime.Now.Date.AddDays(-DateTime.Now.Day + 1);
                _dtpTo.Value = _dtpFrom.Value.AddMonths(1).AddDays(-1);
                _dtpFrom.Checked = true;
                _dtpTo.Checked = true;
                Reload();
            };

            Label lblFrom = new Label();
            lblFrom.Text = "من:";
            lblFrom.AutoSize = true;
            lblFrom.Margin = new Padding(3, 10, 3, 0);

            _dtpFrom = new DateTimePicker();
            _dtpFrom.Format = DateTimePickerFormat.Short;
            _dtpFrom.Width = 105;
            _dtpFrom.ShowCheckBox = false;
            _dtpFrom.Value = DateTime.Now.Date.AddDays(-DateTime.Now.Day + 1);
            _dtpFrom.Margin = new Padding(3, 8, 3, 0);

            Label lblTo = new Label();
            lblTo.Text = "إلى:";
            lblTo.AutoSize = true;
            lblTo.Margin = new Padding(3, 10, 3, 0);

            _dtpTo = new DateTimePicker();
            _dtpTo.Format = DateTimePickerFormat.Short;
            _dtpTo.Width = 105;
            _dtpTo.ShowCheckBox = false;
            _dtpTo.Value = DateTime.Now.Date;
            _dtpTo.Margin = new Padding(3, 8, 3, 0);

            Label lblDepartment = new Label();
            lblDepartment.Text = "القسم:";
            lblDepartment.AutoSize = true;
            lblDepartment.Margin = new Padding(3, 10, 3, 0);

            _cboDepartment = new ComboBox();
            _cboDepartment.DropDownStyle = ComboBoxStyle.DropDownList;
            _cboDepartment.Width = 150;
            _cboDepartment.Margin = new Padding(3, 8, 3, 0);

            Button btnRefresh = new Button();
            btnRefresh.Text = "تحديث";
            btnRefresh.Height = 30;
            btnRefresh.Click += delegate { Reload(); };

            _lblPeriod = new Label();
            _lblPeriod.Text = "";
            _lblPeriod.AutoSize = true;
            _lblPeriod.ForeColor = Theme.StatusOk;
            _lblPeriod.Margin = new Padding(3, 12, 3, 0);

            toolbar.Controls.Add(btnToday);
            toolbar.Controls.Add(btnWeek);
            toolbar.Controls.Add(btnMonth);
            toolbar.Controls.Add(lblFrom);
            toolbar.Controls.Add(_dtpFrom);
            toolbar.Controls.Add(lblTo);
            toolbar.Controls.Add(_dtpTo);
            toolbar.Controls.Add(lblDepartment);
            toolbar.Controls.Add(_cboDepartment);
            toolbar.Controls.Add(btnRefresh);
            toolbar.Controls.Add(_lblPeriod);

            TableLayoutPanel kpis = new TableLayoutPanel();
            kpis.Dock = DockStyle.Top;
            kpis.Height = 96;
            kpis.ColumnCount = 5;
            kpis.RowCount = 2;
            kpis.Dock = DockStyle.Top;
            for (int i = 0; i < 5; i++)
            {
                kpis.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20f));
            }
            kpis.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
            kpis.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));

            kpis.Controls.Add(BuildKpiPanel("إجمالي الموظفين", ref _lblTotal, Theme.HeaderColor), 4, 0);
            kpis.Controls.Add(BuildKpiPanel("أيام الحضور", ref _lblPresent, Theme.StatusOk), 3, 0);
            kpis.Controls.Add(BuildKpiPanel("أيام الغياب", ref _lblAbsent, Theme.StatusBad), 2, 0);
            kpis.Controls.Add(BuildKpiPanel("متأخرون / دقائق", ref _lblLate, Theme.StatusWarn), 1, 0);
            kpis.Controls.Add(BuildKpiPanel("انصراف مبكر / دقائق", ref _lblEarly, Theme.StatusWarn), 0, 0);
            kpis.Controls.Add(BuildKpiPanel("بصمات ناقصة", ref _lblMissing, Theme.StatusBad), 4, 1);
            kpis.Controls.Add(BuildKpiPanel("ساعات إضافية (محتسبة)", ref _lblOvertime, Theme.StatusOk), 3, 1);
            kpis.Controls.Add(BuildKpiPanel("إجازات معتمدة", ref _lblLeaves, Theme.HeaderColor), 2, 1);
            kpis.Controls.Add(BuildKpiPanel("مأموريات معتمدة", ref _lblMissions, Theme.HeaderColor), 1, 1);
            kpis.Controls.Add(BuildKpiPanel("أيام عمل محسوبة", ref _lblWorked, Theme.StatusOk), 0, 1);

            SplitContainer bottom = new SplitContainer();
            bottom.Dock = DockStyle.Fill;
            bottom.Orientation = Orientation.Horizontal;

            _gridDepartments = BuildGrid();
            BuildDepartmentColumns();
            Panel departmentsPanel = new Panel();
            departmentsPanel.Dock = DockStyle.Fill;
            Label lblDepartments = new Label();
            lblDepartments.Text = "إحصائيات الأقسام (عدد الموظفين وأيام الحضور والغياب)";
            lblDepartments.Dock = DockStyle.Top;
            lblDepartments.Height = 24;
            lblDepartments.TextAlign = ContentAlignment.MiddleLeft;
            Panel deptHost = new Panel();
            deptHost.Dock = DockStyle.Fill;
            deptHost.Controls.Add(_gridDepartments);
            departmentsPanel.Controls.Add(deptHost);
            departmentsPanel.Controls.Add(lblDepartments);

            SplitContainer lower = new SplitContainer();
            lower.Dock = DockStyle.Fill;
            lower.Orientation = Orientation.Horizontal;

            _gridFingerprints = BuildGrid();
            BuildFingerprintColumns();
            Panel printHost = new Panel();
            printHost.Dock = DockStyle.Fill;
            Label lblPrints = new Label();
            lblPrints.Text = "آخر عمليات البصمة";
            lblPrints.Dock = DockStyle.Top;
            lblPrints.Height = 24;
            Panel printPanel = new Panel();
            printPanel.Dock = DockStyle.Fill;
            printPanel.Controls.Add(_gridFingerprints);
            printHost.Controls.Add(printPanel);
            printHost.Controls.Add(lblPrints);

            _gridDevices = BuildGrid();
            BuildDeviceColumns();
            Panel deviceHost = new Panel();
            deviceHost.Dock = DockStyle.Fill;
            Label lblDevices = new Label();
            lblDevices.Text = "حالة أجهزة البصمة";
            lblDevices.Dock = DockStyle.Top;
            lblDevices.Height = 24;
            Panel devicePanel = new Panel();
            devicePanel.Dock = DockStyle.Fill;
            devicePanel.Controls.Add(_gridDevices);
            deviceHost.Controls.Add(devicePanel);
            deviceHost.Controls.Add(lblDevices);

            lower.Panel1.Controls.Add(printHost);
            lower.Panel2.Controls.Add(deviceHost);
            bottom.Panel1.Controls.Add(departmentsPanel);
            bottom.Panel2.Controls.Add(lower);
            bottom.SplitterDistance = 260;

            Controls.Add(bottom);
            Controls.Add(kpis);
            Controls.Add(toolbar);
            Controls.Add(header);

            LoadDepartments();
            Reload();
        }

        /// <summary>
        /// The refresh timer starts only when the form is actually shown and is
        /// stopped on close; a form that gets created then disposed by the main
        /// shell (duplicate module) must never leave a live timer behind.
        /// </summary>
        protected override void OnShown(EventArgs args)
        {
            base.OnShown(args);
            if (_refreshTimer == null)
            {
                _refreshTimer = new Timer();
                _refreshTimer.Interval = 120000;
                _refreshTimer.Tick += delegate { Reload(); };
            }
            _refreshTimer.Start();
        }

        protected override void OnFormClosed(FormClosedEventArgs args)
        {
            if (_refreshTimer != null)
            {
                _refreshTimer.Stop();
                _refreshTimer.Dispose();
                _refreshTimer = null;
            }
            base.OnFormClosed(args);
        }

        private DateTime StartOfWeek(DateTime value)
        {
            int daysBack = ((int)value.DayOfWeek + 1) % 7;
            return value.Date.AddDays(-daysBack);
        }

        private Panel BuildKpiPanel(string caption, ref Label valueLabel, Color color)
        {
            Panel panel = new Panel();
            panel.Dock = DockStyle.Fill;
            panel.Margin = new Padding(4);
            panel.BackColor = Color.White;
            panel.BorderStyle = BorderStyle.FixedSingle;

            Label lblValue = new Label();
            lblValue.Text = "0";
            lblValue.Dock = DockStyle.Fill;
            lblValue.TextAlign = ContentAlignment.MiddleCenter;
            lblValue.Font = new Font(Theme.Font(15f), FontStyle.Bold);
            lblValue.ForeColor = color;

            Label lblCaption = new Label();
            lblCaption.Text = caption;
            lblCaption.Dock = DockStyle.Bottom;
            lblCaption.Height = 24;
            lblCaption.TextAlign = ContentAlignment.MiddleCenter;

            panel.Controls.Add(lblValue);
            panel.Controls.Add(lblCaption);
            valueLabel = lblValue;
            return panel;
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
                Logger.LogError(unexpected, "FrmDashboard.LoadDepartments");
            }
        }

        /// <summary>Rebuilds the dashboard payload and repaints every control.</summary>
        private void Reload()
        {
            if (IsDisposed || _gridDepartments == null || _gridFingerprints == null || _gridDevices == null)
            {
                return;
            }
            try
            {
                int? departmentId = _cboDepartment.SelectedValue is int ? (int)_cboDepartment.SelectedValue : 0;
                departmentId = departmentId == 0 ? (int?)null : departmentId;
                DashboardData data = DashboardEngine.Build(_dtpFrom.Value.Date, _dtpTo.Value.Date, departmentId);
                _lblPeriod.Text = data.PeriodText;
                _lblTotal.Text = data.TotalEmployees.ToString();
                _lblPresent.Text = data.Present + " / " + FormatHm(data.WorkedMinutes);
                _lblAbsent.Text = data.Absent.ToString();
                _lblLate.Text = data.Late + " / " + data.LateMinutes.ToString();
                _lblEarly.Text = data.Early + " / " + data.EarlyMinutes.ToString();
                _lblMissing.Text = data.MissingPunch.ToString();
                _lblOvertime.Text = ReportEngine.FormatMinutes((int)Math.Min(data.OvertimeMinutes, int.MaxValue));
                _lblLeaves.Text = data.ApprovedLeaves + " طلب / " + data.LeaveDays + " يوم";
                _lblMissions.Text = data.ApprovedMissions.ToString();
                _lblWorked.Text = FormatHm(data.WorkedMinutes);

                if (_gridDepartments.Columns.Count == 0)
                {
                    BuildDepartmentColumns();
                }
                _gridDepartments.Rows.Clear();
                foreach (DepartmentStat stat in data.DepartmentStats)
                {
                    int rowIndex = _gridDepartments.Rows.Add();
                    DataGridViewRow row = _gridDepartments.Rows[rowIndex];
                    row.Cells["Department"].Value = stat.DepartmentName;
                    row.Cells["Employees"].Value = stat.Employees;
                    row.Cells["Present"].Value = stat.Present;
                    row.Cells["Absent"].Value = stat.Absent;
                }

                if (_gridFingerprints.Columns.Count == 0)
                {
                    BuildFingerprintColumns();
                }
                _gridFingerprints.Rows.Clear();
                foreach (FingerprintLogView print in data.Fingerprints)
                {
                    int rowIndex = _gridFingerprints.Rows.Add();
                    DataGridViewRow row = _gridFingerprints.Rows[rowIndex];
                    row.Cells["LogTime"].Value = print.LogTime == null ? "" :
                        print.LogTime.Value.ToString("yyyy-MM-dd HH:mm:ss");
                    row.Cells["EmployeeName"].Value = print.EmployeeName;
                    row.Cells["DeviceName"].Value = print.DeviceName;
                    row.Cells["Direction"].Value = print.InOutFlag == 1 ? "خروج" :
                        (print.InOutFlag == 0 ? "دخول" : "");
                }

                if (_gridDevices.Columns.Count == 0)
                {
                    BuildDeviceColumns();
                }
                _gridDevices.Rows.Clear();
                foreach (Device device in data.Devices)
                {
                    int rowIndex = _gridDevices.Rows.Add();
                    DataGridViewRow row = _gridDevices.Rows[rowIndex];
                    row.Cells["DeviceName"].Value = device.DeviceName;
                    row.Cells["IpAddress"].Value = device.IpAddress;
                    row.Cells["LastSync"].Value = device.LastSync == null ? "-" :
                        device.LastSync.Value.ToString("yyyy-MM-dd HH:mm");
                    row.Cells["LastStatus"].Value = device.LastStatus;
                    bool ok = device.LastStatus == "Connected" || device.LastStatus == "Downloaded";
                    row.Cells["LastStatus"].Style.ForeColor = ok ? Theme.StatusOk : Theme.StatusWarn;
                }
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmDashboard.Reload");
                Theme.ShowError("حدث خطأ أثناء بناء لوحة التحكم.");
            }
        }

        /// <summary>Formats minutes as H:mm totals for worked hours.</summary>
        private string FormatHm(long minutes)
        {
            return (minutes / 60) + ":" + (minutes % 60).ToString("00");
        }

        private DataGridView BuildGrid()
        {
            DataGridView grid = new DataGridView();
            Theme.StyleGrid(grid);
            grid.Dock = DockStyle.Fill;
            grid.ReadOnly = true;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            return grid;
        }

        private void BuildDepartmentColumns()
        {
            AddColumn(_gridDepartments, "Department", "القسم", 200);
            AddColumn(_gridDepartments, "Employees", "الموظفون", 70);
            AddColumn(_gridDepartments, "Present", "أيام حضور", 80);
            AddColumn(_gridDepartments, "Absent", "أيام غياب", 80);
        }

        private void BuildFingerprintColumns()
        {
            AddColumn(_gridFingerprints, "LogTime", "وقت البصمة", 120);
            AddColumn(_gridFingerprints, "EmployeeName", "الموظف", 140);
            AddColumn(_gridFingerprints, "DeviceName", "الجهاز", 100);
            AddColumn(_gridFingerprints, "Direction", "الاتجاه", 60);
        }

        private void BuildDeviceColumns()
        {
            AddColumn(_gridDevices, "DeviceName", "الجهاز", 120);
            AddColumn(_gridDevices, "IpAddress", "IP", 90);
            AddColumn(_gridDevices, "LastSync", "آخر مزامنة", 110);
            AddColumn(_gridDevices, "LastStatus", "الحالة", 80);
        }

        private void AddColumn(DataGridView grid, string name, string header, int fillWeight)
        {
            DataGridViewTextBoxColumn column = new DataGridViewTextBoxColumn();
            column.Name = name;
            column.HeaderText = header;
            column.FillWeight = fillWeight;
            column.SortMode = DataGridViewColumnSortMode.NotSortable;
            grid.Columns.Add(column);
        }
    }
}
