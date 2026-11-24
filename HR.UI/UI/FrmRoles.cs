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
    /// Roles management (file tab, "Users.Manage"): create roles, rename them
    /// and delete unused ones. UserPermission matrix stays in the existing
    /// permissions screen; admin users are demoted through FrmUsers — both
    /// guards are enforced in UserBLL. New roles complement the three seeded
    /// ones that previously could not be extended at runtime.
    /// </summary>
    public class FrmRoles : Form
    {
        private DataGridView _grid;
        private List<Role> _roles;

        public FrmRoles()
        {
            Theme.Apply(this, false); // MDI child: mirrored layout is not applied — RTL flow comes from the containers
            Text = "إدارة الأدوار";
            ClientSize = new Size(640, 500);
            StartPosition = FormStartPosition.CenterParent;

            Panel header = new Panel();
            Label lblTitle = new Label();
            lblTitle.Text = "أدوار المستخدمين — الأدوار الجديدة تُمنح صلاحياتها من شاشة الصلاحيات";
            lblTitle.ForeColor = Theme.HeaderTextColor;
            lblTitle.Font = Theme.Font(12f);
            lblTitle.Dock = DockStyle.Fill;
            lblTitle.TextAlign = ContentAlignment.MiddleCenter;
            Theme.StyleHeader(header);
            header.Controls.Add(lblTitle);
            header.Dock = DockStyle.Top;

            FlowLayoutPanel toolbar = new FlowLayoutPanel();
            toolbar.Dock = DockStyle.Top;
            toolbar.Height = 56;
            toolbar.BackColor = Theme.ToolbarColor;
            // FlowLayoutPanel: RTL=Yes (inherited from the form) already reverses the flow;
            // an explicit RightToLeft cancelled that reversal and rendered the bar left-to-right.
            toolbar.FlowDirection = FlowDirection.LeftToRight;
            toolbar.WrapContents = false;
            toolbar.Padding = new Padding(6, 8, 6, 4);

            Button btnAdd = new Button();
            btnAdd.Text = "دور جديد";
            btnAdd.Height = 30;
            btnAdd.Click += delegate { OnAddClick(); };

            Button btnRename = new Button();
            btnRename.Text = "إعادة تسمية";
            btnRename.Height = 30;
            btnRename.Click += delegate { OnRenameClick(); };

            Button btnDelete = new Button();
            btnDelete.Text = "حذف";
            btnDelete.Height = 30;
            btnDelete.Click += delegate { OnDeleteClick(); };

            Button btnPermissions = new Button();
            btnPermissions.Text = "منح الصلاحيات…";
            btnPermissions.Height = 30;
            btnPermissions.Click += delegate { OnPermissionsClick(); };

            toolbar.Controls.Add(btnAdd);
            toolbar.Controls.Add(btnRename);
            toolbar.Controls.Add(btnDelete);
            toolbar.Controls.Add(btnPermissions);

            _grid = new DataGridView();
            Theme.StyleGrid(_grid);
            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _grid.CellDoubleClick += delegate { OnRenameClick(); };

            Controls.Add(_grid);
            Controls.Add(toolbar);
            Controls.Add(header);

            Reload();
        }

        private Role SelectedRole()
        {
            if (_grid.CurrentRow == null)
            {
                return null;
            }
            return _grid.CurrentRow.Tag as Role;
        }

        private void Reload()
        {
            try
            {
                _roles = UserBLL.GetRoles();
                _grid.Columns.Clear();
                _grid.Rows.Clear();

                AddColumn("RoleName", "الدور", 300);
                AddColumn("Kind", "النوع", 120);
                AddColumn("Users", "عدد المستخدمين", 140);
                foreach (Role role in _roles)
                {
                    int rowIndex = _grid.Rows.Add();
                    DataGridViewRow row = _grid.Rows[rowIndex];
                    row.Tag = role;
                    row.Cells["RoleName"].Value = role.RoleName;
                    row.Cells["Kind"].Value = role.IsAdmin == true ? "مدمج (مشرف)" : "عادي";
                    row.Cells["Users"].Value = UserBLL.CountUsersInRole(role.RoleId).ToString();
                }
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmRoles.Reload");
                Theme.ShowError("حدث خطأ أثناء تحميل الأدوار.");
            }
        }

        private void OnAddClick()
        {
            FrmRoleEdit dialog = new FrmRoleEdit(null);
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                Reload();
            }
        }

        private void OnRenameClick()
        {
            Role role = SelectedRole();
            if (role == null)
            {
                Theme.ShowInfo("اختر دوراً من القائمة أولاً.");
                return;
            }
            FrmRoleEdit dialog = new FrmRoleEdit(role);
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                Reload();
            }
        }

        private void OnDeleteClick()
        {
            Role role = SelectedRole();
            if (role == null)
            {
                Theme.ShowInfo("اختر دوراً من القائمة أولاً.");
                return;
            }
            if (!Theme.Confirm("حذف الدور '" + role.RoleName + "'؟\nلا يمكن الحذف إن كان مرتبطاً بمستخدمين أو دور مشرف مدمج."))
            {
                return;
            }
            try
            {
                UserBLL.DeleteRole(role.RoleId);
                Reload();
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmRoles.OnDeleteClick");
                Theme.ShowError("حدث خطأ أثناء حذف الدور.");
            }
        }

        private void OnPermissionsClick()
        {
            FrmPermissions dialog = new FrmPermissions();
            dialog.ShowDialog(this);
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
