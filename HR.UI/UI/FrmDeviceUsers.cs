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
    /// Live read of the users registered inside one fingerprint device (rule 9
    /// — قراءة الموظفين من الجهاز): enroll code, device name, privilege,
    /// enabled state and the matched employee (via DeviceEnrollNumber).
    /// </summary>
    public class FrmDeviceUsers : Form
    {
        private readonly int _deviceId;
        private DataGridView _grid;
        private Label _lblSummary;
        private Button _btnReload;
        private Button _btnClose;

        public FrmDeviceUsers(int deviceId)
        {
            _deviceId = deviceId;
            Theme.Apply(this);
            Text = "مستخدمو جهاز البصمة";
            ClientSize = new Size(760, 500);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;

            Panel header = new Panel();
            Label lblTitle = new Label();
            lblTitle.Text = "المستخدمون المسجّلون في الجهاز (قراءة حيّة)";
            lblTitle.ForeColor = Theme.HeaderTextColor;
            lblTitle.Font = Theme.Font(11f);
            lblTitle.Dock = DockStyle.Fill;
            lblTitle.TextAlign = ContentAlignment.MiddleCenter;
            Theme.StyleHeader(header);
            header.Controls.Add(lblTitle);
            header.Dock = DockStyle.Top;

            FlowLayoutPanel toolbar = new FlowLayoutPanel();
            toolbar.Dock = DockStyle.Top;
            toolbar.Height = 50;
            toolbar.BackColor = Theme.ToolbarColor;
            toolbar.FlowDirection = FlowDirection.RightToLeft;
            toolbar.Padding = new Padding(6, 8, 6, 4);

            _btnReload = new Button();
            _btnReload.Text = "إعادة قراءة الجهاز";
            _btnReload.Height = 30;
            _btnReload.Click += delegate { LoadUsers(); };

            _btnClose = new Button();
            _btnClose.Text = "إغلاق";
            _btnClose.Height = 30;
            _btnClose.Click += delegate { Close(); };

            _lblSummary = new Label();
            _lblSummary.AutoSize = true;
            _lblSummary.Margin = new Padding(8, 12, 8, 0);

            toolbar.Controls.Add(_btnReload);
            toolbar.Controls.Add(_btnClose);
            toolbar.Controls.Add(_lblSummary);

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

            Device device = DeviceBLL.GetById(_deviceId);
            if (device != null)
            {
                Text = "مستخدمو الجهاز: " + device.DeviceName;
            }

            LoadUsers();
        }

        private void LoadUsers()
        {
            try
            {
                UseWaitCursor = true;
                List<DeviceUserInfo> users;
                try
                {
                    users = FingerprintBLL.ReadDeviceUsers(_deviceId);
                }
                finally
                {
                    UseWaitCursor = false;
                }

                _grid.Columns.Clear();
                _grid.Rows.Clear();

                AddColumn("EnrollCode", "كود الجهاز", 90);
                AddColumn("FullName", "الاسم على الجهاز", 160);
                AddColumn("Privilege", "الصلاحية", 90);
                AddColumn("Enabled", "ممكّن", 60);
                AddColumn("EmployeeCode", "كود الموظف", 90);
                AddColumn("EmployeeName", "اسم الموظف", 170);
                AddColumn("Matched", "مطابق؟", 70);

                int matched = 0;
                foreach (DeviceUserInfo user in users)
                {
                    if (user.Matched)
                    {
                        matched++;
                    }
                    int rowIndex = _grid.Rows.Add();
                    DataGridViewRow row = _grid.Rows[rowIndex];
                    row.Tag = user;
                    row.Cells["EnrollCode"].Value = user.EnrollCode;
                    row.Cells["FullName"].Value = user.FullName;
                    row.Cells["Privilege"].Value = TranslatePrivilege(user.Privilege);
                    row.Cells["Enabled"].Value = user.Enabled == true ? "نعم" : "لا";
                    row.Cells["EmployeeCode"].Value = user.EmployeeCode;
                    row.Cells["EmployeeName"].Value = user.EmployeeName;
                    row.Cells["Matched"].Value = user.Matched ? "نعم" : "لا";
                    if (!user.Matched)
                    {
                        row.DefaultCellStyle.BackColor = Theme.StatusWarn;
                    }
                }
                _lblSummary.Text = "المستخدمون: " + users.Count + " • مطابقون لموظفين: " + matched +
                    " • غير مطابقين: " + (users.Count - matched);
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (InvalidOperationException expectedSdk)
            {
                Theme.ShowError(expectedSdk.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmDeviceUsers.LoadUsers");
                Theme.ShowError("حدث خطأ أثناء قراءة مستخدمي الجهاز.");
            }
        }

        private static string TranslatePrivilege(int? privilege)
        {
            if (privilege == null)
            {
                return "-";
            }
            if (privilege.Value == 0)
            {
                return "مستخدم";
            }
            if (privilege.Value == 2)
            {
                return "مسؤول";
            }
            return "مستوى " + privilege.Value;
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
