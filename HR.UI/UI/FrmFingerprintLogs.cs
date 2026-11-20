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
    /// Raw fingerprint-log browser (rule 9): filter by device and date range,
    /// shows every stored punch with its source device and employee mapping.
    /// Read-only screen; data comes from FingerprintBLL.SearchRawLogs so the
    /// UI never touches HR.DAL, and no device session is opened here.
    /// </summary>
    public class FrmFingerprintLogs : Form
    {
        private const int MaxRows = 1000;

        private ComboBox _cboDevice;
        private DateTimePicker _dtpFrom;
        private DateTimePicker _dtpTo;
        private Button _btnSearch;
        private Label _lblCount;
        private DataGridView _grid;
        private List<Device> _devices;

        public FrmFingerprintLogs()
        {
            Theme.Apply(this);
            Text = "سجلات البصمة الخام";
            ClientSize = new Size(1050, 600);
            StartPosition = FormStartPosition.CenterParent;

            Panel header = new Panel();
            Label lblTitle = new Label();
            lblTitle.Text = "سجل البصمات الخام كما وردت من الأجهزة";
            lblTitle.ForeColor = Theme.HeaderTextColor;
            lblTitle.Font = Theme.Font(12f);
            lblTitle.Dock = DockStyle.Fill;
            lblTitle.TextAlign = ContentAlignment.MiddleCenter;
            Theme.StyleHeader(header);
            header.Controls.Add(lblTitle);
            header.Dock = DockStyle.Top;

            FlowLayoutPanel filterBar = new FlowLayoutPanel();
            filterBar.Dock = DockStyle.Top;
            filterBar.Height = 46;
            filterBar.BackColor = Theme.ToolbarColor;
            filterBar.FlowDirection = FlowDirection.RightToLeft;
            filterBar.WrapContents = false;
            filterBar.Padding = new Padding(6, 8, 6, 4);

            Label lblDevice = new Label();
            lblDevice.Text = "الجهاز:";
            lblDevice.AutoSize = true;
            lblDevice.Margin = new Padding(3, 10, 3, 0);

            _cboDevice = new ComboBox();
            _cboDevice.DropDownStyle = ComboBoxStyle.DropDownList;
            _cboDevice.Width = 180;
            _cboDevice.Margin = new Padding(1, 6, 1, 0);

            Label lblFrom = new Label();
            lblFrom.Text = "من:";
            lblFrom.AutoSize = true;
            lblFrom.Margin = new Padding(10, 10, 3, 0);

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

            _btnSearch = new Button();
            _btnSearch.Text = "بحث";
            _btnSearch.Height = 28;
            _btnSearch.Margin = new Padding(10, 5, 3, 0);
            _btnSearch.Click += delegate { Reload(); };

            _lblCount = new Label();
            _lblCount.Text = "";
            _lblCount.AutoSize = true;
            _lblCount.ForeColor = Theme.HeaderTextColor;
            _lblCount.Margin = new Padding(14, 10, 3, 0);

            filterBar.Controls.Add(lblDevice);
            filterBar.Controls.Add(_cboDevice);
            filterBar.Controls.Add(lblFrom);
            filterBar.Controls.Add(_dtpFrom);
            filterBar.Controls.Add(lblTo);
            filterBar.Controls.Add(_dtpTo);
            filterBar.Controls.Add(_btnSearch);
            filterBar.Controls.Add(_lblCount);

            _grid = new DataGridView();
            Theme.StyleGrid(_grid);
            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            Controls.Add(_grid);
            Controls.Add(filterBar);
            Controls.Add(header);

            LoadFilterDefaults();
            Reload();
        }

        private void LoadFilterDefaults()
        {
            DateTime today = DateTime.Today;
            _dtpFrom.Value = new DateTime(today.Year, today.Month, 1);
            _dtpTo.Value = today;

            Dictionary<int, string> devices = new Dictionary<int, string>();
            devices[0] = "كل الأجهزة";
            try
            {
                _devices = DeviceBLL.GetAll(true);
                foreach (Device device in _devices)
                {
                    devices[device.DeviceId] = device.DeviceName;
                }
            }
            catch (Exception unexpected)
            {
                _devices = new List<Device>();
                Logger.LogError(unexpected, "FrmFingerprintLogs.LoadFilterDefaults");
                Theme.ShowError("حدث خطأ أثناء تحميل قائمة الأجهزة.");
            }
            _cboDevice.DataSource = new BindingSource(devices, null);
            _cboDevice.DisplayMember = "Value";
            _cboDevice.ValueMember = "Key";
        }

        private void Reload()
        {
            try
            {
                int selectedDeviceId = (int)_cboDevice.SelectedValue;
                int? deviceId = selectedDeviceId == 0 ? (int?)null : selectedDeviceId;
                DateTime fromTime = _dtpFrom.Value.Date;
                DateTime toTime = _dtpTo.Value.Date.AddDays(1).AddSeconds(-1);
                List<FingerprintLogView> rows = FingerprintBLL.SearchRawLogs(deviceId, fromTime, toTime, MaxRows);

                _grid.Columns.Clear();
                _grid.Rows.Clear();
                AddColumn("LogId", "ر.ق", 45);
                AddColumn("DeviceName", "الجهاز", 110);
                AddColumn("EmployeeCode", "كود الموظف", 80);
                AddColumn("EmployeeName", "اسم الموظف", 170);
                AddColumn("EmpDevCode", "كود الجهاز", 80);
                AddColumn("LogTime", "وقت السجل", 130);
                AddColumn("InOutFlag", "النوع", 70);
                AddColumn("SourceFile", "المصدر", 90);
                AddColumn("CreatedAt", "وقت الحفظ", 130);

                int matchedCount = 0;
                foreach (FingerprintLogView row in rows)
                {
                    int rowIndex = _grid.Rows.Add();
                    DataGridViewRow gridRow = _grid.Rows[rowIndex];
                    gridRow.Cells["LogId"].Value = row.LogId;
                    gridRow.Cells["DeviceName"].Value = row.DeviceName;
                    if (row.EmployeeId != null)
                    {
                        matchedCount++;
                        gridRow.Cells["EmployeeCode"].Value = row.EmployeeCode;
                        gridRow.Cells["EmployeeName"].Value = row.EmployeeName;
                    }
                    else
                    {
                        gridRow.Cells["EmployeeCode"].Value = "-";
                        gridRow.Cells["EmployeeName"].Value = "(غير مطابق)";
                        gridRow.Cells["EmployeeName"].Style.BackColor = Theme.StatusWarn;
                    }
                    gridRow.Cells["EmpDevCode"].Value = row.EmpDevCode;
                    gridRow.Cells["LogTime"].Value = row.LogTime == null ? null :
                        (object)row.LogTime.Value.ToString("yyyy-MM-dd HH:mm:ss");
                    gridRow.Cells["InOutFlag"].Value = TranslateFlag(row.InOutFlag);
                    gridRow.Cells["SourceFile"].Value = string.IsNullOrEmpty(row.SourceFile) ? "-" : row.SourceFile;
                    gridRow.Cells["CreatedAt"].Value = row.CreatedAt == null ? null :
                        (object)row.CreatedAt.Value.ToString("yyyy-MM-dd HH:mm:ss");
                }

                _lblCount.Text = "عدد السجلات: " + rows.Count + " / " + MaxRows +
                    " — مطابقة لموظف: " + matchedCount;
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmFingerprintLogs.Reload");
                Theme.ShowError("حدث خطأ أثناء تحميل سجلات البصمة.");
            }
        }

        private static string TranslateFlag(int? flag)
        {
            if (flag == null)
            {
                return "-";
            }
            if (flag.Value == 0)
            {
                return "دخول";
            }
            if (flag.Value == 1)
            {
                return "خروج";
            }
            return "نوع " + flag.Value;
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
