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
    /// User create / profile dialog (rule 21). Add mode collects username,
    /// full name, role, department and an initial password (validated by
    /// UserBLL, stored SHA-256 + salt). Edit mode updates the profile only;
    /// password changes go through "Change Password" or the admin reset.
    /// </summary>
    public class FrmUserEdit : Form
    {
        private readonly UserView _user;
        private TextBox _txtUsername;
        private TextBox _txtFullName;
        private ComboBox _cboRole;
        private ComboBox _cboDepartment;
        private TextBox _txtPassword;
        private TextBox _txtConfirm;
        private Button _btnSave;
        private Button _btnCancel;

        public FrmUserEdit(UserView user)
        {
            _user = user;
            Theme.Apply(this);
            Text = user == null ? "مستخدم جديد" : "تعديل مستخدم";
            ClientSize = new Size(470, user == null ? 330 : 240);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterParent;

            bool isAdd = user == null;

            Label lblUsername = new Label();
            lblUsername.Text = "اسم المستخدم:";
            lblUsername.Location = new Point(20, 25);
            lblUsername.Size = new Size(130, 22);

            _txtUsername = new TextBox();
            _txtUsername.Location = new Point(155, 22);
            _txtUsername.Width = 280;
            _txtUsername.Text = isAdd ? "" : user.Username;
            _txtUsername.RightToLeft = RightToLeft.No;

            Label lblFullName = new Label();
            lblFullName.Text = "الاسم الكامل:";
            lblFullName.Location = new Point(20, 65);
            lblFullName.Size = new Size(130, 22);

            _txtFullName = new TextBox();
            _txtFullName.Location = new Point(155, 62);
            _txtFullName.Width = 280;
            _txtFullName.Text = isAdd ? "" : user.FullName;

            Label lblRole = new Label();
            lblRole.Text = "الدور:";
            lblRole.Location = new Point(20, 105);
            lblRole.Size = new Size(130, 22);

            _cboRole = new ComboBox();
            _cboRole.DropDownStyle = ComboBoxStyle.DropDownList;
            _cboRole.Location = new Point(155, 102);
            _cboRole.Width = 280;
            LoadRoles();

            Label lblDepartment = new Label();
            lblDepartment.Text = "القسم (اختياري):";
            lblDepartment.Location = new Point(20, 145);
            lblDepartment.Size = new Size(130, 22);

            _cboDepartment = new ComboBox();
            _cboDepartment.DropDownStyle = ComboBoxStyle.DropDownList;
            _cboDepartment.Location = new Point(155, 142);
            _cboDepartment.Width = 280;
            LoadDepartments();

            int yPassword = 185;
            if (isAdd)
            {
                Label lblPassword = new Label();
                lblPassword.Text = "كلمة المرور:";
                lblPassword.Location = new Point(20, yPassword + 3);
                lblPassword.Size = new Size(130, 22);

                _txtPassword = new TextBox();
                _txtPassword.Location = new Point(155, yPassword);
                _txtPassword.Width = 280;
                _txtPassword.UseSystemPasswordChar = true;
                _txtPassword.RightToLeft = RightToLeft.No;

                Label lblConfirm = new Label();
                lblConfirm.Text = "تأكيد كلمة المرور:";
                lblConfirm.Location = new Point(20, yPassword + 40);
                lblConfirm.Size = new Size(130, 22);

                _txtConfirm = new TextBox();
                _txtConfirm.Location = new Point(155, yPassword + 37);
                _txtConfirm.Width = 280;
                _txtConfirm.UseSystemPasswordChar = true;
                _txtConfirm.RightToLeft = RightToLeft.No;

                Controls.Add(lblPassword);
                Controls.Add(_txtPassword);
                Controls.Add(lblConfirm);
                Controls.Add(_txtConfirm);
            }

            _btnSave = new Button();
            _btnSave.Text = "حفظ";
            _btnSave.Location = new Point(305, 275);
            _btnSave.Size = new Size(130, 34);
            _btnSave.Click += delegate { Save(); };

            _btnCancel = new Button();
            _btnCancel.Text = "إلغاء";
            _btnCancel.Location = new Point(20, 275);
            _btnCancel.Size = new Size(130, 34);
            _btnCancel.DialogResult = DialogResult.Cancel;

            Controls.Add(lblUsername);
            Controls.Add(_txtUsername);
            Controls.Add(lblFullName);
            Controls.Add(_txtFullName);
            Controls.Add(lblRole);
            Controls.Add(_cboRole);
            Controls.Add(lblDepartment);
            Controls.Add(_cboDepartment);
            Controls.Add(_btnSave);
            Controls.Add(_btnCancel);
            AcceptButton = _btnSave;
            CancelButton = _btnCancel;
        }

        private void LoadRoles()
        {
            try
            {
                List<Role> roles = UserBLL.GetRoles();
                Dictionary<int, string> items = new Dictionary<int, string>();
                foreach (Role role in roles)
                {
                    items.Add(role.RoleId, role.RoleName + (role.IsAdmin == true ? " (مسؤول)" : ""));
                }
                _cboRole.DataSource = new BindingSource(items, null);
                _cboRole.DisplayMember = "Value";
                _cboRole.ValueMember = "Key";
                if (_user != null && _user.RoleId != null && items.ContainsKey(_user.RoleId.Value))
                {
                    _cboRole.SelectedValue = _user.RoleId.Value;
                }
                if (_cboRole.SelectedIndex < 0 && _cboRole.Items.Count > 0)
                {
                    _cboRole.SelectedIndex = 0;
                }
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmUserEdit.LoadRoles");
                Theme.ShowError("حدث خطأ أثناء تحميل الأدوار.");
            }
        }

        private void LoadDepartments()
        {
            try
            {
                List<Department> departments = OrganizationBLL.Departments(true);
                Dictionary<int, string> items = new Dictionary<int, string>();
                items.Add(0, "— بلا قسم —");
                foreach (Department department in departments)
                {
                    items.Add(department.DepartmentId, department.DepartmentName);
                }
                _cboDepartment.DataSource = new BindingSource(items, null);
                _cboDepartment.DisplayMember = "Value";
                _cboDepartment.ValueMember = "Key";
                if (_user != null && _user.DeptId != null && items.ContainsKey(_user.DeptId.Value))
                {
                    _cboDepartment.SelectedValue = _user.DeptId.Value;
                }
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmUserEdit.LoadDepartments");
                Theme.ShowError("حدث خطأ أثناء تحميل الأقسام.");
            }
        }

        private void Save()
        {
            try
            {
                string username = (_txtUsername.Text ?? string.Empty).Trim();
                string fullName = (_txtFullName.Text ?? string.Empty).Trim();
                if (!(_cboRole.SelectedValue is int) || (int)_cboRole.SelectedValue == 0)
                {
                    Theme.ShowError("اختر دورًا للمستخدم.");
                    return;
                }
                int roleId = (int)_cboRole.SelectedValue;
                int deptId = _cboDepartment.SelectedValue is int ? (int)_cboDepartment.SelectedValue : 0;
                int? deptValue = deptId == 0 ? (int?)null : deptId;

                if (_user == null)
                {
                    if (_txtPassword.Text != _txtConfirm.Text)
                    {
                        Theme.ShowError("كلمة المرور وتأكيدها غير متطابقين.");
                        return;
                    }
                    UserBLL.CreateUser(username, fullName, roleId, _txtPassword.Text, deptValue);
                }
                else
                {
                    UserBLL.UpdateUser(_user.UserId, username, fullName, roleId, deptValue);
                }
                DialogResult = DialogResult.OK;
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmUserEdit.Save");
                Theme.ShowError("حدث خطأ أثناء حفظ المستخدم.");
            }
        }
    }
}
