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
    /// Long-duration leaves browser (child care, family care, going abroad,
    /// income improvement): status/employee filters, full life-cycle
    /// (add, edit, approve, reject, end) with an explicit end date for
    /// approved or open-ended periods. Approval actions are gated by
    /// "Leaves.Approve"; all logic lives in LongLeaveBLL.
    /// </summary>
    public class FrmLongLeaves : Form
    {
        private static readonly string[] StatusValues =
            new string[] { "", "Pending", "Approved", "Rejected", "Ended" };

        private ComboBox _cboStatus;
        private ComboBox _cboType;
        private TextBox _txtEmployee;
        private Button _btnRefresh;
        private Button _btnAdd;
        private Button _btnEdit;
        private Button _btnApprove;
        private Button _btnReject;
        private Button _btnEnd;
        private DataGridView _grid;
        private List<LongLeaveView> _rows;

        public FrmLongLeaves()
        {
            Theme.Apply(this, false); // MDI child: mirrored layout is not applied — RTL flow comes from the containers
            Text = "الإجازات طويلة المدة";
            ClientSize = new Size(1050, 600);
            StartPosition = FormStartPosition.CenterParent;

            Panel header = new Panel();
            Label lblTitle = new Label();
            lblTitle.Text = "الإجازات طويلة المدة — كرعاية الطفل والأسرة والسفر والتحسين";
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
            string[] statusLabels = new string[] { "كل الحالات", "معلّق", "معتمد", "مرفوض", "منتهٍ" };
            foreach (string label in statusLabels)
            {
                _cboStatus.Items.Add(label);
            }
            _cboStatus.SelectedIndex = 0;
            _cboStatus.Margin = new Padding(3, 8, 3, 0);

            _cboType = new ComboBox();
            _cboType.DropDownStyle = ComboBoxStyle.DropDownList;
            _cboType.Width = 150;
            _cboType.Margin = new Padding(3, 8, 3, 0);
            _cboType.Items.Add("كل الأنواع");
            _cboType.SelectedIndex = 0;

            Label lblEmployee = new Label();
            lblEmployee.Text = "الموظف:";
            lblEmployee.AutoSize = true;
            lblEmployee.Margin = new Padding(3, 10, 3, 0);

            _txtEmployee = new TextBox();
            _txtEmployee.Width = 130;
            _txtEmployee.Margin = new Padding(3, 8, 3, 0);

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

            _btnEnd = new Button();
            _btnEnd.Text = "تحديد النهاية";
            _btnEnd.Height = 30;
            _btnEnd.Click += delegate { OnEndClick(); };

            toolbar.Controls.Add(_cboStatus);
            toolbar.Controls.Add(_cboType);
            toolbar.Controls.Add(lblEmployee);
            toolbar.Controls.Add(_txtEmployee);
            toolbar.Controls.Add(_btnRefresh);
            toolbar.Controls.Add(_btnAdd);
            toolbar.Controls.Add(_btnEdit);
            toolbar.Controls.Add(_btnApprove);
            toolbar.Controls.Add(_btnReject);
            toolbar.Controls.Add(_btnEnd);

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
            _btnEnd.Enabled = AppContext.HasPermission("Leaves.Approve");
            LoadTypes();
            Reload();
        }

        private void LoadTypes()
        {
            try
            {
                List<LongLeaveType> types = LongLeaveBLL.Types();
                _cboType.Items.Clear();
                _cboType.Items.Add("كل الأنواع");
                foreach (LongLeaveType type in types)
                {
                    _cboType.Items.Add(type.TypeName);
                }
                _cboType.SelectedIndex = 0;
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmLongLeaves.LoadTypes");
            }
        }

        private LongLeaveView Selected()
        {
            if (_grid.CurrentRow == null)
            {
                return null;
            }
            return _grid.CurrentRow.Tag as LongLeaveView;
        }

        private void UpdateStatusIndicators()
        {
            LongLeaveView request = Selected();
            if (request == null)
            {
                return;
            }
            if (request.Status == LongLeaveBLL.StatusApproved)
            {
                _grid.CurrentRow.DefaultCellStyle.BackColor = Theme.StatusOk;
            }
            else if (request.Status == LongLeaveBLL.StatusRejected)
            {
                _grid.CurrentRow.DefaultCellStyle.BackColor = Theme.StatusBad;
            }
            else if (request.Status == LongLeaveBLL.StatusEnded)
            {
                _grid.CurrentRow.DefaultCellStyle.BackColor = Theme.StatusOk;
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

        private int? SelectedTypeId()
        {
            int statusIndex = _cboType.SelectedIndex;
            if (statusIndex <= 0)
            {
                return null;
            }
            List<LongLeaveType> types = LongLeaveBLL.Types();
            if (statusIndex - 1 < types.Count)
            {
                return types[statusIndex - 1].LongLeaveTypeId;
            }
            return null;
        }

        private void Reload()
        {
            try
            {
                string status = StatusValues[_cboStatus.SelectedIndex >= 0 ? _cboStatus.SelectedIndex : 0];
                int? typeId = SelectedTypeId();
                int? employeeId = ResolveEmployee();
                _rows = LongLeaveBLL.Search(status, typeId, employeeId, null, null);
                _grid.Columns.Clear();
                _grid.Rows.Clear();

                AddColumn("EmployeeCode", "الكود", 70);
                AddColumn("EmployeeName", "الموظف", 160);
                AddColumn("TypeName", "النوع", 120);
                AddColumn("FromDate", "من", 90);
                AddColumn("ToDate", "إلى", 90);
                AddColumn("Duration", "المدة", 100);
                AddColumn("Status", "الحالة", 80);
                AddColumn("Reason", "السبب", 140);
                AddColumn("Notes", "ملاحظات", 140);

                foreach (LongLeaveView request in _rows)
                {
                    int rowIndex = _grid.Rows.Add();
                    DataGridViewRow row = _grid.Rows[rowIndex];
                    row.Tag = request;
                    row.Cells["EmployeeCode"].Value = request.EmployeeCode;
                    row.Cells["EmployeeName"].Value = request.EmployeeName;
                    row.Cells["TypeName"].Value = request.TypeName;
                    row.Cells["FromDate"].Value = request.FromDate == null ? null :
                        request.FromDate.Value.ToString("yyyy-MM-dd");
                    row.Cells["ToDate"].Value = request.ToDate == null ? "غير محددة" :
                        request.ToDate.Value.ToString("yyyy-MM-dd");
                    row.Cells["Duration"].Value = DescribeDuration(request);
                    row.Cells["Status"].Value = TranslateStatus(request.Status);
                    row.Cells["Reason"].Value = request.Reason;
                    row.Cells["Notes"].Value = request.Notes;
                }
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmLongLeaves.Reload");
                Theme.ShowError("حدث خطأ أثناء تحميل الإجازات طويلة المدة.");
            }
        }

        private static string DescribeDuration(LongLeaveView request)
        {
            if (request.ToDate == null)
            {
                return "غير محددة";
            }
            if (request.FromDate == null)
            {
                return string.Empty;
            }
            int days = (request.ToDate.Value.Date - request.FromDate.Value.Date).Days + 1;
            return days.ToString() + " يوماً";
        }

        private static string TranslateStatus(string status)
        {
            if (status == LongLeaveBLL.StatusApproved)
            {
                return "معتمد";
            }
            if (status == LongLeaveBLL.StatusRejected)
            {
                return "مرفوض";
            }
            if (status == LongLeaveBLL.StatusEnded)
            {
                return "منتهٍ";
            }
            if (status == LongLeaveBLL.StatusPending)
            {
                return "معلّق";
            }
            return status;
        }

        private void OnAddClick()
        {
            FrmLongLeaveEdit dialog = new FrmLongLeaveEdit(null);
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                Reload();
            }
        }

        private void OnEditClick()
        {
            LongLeaveView request = Selected();
            if (request == null)
            {
                Theme.ShowInfo("اختر طلبًا من القائمة أولاً.");
                return;
            }
            if (request.Status == LongLeaveBLL.StatusApproved || request.Status == LongLeaveBLL.StatusEnded)
            {
                Theme.ShowError("لا يمكن تعديل إجازة معتمدة — استخدم «تحديد النهاية».");
                return;
            }
            FrmLongLeaveEdit dialog = new FrmLongLeaveEdit(request.LongLeaveId);
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                Reload();
            }
        }

        private void OnApproveClick()
        {
            LongLeaveView request = Selected();
            if (request == null)
            {
                Theme.ShowInfo("اختر طلبًا من القائمة أولاً.");
                return;
            }
            if (!Theme.Confirm("اعتماد إجازة الموظف " + request.EmployeeName + " (" + request.TypeName + ")؟"))
            {
                return;
            }
            try
            {
                LongLeaveBLL.Approve(request.LongLeaveId);
                Theme.ShowInfo("تم اعتماد الإجازة وستوقف الحساب تلقائياً طوال الفترة.");
                Reload();
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmLongLeaves.OnApproveClick");
                Theme.ShowError("حدث خطأ أثناء اعتماد الإجازة.");
            }
        }

        private void OnRejectClick()
        {
            LongLeaveView request = Selected();
            if (request == null)
            {
                Theme.ShowInfo("اختر طلبًا من القائمة أولاً.");
                return;
            }
            if (!Theme.Confirm("رفض طلب الموظف " + request.EmployeeName + "؟"))
            {
                return;
            }
            try
            {
                LongLeaveBLL.Reject(request.LongLeaveId);
                Reload();
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmLongLeaves.OnRejectClick");
                Theme.ShowError("حدث خطأ أثناء رفض الطلب.");
            }
        }

        private void OnEndClick()
        {
            LongLeaveView request = Selected();
            if (request == null)
            {
                Theme.ShowInfo("اختر طلبًا من القائمة أولاً.");
                return;
            }
            if (request.Status == LongLeaveBLL.StatusRejected)
            {
                Theme.ShowError("الطلب مرفوض بالفعل.");
                return;
            }
            using (FrmEndDialog dialog = new FrmEndDialog(request))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
            }
            Reload();
        }

        private sealed class FrmEndDialog : Form
        {
            private readonly LongLeaveView _request;
            private readonly DateTimePicker _dtpEnd;

            public FrmEndDialog(LongLeaveView request)
            {
                _request = request;
                Theme.Apply(this);
                Text = "تحديد تاريخ نهاية الإجازة";
                ClientSize = new Size(360, 150);
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MinimizeBox = false;
                MaximizeBox = false;
                StartPosition = FormStartPosition.CenterParent;

                Label lbl = new Label();
                lbl.Text = "تاريخ نهاية إجازة " + (request.EmployeeName ?? string.Empty) + ":";
                lbl.Location = new Point(20, 20);
                lbl.AutoSize = true;

                _dtpEnd = new DateTimePicker();
                _dtpEnd.Format = DateTimePickerFormat.Short;
                _dtpEnd.Location = new Point(20, 50);
                _dtpEnd.Size = new Size(300, 26);
                _dtpEnd.Value = DateTime.Now.Date;

                Button btnOk = new Button();
                btnOk.Text = "موافق";
                btnOk.Location = new Point(210, 100);
                btnOk.Size = new Size(110, 30);
                btnOk.Click += delegate { OnOk(); };

                Button btnCancel = new Button();
                btnCancel.Text = "إلغاء";
                btnCancel.Location = new Point(20, 100);
                btnCancel.Size = new Size(110, 30);
                btnCancel.DialogResult = DialogResult.Cancel;

                Controls.Add(lbl);
                Controls.Add(_dtpEnd);
                Controls.Add(btnOk);
                Controls.Add(btnCancel);
                CancelButton = btnCancel;
            }

            private void OnOk()
            {
                try
                {
                    LongLeaveBLL.SetEndDate(_request.LongLeaveId, _dtpEnd.Value.Date);
                    Theme.ShowInfo("تم تحديث تاريخ نهاية الإجازة.");
                    DialogResult = DialogResult.OK;
                    Close();
                }
                catch (BusinessException expected)
                {
                    Theme.ShowError(expected.Message);
                }
                catch (Exception unexpected)
                {
                    Logger.LogError(unexpected, "FrmLongLeaves.FrmEndDialog");
                    Theme.ShowError("حدث خطأ أثناء تحديد نهاية الإجازة.");
                }
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
