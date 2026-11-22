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
    /// Mission browser (rule 16): status/employee/date filters and the
    /// request life-cycle. Approval and rejection are gated by
    /// "Missions.Approve"; add/edit is available to any authenticated user.
    /// All logic lives in MissionBLL.
    /// </summary>
    public class FrmMissions : Form
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
        private DataGridView _grid;
        private List<MissionView> _rows;

        public FrmMissions()
        {
            Theme.Apply(this, false); // MDI child: mirrored layout is not applied — RTL flow comes from the containers
            Text = "المأموريات";
            ClientSize = new Size(1050, 600);
            StartPosition = FormStartPosition.CenterParent;

            Panel header = new Panel();
            Label lblTitle = new Label();
            lblTitle.Text = "إدارة المأموريات — اعتماد وإعفاء من حساب الحضور";
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

            _btnRefresh = new Button();
            _btnRefresh.Text = "تحديث";
            _btnRefresh.Height = 30;
            _btnRefresh.Click += delegate { Reload(); };

            _btnAdd = new Button();
            _btnAdd.Text = "مأمورية جديدة";
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

            _grid = new DataGridView();
            Theme.StyleGrid(_grid);
            Theme.StyleStatusColumn(_grid, "Status"); // status color pill
            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _grid.SelectionChanged += delegate { UpdateStatusIndicators(); };

            // approvals quick menu on the grid (mirror of the toolbar actions)
            ContextMenuStrip gridMenu = new ContextMenuStrip();
            gridMenu.RightToLeft = RightToLeft.Yes;
            gridMenu.Font = Theme.Font(9.5f);
            ToolStripMenuItem miApprove = new ToolStripMenuItem("اعتماد");
            miApprove.Click += delegate { OnApproveClick(); };
            ToolStripMenuItem miReject = new ToolStripMenuItem("رفض");
            miReject.Click += delegate { OnRejectClick(); };
            ToolStripMenuItem miOpen = new ToolStripMenuItem("فتح / عرض");
            miOpen.Click += delegate { OnEditClick(); };
            ToolStripMenuItem miPrint = new ToolStripMenuItem("طباعة");
            miPrint.Click += delegate { GridPrintHelper.Print(_grid, "قائمة المأموريات"); };
            gridMenu.Items.Add(miApprove);
            gridMenu.Items.Add(miReject);
            gridMenu.Items.Add(miOpen);
            gridMenu.Items.Add(new ToolStripSeparator());
            gridMenu.Items.Add(miPrint);
            _grid.ContextMenuStrip = gridMenu;

            Controls.Add(_grid);
            Controls.Add(toolbar);
            Controls.Add(header);

            _btnApprove.Enabled = AppContext.HasPermission("Missions.Approve");
            _btnReject.Enabled = AppContext.HasPermission("Missions.Approve");
            Reload();
        }

        private MissionView SelectedMission()
        {
            if (_grid.CurrentRow == null)
            {
                return null;
            }
            return _grid.CurrentRow.Tag as MissionView;
        }

        private void UpdateStatusIndicators()
        {
            MissionView mission = SelectedMission();
            if (mission == null)
            {
                return;
            }
            if (mission.Status == MissionBLL.StatusApproved)
            {
                _grid.CurrentRow.DefaultCellStyle.BackColor = Theme.StatusOk;
            }
            else if (mission.Status == MissionBLL.StatusRejected)
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
                _rows = MissionBLL.Search(status, employeeId, fromDate, toDate);
                _grid.Columns.Clear();
                _grid.Rows.Clear();

                AddColumn("EmployeeCode", "الكود", 70);
                AddColumn("EmployeeName", "الموظف", 150);
                AddColumn("Start", "من", 110);
                AddColumn("End", "إلى", 110);
                AddColumn("Location", "المكان", 110);
                AddColumn("Purpose", "السبب", 130);
                AddColumn("IsExemptAtt", "إعفاء حضور", 80);
                AddColumn("Status", "الحالة", 80);
                AddColumn("ApprovedBy", "المُعتمِد", 80);

                foreach (MissionView mission in _rows)
                {
                    int rowIndex = _grid.Rows.Add();
                    DataGridViewRow row = _grid.Rows[rowIndex];
                    row.Tag = mission;
                    row.Cells["EmployeeCode"].Value = mission.EmployeeCode;
                    row.Cells["EmployeeName"].Value = mission.EmployeeName;
                    row.Cells["Start"].Value = (mission.StartDate == null ? null :
                        mission.StartDate.Value.ToString("yyyy-MM-dd")) +
                        (string.IsNullOrEmpty(mission.StartTime) ? "" : " " + mission.StartTime);
                    row.Cells["End"].Value = (mission.EndDate == null ? null :
                        mission.EndDate.Value.ToString("yyyy-MM-dd")) +
                        (string.IsNullOrEmpty(mission.EndTime) ? "" : " " + mission.EndTime);
                    row.Cells["Location"].Value = mission.Location;
                    row.Cells["Purpose"].Value = mission.Purpose;
                    row.Cells["IsExemptAtt"].Value = mission.IsExemptAtt == true ? "نعم" : "لا";
                    row.Cells["Status"].Value = TranslateStatus(mission.Status);
                    row.Cells["ApprovedBy"].Value = mission.ApprovedBy;
                }
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmMissions.Reload");
                Theme.ShowError("حدث خطأ أثناء تحميل المأموريات.");
            }
        }

        private void OnAddClick()
        {
            FrmMissionEdit dialog = new FrmMissionEdit(null);
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                Reload();
            }
        }

        private void OnEditClick()
        {
            MissionView mission = SelectedMission();
            if (mission == null)
            {
                Theme.ShowInfo("اختر مأمورية من القائمة أولًا.");
                return;
            }
            if (mission.Status == MissionBLL.StatusApproved)
            {
                Theme.ShowError("لا يمكن تعديل مأمورية معتمدة.");
                return;
            }
            if (mission.Status == MissionBLL.StatusRejected)
            {
                Theme.ShowError("لا يمكن تعديل مأمورية مرفوضة — أنشئ مأمورية جديدة.");
                return;
            }
            FrmMissionEdit dialog = new FrmMissionEdit(mission.MissionId);
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                Reload();
            }
        }

        private void OnApproveClick()
        {
            MissionView mission = SelectedMission();
            if (mission == null)
            {
                Theme.ShowInfo("اختر مأمورية من القائمة أولًا.");
                return;
            }
            if (!Theme.Confirm("اعتماد مأمورية الموظف " + mission.EmployeeName + "؟"))
            {
                return;
            }
            try
            {
                MissionBLL.Approve(mission.MissionId);
                Theme.ShowInfo("تم اعتماد المأمورية؛ سيتجاهل محرك الحضور أيامها إذا كانت معفاة.");
                Reload();
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmMissions.OnApproveClick");
                Theme.ShowError("حدث خطأ أثناء اعتماد المأمورية.");
            }
        }

        private void OnRejectClick()
        {
            MissionView mission = SelectedMission();
            if (mission == null)
            {
                Theme.ShowInfo("اختر مأمورية من القائمة أولًا.");
                return;
            }
            if (!Theme.Confirm("رفض مأمورية الموظف " + mission.EmployeeName + "؟"))
            {
                return;
            }
            try
            {
                MissionBLL.Reject(mission.MissionId);
                Reload();
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmMissions.OnRejectClick");
                Theme.ShowError("حدث خطأ أثناء رفض المأمورية.");
            }
        }

        private static string TranslateStatus(string status)
        {
            if (status == MissionBLL.StatusApproved)
            {
                return "معتمد";
            }
            if (status == MissionBLL.StatusRejected)
            {
                return "مرفوض";
            }
            if (status == MissionBLL.StatusPending)
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
