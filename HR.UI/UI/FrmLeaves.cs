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
    /// Leave request browser (rule 15): status/employee/date filters, request
    /// life-cycle (add, edit, approve, reject) and a balance viewer. Approval
    /// and rejection are gated by "Leaves.Approve"; add/edit is available to
    /// any authenticated user. All logic lives in LeaveBLL.
    /// </summary>
    public class FrmLeaves : Form
    {
        private static readonly string[] StatusValues =
            new string[] { "", "Pending", "Approved", "Rejected" };

        private ComboBox _cboStatus;
        private TextBox _txtEmployee;
        private DateTimePicker _dtpFrom;
        private DateTimePicker _dtpTo;
        private Button _btnRefresh;
        private Button _btnAdd;
        private Button _btnEdit;
        private Button _btnApprove;
        private Button _btnReject;
        private Button _btnBalances;
        private Button _btnLong;
        private DataGridView _grid;
        private List<LeaveRequestView> _rows;

        public FrmLeaves()
        {
            Theme.Apply(this);
            Text = "الإجازات";
            ClientSize = new Size(1050, 600);
            StartPosition = FormStartPosition.CenterParent;

            Panel header = new Panel();
            Label lblTitle = new Label();
            lblTitle.Text = "إدارة الإجازات — طلبات، اعتماد، وأرصدة";
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
            toolbar.FlowDirection = FlowDirection.RightToLeft;
            toolbar.WrapContents = true;
            toolbar.Padding = new Padding(6, 8, 6, 4);

            _cboStatus = new ComboBox();
            _cboStatus.DropDownStyle = ComboBoxStyle.DropDownList;
            _cboStatus.Width = 110;
            List<string> statusLabels = new List<string>();
            statusLabels.Add("كل الحالات");
            statusLabels.Add("معلّق");
            statusLabels.Add("معتمد");
            statusLabels.Add("مرفوض");
            foreach (string label in statusLabels)
            {
                _cboStatus.Items.Add(label);
            }
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

            _btnRefresh = new Button();
            _btnRefresh.Text = "تحديث";
            _btnRefresh.Height = 30;
            _btnRefresh.Click += delegate { Reload(); };

            _btnAdd = new Button();
            _btnAdd.Text = "طلب إجازة";
            _btnAdd.Height = 30;
            _btnAdd.Click += delegate { OnAddClick(); };

            _btnEdit = new Button();
            _btnEdit.Text = "تعديل";
            _btnEdit.Height = 30;
            _btnEdit.Click += delegate { OnEditClick(); };

            _btnApprove = new Button();
            _btnApprove.Text = "اعتماد";
            _btnApprove.Height = 30;
            _btnApprove.Click += delegate { OnApproveClick(); };

            _btnReject = new Button();
            _btnReject.Text = "رفض";
            _btnReject.Height = 30;
            _btnReject.Click += delegate { OnRejectClick(); };

            _btnBalances = new Button();
            _btnBalances.Text = "الأرصدة";
            _btnBalances.Height = 30;
            _btnBalances.Click += delegate { OnBalancesClick(); };

            _btnLong = new Button();
            _btnLong.Text = "إجازات طويلة";
            _btnLong.Height = 30;
            _btnLong.Click += delegate { OnLongLeavesClick(); };

            toolbar.Controls.Add(_cboStatus);
            toolbar.Controls.Add(lblEmployee);
            toolbar.Controls.Add(_txtEmployee);
            toolbar.Controls.Add(_dtpFrom);
            toolbar.Controls.Add(_dtpTo);
            toolbar.Controls.Add(_btnRefresh);
            toolbar.Controls.Add(_btnAdd);
            toolbar.Controls.Add(_btnEdit);
            toolbar.Controls.Add(_btnApprove);
            toolbar.Controls.Add(_btnReject);
            toolbar.Controls.Add(_btnBalances);
            toolbar.Controls.Add(_btnLong);

            _grid = new DataGridView();
            Theme.StyleGrid(_grid);
            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _grid.SelectionChanged += delegate { UpdateStatusIndicators(); };

            Controls.Add(_grid);
            Controls.Add(toolbar);
            Controls.Add(header);

            _btnApprove.Enabled = AppContext.HasPermission("Leaves.Approve");
            _btnReject.Enabled = AppContext.HasPermission("Leaves.Approve");
            Reload();
        }

        private LeaveRequestView SelectedRequest()
        {
            if (_grid.CurrentRow == null)
            {
                return null;
            }
            return _grid.CurrentRow.Tag as LeaveRequestView;
        }

        private void UpdateStatusIndicators()
        {
            LeaveRequestView request = SelectedRequest();
            if (request == null)
            {
                return;
            }
            if (request.Status == LeaveBLL.StatusApproved)
            {
                _grid.CurrentRow.DefaultCellStyle.BackColor = Theme.StatusOk;
            }
            else if (request.Status == LeaveBLL.StatusRejected)
            {
                _grid.CurrentRow.DefaultCellStyle.BackColor = Theme.StatusBad;
            }
            else
            {
                _grid.CurrentRow.DefaultCellStyle.BackColor = Theme.StatusWarn;
            }
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
                _rows = LeaveBLL.Search(status, employeeId, fromDate, toDate);
                _grid.Columns.Clear();
                _grid.Rows.Clear();

                AddColumn("EmployeeCode", "الكود", 70);
                AddColumn("EmployeeName", "الموظف", 160);
                AddColumn("TypeName", "النوع", 100);
                AddColumn("StartDate", "من", 90);
                AddColumn("EndDate", "إلى", 90);
                AddColumn("DaysCount", "الأيام", 60);
                AddColumn("Status", "الحالة", 80);
                AddColumn("ApprovedBy", "المُعتمِد", 80);
                AddColumn("Reason", "السبب", 140);
                AddColumn("RejectReason", "سبب الرفض", 140);

                foreach (LeaveRequestView request in _rows)
                {
                    int rowIndex = _grid.Rows.Add();
                    DataGridViewRow row = _grid.Rows[rowIndex];
                    row.Tag = request;
                    row.Cells["EmployeeCode"].Value = request.EmployeeCode;
                    row.Cells["EmployeeName"].Value = request.EmployeeName;
                    row.Cells["TypeName"].Value = request.TypeName;
                    row.Cells["StartDate"].Value = request.StartDate == null ? null :
                        request.StartDate.Value.ToString("yyyy-MM-dd");
                    row.Cells["EndDate"].Value = request.EndDate == null ? null :
                        request.EndDate.Value.ToString("yyyy-MM-dd");
                    row.Cells["DaysCount"].Value = request.DaysCount;
                    row.Cells["Status"].Value = TranslateStatus(request.Status);
                    row.Cells["ApprovedBy"].Value = request.ApprovedBy;
                    row.Cells["Reason"].Value = request.Reason;
                    row.Cells["RejectReason"].Value = request.RejectReason;
                }
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmLeaves.Reload");
                Theme.ShowError("حدث خطأ أثناء تحميل طلبات الإجازة.");
            }
        }

        private void OnAddClick()
        {
            FrmLeaveEdit dialog = new FrmLeaveEdit(null);
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                Reload();
            }
        }

        private void OnEditClick()
        {
            LeaveRequestView request = SelectedRequest();
            if (request == null)
            {
                Theme.ShowInfo("اختر طلبًا من القائمة أولًا.");
                return;
            }
            if (request.Status == LeaveBLL.StatusApproved)
            {
                Theme.ShowError("لا يمكن تعديل طلب معتمد.");
                return;
            }
            if (request.Status == LeaveBLL.StatusRejected)
            {
                Theme.ShowError("لا يمكن تعديل طلب مرفوض — استخدم زر سبب الرفض.");
                return;
            }
            FrmLeaveEdit dialog = new FrmLeaveEdit(request.LeaveId);
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                Reload();
            }
        }

        private void OnApproveClick()
        {
            LeaveRequestView request = SelectedRequest();
            if (request == null)
            {
                Theme.ShowInfo("اختر طلبًا من القائمة أولًا.");
                return;
            }
            if (!Theme.Confirm("اعتماد إجازة الموظف " + request.EmployeeName +
                " من " + request.StartDate.Value.ToString("yyyy-MM-dd") +
                " إلى " + request.EndDate.Value.ToString("yyyy-MM-dd") + "؟"))
            {
                return;
            }
            try
            {
                LeaveBLL.Approve(request.LeaveId);
                Theme.ShowInfo("تم اعتماد الإجازة وخصمها من الرصيد.");
                Reload();
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmLeaves.OnApproveClick");
                Theme.ShowError("حدث خطأ أثناء اعتماد الإجازة.");
            }
        }

        private void OnRejectClick()
        {
            LeaveRequestView request = SelectedRequest();
            if (request == null)
            {
                Theme.ShowInfo("اختر طلبًا من القائمة أولاً.");
                return;
            }
            if (request.Status == LeaveBLL.StatusApproved)
            {
                Theme.ShowError("لا يمكن رفض طلب معتمد.");
                return;
            }
            if (request.Status == LeaveBLL.StatusPending)
            {
                if (!Theme.Confirm("رفض طلب إجازة الموظف " + request.EmployeeName + "؟"))
                {
                    return;
                }
                FrmRejectReason dialog = new FrmRejectReason("سبب رفض طلب الإجازة", null);
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
                try
                {
                    LeaveBLL.Reject(request.LeaveId, dialog.ReasonText);
                    Theme.ShowInfo("تم رفض الطلب.");
                    Reload();
                }
                catch (BusinessException expected)
                {
                    Theme.ShowError(expected.Message);
                }
                catch (Exception unexpected)
                {
                    Logger.LogError(unexpected, "FrmLeaves.OnRejectClick");
                    Theme.ShowError("حدث خطأ أثناء رفض طلب الإجازة.");
                }
                return;
            }
            if (request.Status == LeaveBLL.StatusRejected)
            {
                FrmRejectReason dialog = new FrmRejectReason("تحديث سبب الرفض", request.RejectReason);
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
                try
                {
                    LeaveBLL.SetRejectReason(request.LeaveId, dialog.ReasonText);
                    Theme.ShowInfo("تم تحديث سبب الرفض.");
                    Reload();
                }
                catch (BusinessException expected)
                {
                    Theme.ShowError(expected.Message);
                }
                catch (Exception unexpected)
                {
                    Logger.LogError(unexpected, "FrmLeaves.OnRejectClick");
                    Theme.ShowError("حدث خطأ أثناء تحديث سبب الرفض.");
                }
            }
        }

        private void OnLongLeavesClick()
        {
            FrmLongLeaves dialog = new FrmLongLeaves();
            dialog.ShowDialog(this);
        }

        private void OnBalancesClick()
        {
            LeaveRequestView request = SelectedRequest();
            FrmLeaveBalances dialog = new FrmLeaveBalances(request == null ? null : (int?)request.EmployeeId);
            dialog.ShowDialog(this);
        }

        private static string TranslateStatus(string status)
        {
            if (status == LeaveBLL.StatusApproved)
            {
                return "معتمد";
            }
            if (status == LeaveBLL.StatusRejected)
            {
                return "مرفوض";
            }
            if (status == LeaveBLL.StatusPending)
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
