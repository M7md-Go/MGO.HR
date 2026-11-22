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
    /// Manual attendance corrections: requests that fix the punches or final
    /// status of one employee-day. Approval (permission
    /// "Attendance.ApproveAdjust") applies the correction to [Attendance] with
    /// IsManuallyChg so recalculations keep it.
    /// </summary>
    public class FrmAdjustments : Form
    {
        private static readonly string[] StatusValues =
            new string[] { "", "Pending", "Approved", "Rejected" };

        private ComboBox _cboStatus;
        private TextBox _txtEmployee;
        private DateTimePicker _dtpFrom;
        private DateTimePicker _dtpTo;
        private Button _btnAdd;
        private Button _btnEdit;
        private Button _btnApprove;
        private Button _btnReject;
        private DataGridView _grid;
        private List<AttendanceAdjustmentView> _rows;

        public FrmAdjustments()
        {
            Theme.Apply(this, false); // MDI child: mirrored layout is not applied — RTL flow comes from the containers
            Text = "تصحيحات الحضور اليدوية";
            ClientSize = new Size(1080, 580);
            StartPosition = FormStartPosition.CenterParent;

            Panel header = new Panel();
            Label lblTitle = new Label();
            lblTitle.Text = "تصحيحات الحضور اليدوية — اعتماد يُطبق على سجل الحضور فعليًا";
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
            _cboStatus.Items.Add("مطبّق");
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
            _dtpTo.Value = DateTime.Now.Date;

            Button btnRefresh = new Button();
            btnRefresh.Text = "تحديث";
            btnRefresh.Height = 30;
            btnRefresh.Click += delegate { Reload(); };

            _btnAdd = new Button();
            _btnAdd.Text = "تصحيح جديد";
            _btnAdd.Height = 30;
            _btnAdd.Click += delegate { OnAddClick(); };

            _btnEdit = new Button();
            _btnEdit.Text = "تعديل";
            _btnEdit.Height = 30;
            _btnEdit.Click += delegate { OnEditClick(); };

            _btnApprove = new Button();
            _btnApprove.Text = "اعتماد وتطبيق";
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
            toolbar.Controls.Add(_btnAdd);
            toolbar.Controls.Add(_btnEdit);
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
            _grid.CellDoubleClick += delegate { OnEditClick(); };

            // management quick menu on the grid
            ContextMenuStrip gridMenu = new ContextMenuStrip();
            gridMenu.RightToLeft = RightToLeft.Yes;
            gridMenu.Font = Theme.Font(9.5f);
            ToolStripMenuItem miApprove = new ToolStripMenuItem("اعتماد وتطبيق");
            miApprove.Click += delegate { OnApproveClick(); };
            ToolStripMenuItem miReject = new ToolStripMenuItem("رفض");
            miReject.Click += delegate { OnRejectClick(); };
            ToolStripMenuItem miOpen = new ToolStripMenuItem("فتح / عرض");
            miOpen.Click += delegate { OnEditClick(); };
            ToolStripMenuItem miPrint = new ToolStripMenuItem("طباعة");
            miPrint.Click += delegate { GridPrintHelper.Print(_grid, "قائمة تصحيحات الحضور"); };
            gridMenu.Items.Add(miApprove);
            gridMenu.Items.Add(miReject);
            gridMenu.Items.Add(miOpen);
            gridMenu.Items.Add(new ToolStripSeparator());
            gridMenu.Items.Add(miPrint);
            _grid.ContextMenuStrip = gridMenu;

            Controls.Add(_grid);
            Controls.Add(toolbar);
            Controls.Add(header);

            bool canEdit = AppContext.HasPermission("Attendance.Edit");
            bool canApprove = AppContext.HasPermission("Attendance.ApproveAdjust");
            _btnAdd.Enabled = canEdit;
            _btnEdit.Enabled = canEdit;
            _btnApprove.Enabled = canApprove;
            _btnReject.Enabled = canApprove;
            Reload();
        }

        private AttendanceAdjustmentView SelectedRow()
        {
            if (_grid.CurrentRow == null)
            {
                return null;
            }
            return _grid.CurrentRow.Tag as AttendanceAdjustmentView;
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
                _rows = AttendanceAdjustmentBLL.Browse(status, employeeId, fromDate, toDate);
                _grid.Columns.Clear();
                _grid.Rows.Clear();

                AddColumn("EmployeeCode", "الكود", 70);
                AddColumn("EmployeeName", "الموظف", 150);
                AddColumn("AttDate", "اليوم", 90);
                AddColumn("Type", "النوع", 110);
                AddColumn("In", "دخول جديد", 70);
                AddColumn("Out", "خروج جديد", 70);
                AddColumn("Status2", "الحالة الجديدة", 100);
                AddColumn("Reason", "السبب", 170);
                AddColumn("Status", "حالة الطلب", 80);

                foreach (AttendanceAdjustmentView row in _rows)
                {
                    int rowIndex = _grid.Rows.Add();
                    DataGridViewRow gridRow = _grid.Rows[rowIndex];
                    gridRow.Tag = row;
                    gridRow.Cells["EmployeeCode"].Value = row.EmployeeCode;
                    gridRow.Cells["EmployeeName"].Value = row.EmployeeName;
                    gridRow.Cells["AttDate"].Value = row.AttDate == null ? "" :
                        row.AttDate.Value.ToString("yyyy-MM-dd");
                    gridRow.Cells["Type"].Value = AttendanceAdjustmentBLL.TypeName(row.AdjustmentType);
                    gridRow.Cells["In"].Value = row.NewInTime;
                    gridRow.Cells["Out"].Value = row.NewOutTime;
                    gridRow.Cells["Status2"].Value = AttendanceAdjustmentBLL.StatusName(row.NewStatus);
                    gridRow.Cells["Reason"].Value = row.Reason;
                    gridRow.Cells["Status"].Value = TranslateStatus(row.Status);
                }
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmAdjustments.Reload");
                Theme.ShowError("حدث خطأ أثناء تحميل التصحيحات.");
            }
        }

        private void OnAddClick()
        {
            FrmAdjustmentEdit dialog = new FrmAdjustmentEdit(null);
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                Reload();
            }
        }

        private void OnEditClick()
        {
            AttendanceAdjustmentView row = SelectedRow();
            if (row == null)
            {
                Theme.ShowInfo("اختر تصحيحًا من القائمة أولاً.");
                return;
            }
            if (row.Status != AttendanceAdjustmentBLL.StatusPending)
            {
                Theme.ShowInfo("التصحيحات المعتمدة والمرفوضة للعرض فقط.");
            }
            FrmAdjustmentEdit dialog = new FrmAdjustmentEdit(row.AdjustmentId);
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                Reload();
            }
        }

        private void OnApproveClick()
        {
            AttendanceAdjustmentView row = SelectedRow();
            if (row == null)
            {
                Theme.ShowInfo("اختر تصحيحًا من القائمة أولاً.");
                return;
            }
            if (row.Status != AttendanceAdjustmentBLL.StatusPending)
            {
                Theme.ShowInfo("الطلب معتمد أو مرفوض بالفعل.");
                return;
            }
            if (!Theme.Confirm("اعتماد تصحيح يوم " + (row.AttDate == null ? "" : row.AttDate.Value.ToString("yyyy-MM-dd")) + " للموظف " +
                row.EmployeeName + "؟\nسيُطبَّق التصحيح على سجل الحضور فورًا ولن يمسه إعادة الحساب."))
            {
                return;
            }
            try
            {
                AttendanceAdjustmentBLL.Approve(row.AdjustmentId);
                Theme.ShowInfo("تم اعتماد التصحيح وتطبيقه على سجل الحضور.");
                Reload();
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmAdjustments.OnApproveClick");
                Theme.ShowError("حدث خطأ أثناء اعتماد التصحيح.");
            }
        }

        private void OnRejectClick()
        {
            AttendanceAdjustmentView row = SelectedRow();
            if (row == null)
            {
                Theme.ShowInfo("اختر تصحيحًا من القائمة أولاً.");
                return;
            }
            if (row.Status != AttendanceAdjustmentBLL.StatusPending)
            {
                Theme.ShowInfo("الطلب معتمد أو مرفوض بالفعل.");
                return;
            }
            if (!Theme.Confirm("رفض تصحيح يوم " + (row.AttDate == null ? "" : row.AttDate.Value.ToString("yyyy-MM-dd")) + " للموظف " + row.EmployeeName + "؟"))
            {
                return;
            }
            try
            {
                AttendanceAdjustmentBLL.Reject(row.AdjustmentId);
                Reload();
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmAdjustments.OnRejectClick");
                Theme.ShowError("حدث خطأ أثناء رفض التصحيح.");
            }
        }

        private static string TranslateStatus(string status)
        {
            if (status == AttendanceAdjustmentBLL.StatusApproved)
            {
                return "مطبّق";
            }
            if (status == AttendanceAdjustmentBLL.StatusRejected)
            {
                return "مرفوض";
            }
            if (status == AttendanceAdjustmentBLL.StatusPending)
            {
                return "معلّق";
            }
            return status;
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
