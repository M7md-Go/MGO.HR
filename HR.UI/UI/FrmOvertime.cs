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
    /// Overtime register ([Overtime]): engine-computed and manual rows with an
    /// approve/reject life-cycle. Approval is gated by the same
    /// "Attendance.Edit" permission the engine uses (inside OvertimeBLL).
    /// </summary>
    public class FrmOvertime : Form
    {
        private static readonly string[] StatusValues =
            new string[] { "", "Pending", "Approved", "Rejected" };

        private ComboBox _cboStatus;
        private TextBox _txtEmployee;
        private DateTimePicker _dtpFrom;
        private DateTimePicker _dtpTo;
        private Button _btnApprove;
        private Button _btnReject;
        private DataGridView _grid;
        private List<OvertimeView> _rows;

        public FrmOvertime()
        {
            Theme.Apply(this, false); // MDI child: mirrored layout is not applied — RTL flow comes from the containers
            Text = "الساعات الإضافية";
            ClientSize = new Size(1000, 560);
            StartPosition = FormStartPosition.CenterParent;

            Panel header = new Panel();
            Label lblTitle = new Label();
            lblTitle.Text = "الساعات الإضافية — الاعتماد من حساب الحضور والمحرك";
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

            _cboStatus = new ComboBox();
            _cboStatus.DropDownStyle = ComboBoxStyle.DropDownList;
            _cboStatus.Width = 110;
            _cboStatus.Items.Add("كل الحالات");
            _cboStatus.Items.Add("معلّق");
            _cboStatus.Items.Add("معتمد");
            _cboStatus.Items.Add("مرفوض");
            _cboStatus.SelectedIndex = 0;
            _cboStatus.Margin = new Padding(3, 8, 3, 0);

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
            _dtpTo.Value = DateTime.Now.AddMonths(1);

            Button btnRefresh = new Button();
            btnRefresh.Text = "تحديث";
            btnRefresh.Height = 30;
            btnRefresh.Click += delegate { Reload(); };

            _btnApprove = new Button();
            _btnApprove.Text = "اعتماد";
            _btnApprove.Height = 30;
            _btnApprove.Click += delegate { OnApproveClick(); };

            _btnReject = new Button();
            _btnReject.Text = "رفض";
            _btnReject.Height = 30;
            _btnReject.Click += delegate { OnRejectClick(); };

            toolbar.Controls.Add(_cboStatus);
            toolbar.Controls.Add(lblEmployee);
            toolbar.Controls.Add(_txtEmployee);
            toolbar.Controls.Add(_dtpFrom);
            toolbar.Controls.Add(_dtpTo);
            toolbar.Controls.Add(btnRefresh);
            toolbar.Controls.Add(_btnApprove);
            toolbar.Controls.Add(_btnReject);

            _grid = new DataGridView();
            Theme.StyleGrid(_grid);
            Theme.StyleStatusColumn(_grid, "Status"); // status color pill
            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            // approvals quick menu on the grid
            ContextMenuStrip gridMenu = new ContextMenuStrip();
            gridMenu.RightToLeft = RightToLeft.Yes;
            gridMenu.Font = Theme.Font(9.5f);
            ToolStripMenuItem miApprove = new ToolStripMenuItem("اعتماد");
            miApprove.Click += delegate { OnApproveClick(); };
            ToolStripMenuItem miReject = new ToolStripMenuItem("رفض");
            miReject.Click += delegate { OnRejectClick(); };
            ToolStripMenuItem miPrint = new ToolStripMenuItem("طباعة");
            miPrint.Click += delegate { GridPrintHelper.Print(_grid, "قائمة الساعات الإضافية"); };
            gridMenu.Items.Add(miApprove);
            gridMenu.Items.Add(miReject);
            gridMenu.Items.Add(new ToolStripSeparator());
            gridMenu.Items.Add(miPrint);
            _grid.ContextMenuStrip = gridMenu;

            Controls.Add(_grid);
            Controls.Add(toolbar);
            Controls.Add(header);

            bool canApprove = AppContext.HasAnyPermission(new string[] { "Overtime.Approve", "Attendance.Overtime", "Attendance.Edit" });
            _btnApprove.Enabled = canApprove;
            _btnReject.Enabled = canApprove;
            Reload();
        }

        private OvertimeView SelectedRow()
        {
            if (_grid.CurrentRow == null)
            {
                return null;
            }
            return _grid.CurrentRow.Tag as OvertimeView;
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
                string status = StatusValues[_cboStatus.SelectedIndex >= 0 ? _cboStatus.SelectedIndex : 0];
                int? employeeId = ResolveEmployee();
                DateTime? fromDate = _dtpFrom.Checked ? (DateTime?)_dtpFrom.Value.Date : null;
                DateTime? toDate = _dtpTo.Checked ? (DateTime?)_dtpTo.Value.Date : null;
                _rows = OvertimeBLL.Search(fromDate, toDate, employeeId, status);
                _grid.Columns.Clear();
                _grid.Rows.Clear();

                AddColumn("EmployeeCode", "الكود", 70);
                AddColumn("EmployeeName", "الموظف", 160);
                AddColumn("OtDate", "التاريخ", 90);
                AddColumn("Planned", "مخطط (د)", 80);
                AddColumn("Actual", "فعلي (د)", 80);
                AddColumn("Effective", "المحتسَب (د)", 90);
                AddColumn("Rate", "المعامل", 60);
                AddColumn("Source", "المصدر", 80);
                AddColumn("Status", "الحالة", 80);

                foreach (OvertimeView row in _rows)
                {
                    int rowIndex = _grid.Rows.Add();
                    DataGridViewRow gridRow = _grid.Rows[rowIndex];
                    gridRow.Tag = row;
                    gridRow.Cells["EmployeeCode"].Value = row.EmployeeCode;
                    gridRow.Cells["EmployeeName"].Value = row.EmployeeName;
                    gridRow.Cells["OtDate"].Value = row.OtDate == null ? "" :
                        row.OtDate.Value.ToString("yyyy-MM-dd");
                    gridRow.Cells["Planned"].Value = row.PlannedMinutes;
                    gridRow.Cells["Actual"].Value = row.ActualMinutes;
                    gridRow.Cells["Effective"].Value = row.EffectiveMinutes;
                    gridRow.Cells["Rate"].Value = row.Rate == null ? "" : row.Rate.Value.ToString("0.##");
                    bool fromEngine = !string.Equals(row.Source, "Manual", StringComparison.Ordinal);
                    gridRow.Cells["Source"].Value = fromEngine ? "المحرك" : "يدوي";
                    gridRow.Cells["Status"].Value = OvertimeBLL.StatusName(row.Status);
                }
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmOvertime.Reload");
                Theme.ShowError("حدث خطأ أثناء تحميل الساعات الإضافية.");
            }
        }

        private void OnApproveClick()
        {
            OvertimeView row = SelectedRow();
            if (row == null)
            {
                Theme.ShowInfo("اختر سطرًا من القائمة أولاً.");
                return;
            }
            if (row.Status == OvertimeBLL.StatusApproved)
            {
                Theme.ShowInfo("السطر معتمد بالفعل.");
                return;
            }
            if (!Theme.Confirm("اعتماد ساعات " + row.EmployeeName + " (" +
                (row.OtDate == null ? "" : row.OtDate.Value.ToString("yyyy-MM-dd")) + ")؟"))
            {
                return;
            }
            try
            {
                OvertimeBLL.Approve(row.OvertimeId);
                Reload();
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmOvertime.OnApproveClick");
                Theme.ShowError("حدث خطأ أثناء الاعتماد.");
            }
        }

        private void OnRejectClick()
        {
            OvertimeView row = SelectedRow();
            if (row == null)
            {
                Theme.ShowInfo("اختر سطرًا من القائمة أولاً.");
                return;
            }
            if (!Theme.Confirm("رفض ساعات " + row.EmployeeName + " (" +
                (row.OtDate == null ? "" : row.OtDate.Value.ToString("yyyy-MM-dd")) + ")؟"))
            {
                return;
            }
            try
            {
                OvertimeBLL.Reject(row.OvertimeId);
                Reload();
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmOvertime.OnRejectClick");
                Theme.ShowError("حدث خطأ أثناء الرفض.");
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
