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
    /// Permission matrix (rule 21 + BR-19 Default-Deny, permission
    /// "Users.Manage"). Section 1 edits the role matrix (Admin role is read
    /// only — it is IsAdmin and owns every permission). Section 2 edits
    /// per-user overrides on top of the role matrix: Default / Granted /
    /// Revoked, cleared back to Default with "Remove Override".
    /// </summary>
    public class FrmPermissions : Form
    {
        private ComboBox _cboRole;
        private DataGridView _gridRole;
        private List<Permission> _catalog;
        private List<Role> _roleCache;
        private Dictionary<string, bool> _roleGranted;
        private Dictionary<string, int> _permissionIdByKey;

        private ComboBox _cboUser;
        private DataGridView _gridUser;
        private List<UserView> _users;
        private Dictionary<string, bool?> _userOverrides;

        public FrmPermissions()
        {
            Theme.Apply(this);
            Text = "مصفوفة الصلاحيات";
            ClientSize = new Size(760, 700);
            StartPosition = FormStartPosition.CenterParent;

            Panel header = new Panel();
            Label lblTitle = new Label();
            lblTitle.Text = "صلاحيات الأدوار + التجاوزات على مستوى المستخدم";
            lblTitle.ForeColor = Theme.HeaderTextColor;
            lblTitle.Font = Theme.Font(12f);
            lblTitle.Dock = DockStyle.Fill;
            lblTitle.TextAlign = ContentAlignment.MiddleCenter;
            Theme.StyleHeader(header);
            header.Controls.Add(lblTitle);
            header.Dock = DockStyle.Top;

            Panel rolePane = new Panel();
            rolePane.Dock = DockStyle.Top;
            rolePane.Height = 320;

            FlowLayoutPanel roleBar = new FlowLayoutPanel();
            roleBar.Dock = DockStyle.Top;
            roleBar.Height = 46;
            roleBar.FlowDirection = FlowDirection.RightToLeft;
            roleBar.WrapContents = false;
            roleBar.Padding = new Padding(6, 8, 6, 4);

            Label lblRole = new Label();
            lblRole.Text = "الدور:";
            lblRole.AutoSize = true;
            lblRole.Margin = new Padding(3, 10, 3, 0);

            _cboRole = new ComboBox();
            _cboRole.DropDownStyle = ComboBoxStyle.DropDownList;
            _cboRole.Width = 160;
            _cboRole.SelectedIndexChanged += delegate { LoadRoleMatrix(); };
            _cboRole.Margin = new Padding(3, 8, 3, 0);

            Button btnApplyRole = new Button();
            btnApplyRole.Text = "تطبيق على الدور";
            btnApplyRole.Height = 30;
            btnApplyRole.Click += delegate { ApplyRoleChanges(); };

            roleBar.Controls.Add(lblRole);
            roleBar.Controls.Add(_cboRole);
            roleBar.Controls.Add(btnApplyRole);

            _gridRole = new DataGridView();
            Theme.StyleGrid(_gridRole);
            _gridRole.Dock = DockStyle.Fill;

            rolePane.Controls.Add(_gridRole);
            rolePane.Controls.Add(roleBar);

            Panel userBar = new FlowLayoutPanel();
            userBar.Dock = DockStyle.Top;
            userBar.Height = 46;
            userBar.FlowDirection = FlowDirection.RightToLeft;
            userBar.WrapContents = false;
            userBar.Padding = new Padding(6, 8, 6, 4);

            Label lblUser = new Label();
            lblUser.Text = "المستخدم:";
            lblUser.AutoSize = true;
            lblUser.Margin = new Padding(3, 10, 3, 0);

            _cboUser = new ComboBox();
            _cboUser.DropDownStyle = ComboBoxStyle.DropDownList;
            _cboUser.Width = 160;
            _cboUser.SelectedIndexChanged += delegate { LoadUserOverrides(); };
            _cboUser.Margin = new Padding(3, 8, 3, 0);

            Button btnApplyUser = new Button();
            btnApplyUser.Text = "تطبيق التجاوزات";
            btnApplyUser.Height = 30;
            btnApplyUser.Click += delegate { ApplyUserChanges(); };

            userBar.Controls.Add(lblUser);
            userBar.Controls.Add(_cboUser);
            userBar.Controls.Add(btnApplyUser);

            Panel userPane = new Panel();
            userPane.Dock = DockStyle.Fill;
            userPane.Controls.Add(_gridUserPrepared());
            userPane.Controls.Add(userBar);

            Controls.Add(userPane);
            Controls.Add(rolePane);
            Controls.Add(header);

            LoadCatalog();
            LoadRoles();
            LoadUsers();
        }

        private DataGridView _gridUserPrepared()
        {
            _gridUser = new DataGridView();
            Theme.StyleGrid(_gridUser);
            _gridUser.Dock = DockStyle.Fill;
            _gridUser.ReadOnly = true;
            _gridUser.AllowUserToAddRows = false;
            _gridUser.AllowUserToDeleteRows = false;
            _gridUser.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            return _gridUser;
        }

        private void LoadCatalog()
        {
            try
            {
                _catalog = UserBLL.GetPermissionsCatalog();
                _permissionIdByKey = new Dictionary<string, int>();
                foreach (Permission permission in _catalog)
                {
                    _permissionIdByKey[permission.PermissionKey] = permission.PermissionId;
                }
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmPermissions.LoadCatalog");
                Theme.ShowError("حدث خطأ أثناء تحميل الصلاحيات.");
            }
        }

        private void LoadRoles()
        {
            try
            {
                _roleCache = UserBLL.GetRoles();
                Dictionary<int, string> items = new Dictionary<int, string>();
                foreach (Role role in _roleCache)
                {
                    items.Add(role.RoleId, role.RoleName + (role.IsAdmin == true ? " (كل الصلاحيات)" : ""));
                }
                _cboRole.DataSource = new BindingSource(items, null);
                _cboRole.DisplayMember = "Value";
                _cboRole.ValueMember = "Key";
                if (_cboRole.SelectedIndex < 0 && _cboRole.Items.Count > 0)
                {
                    _cboRole.SelectedIndex = 0;
                }
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmPermissions.LoadRoles");
                Theme.ShowError("حدث خطأ أثناء تحميل الأدوار.");
            }
        }

        private void LoadUsers()
        {
            try
            {
                _users = UserBLL.ListUsers();
                Dictionary<int, string> items = new Dictionary<int, string>();
                foreach (UserView user in _users)
                {
                    items.Add(user.UserId, user.Username + (user.IsDisabled == true ? " (موقوف)" : ""));
                }
                _cboUser.DataSource = new BindingSource(items, null);
                _cboUser.DisplayMember = "Value";
                _cboUser.ValueMember = "Key";
                if (_cboUser.SelectedIndex < 0 && _cboUser.Items.Count > 0)
                {
                    _cboUser.SelectedIndex = 0;
                }
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmPermissions.LoadUsers");
                Theme.ShowError("حدث خطأ أثناء تحميل المستخدمين.");
            }
        }

        private bool CurrentRoleIsAdmin()
        {
            int? roleId = _cboRole.SelectedValue as int?;
            if (roleId == null)
            {
                return false;
            }
            if (_roleCache == null)
            {
                return false;
            }
            foreach (Role role in _roleCache)
            {
                if (role.RoleId == roleId.Value)
                {
                    return role.IsAdmin == true;
                }
            }
            return false;
        }

        private void LoadRoleMatrix()
        {
            try
            {
                if (_catalog == null)
                {
                    return;
                }
                int? roleId = _cboRole.SelectedValue as int?;
                if (roleId == null)
                {
                    return;
                }
                List<string> keys = UserBLL.GetRoleKeys(roleId.Value);
                _roleGranted = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
                foreach (string key in keys)
                {
                    _roleGranted[key] = true;
                }
                bool isAdminRole = CurrentRoleIsAdmin();
                BuildMatrixGrid(_gridRole, isAdminRole, isAdminRole);
                foreach (Permission permission in _catalog)
                {
                    int rowIndex = _gridRole.Rows.Add();
                    DataGridViewRow row = _gridRole.Rows[rowIndex];
                    row.Cells["Granted"].Value = _roleGranted.ContainsKey(permission.PermissionKey);
                    row.Cells["Key"].Value = permission.PermissionKey;
                    row.Cells["Desc"].Value = permission.DescriptionAr;
                }
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmPermissions.LoadRoleMatrix");
                Theme.ShowError("حدث خطأ أثناء تحميل مصفوفة الدور.");
            }
        }

        /// <summary>
        /// Builds a matrix grid: checkbox (or override combo) + key + Arabic
        /// description. readOnlyAll locks every cell (Admin role display).
        /// </summary>
        private void BuildMatrixGrid(DataGridView grid, bool readOnlyAll, bool useOverrideCell)
        {
            grid.Columns.Clear();
            grid.Rows.Clear();
            DataGridViewCheckBoxColumn granted = new DataGridViewCheckBoxColumn();
            granted.Name = "Granted";
            granted.HeaderText = useOverrideCell ? "الوضع" : "ممنوح";
            granted.FillWeight = 34;
            granted.ReadOnly = readOnlyAll;
            grid.Columns.Add(granted);

            AddTextColumn(grid, "Key", "المفتاح", 140, true);
            AddTextColumn(grid, "Desc", "الوصف", 220, true);
            if (!readOnlyAll)
            {
                grid.ReadOnly = false;
            }
        }

        private void AddTextColumn(DataGridView grid, string name, string header, int fillWeight, bool readOnly)
        {
            DataGridViewTextBoxColumn column = new DataGridViewTextBoxColumn();
            column.Name = name;
            column.HeaderText = header;
            column.FillWeight = fillWeight;
            column.SortMode = DataGridViewColumnSortMode.NotSortable;
            column.ReadOnly = readOnly;
            grid.Columns.Add(column);
        }

        private void ApplyRoleChanges()
        {
            try
            {
                int? roleId = _cboRole.SelectedValue as int?;
                if (roleId == null || _roleGranted == null)
                {
                    Theme.ShowInfo("اختر دورًا أولاً.");
                    return;
                }
                if (CurrentRoleIsAdmin())
                {
                    Theme.ShowInfo("دور Admin يملك جميع الصلاحيات ولا يحتاج إلى تعديل.");
                    return;
                }
                int changes = 0;
                List<string> processed = new List<string>();
                foreach (DataGridViewRow row in _gridRole.Rows)
                {
                    string key = Convert.ToString(row.Cells["Key"].Value);
                    bool granted = row.Cells["Granted"].Value != null && (bool)row.Cells["Granted"].Value;
                    processed.Add(key);
                    bool wasGranted = _roleGranted.ContainsKey(key);
                    if (wasGranted != granted && _permissionIdByKey.ContainsKey(key))
                    {
                        UserBLL.SetRolePermission(roleId.Value, _permissionIdByKey[key], granted);
                        changes++;
                    }
                }
                Theme.ShowInfo("تم تطبيق " + changes + " تغييرًا على دور الصلاحيات.");
                LoadRoleMatrix();
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmPermissions.ApplyRoleChanges");
                Theme.ShowError("حدث خطأ أثناء تطبيق تغييرات الدور.");
            }
        }

        private void LoadUserOverrides()
        {
            try
            {
                if (_catalog == null)
                {
                    return;
                }
                int? userId = _cboUser.SelectedValue as int?;
                if (userId == null)
                {
                    return;
                }
                _userOverrides = new Dictionary<string, bool?>(StringComparer.OrdinalIgnoreCase);
                foreach (KeyValuePair<string, bool> pair in UserBLL.GetUserOverrides(userId.Value))
                {
                    _userOverrides[pair.Key] = pair.Value;
                }
                bool targetIsAdmin = false;
                UserView selected = null;
                foreach (UserView user in _users)
                {
                    if (user.UserId == userId.Value)
                    {
                        selected = user;
                        break;
                    }
                }
                if (selected != null && selected.RoleId != null)
                {
                    foreach (Role role in _roleCache)
                    {
                        if (role.RoleId == selected.RoleId.Value)
                        {
                            targetIsAdmin = role.IsAdmin == true;
                            break;
                        }
                    }
                }
                _gridUser.Columns.Clear();
                _gridUser.Rows.Clear();

                DataGridViewComboBoxColumn mode = new DataGridViewComboBoxColumn();
                mode.Name = "Mode";
                mode.HeaderText = "الوضع";
                mode.FillWeight = 50;
                mode.FlatStyle = FlatStyle.Flat;
                mode.Items.Add("افتراضي");
                mode.Items.Add("منح");
                mode.Items.Add("سحب");
                mode.ReadOnly = targetIsAdmin;
                _gridUser.Columns.Add(mode);

                AddTextColumn(_gridUser, "Key", "المفتاح", 140, true);
                AddTextColumn(_gridUser, "Desc", "الوصف", 220, true);
                _gridUser.ReadOnly = targetIsAdmin;

                foreach (Permission permission in _catalog)
                {
                    int rowIndex = _gridUser.Rows.Add();
                    DataGridViewRow row = _gridUser.Rows[rowIndex];
                    bool? state;
                    _userOverrides.TryGetValue(permission.PermissionKey, out state);
                    row.Cells["Mode"].Value = state == null ? "افتراضي" :
                        (state.Value ? "منح" : "سحب");
                    row.Cells["Key"].Value = permission.PermissionKey;
                    row.Cells["Desc"].Value = permission.DescriptionAr;
                }
                if (targetIsAdmin)
                {
                    Theme.ShowInfo("طالما المستخدم بدور Admin فصلاحياته كاملة ولا تُطبَّق التجاوزات.");
                }
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmPermissions.LoadUserOverrides");
                Theme.ShowError("حدث خطأ أثناء تحميل تجاوزات المستخدم.");
            }
        }

        private void ApplyUserChanges()
        {
            try
            {
                int? userId = _cboUser.SelectedValue as int?;
                if (userId == null || _userOverrides == null)
                {
                    Theme.ShowInfo("اختر مستخدمًا أولاً.");
                    return;
                }
                int changes = 0;
                foreach (DataGridViewRow row in _gridUser.Rows)
                {
                    string key = Convert.ToString(row.Cells["Key"].Value);
                    string mode = Convert.ToString(row.Cells["Mode"].Value);
                    bool? wanted = mode == "منح" ? true :
                        (mode == "سحب" ? false : (bool?)null);
                    bool? current;
                    _userOverrides.TryGetValue(key, out current);
                    bool same = (current == null && wanted == null) ||
                        (current != null && wanted != null && current.Value == wanted.Value);
                    if (!same && _permissionIdByKey.ContainsKey(key))
                    {
                        UserBLL.SetUserOverride(userId.Value, _permissionIdByKey[key], wanted);
                        changes++;
                    }
                }
                Theme.ShowInfo("تم تطبيق " + changes + " تجاوزًا.");
                LoadUserOverrides();
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmPermissions.ApplyUserChanges");
                Theme.ShowError("حدث خطأ أثناء تطبيق التجاوزات.");
            }
        }
    }
}
