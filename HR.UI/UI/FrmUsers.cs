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
    /// User accounts browser (rule 21 + 22): create / edit / disable / unlock
    /// / password reset. Every write is gated by "Users.Manage" (buttons
    /// disabled without it, BLL re-checks anyway). Passwords are SHA-256 +
    /// salt via UserBLL; no hash value is ever shown in the grid.
    /// </summary>
    public class FrmUsers : Form
    {
        private Button _btnAdd;
        private Button _btnEdit;
        private Button _btnToggle;
        private Button _btnUnlock;
        private Button _btnResetPassword;
        private Button _btnPermissions;
        private DataGridView _grid;
        private List<UserView> _rows;

        public FrmUsers()
        {
            Theme.Apply(this);
            Text = "المستخدمون والصلاحيات";
            ClientSize = new Size(1000, 560);
            StartPosition = FormStartPosition.CenterParent;

            Panel header = new Panel();
            Label lblTitle = new Label();
            lblTitle.Text = "إدارة المستخدمين — أدوار، إيقاف/تفعيل، فتح الحساب، إعادة تعيين كلمة المرور";
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

            _btnAdd = new Button();
            _btnAdd.Text = "مستخدم جديد";
            _btnAdd.Height = 30;
            _btnAdd.Click += delegate { OnAddClick(); };

            _btnEdit = new Button();
            _btnEdit.Text = "تعديل";
            _btnEdit.Height = 30;
            _btnEdit.Click += delegate { OnEditClick(); };

            _btnToggle = new Button();
            _btnToggle.Text = "إيقاف / تفعيل";
            _btnToggle.Height = 30;
            _btnToggle.Click += delegate { OnToggleClick(); };

            _btnUnlock = new Button();
            _btnUnlock.Text = "فتح الحساب";
            _btnUnlock.Height = 30;
            _btnUnlock.Click += delegate { OnUnlockClick(); };

            _btnResetPassword = new Button();
            _btnResetPassword.Text = "إعادة تعيين كلمة المرور";
            _btnResetPassword.Height = 30;
            _btnResetPassword.Click += delegate { OnResetPasswordClick(); };

            _btnPermissions = new Button();
            _btnPermissions.Text = "مصفوفة الصلاحيات";
            _btnPermissions.Height = 30;
            _btnPermissions.Click += delegate { ShowForm(new FrmPermissions()); };

            toolbar.Controls.Add(_btnAdd);
            toolbar.Controls.Add(_btnEdit);
            toolbar.Controls.Add(_btnToggle);
            toolbar.Controls.Add(_btnUnlock);
            toolbar.Controls.Add(_btnResetPassword);
            toolbar.Controls.Add(_btnPermissions);

            _grid = new DataGridView();
            Theme.StyleGrid(_grid);
            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _grid.CellDoubleClick += delegate { OnEditClick(); };

            Controls.Add(_grid);
            Controls.Add(toolbar);
            Controls.Add(header);

            bool canManage = AppContext.HasPermission("Users.Manage");
            _btnAdd.Enabled = canManage;
            _btnEdit.Enabled = canManage;
            _btnToggle.Enabled = canManage;
            _btnUnlock.Enabled = canManage;
            _btnResetPassword.Enabled = canManage;
            _btnPermissions.Enabled = canManage;
            Reload();
        }

        private UserView SelectedUser()
        {
            if (_grid.CurrentRow == null)
            {
                return null;
            }
            return _grid.CurrentRow.Tag as UserView;
        }

        private void Reload()
        {
            try
            {
                _rows = UserBLL.ListUsers();
                _grid.Columns.Clear();
                _grid.Rows.Clear();

                AddColumn("Username", "اسم المستخدم", 90);
                AddColumn("FullName", "الاسم الكامل", 130);
                AddColumn("RoleName", "الدور", 70);
                AddColumn("DeptName", "القسم", 100);
                AddColumn("Status", "الحالة", 70);
                AddColumn("FailedLogins", "محاولات فاشلة", 60);
                AddColumn("LastLogin", "آخر دخول", 110);

                foreach (UserView user in _rows)
                {
                    int rowIndex = _grid.Rows.Add();
                    DataGridViewRow row = _grid.Rows[rowIndex];
                    row.Tag = user;
                    row.Cells["Username"].Value = user.Username;
                    row.Cells["FullName"].Value = user.FullName;
                    row.Cells["RoleName"].Value = user.RoleName;
                    row.Cells["DeptName"].Value = user.DeptName;
                    bool locked = user.IsLocked == true;
                    bool disabled = user.IsDisabled == true;
                    row.Cells["Status"].Value = disabled ? "موقوف" :
                        (locked ? "مقفول" : "نشط");
                    if (disabled)
                    {
                        row.Cells["Status"].Style.ForeColor = Theme.StatusBad;
                    }
                    else if (locked)
                    {
                        row.Cells["Status"].Style.ForeColor = Theme.StatusWarn;
                    }
                    else
                    {
                        row.Cells["Status"].Style.ForeColor = Theme.StatusOk;
                    }
                    row.Cells["FailedLogins"].Value = user.FailedLogins ?? 0;
                    row.Cells["LastLogin"].Value = user.LastLogin == null ? "-" :
                        user.LastLogin.Value.ToString("yyyy-MM-dd HH:mm");
                }
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmUsers.Reload");
                Theme.ShowError("حدث خطأ أثناء تحميل المستخدمين.");
            }
        }

        private void OnAddClick()
        {
            FrmUserEdit dialog = new FrmUserEdit(null);
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                Reload();
            }
        }

        private void OnEditClick()
        {
            UserView user = SelectedUser();
            if (user == null)
            {
                Theme.ShowInfo("اختر مستخدمًا من القائمة أولاً.");
                return;
            }
            FrmUserEdit dialog = new FrmUserEdit(user);
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                Reload();
            }
        }

        private void OnToggleClick()
        {
            UserView user = SelectedUser();
            if (user == null)
            {
                Theme.ShowInfo("اختر مستخدمًا من القائمة أولاً.");
                return;
            }
            bool disable = user.IsDisabled != true;
            if (!Theme.Confirm(disable ?
                "إيقاف الحساب '" + user.Username + "؟ لن يتمكن من تسجيل الدخول." :
                "تفعيل الحساب '" + user.Username + "؟"))
            {
                return;
            }
            try
            {
                UserBLL.SetDisabled(user.UserId, disable);
                Reload();
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmUsers.OnToggleClick");
                Theme.ShowError("حدث خطأ أثناء تغيير حالة الحساب.");
            }
        }

        private void OnUnlockClick()
        {
            UserView user = SelectedUser();
            if (user == null)
            {
                Theme.ShowInfo("اختر مستخدمًا من القائمة أولاً.");
                return;
            }
            try
            {
                UserBLL.Unlock(user.UserId);
                Theme.ShowInfo("تم فتح الحساب وإعادة تصفير المحاولات الفاشلة.");
                Reload();
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmUsers.OnUnlockClick");
                Theme.ShowError("حدث خطأ أثناء فتح الحساب.");
            }
        }

        private void OnResetPasswordClick()
        {
            UserView user = SelectedUser();
            if (user == null)
            {
                Theme.ShowInfo("اختر مستخدمًا من القائمة أولاً.");
                return;
            }
            if (!Theme.Confirm("إعادة تعيين كلمة مرور المستخدم '" + user.Username + "؟ سيتم تسجيل العملية في سجل العمليات."))
            {
                return;
            }
            string newPassword = PromptPassword();
            if (newPassword == null)
            {
                return;
            }
            try
            {
                UserBLL.ResetPassword(user.UserId, newPassword);
                Theme.ShowInfo("تم تعيين كلمة المرور الجديدة.");
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmUsers.OnResetPasswordClick");
                Theme.ShowError("حدث خطأ أثناء إعادة تعيين كلمة المرور.");
            }
        }

        /// <summary>Small password prompt for the admin reset flow (policy validated by UserBLL).</summary>
        private string PromptPassword()
        {
            Form prompt = new Form();
            Theme.Apply(prompt);
            prompt.Text = "كلمة مرور جديدة";
            prompt.ClientSize = new Size(360, 150);
            prompt.FormBorderStyle = FormBorderStyle.FixedDialog;
            prompt.MinimizeBox = false;
            prompt.MaximizeBox = false;
            prompt.StartPosition = FormStartPosition.CenterParent;

            Label lbl = new Label();
            lbl.Text = "كلمة المرور الجديدة (8 أحرف على الأقل، حروف وأرقام):";
            lbl.Location = new Point(15, 15);
            lbl.AutoSize = true;

            TextBox txt = new TextBox();
            txt.Location = new Point(15, 45);
            txt.Width = 320;

            Button ok = new Button();
            ok.Text = "تعيين";
            ok.DialogResult = DialogResult.OK;
            ok.Location = new Point(205, 95);
            ok.Width = 130;

            Button cancel = new Button();
            cancel.Text = "إلغاء";
            cancel.DialogResult = DialogResult.Cancel;
            cancel.Location = new Point(15, 95);
            cancel.Width = 130;

            prompt.Controls.Add(lbl);
            prompt.Controls.Add(txt);
            prompt.Controls.Add(ok);
            prompt.Controls.Add(cancel);
            prompt.AcceptButton = ok;
            prompt.CancelButton = cancel;

            if (prompt.ShowDialog(this) == DialogResult.OK)
            {
                return txt.Text;
            }
            return null;
        }

        private void ShowForm(Form form)
        {
            form.ShowDialog(this);
            Reload();
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
