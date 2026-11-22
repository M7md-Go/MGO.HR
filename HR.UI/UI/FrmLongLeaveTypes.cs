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
    /// Long-leave-types catalogue: maternity, study, unpaid and similar kinds
    /// with unlimited or month-capped durations. Writes are gated by
    /// "Org.Manage" inside LongLeaveTypeBLL.
    /// </summary>
    public class FrmLongLeaveTypes : Form
    {
        private Button _btnRefresh;
        private Button _btnAdd;
        private Button _btnEdit;
        private Button _btnDelete;
        private DataGridView _grid;
        private List<LongLeaveType> _rows;

        public FrmLongLeaveTypes()
        {
            Theme.Apply(this, false); // MDI child: mirrored layout is not applied — RTL flow comes from the containers
            Text = "أنواع الإجازات طويلة المدة";
            ClientSize = new Size(940, 540);
            StartPosition = FormStartPosition.CenterParent;

            Panel header = new Panel();
            Label lblTitle = new Label();
            lblTitle.Text = "أنواع الإجازات طويلة المدة — حدها الشهري وحالتها";
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
            toolbar.WrapContents = false;
            toolbar.Padding = new Padding(6, 8, 6, 4);

            _btnRefresh = new Button();
            _btnRefresh.Text = "تحديث";
            _btnRefresh.Height = 30;
            _btnRefresh.Click += delegate { Reload(); };

            _btnAdd = new Button();
            _btnAdd.Text = "نوع جديد";
            _btnAdd.Height = 30;
            _btnAdd.Click += delegate { OnAddClick(); };

            _btnEdit = new Button();
            _btnEdit.Text = "تعديل";
            _btnEdit.Height = 30;
            _btnEdit.Click += delegate { OnEditClick(); };

            _btnDelete = new Button();
            _btnDelete.Text = "حذف";
            _btnDelete.Height = 30;
            _btnDelete.Click += delegate { OnDeleteClick(); };

            toolbar.Controls.Add(_btnRefresh);
            toolbar.Controls.Add(_btnAdd);
            toolbar.Controls.Add(_btnEdit);
            toolbar.Controls.Add(_btnDelete);

            _grid = new DataGridView();
            Theme.StyleGrid(_grid);
            Theme.StyleStatusColumn(_grid, "Active");
            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _grid.CellDoubleClick += delegate { OnEditClick(); };

            // management quick menu on the grid
            ContextMenuStrip gridMenu = new ContextMenuStrip();
            gridMenu.RightToLeft = RightToLeft.Yes;
            gridMenu.Font = Theme.Font(9.5f);
            ToolStripMenuItem miAdd = new ToolStripMenuItem("نوع جديد");
            miAdd.Click += delegate { OnAddClick(); };
            ToolStripMenuItem miEdit = new ToolStripMenuItem("تعديل");
            miEdit.Click += delegate { OnEditClick(); };
            ToolStripMenuItem miDelete = new ToolStripMenuItem("حذف");
            miDelete.Click += delegate { OnDeleteClick(); };
            ToolStripMenuItem miPrint = new ToolStripMenuItem("طباعة");
            miPrint.Click += delegate { GridPrintHelper.Print(_grid, "أنواع الإجازات طويلة المدة"); };
            gridMenu.Items.Add(miAdd);
            gridMenu.Items.Add(miEdit);
            gridMenu.Items.Add(miDelete);
            gridMenu.Items.Add(new ToolStripSeparator());
            gridMenu.Items.Add(miPrint);
            _grid.ContextMenuStrip = gridMenu;

            Controls.Add(_grid);
            Controls.Add(toolbar);
            Controls.Add(header);

            bool canManage = AppContext.HasPermission("Org.Manage");
            _btnAdd.Enabled = canManage;
            _btnEdit.Enabled = canManage;
            _btnDelete.Enabled = canManage;
            Reload();
        }

        private LongLeaveType SelectedType()
        {
            if (_grid.CurrentRow == null)
            {
                return null;
            }
            return _grid.CurrentRow.Tag as LongLeaveType;
        }

        private void Reload()
        {
            try
            {
                _rows = LongLeaveTypeBLL.List();
                _grid.Columns.Clear();
                _grid.Rows.Clear();

                AddColumn("Code", "الكود", 70);
                AddColumn("Name", "المسمى", 200);
                AddColumn("Limited", "مدة محدودة", 80);
                AddColumn("MaxMonths", "الحد (أشهر)", 80);
                AddColumn("Active", "الحالة", 70);
                AddColumn("Notes", "ملاحظات", 160);

                foreach (LongLeaveType type in _rows)
                {
                    int rowIndex = _grid.Rows.Add();
                    DataGridViewRow row = _grid.Rows[rowIndex];
                    row.Tag = type;
                    row.Cells["Code"].Value = type.TypeCode;
                    row.Cells["Name"].Value = type.TypeName;
                    row.Cells["Limited"].Value = type.IsLimited == true ? "نعم" : "مفتوحة";
                    row.Cells["MaxMonths"].Value = type.IsLimited == true && type.MaxMonths != null ?
                        type.MaxMonths.Value.ToString() : "—";
                    row.Cells["Active"].Value = type.IsActive == true ? "نشط" : "معطّل";
                    row.Cells["Notes"].Value = type.Notes;
                }
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmLongLeaveTypes.Reload");
                Theme.ShowError("حدث خطأ أثناء تحميل الأنواع.");
            }
        }

        private void OnAddClick()
        {
            FrmLongLeaveTypeEdit dialog = new FrmLongLeaveTypeEdit(null);
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                Reload();
            }
        }

        private void OnEditClick()
        {
            LongLeaveType type = SelectedType();
            if (type == null)
            {
                Theme.ShowInfo("اختر نوعًا من القائمة أولاً.");
                return;
            }
            FrmLongLeaveTypeEdit dialog = new FrmLongLeaveTypeEdit(type.LongLeaveTypeId);
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                Reload();
            }
        }

        private void OnDeleteClick()
        {
            LongLeaveType type = SelectedType();
            if (type == null)
            {
                Theme.ShowInfo("اختر نوعًا من القائمة أولاً.");
                return;
            }
            if (!Theme.Confirm("حذف النوع '" + type.TypeName + "'؟ لا يمكن الحذف إذا كانت هناك طلبات مرتبطة به — عطّله بدلاً من ذلك."))
            {
                return;
            }
            try
            {
                LongLeaveTypeBLL.Delete(type.LongLeaveTypeId);
                Reload();
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmLongLeaveTypes.OnDeleteClick");
                Theme.ShowError("حدث خطأ أثناء حذف النوع.");
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
