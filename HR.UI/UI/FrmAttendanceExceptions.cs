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
    /// Registry of attendance exceptions produced by the engine during a
    /// recalculation (BadShift, Absent, MissingPunch, ...) and added manually by
    /// users. Manual rows survive recalculations and can be deleted here;
    /// engine rows are regenerated and can only be removed by recalculating.
    /// </summary>
    public class FrmAttendanceExceptions : Form
    {
        private static readonly string[] TypeCodes = new string[]
        {
            "", "BadShift", "Absent", "MissingPunch", "ExtraPunch", "Late", "EarlyLeave", "OverContinuous"
        };

        private ComboBox _cboType;
        private TextBox _txtEmployee;
        private DateTimePicker _dtpFrom;
        private DateTimePicker _dtpTo;
        private Button _btnDelete;
        private DataGridView _grid;
        private List<AttendanceExceptionView> _rows;

        public FrmAttendanceExceptions()
        {
            Theme.Apply(this, false); // MDI child: mirrored layout is not applied — RTL flow comes from the containers
            Text = "سجل استثناءات الحضور";
            ClientSize = new Size(1040, 560);
            StartPosition = FormStartPosition.CenterParent;

            Panel header = new Panel();
            Label lblTitle = new Label();
            lblTitle.Text = "سجل استثناءات الحضور — المحرك يعيد توليد أسطره عند إعادة الحساب، والأسطر اليدوية تبقى ثابتة";
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

            Label lblType = new Label();
            lblType.Text = "النوع:";
            lblType.AutoSize = true;
            lblType.Margin = new Padding(3, 10, 3, 0);

            _cboType = new ComboBox();
            _cboType.DropDownStyle = ComboBoxStyle.DropDownList;
            _cboType.Width = 130;
            _cboType.Margin = new Padding(3, 8, 3, 0);
            _cboType.Items.Add("كل الأنواع");
            _cboType.Items.Add("وردية غير محددة");
            _cboType.Items.Add("غياب");
            _cboType.Items.Add("بصمة ناقصة");
            _cboType.Items.Add("بصمة زائدة");
            _cboType.Items.Add("تأخير (دقائق)");
            _cboType.Items.Add("انصراف مبكر (دقائق)");
            _cboType.Items.Add("تجاوز ساعات متواصلة");
            _cboType.SelectedIndex = 0;

            Label lblEmployee = new Label();
            lblEmployee.Text = "الموظف:";
            lblEmployee.AutoSize = true;
            lblEmployee.Margin = new Padding(3, 10, 3, 0);

            _txtEmployee = new TextBox();
            _txtEmployee.Width = 130;
            _txtEmployee.Margin = new Padding(3, 8, 3, 0);

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
            _dtpTo.Value = DateTime.Now.Date;

            Button btnRefresh = new Button();
            btnRefresh.Text = "تحديث";
            btnRefresh.Height = 30;
            btnRefresh.Click += delegate { Reload(); };

            _btnDelete = new Button();
            _btnDelete.Text = "حذف الاستثناء";
            _btnDelete.Height = 30;
            _btnDelete.Click += delegate { OnDeleteClick(); };

            toolbar.Controls.Add(_cboType);
            toolbar.Controls.Add(lblType);
            toolbar.Controls.Add(lblEmployee);
            toolbar.Controls.Add(_txtEmployee);
            toolbar.Controls.Add(_dtpFrom);
            toolbar.Controls.Add(_dtpTo);
            toolbar.Controls.Add(btnRefresh);
            toolbar.Controls.Add(_btnDelete);

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
            ToolStripMenuItem miDelete = new ToolStripMenuItem("حذف الاستثناء");
            miDelete.Click += delegate { OnDeleteClick(); };
            ToolStripMenuItem miPrint = new ToolStripMenuItem("طباعة");
            miPrint.Click += delegate { GridPrintHelper.Print(_grid, "سجل استثناءات الحضور"); };
            gridMenu.Items.Add(miDelete);
            gridMenu.Items.Add(new ToolStripSeparator());
            gridMenu.Items.Add(miPrint);
            _grid.ContextMenuStrip = gridMenu;

            Controls.Add(_grid);
            Controls.Add(toolbar);
            Controls.Add(header);

            _btnDelete.Enabled = AppContext.HasPermission("Attendance.Edit");
            Reload();
        }

        private AttendanceExceptionView SelectedRow()
        {
            if (_grid.CurrentRow == null)
            {
                return null;
            }
            return _grid.CurrentRow.Tag as AttendanceExceptionView;
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
                string excType = TypeCodes[_cboType.SelectedIndex >= 0 ? _cboType.SelectedIndex : 0];
                int? employeeId = ResolveEmployee();
                DateTime? fromDate = _dtpFrom.Checked ? (DateTime?)_dtpFrom.Value.Date : null;
                DateTime? toDate = _dtpTo.Checked ? (DateTime?)_dtpTo.Value.Date : null;
                _rows = AttendanceExceptionBLL.Browse(employeeId, fromDate, toDate, excType);
                _grid.Columns.Clear();
                _grid.Rows.Clear();

                AddColumn("EmployeeCode", "الكود", 70);
                AddColumn("EmployeeName", "الموظف", 170);
                AddColumn("AttDate", "اليوم", 95);
                AddColumn("Type", "النوع", 140);
                AddColumn("ExcValue", "القيمة", 80);
                AddColumn("Source", "المصدر", 90);
                AddColumn("CreatedAt", "أُضيف في", 110);

                foreach (AttendanceExceptionView row in _rows)
                {
                    int rowIndex = _grid.Rows.Add();
                    DataGridViewRow gridRow = _grid.Rows[rowIndex];
                    gridRow.Tag = row;
                    gridRow.Cells["EmployeeCode"].Value = row.EmployeeCode;
                    gridRow.Cells["EmployeeName"].Value = row.EmployeeName;
                    gridRow.Cells["AttDate"].Value = row.AttDate == null ? "" :
                        row.AttDate.Value.ToString("yyyy-MM-dd");
                    gridRow.Cells["Type"].Value = AttendanceExceptionBLL.TypeName(row.ExcType);
                    gridRow.Cells["ExcValue"].Value = row.ExcValue == null ? "" :
                        row.ExcValue.Value.ToString();
                    gridRow.Cells["Source"].Value = row.IsManual ? "يدوي" : "المحرك";
                    gridRow.Cells["CreatedAt"].Value = row.CreatedAt == null ? "" :
                        row.CreatedAt.Value.ToString("yyyy-MM-dd HH:mm");
                }
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmAttendanceExceptions.Reload");
                Theme.ShowError("حدث خطأ أثناء تحميل سجل الاستثناءات.");
            }
        }

        private void OnDeleteClick()
        {
            AttendanceExceptionView row = SelectedRow();
            if (row == null)
            {
                Theme.ShowInfo("اختر استثناء من القائمة أولاً.");
                return;
            }
            if (!row.IsManual)
            {
                Theme.ShowInfo("هذا الاستثناء مولَّد من محرك الحضور — أعِد حساب الفترة بدلاً من حذفه يدوياً.");
                return;
            }
            if (!Theme.Confirm("حذف الاستثناء اليدوي لليوم " + (row.AttDate == null ? "" : row.AttDate.Value.ToString("yyyy-MM-dd")) +
                " للموظف " + row.EmployeeName + "؟"))
            {
                return;
            }
            try
            {
                AttendanceExceptionBLL.DeleteManual(row.ExceptionId);
                Reload();
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmAttendanceExceptions.OnDeleteClick");
                Theme.ShowError("حدث خطأ أثناء حذف الاستثناء.");
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
