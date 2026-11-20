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
    /// Organization structure screen (rules 3/26): manages companies, branches,
    /// departments, sections, job titles, actual jobs, qualifications and employee
    /// statuses through OrganizationBLL only — every write is permission-gated
    /// ("Org.Manage") and audit-logged inside the BLL. Deletion is safe: rows in
    /// use are refused by the BLL and must be deactivated instead.
    /// </summary>
    public class FrmLookupManager : Form
    {
        private static readonly LookupTable[] TableKeys = new LookupTable[]
        {
            LookupTable.Companies,
            LookupTable.Branches,
            LookupTable.Departments,
            LookupTable.Sections,
            LookupTable.JobTitles,
            LookupTable.ActualJobs,
            LookupTable.Qualifications,
            LookupTable.EmployeeStatus
        };

        private static readonly string[] TableLabels = new string[]
        {
            "الشركات",
            "الفروع",
            "الأقسام",
            "القطاعات",
            "المسميات الوظيفية",
            "الوظائف الفعلية",
            "المؤهلات",
            "حالات الموظف"
        };

        private ComboBox _cboTable;
        private CheckBox _chkIncludeInactive;
        private Button _btnRefresh;
        private Button _btnAdd;
        private Button _btnEdit;
        private Button _btnToggle;
        private Button _btnDelete;
        private DataGridView _grid;
        private List<LookupRow> _rows;
        private Dictionary<int, string> _parentNames;

        public FrmLookupManager()
        {
            Theme.Apply(this);
            Text = "الهيكل التنظيمي";
            ClientSize = new Size(900, 560);
            StartPosition = FormStartPosition.CenterParent;

            Panel header = new Panel();
            Label lblTitle = new Label();
            lblTitle.Text = "إدارة الهيكل التنظيمي";
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
            toolbar.FlowDirection = FlowDirection.RightToLeft;
            toolbar.Padding = new Padding(6, 8, 6, 4);

            Label lblTable = new Label();
            lblTable.Text = "الجدول:";
            lblTable.AutoSize = true;
            lblTable.Margin = new Padding(3, 8, 3, 0);

            _cboTable = new ComboBox();
            _cboTable.Width = 170;
            _cboTable.DropDownStyle = ComboBoxStyle.DropDownList;
            for (int index = 0; index < TableLabels.Length; index++)
            {
                _cboTable.Items.Add(TableLabels[index]);
            }
            _cboTable.SelectedIndex = 0;
            _cboTable.SelectedIndexChanged += delegate { Reload(); };

            _chkIncludeInactive = new CheckBox();
            _chkIncludeInactive.Text = "تضمين المعطلة";
            _chkIncludeInactive.AutoSize = true;
            _chkIncludeInactive.Margin = new Padding(3, 8, 3, 0);
            _chkIncludeInactive.CheckedChanged += delegate { Reload(); };

            _btnRefresh = new Button();
            _btnRefresh.Text = "تحديث";
            _btnRefresh.Height = 30;
            _btnRefresh.Click += delegate { Reload(); };

            _btnAdd = new Button();
            _btnAdd.Text = "إضافة";
            _btnAdd.Height = 30;
            _btnAdd.Click += OnAddClick;

            _btnEdit = new Button();
            _btnEdit.Text = "تعديل";
            _btnEdit.Height = 30;
            _btnEdit.Click += OnEditClick;

            _btnToggle = new Button();
            _btnToggle.Text = "تعطيل / تفعيل";
            _btnToggle.Height = 30;
            _btnToggle.Click += OnToggleClick;

            _btnDelete = new Button();
            _btnDelete.Text = "حذف";
            _btnDelete.Height = 30;
            _btnDelete.Click += OnDeleteClick;

            toolbar.Controls.Add(lblTable);
            toolbar.Controls.Add(_cboTable);
            toolbar.Controls.Add(_chkIncludeInactive);
            toolbar.Controls.Add(_btnRefresh);
            toolbar.Controls.Add(_btnAdd);
            toolbar.Controls.Add(_btnEdit);
            toolbar.Controls.Add(_btnToggle);
            toolbar.Controls.Add(_btnDelete);

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
            Shown += delegate { Reload(); };
        }

        /// <summary>Enables the write buttons only for users holding "Org.Manage".</summary>
        private void ApplyPermissionButtons()
        {
            bool canManage = AppContext.HasPermission("Org.Manage");
            _btnAdd.Enabled = canManage;
            _btnEdit.Enabled = canManage;
            _btnToggle.Enabled = canManage;
            _btnDelete.Enabled = canManage;
        }

        /// <summary>Currently selected lookup table.</summary>
        private LookupTable CurrentTable()
        {
            return TableKeys[_cboTable.SelectedIndex];
        }

        /// <summary>Only these tables carry a parent (hierarchy) column.</summary>
        private static bool HasParent(LookupTable table)
        {
            return table == LookupTable.Branches ||
                   table == LookupTable.Departments ||
                   table == LookupTable.Sections ||
                   table == LookupTable.ActualJobs;
        }

        /// <summary>Only these tables carry no IsActive column at all.</summary>
        private static bool HasActiveFlag(LookupTable table)
        {
            return table != LookupTable.Qualifications &&
                   table != LookupTable.EmployeeStatus;
        }

        /// <summary>Parent display names for the current table; empty when the table has no parent.</summary>
        private static Dictionary<int, string> LoadParentNames(LookupTable table)
        {
            Dictionary<int, string> names = new Dictionary<int, string>();
            if (table == LookupTable.Branches)
            {
                foreach (Company company in OrganizationBLL.Companies(true))
                {
                    names[company.CompanyId] = company.CompanyNameAr;
                }
            }
            else if (table == LookupTable.Departments)
            {
                foreach (Branch branch in OrganizationBLL.Branches(true))
                {
                    names[branch.BranchId] = branch.BranchName;
                }
            }
            else if (table == LookupTable.Sections || table == LookupTable.ActualJobs)
            {
                foreach (Department department in OrganizationBLL.Departments(true))
                {
                    names[department.DepartmentId] = department.DepartmentName;
                }
            }
            return names;
        }

        private LookupRow SelectedRow()
        {
            if (_grid.CurrentRow == null || _grid.CurrentRow.Tag == null)
            {
                Theme.ShowInfo("اختر سجلًا من القائمة أولًا.");
                return null;
            }
            return _grid.CurrentRow.Tag as LookupRow;
        }

        private void Reload()
        {
            if (_cboTable.SelectedIndex < 0)
            {
                return;
            }
            try
            {
                LookupTable table = CurrentTable();
                _rows = OrganizationBLL.Rows(table, _chkIncludeInactive.Checked);
                _parentNames = LoadParentNames(table);
                BindGrid();
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmLookupManager.Reload");
                Theme.ShowError("خطأ أثناء تحميل البيانات. راجع السجلات للمزيد.");
            }
        }

        private void BindGrid()
        {
            _grid.Columns.Clear();
            _grid.Columns.Add("c0", "الكود");
            _grid.Columns.Add("c1", "الاسم");
            _grid.Columns.Add("c2", HasParent(CurrentTable()) ? "الأصل" : "—");
            _grid.Columns.Add("c3", "الحالة");
            _grid.Columns[0].FillWeight = 15;
            _grid.Columns[1].FillWeight = 40;
            _grid.Columns[2].FillWeight = 30;
            _grid.Columns[3].FillWeight = 15;
            _grid.Rows.Clear();
            if (_rows == null)
            {
                return;
            }
            foreach (LookupRow row in _rows)
            {
                string parent = null;
                if (row.ParentId != null && _parentNames.ContainsKey(row.ParentId.Value))
                {
                    parent = _parentNames[row.ParentId.Value];
                }
                string state = "—";
                if (HasActiveFlag(CurrentTable()))
                {
                    state = row.IsActive == false ? "معطل" : "نشط";
                }
                int index = _grid.Rows.Add(row.Code, row.Name, parent, state);
                _grid.Rows[index].Tag = row;
                if (HasActiveFlag(CurrentTable()) && row.IsActive == false)
                {
                    _grid.Rows[index].Cells[3].Style.ForeColor = Theme.StatusWarn;
                }
            }
        }

        private void OnAddClick(object sender, EventArgs args)
        {
            try
            {
                LookupTable table = CurrentTable();
                using (LookupEditDialog dialog = new LookupEditDialog(table, LoadParentNames(table), null))
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK)
                    {
                        return;
                    }
                    OrganizationBLL.Create(table, dialog.Code, dialog.Name, dialog.ParentId, dialog.IsActive);
                }
                Theme.ShowInfo("تم إضافة السجل بنجاح.");
                Reload();
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmLookupManager.OnAddClick");
                Theme.ShowError("حدث خطأ أثناء الإضافة. تم حفظ التفاصيل في السجلات.");
            }
        }

        private void OnEditClick(object sender, EventArgs args)
        {
            try
            {
                LookupRow row = SelectedRow();
                if (row == null)
                {
                    return;
                }
                LookupTable table = CurrentTable();
                using (LookupEditDialog dialog = new LookupEditDialog(table, LoadParentNames(table), row))
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK)
                    {
                        return;
                    }
                    OrganizationBLL.Update(table, row.Id, dialog.Code, dialog.Name, dialog.ParentId, dialog.IsActive);
                }
                Theme.ShowInfo("تم حفظ التعديلات بنجاح.");
                Reload();
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmLookupManager.OnEditClick");
                Theme.ShowError("حدث خطأ أثناء التعديل. تم حفظ التفاصيل في السجلات.");
            }
        }

        private void OnToggleClick(object sender, EventArgs args)
        {
            try
            {
                LookupTable table = CurrentTable();
                if (!HasActiveFlag(table))
                {
                    Theme.ShowInfo("هذا الجدول لا يدعم التفعيل والتعطيل.");
                    return;
                }
                LookupRow row = SelectedRow();
                if (row == null)
                {
                    return;
                }
                bool activate = row.IsActive == false;
                OrganizationBLL.SetActive(table, row.Id, activate);
                Theme.ShowInfo(activate ? "تم تفعيل السجل." : "تم تعطيل السجل.");
                Reload();
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmLookupManager.OnToggleClick");
                Theme.ShowError("حدث خطأ أثناء تغيير الحالة. تم حفظ التفاصيل في السجلات.");
            }
        }

        private void OnDeleteClick(object sender, EventArgs args)
        {
            try
            {
                LookupRow row = SelectedRow();
                if (row == null)
                {
                    return;
                }
                bool confirmed = Theme.Confirm("هل تريد حذف السجل '" + row.Code + " - " + row.Name + "'؟");
                if (!confirmed)
                {
                    return;
                }
                OrganizationBLL.Delete(CurrentTable(), row.Id);
                Theme.ShowInfo("تم حذف السجل.");
                Reload();
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmLookupManager.OnDeleteClick");
                Theme.ShowError("حدث خطأ أثناء الحذف. تم حفظ التفاصيل في السجلات.");
            }
        }

        /// <summary>
        /// Add/edit dialog for one lookup row. Binds the parent list through
        /// (Id, Name) pairs with a (بدون) sentinel at Id 0 — the same pattern as
        /// FrmEmployeeEdit — so the saved ParentId is always a real entity Id.
        /// </summary>
        private class LookupEditDialog : Form
        {
            private const string OptionNone = "(بدون)";

            private readonly LookupTable _table;
            private readonly bool _hasParent;
            private readonly Dictionary<int, string> _parentNames;
            private TextBox _txtCode;
            private TextBox _txtName;
            private ComboBox _cboParent;
            private Label _lblParent;
            private CheckBox _chkActive;

            public string Code { get; private set; }
            public string Name { get; private set; }
            public int? ParentId { get; private set; }
            public bool IsActive { get; private set; }

            public LookupEditDialog(LookupTable table, Dictionary<int, string> parentNames, LookupRow existing)
            {
                _table = table;
                _hasParent = HasParent(table);
                _parentNames = parentNames ?? new Dictionary<int, string>();

                Theme.Apply(this);
                Text = existing == null ? "إضافة سجل جديد" : "تعديل سجل";
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MinimizeBox = false;
                MaximizeBox = false;
                StartPosition = FormStartPosition.CenterParent;
                ClientSize = new Size(420, 190);

                Label lblCode = new Label();
                lblCode.Text = "الكود *:";
                lblCode.Location = new Point(20, 22);
                lblCode.Size = new Size(110, 22);

                _txtCode = new TextBox();
                _txtCode.Location = new Point(135, 18);
                _txtCode.Size = new Size(260, 26);
                _txtCode.RightToLeft = RightToLeft.No;
                _txtCode.MaxLength = 20;

                Label lblName = new Label();
                lblName.Text = "الاسم *:";
                lblName.Location = new Point(20, 57);
                lblName.Size = new Size(110, 22);

                _txtName = new TextBox();
                _txtName.Location = new Point(135, 53);
                _txtName.Size = new Size(260, 26);
                _txtName.MaxLength = 150;

                _lblParent = new Label();
                _lblParent.Text = "الأصل:";
                _lblParent.Location = new Point(20, 92);
                _lblParent.Size = new Size(110, 22);

                _cboParent = new ComboBox();
                _cboParent.Location = new Point(135, 88);
                _cboParent.Size = new Size(260, 26);
                _cboParent.DropDownStyle = ComboBoxStyle.DropDownList;

                if (_hasParent)
                {
                    Dictionary<int, string> options = new Dictionary<int, string>();
                    options[0] = OptionNone;
                    foreach (KeyValuePair<int, string> pair in _parentNames)
                    {
                        options.Add(pair.Key, pair.Value);
                    }
                    _cboParent.DataSource = new BindingSource(options, null);
                    _cboParent.DisplayMember = "Value";
                    _cboParent.ValueMember = "Key";
                }
                else
                {
                    _cboParent.Visible = false;
                    _lblParent.Visible = false;
                }

                _chkActive = new CheckBox();
                _chkActive.Text = "نشط";
                _chkActive.Location = new Point(135, 123);
                _chkActive.AutoSize = true;

                Button btnOk = new Button();
                btnOk.Text = "حفظ";
                btnOk.Location = new Point(150, 155);
                btnOk.Size = new Size(110, 28);
                btnOk.Click += OnOkClick;

                Button btnCancel = new Button();
                btnCancel.Text = "إلغاء";
                btnCancel.Location = new Point(270, 155);
                btnCancel.Size = new Size(110, 28);
                btnCancel.Click += delegate { DialogResult = DialogResult.Cancel; };

                Controls.Add(lblCode);
                Controls.Add(_txtCode);
                Controls.Add(lblName);
                Controls.Add(_txtName);
                Controls.Add(_lblParent);
                Controls.Add(_cboParent);
                Controls.Add(_chkActive);
                Controls.Add(btnOk);
                Controls.Add(btnCancel);

                if (existing != null)
                {
                    _txtCode.Text = existing.Code ?? string.Empty;
                    _txtName.Text = existing.Name ?? string.Empty;
                    if (_hasParent)
                    {
                        _cboParent.SelectedValue = existing.ParentId == null ? 0 : existing.ParentId.Value;
                    }
                    _chkActive.Checked = existing.IsActive != false;
                }
                else
                {
                    _chkActive.Checked = true;
                }
            }

            private void OnOkClick(object sender, EventArgs args)
            {
                if (string.IsNullOrWhiteSpace(_txtCode.Text))
                {
                    Theme.ShowError("كود السجل إلزامي.");
                    return;
                }
                if (string.IsNullOrWhiteSpace(_txtName.Text))
                {
                    Theme.ShowError("اسم السجل إلزامي.");
                    return;
                }
                Code = _txtCode.Text.Trim();
                Name = _txtName.Text.Trim();
                if (_hasParent)
                {
                    object value = _cboParent.SelectedValue;
                    int parentKey = value == null ? 0 : Convert.ToInt32(value);
                    ParentId = parentKey == 0 ? (int?)null : parentKey;
                }
                else
                {
                    ParentId = null;
                }
                IsActive = HasActiveFlag(_table) ? _chkActive.Checked : true;
                DialogResult = DialogResult.OK;
            }
        }
    }
}
