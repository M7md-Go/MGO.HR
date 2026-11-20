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
    /// Login attempts browser (rules 21): newest LoginHistory rows with a user
    /// filter, showing when, from which machine and IP, success or failure.
    /// Security trail — permission "Audit.View".
    /// </summary>
    public class FrmLoginHistory : Form
    {
        private ComboBox _cboUser;
        private DataGridView _grid;
        private Dictionary<int, string> _userNames;

        public FrmLoginHistory()
        {
            Theme.Apply(this);
            Text = "سجل تسجيل الدخول";
            ClientSize = new Size(950, 550);
            StartPosition = FormStartPosition.CenterParent;

            Panel header = new Panel();
            Label lblTitle = new Label();
            lblTitle.Text = "سجل محاولات تسجيل الدخول — آخر 1000 محاولة";
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
            toolbar.WrapContents = false;
            toolbar.Padding = new Padding(6, 8, 6, 4);

            Label lblUser = new Label();
            lblUser.Text = "المستخدم:";
            lblUser.AutoSize = true;
            lblUser.Margin = new Padding(3, 10, 3, 0);

            _cboUser = new ComboBox();
            _cboUser.Width = 200;
            _cboUser.DropDownWidth = 260;
            _cboUser.Margin = new Padding(3, 8, 3, 0);

            Button btnView = new Button();
            btnView.Text = "عرض";
            btnView.Height = 30;
            btnView.Click += delegate { Reload(); };

            toolbar.Controls.Add(lblUser);
            toolbar.Controls.Add(_cboUser);
            toolbar.Controls.Add(btnView);

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

            LoadUsers();
            Reload();
        }

        private void LoadUsers()
        {
            try
            {
                Dictionary<int, string> items = new Dictionary<int, string>();
                items.Add(0, "كل المستخدمين");
                _userNames = new Dictionary<int, string>();
                List<UserView> users = UserBLL.ListUsers();
                foreach (UserView user in users)
                {
                    items.Add(user.UserId, user.Username + " - " + user.FullName);
                    _userNames[user.UserId] = user.Username;
                }
                _cboUser.DataSource = new BindingSource(items, null);
                _cboUser.DisplayMember = "Value";
                _cboUser.ValueMember = "Key";
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmLoginHistory.LoadUsers");
            }
        }

        private void Reload()
        {
            try
            {
                int userId = _cboUser.SelectedValue is int ? (int)_cboUser.SelectedValue : 0;
                List<LoginHistory> rows = UserBLL.GetLoginHistory(userId == 0 ? (int?)null : (int?)userId, 1000);
                _grid.Columns.Clear();
                _grid.Rows.Clear();

                AddColumn("AttemptTime", "الوقت", 130);
                AddColumn("Username", "المستخدم", 160);
                AddColumn("Success", "النتيجة", 90);
                AddColumn("MachineName", "الجهاز", 160);
                AddColumn("IpAddress", "IP", 110);

                foreach (LoginHistory history in rows)
                {
                    int rowIndex = _grid.Rows.Add();
                    DataGridViewRow row = _grid.Rows[rowIndex];
                    row.Tag = history;
                    row.Cells["AttemptTime"].Value = history.AttemptTime == null ? "-" :
                        history.AttemptTime.Value.ToString("yyyy-MM-dd HH:mm");
                    string username;
                    row.Cells["Username"].Value = history.UserId == null ? "-" :
                        (_userNames != null && _userNames.TryGetValue(history.UserId.Value, out username) ? username : history.UserId.Value.ToString());
                    row.Cells["Success"].Value = history.Success == true ? "نجاح" : "فشل";
                    row.Cells["MachineName"].Value = history.MachineName;
                    row.Cells["IpAddress"].Value = history.IpAddress;
                    if (history.Success != true)
                    {
                        row.DefaultCellStyle.ForeColor = Theme.StatusBad;
                    }
                }
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmLoginHistory.Reload");
                Theme.ShowError("حدث خطأ أثناء تحميل سجل تسجيل الدخول.");
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
