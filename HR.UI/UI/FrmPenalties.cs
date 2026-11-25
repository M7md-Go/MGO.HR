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
    /// Penalty browser (rule 17): employee/date/type filters and CRUD. Every
    /// write is gated by "Penalties.Add" (the dashboard/report module only
    /// reads). Buttons are disabled for users without the permission and the
    /// BLL re-checks anyway. All logic lives in PenaltyBLL.
    /// </summary>
    public class FrmPenalties : Form
    {
        private static readonly string[] TypeCodes = new string[]
        {
            "", "Warning", "Deduction", "Penalty", "Absence", "Violation"
        };

        private static readonly string[] TypeLabels = new string[]
        {
            "كل الأنواع", "إنذار", "خصم", "جزاء", "غياب", "مخالفة"
        };

        private TextBox _txtEmployee;
        private DateTimePicker _dtpFrom;
        private DateTimePicker _dtpTo;
        private ComboBox _cboType;
        private Button _btnRefresh;
        private Button _btnAdd;
        private Button _btnEdit;
        private Button _btnDelete;
        private DataGridView _grid;
        private List<PenaltyView> _rows;

        public FrmPenalties()
        {
            Theme.Apply(this, false); // MDI child: mirrored layout is not applied — RTL flow comes from the containers
            Text = "الجزاءات";
            ClientSize = new Size(1050, 600);
            StartPosition = FormStartPosition.CenterParent;

            Panel header = new Panel();
            Label lblTitle = new Label();
            lblTitle.Text = "إدارة الجزاءات — إنذار، خصم، جزاء، غياب، مخالفة";
            lblTitle.ForeColor = Theme.HeaderTextColor;
            lblTitle.Font = Theme.Font(12f);
            lblTitle.Dock = DockStyle.Fill;
            lblTitle.TextAlign = ContentAlignment.MiddleCenter;
            Theme.StyleHeader(header);
            header.Controls.Add(lblTitle);
            header.Dock = DockStyle.Top;

            FlowLayoutPanel toolbar = new FlowLayoutPanel();
            toolbar.Dock = DockStyle.Top;
            toolbar.Height = 88;
            toolbar.BackColor = Theme.ToolbarColor;
            // FlowLayoutPanel: RTL=Yes (inherited from the form) already reverses the flow;
            // an explicit RightToLeft cancelled that reversal and rendered the bar left-to-right.
            toolbar.FlowDirection = FlowDirection.LeftToRight;
            toolbar.WrapContents = true;
            toolbar.Padding = new Padding(6, 8, 6, 4);

            Label lblEmployee = new Label();
            lblEmployee.Text = "الموظف:";
            lblEmployee.AutoSize = true;
            lblEmployee.Margin = new Padding(3, 10, 3, 0);

            _txtEmployee = new TextBox();
            _txtEmployee.Width = 130;
            _txtEmployee.Margin = new Padding(3, 8, 3, 0);

            _cboType = new ComboBox();
            _cboType.DropDownStyle = ComboBoxStyle.DropDownList;
            _cboType.Width = 100;
            foreach (string label in TypeLabels)
            {
                _cboType.Items.Add(label);
            }
            _cboType.SelectedIndex = 0;
            _cboType.Margin = new Padding(3, 8, 3, 0);

            _dtpFrom = new DateTimePicker();
            _dtpFrom.Format = DateTimePickerFormat.Short;
            _dtpFrom.Width = 110;
            _dtpFrom.ShowCheckBox = true;
            _dtpFrom.Margin = new Padding(3, 8, 3, 0);
            _dtpFrom.Value = DateTime.Now.AddMonths(-1);

            _dtpTo = new DateTimePicker();
            _dtpTo.Format = DateTimePickerFormat.Short;
            _dtpTo.Width = 110;
            _dtpTo.ShowCheckBox = true;
            _dtpTo.Margin = new Padding(3, 8, 3, 0);
            _dtpTo.Value = DateTime.Now;

            _btnRefresh = new Button();
            _btnRefresh.Text = "تحديث";
            _btnRefresh.Height = 30;
            _btnRefresh.Click += delegate { Reload(); };

            _btnAdd = new Button();
            _btnAdd.Text = "جزاء جديد";
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

            toolbar.Controls.Add(lblEmployee);
            toolbar.Controls.Add(_txtEmployee);
            toolbar.Controls.Add(_cboType);
            toolbar.Controls.Add(_dtpFrom);
            toolbar.Controls.Add(_dtpTo);
            toolbar.Controls.Add(_btnRefresh);
            toolbar.Controls.Add(_btnAdd);
            toolbar.Controls.Add(_btnEdit);
            toolbar.Controls.Add(_btnDelete);

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

            bool canManage = AppContext.HasAnyPermission(new string[] { "Penalties.Add", "Penalties.Delete" });
            _btnAdd.Enabled = canManage;
            _btnEdit.Enabled = canManage;
            _btnDelete.Enabled = AppContext.HasAnyPermission(new string[] { "Penalties.Delete", "Penalties.Add" });
            Reload();
        }

        private PenaltyView SelectedPenalty()
        {
            if (_grid.CurrentRow == null)
            {
                return null;
            }
            return _grid.CurrentRow.Tag as PenaltyView;
        }

        private int? ResolveEmployee()
        {
            string text = (_txtEmployee.Text ?? string.Empty).Trim();
            if (text.Length == 0)
            {
                return null;
            }
            EmployeeSearchFilter filter = new EmployeeSearchFilter();
            filter.Text = text;
            filter.IncludeArchived = true;
            filter.PageSize = 1;
            List<Employee> matches = EmployeeBLL.Search(filter);
            return matches.Count > 0 ? matches[0].EmployeeId : (int?)null;
        }

        private void Reload()
        {
            try
            {
                int? employeeId = ResolveEmployee();
                string typeCode = TypeCodes[_cboType.SelectedIndex >= 0 ? _cboType.SelectedIndex : 0];
                DateTime? fromDate = _dtpFrom.Checked ? (DateTime?)_dtpFrom.Value.Date : null;
                DateTime? toDate = _dtpTo.Checked ? (DateTime?)_dtpTo.Value.Date : null;
                _rows = PenaltyBLL.Search(employeeId, fromDate, toDate,
                    typeCode.Length == 0 ? null : typeCode);
                _grid.Columns.Clear();
                _grid.Rows.Clear();

                AddColumn("EmployeeCode", "الكود", 70);
                AddColumn("EmployeeName", "الموظف", 150);
                AddColumn("AttDate", "تاريخ المخالفة", 95);
                AddColumn("PenaltyDate", "تاريخ الجزاء", 95);
                AddColumn("TypeName", "النوع", 85);
                AddColumn("Amount", "القيمة", 75);
                AddColumn("Reason", "السبب", 150);
                AddColumn("IssuedBy", "سجّله", 70);

                foreach (PenaltyView penalty in _rows)
                {
                    int rowIndex = _grid.Rows.Add();
                    DataGridViewRow row = _grid.Rows[rowIndex];
                    row.Tag = penalty;
                    row.Cells["EmployeeCode"].Value = penalty.EmployeeCode;
                    row.Cells["EmployeeName"].Value = penalty.EmployeeName;
                    row.Cells["AttDate"].Value = penalty.AttDate == null ? null :
                        penalty.AttDate.Value.ToString("yyyy-MM-dd");
                    row.Cells["PenaltyDate"].Value = penalty.PenaltyDate == null ? null :
                        penalty.PenaltyDate.Value.ToString("yyyy-MM-dd");
                    row.Cells["TypeName"].Value = PenaltyBLL.TypeName(penalty.TypeCode);
                    row.Cells["Amount"].Value = penalty.Amount == null ? "-" :
                        penalty.Amount.Value.ToString("N2");
                    row.Cells["Reason"].Value = penalty.Reason;
                    row.Cells["IssuedBy"].Value = penalty.IssuedBy;
                }
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmPenalties.Reload");
                Theme.ShowError("حدث خطأ أثناء تحميل الجزاءات.");
            }
        }

        private void OnAddClick()
        {
            FrmPenaltyEdit dialog = new FrmPenaltyEdit(null);
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                Reload();
            }
        }

        private void OnEditClick()
        {
            PenaltyView penalty = SelectedPenalty();
            if (penalty == null)
            {
                Theme.ShowInfo("اختر جزاءً من القائمة أولًا.");
                return;
            }
            FrmPenaltyEdit dialog = new FrmPenaltyEdit(penalty.PenaltyId);
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                Reload();
            }
        }

        private void OnDeleteClick()
        {
            PenaltyView penalty = SelectedPenalty();
            if (penalty == null)
            {
                Theme.ShowInfo("اختر جزاءً من القائمة أولًا.");
                return;
            }
            if (!Theme.Confirm("حذف جزاء (" + PenaltyBLL.TypeName(penalty.TypeCode) +
                ") للموظف " + penalty.EmployeeName + "؟ ستبقى العملية في سجل العمليات."))
            {
                return;
            }
            try
            {
                PenaltyBLL.Delete(penalty.PenaltyId);
                Reload();
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmPenalties.OnDeleteClick");
                Theme.ShowError("حدث خطأ أثناء حذف الجزاء.");
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
