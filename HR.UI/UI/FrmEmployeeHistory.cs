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
    /// Read-only trail of one employee's data changes (rule 8): every recorded
    /// change of status / archive / tracked fields with old and new values and
    /// the changing user. History is written automatically by the BLL layer —
    /// this screen only reads it.
    /// </summary>
    public class FrmEmployeeHistory : Form
    {
        private TextBox _txtEmployee;
        private Label _lblEmployeeName;
        private DataGridView _grid;
        private List<EmployeeHistory> _rows;
        private int _employeeId;

        public FrmEmployeeHistory()
        {
            _employeeId = 0;
            Theme.Apply(this, false); // MDI child: mirrored layout is not applied — RTL flow comes from the containers
            Text = "سجل تاريخ الموظف";
            ClientSize = new Size(1000, 560);
            StartPosition = FormStartPosition.CenterParent;

            Panel header = new Panel();
            Label lblTitle = new Label();
            lblTitle.Text = "سجل تاريخ الموظف — كل تغييرات الحالة والحقول المُتتبَّعة مع القيم قبل وبعد";
            lblTitle.ForeColor = Theme.HeaderTextColor;
            lblTitle.Font = Theme.Font(12f);
            lblTitle.Dock = DockStyle.Fill;
            lblTitle.TextAlign = ContentAlignment.MiddleCenter;
            Theme.StyleHeader(header);
            header.Controls.Add(lblTitle);
            header.Dock = DockStyle.Top;

            FlowLayoutPanel toolbar = new FlowLayoutPanel();
            toolbar.Dock = DockStyle.Top;
            toolbar.Height = 60;
            toolbar.BackColor = Theme.ToolbarColor;
            // FlowLayoutPanel: RTL=Yes (inherited from the form) already reverses the flow;
            // an explicit RightToLeft cancelled that reversal and rendered the bar left-to-right.
            toolbar.FlowDirection = FlowDirection.LeftToRight;
            toolbar.WrapContents = false;
            toolbar.Padding = new Padding(6, 8, 6, 4);

            Label lblEmployee = new Label();
            lblEmployee.Text = "الموظف:";
            lblEmployee.AutoSize = true;
            lblEmployee.Margin = new Padding(3, 10, 3, 0);

            _txtEmployee = new TextBox();
            _txtEmployee.Width = 220;
            _txtEmployee.Margin = new Padding(3, 6, 3, 0);

            _lblEmployeeName = new Label();
            _lblEmployeeName.AutoSize = true;
            _lblEmployeeName.ForeColor = Theme.HeaderTextColor;
            _lblEmployeeName.Margin = new Padding(3, 10, 3, 0);

            Button btnRefresh = new Button();
            btnRefresh.Text = "عرض السجل";
            btnRefresh.Height = 30;
            btnRefresh.Click += delegate { Reload(); };

            toolbar.Controls.Add(lblEmployee);
            toolbar.Controls.Add(_txtEmployee);
            toolbar.Controls.Add(btnRefresh);
            toolbar.Controls.Add(_lblEmployeeName);

            _grid = new DataGridView();
            Theme.StyleGrid(_grid);
            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            ContextMenuStrip gridMenu = new ContextMenuStrip();
            gridMenu.RightToLeft = RightToLeft.Yes;
            gridMenu.Font = Theme.Font(9.5f);
            ToolStripMenuItem miPrint = new ToolStripMenuItem("طباعة");
            miPrint.Click += delegate { GridPrintHelper.Print(_grid, "سجل تاريخ الموظف"); };
            gridMenu.Items.Add(miPrint);
            _grid.ContextMenuStrip = gridMenu;

            Controls.Add(_grid);
            Controls.Add(toolbar);
            Controls.Add(header);
        }

        private Employee ResolveEmployee()
        {
            string resolveError;
            Employee employee = EmployeeResolve.FromText(_txtEmployee.Text, out resolveError);
            if (resolveError != null)
            {
                throw new BusinessException(resolveError);
            }
            return employee;
        }

        private void Reload()
        {
            try
            {
                Employee employee = ResolveEmployee();
                if (employee == null)
                {
                    Theme.ShowInfo("أدخل كود الموظف أو اسمه أولاً.");
                    return;
                }
                _employeeId = employee.EmployeeId;
                _lblEmployeeName.Text = employee.FullNameAr + " (" + employee.EmployeeCode + ")";
                _rows = EmployeeHistoryBLL.GetForEmployee(_employeeId, 1000);
                _grid.Columns.Clear();
                _grid.Rows.Clear();

                AddColumn("ChangeDate", "التاريخ والوقت", 130);
                AddColumn("ChangeType", "نوع التغيير", 110);
                AddColumn("FieldName", "الحقل", 140);
                AddColumn("OldValue", "القيمة السابقة", 160);
                AddColumn("NewValue", "القيمة الجديدة", 160);
                AddColumn("ChangedBy", "بواسطة مستخدم", 100);

                foreach (EmployeeHistory row in _rows)
                {
                    int rowIndex = _grid.Rows.Add();
                    DataGridViewRow gridRow = _grid.Rows[rowIndex];
                    gridRow.Tag = row;
                    gridRow.Cells["ChangeDate"].Value = row.ChangeDate == null ? "" :
                        row.ChangeDate.Value.ToString("yyyy-MM-dd HH:mm");
                    gridRow.Cells["ChangeType"].Value = TranslateChangeType(row.ChangeType);
                    gridRow.Cells["FieldName"].Value = row.FieldName ?? "";
                    gridRow.Cells["OldValue"].Value = row.OldValue ?? "";
                    gridRow.Cells["NewValue"].Value = row.NewValue ?? "";
                    gridRow.Cells["ChangedBy"].Value = row.ChangedBy == null ? "" :
                        row.ChangedBy.Value.ToString();
                }
                if (_rows.Count == 0)
                {
                    Theme.ShowInfo("لا توجد تغييرات مسجَّلة لهذا الموظف بعد.");
                }
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmEmployeeHistory.Reload");
                Theme.ShowError("حدث خطأ أثناء تحميل سجل التاريخ.");
            }
        }

        private static string TranslateChangeType(string changeType)
        {
            if (changeType == "StatusChange") { return "تغيير حالة"; }
            if (changeType == "Archive") { return "أرشفة"; }
            if (changeType == "Unarchive") { return "إلغاء أرشفة"; }
            return changeType;
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
