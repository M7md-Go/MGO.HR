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
    /// Employees screen (rule 8): search, filters, paging, add/edit, suspend,
    /// activate, archive — with permission-aware toolbar buttons.
    /// </summary>
    public class FrmEmployees : Form
    {
        private DataGridView _grid;
        private TextBox _txtSearch;
        private ComboBox _cboDepartment;
        private ComboBox _cboStatus;
        private Button _btnSearch;
        private Button _btnMore;
        private Button _btnAdd;
        private Button _btnEdit;
        private Button _btnSuspend;
        private Button _btnActivate;
        private Button _btnArchive;
        private List<Employee> _rows;
        private Dictionary<int, string> _departmentNames;

        public FrmEmployees()
        {
            Theme.Apply(this, false); // MDI child: mirrored layout is not applied — RTL flow comes from the containers
            Text = "الموظفون";
            ClientSize = new Size(1040, 620);
            StartPosition = FormStartPosition.CenterParent;

            Panel header = new Panel();
            Label lblTitle = new Label();
            lblTitle.Text = "إدارة الموظفين";
            lblTitle.ForeColor = Theme.HeaderTextColor;
            lblTitle.Font = Theme.Font(12f);
            lblTitle.Dock = DockStyle.Fill;
            lblTitle.TextAlign = ContentAlignment.MiddleCenter;
            Theme.StyleHeader(header);
            header.Controls.Add(lblTitle);
            header.Dock = DockStyle.Top;

            FlowLayoutPanel toolbar = new FlowLayoutPanel();
            toolbar.Dock = DockStyle.Top;
            toolbar.Height = 46;
            toolbar.BackColor = Theme.ToolbarColor;
            // FlowLayoutPanel: RTL=Yes (inherited from the form) already reverses the flow;
            // an explicit RightToLeft cancelled that reversal and rendered the bar left-to-right.
            toolbar.FlowDirection = FlowDirection.LeftToRight;
            toolbar.Padding = new Padding(6, 8, 6, 4);

            _txtSearch = new TextBox();
            _txtSearch.Width = 160;
            _txtSearch.Name = "txtSearch";
            _txtSearch.TabIndex = 1;
            _txtSearch.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter)
                {
                    Search(null);
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
            };

            _cboDepartment = new ComboBox();
            _cboDepartment.Width = 150;
            _cboDepartment.DropDownStyle = ComboBoxStyle.DropDownList;

            _cboStatus = new ComboBox();
            _cboStatus.Width = 110;
            _cboStatus.DropDownStyle = ComboBoxStyle.DropDownList;
            _cboStatus.Items.Add("الكل");
            _cboStatus.Items.Add("على رأس العمل");
            _cboStatus.Items.Add("موقوف");
            _cboStatus.Items.Add("مؤرشف");
            _cboStatus.SelectedIndex = 1;

            _btnSearch = new Button();
            _btnSearch.Text = "بحث";
            _btnSearch.Height = 30;
            _btnSearch.Click += delegate { Search(null); };

            _btnMore = new Button();
            _btnMore.Text = "تحميل المزيد";
            _btnMore.Height = 30;
            _btnMore.Visible = false;
            _btnMore.Click += delegate { Search(LastRowCode()); };

            _btnAdd = new Button();
            _btnAdd.Text = "إضافة موظف";
            _btnAdd.Height = 30;
            _btnAdd.Click += OnAddClick;

            _btnEdit = new Button();
            _btnEdit.Text = "تعديل";
            _btnEdit.Height = 30;
            _btnEdit.Click += OnEditClick;

            _btnSuspend = new Button();
            _btnSuspend.Text = "إيقاف";
            _btnSuspend.Height = 30;
            _btnSuspend.Click += OnSuspendClick;

            _btnActivate = new Button();
            _btnActivate.Text = "إعادة تفعيل";
            _btnActivate.Height = 30;
            _btnActivate.Click += OnActivateClick;

            _btnArchive = new Button();
            _btnArchive.Text = "أرشفة";
            _btnArchive.Height = 30;
            _btnArchive.Click += OnArchiveClick;

            toolbar.Controls.Add(_txtSearch);
            toolbar.Controls.Add(_cboDepartment);
            toolbar.Controls.Add(_cboStatus);
            toolbar.Controls.Add(_btnSearch);
            toolbar.Controls.Add(_btnMore);
            toolbar.Controls.Add(_btnAdd);
            toolbar.Controls.Add(_btnEdit);
            toolbar.Controls.Add(_btnSuspend);
            toolbar.Controls.Add(_btnActivate);
            toolbar.Controls.Add(_btnArchive);

            _grid = new DataGridView();
            Theme.StyleGrid(_grid);
            _grid.Dock = DockStyle.Fill;
            _grid.CellDoubleClick += delegate(object sender, DataGridViewCellEventArgs e)
            {
                if (e.RowIndex >= 0)
                {
                    OnEditClick(null, null);
                }
            };

            Controls.Add(_grid);
            Controls.Add(toolbar);
            Controls.Add(header);

            ApplyPermissionButtons();
            BuildGridContextMenu();
            Shown += delegate { LoadLookups(); Search(null); };
        }

        /// <summary>Right-click grid menu: toolbar operations plus print.</summary>
        private void BuildGridContextMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.RightToLeft = RightToLeft.Yes;
            menu.Font = Theme.Font(9.5f);
            ToolStripMenuItem add = new ToolStripMenuItem("إضافة موظف");
            add.Click += OnAddClick;
            ToolStripMenuItem edit = new ToolStripMenuItem("تعديل");
            edit.Click += OnEditClick;
            ToolStripMenuItem activate = new ToolStripMenuItem("تفعيل");
            activate.Click += OnActivateClick;
            ToolStripMenuItem suspend = new ToolStripMenuItem("إيقاف");
            suspend.Click += OnSuspendClick;
            ToolStripMenuItem archive = new ToolStripMenuItem("أرشفة");
            archive.Click += OnArchiveClick;
            ToolStripMenuItem print = new ToolStripMenuItem("طباعة");
            print.Click += delegate { GridPrintHelper.Print(_grid, "قائمة الموظفين"); };
            menu.Items.Add(add);
            menu.Items.Add(edit);
            menu.Items.Add(activate);
            menu.Items.Add(suspend);
            menu.Items.Add(archive);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(print);
            _grid.ContextMenuStrip = menu;
        }

        private void ApplyPermissionButtons()
        {
            _btnAdd.Enabled = AppContext.HasPermission("Employees.Add");
            _btnEdit.Enabled = AppContext.HasPermission("Employees.Edit");
            _btnSuspend.Enabled = AppContext.HasPermission("Employees.Edit");
            _btnActivate.Enabled = AppContext.HasPermission("Employees.Edit");
            _btnArchive.Enabled = AppContext.HasPermission("Employees.Archive");
        }

        private void LoadLookups()
        {
            _cboDepartment.Items.Clear();
            _cboDepartment.Items.Add("كل الأقسام");
            _departmentNames = new Dictionary<int, string>();
            foreach (Department department in OrganizationBLL.Departments(false))
            {
                _cboDepartment.Items.Add(department.DepartmentName);
                _departmentNames[department.DepartmentId] = department.DepartmentName;
            }
            _cboDepartment.SelectedIndex = 0;
        }

        private string StatusFilterText()
        {
            switch (_cboStatus.SelectedIndex)
            {
                case 1: return EmployeeBLL.StatusActive;
                case 2: return EmployeeBLL.StatusSuspended;
                case 3: return EmployeeBLL.StatusArchived;
                default: return null;
            }
        }

        private string LastRowCode()
        {
            if (_rows == null || _rows.Count == 0)
            {
                return null;
            }
            return _rows[_rows.Count - 1].EmployeeCode;
        }

        private void Search(string lastCode)
        {
            try
            {
                EmployeeSearchFilter filter = new EmployeeSearchFilter();
                filter.Text = _txtSearch.Text;
                if (_cboDepartment.SelectedIndex > 0)
                {
                    foreach (KeyValuePair<int, string> pair in _departmentNames)
                    {
                        if (string.Equals(pair.Value, Convert.ToString(_cboDepartment.SelectedItem), StringComparison.Ordinal))
                        {
                            filter.DepartmentId = pair.Key;
                            break;
                        }
                    }
                }
                filter.Status = StatusFilterText();
                filter.IncludeArchived = string.Equals(filter.Status, EmployeeBLL.StatusArchived, StringComparison.Ordinal) ||
                                         _cboStatus.SelectedIndex == 0;
                filter.LastCode = lastCode;
                List<Employee> rows = EmployeeBLL.Search(filter);
                if (lastCode == null || _rows == null)
                {
                    _rows = rows;
                }
                else
                {
                    _rows.AddRange(rows);
                }
                BindGrid();
                _btnMore.Visible = rows.Count >= 100;
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmEmployees.Search");
                Theme.ShowError("خطأ أثناء تحميل بيانات الموظفين. راجع السجلات للمزيد.");
            }
        }

        private void BindGrid()
        {
            _grid.Columns.Clear();
            _grid.Columns.Add("c0", "الكود");
            _grid.Columns.Add("c1", "الاسم بالعربية");
            _grid.Columns.Add("c2", "الرقم القومي");
            _grid.Columns.Add("c3", "القسم");
            _grid.Columns.Add("c4", "الهاتف");
            _grid.Columns.Add("c5", "حالة الموظف");
            _grid.Columns.Add("c6", "رقم البصمة");
            _grid.Columns[0].FillWeight = 10;
            _grid.Columns[1].FillWeight = 24;
            _grid.Columns[2].FillWeight = 15;
            _grid.Columns[3].FillWeight = 18;
            _grid.Columns[4].FillWeight = 12;
            _grid.Columns[5].FillWeight = 11;
            _grid.Columns[6].FillWeight = 10;
            _grid.Rows.Clear();
            if (_rows == null)
            {
                return;
            }
            foreach (Employee employee in _rows)
            {
                string department = null;
                if (employee.DepartmentId != null && _departmentNames != null &&
                    _departmentNames.ContainsKey(employee.DepartmentId.Value))
                {
                    department = _departmentNames[employee.DepartmentId.Value];
                }
                int index = _grid.Rows.Add(
                    employee.EmployeeCode,
                    employee.FullNameAr,
                    employee.NationalId,
                    department,
                    employee.Phone,
                    TranslateStatus(employee.IsDeleted == true ? EmployeeBLL.StatusArchived : employee.Status),
                    employee.DeviceEnrollNumber);
                _grid.Rows[index].Tag = employee;
                if (string.Equals(employee.Status, EmployeeBLL.StatusSuspended, StringComparison.Ordinal))
                {
                    _grid.Rows[index].Cells[5].Style.ForeColor = Theme.StatusWarn;
                }
                if (string.Equals(employee.Status, EmployeeBLL.StatusArchived, StringComparison.Ordinal) || employee.IsDeleted == true)
                {
                    _grid.Rows[index].Cells[5].Style.ForeColor = Theme.StatusBad;
                }
            }
        }

        private static string TranslateStatus(string status)
        {
            if (string.Equals(status, EmployeeBLL.StatusActive, StringComparison.Ordinal))
            {
                return "على رأس العمل";
            }
            if (string.Equals(status, EmployeeBLL.StatusSuspended, StringComparison.Ordinal))
            {
                return "موقوف";
            }
            if (string.Equals(status, EmployeeBLL.StatusArchived, StringComparison.Ordinal))
            {
                return "مؤرشف";
            }
            return status;
        }

        private Employee SelectedEmployee()
        {
            if (_grid.CurrentRow == null || _grid.CurrentRow.Tag == null)
            {
                Theme.ShowInfo("اختر موظفًا من القائمة أولًا.");
                return null;
            }
            return _grid.CurrentRow.Tag as Employee;
        }

        private void OnAddClick(object sender, EventArgs args)
        {
            try
            {
                using (FrmEmployeeEdit dialog = new FrmEmployeeEdit(null))
                {
                    if (dialog.ShowDialog(this) == DialogResult.OK)
                    {
                        Search(null);
                    }
                }
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmEmployees.OnAddClick");
                Theme.ShowError("حدث خطأ غير متوقع. راجع السجلات.");
            }
        }

        private void OnEditClick(object sender, EventArgs args)
        {
            Employee employee = SelectedEmployee();
            if (employee == null)
            {
                return;
            }
            using (FrmEmployeeEdit dialog = new FrmEmployeeEdit(employee.EmployeeId))
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    Search(null);
                }
            }
        }

        private void OnSuspendClick(object sender, EventArgs args)
        {
            Employee employee = SelectedEmployee();
            if (employee == null)
            {
                return;
            }
            if (!Theme.Confirm("إيقاف الموظف '" + employee.EmployeeCode + " - " + employee.FullNameAr + "'؟"))
            {
                return;
            }
            try
            {
                EmployeeBLL.Suspend(employee.EmployeeId, "إيقاف من شاشة الموظفين");
                Search(null);
                Theme.ShowInfo("تم إيقاف الموظف.");
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmEmployees.OnSuspendClick");
                Theme.ShowError("خطأ غير متوقع. راجع السجلات.");
            }
        }

        private void OnActivateClick(object sender, EventArgs args)
        {
            Employee employee = SelectedEmployee();
            if (employee == null)
            {
                return;
            }
            try
            {
                if (employee.IsDeleted == true)
                {
                    EmployeeBLL.Unarchive(employee.EmployeeId, "استعادة من شاشة الموظفين");
                    Search(null);
                    Theme.ShowInfo("تم استعادة الموظف من الأرشفة وإعادته لتاريخ العمل.");
                }
                else
                {
                    EmployeeBLL.Activate(employee.EmployeeId, "تفعيل من شاشة الموظفين");
                    Search(null);
                    Theme.ShowInfo("تم إعادة تفعيل الموظف.");
                }
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmEmployees.OnActivateClick");
                Theme.ShowError("خطأ غير متوقع. راجع السجلات.");
            }
        }

        private void OnArchiveClick(object sender, EventArgs args)
        {
            Employee employee = SelectedEmployee();
            if (employee == null)
            {
                return;
            }
            if (!Theme.Confirm("أرشفة الموظف '" + employee.EmployeeCode + "'؟ سيظهر في التقارير التاريخية ولن يظهر في الجداول والحضور."))
            {
                return;
            }
            try
            {
                EmployeeBLL.Archive(employee.EmployeeId, "أرشفة من شاشة الموظفين");
                Search(null);
                Theme.ShowInfo("تم أرشفة الموظف.");
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmEmployees.OnArchiveClick");
                Theme.ShowError("خطأ غير متوقع. راجع السجلات.");
            }
        }
    }
}
