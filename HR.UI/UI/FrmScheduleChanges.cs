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
    /// Post-approval shift-change requests (RosterCore lesson, order 45f44386):
    /// list / create / decide / execute. Create needs "Schedule.Edit" and is only
    /// allowed when the month is approved+locked; the hour-balance rule
    /// (sum of new hours == sum of old hours across the group) is enforced in
    /// ScheduleChangeBLL. Decide needs "Schedule.Approve" (rejection reason is
    /// mandatory); Execute needs "Schedule.Unlock" and writes through
    /// ScheduleDAL.ReplaceEmployeeDetails. Every step is audited.
    /// </summary>
    public class FrmScheduleChanges : Form
    {
        private ComboBox _cboStatus;
        private Button _btnRefresh;
        private Button _btnNew;
        private Button _btnDecideApprove;
        private Button _btnDecideReject;
        private Button _btnExecute;
        private Button _btnItems;
        private DataGridView _grid;
        private Dictionary<int, string> _userNames;

        public FrmScheduleChanges()
        {
            Theme.Apply(this, false); // MDI child: mirrored layout is not applied — RTL flow comes from the containers
            Text = "طلبات تعديل الورديات (بعد الاعتماد)";
            ClientSize = new Size(900, 560);
            StartPosition = FormStartPosition.CenterParent;

            Panel header = new Panel();
            Label lblTitle = new Label();
            lblTitle.Text = "طلبات تعديل الورديات بعد اعتماد الجدول وقفله — شرط التوازن: مجموع الساعات الجديدة = القديم";
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
            toolbar.FlowDirection = FlowDirection.LeftToRight;
            toolbar.WrapContents = false;
            toolbar.Padding = new Padding(6, 8, 6, 4);

            Label lblStatus = new Label();
            lblStatus.Text = "الحالة:";
            lblStatus.AutoSize = true;
            lblStatus.Margin = new Padding(3, 10, 3, 0);

            _cboStatus = new ComboBox();
            _cboStatus.DropDownStyle = ComboBoxStyle.DropDownList;
            _cboStatus.Width = 110;
            _cboStatus.Margin = new Padding(3, 8, 3, 0);
            _cboStatus.Items.Add("الكل");
            _cboStatus.Items.Add("معلقة");
            _cboStatus.Items.Add("معتمدة");
            _cboStatus.Items.Add("منفذة");
            _cboStatus.Items.Add("مرفوضة");
            _cboStatus.SelectedIndex = 0;

            _btnRefresh = new Button();
            _btnRefresh.Text = "تحديث";
            _btnRefresh.Height = 30;
            _btnRefresh.Click += delegate { Reload(); };

            _btnNew = new Button();
            _btnNew.Text = "طلب جديد";
            _btnNew.Height = 30;
            _btnNew.Click += delegate { OnNewClick(); };

            _btnDecideApprove = new Button();
            _btnDecideApprove.Text = "اعتماد الطلب";
            _btnDecideApprove.Height = 30;
            _btnDecideApprove.Click += delegate { OnDecideClick(true); };

            _btnDecideReject = new Button();
            _btnDecideReject.Text = "رفض الطلب";
            _btnDecideReject.Height = 30;
            _btnDecideReject.Click += delegate { OnDecideClick(false); };

            _btnExecute = new Button();
            _btnExecute.Text = "تنفيذ";
            _btnExecute.Height = 30;
            _btnExecute.Click += delegate { OnExecuteClick(); };

            _btnItems = new Button();
            _btnItems.Text = "عرض البنود";
            _btnItems.Height = 30;
            _btnItems.Click += delegate { OnItemsClick(); };

            toolbar.Controls.Add(lblStatus);
            toolbar.Controls.Add(_cboStatus);
            toolbar.Controls.Add(_btnRefresh);
            toolbar.Controls.Add(_btnNew);
            toolbar.Controls.Add(_btnDecideApprove);
            toolbar.Controls.Add(_btnDecideReject);
            toolbar.Controls.Add(_btnExecute);
            toolbar.Controls.Add(_btnItems);

            _grid = new DataGridView();
            Theme.StyleGrid(_grid);
            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.RowHeadersVisible = false;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.MultiSelect = false;

            AddColumn("RequestId", "رقم", 50);
            AddColumn("Month", "الشهر", 80);
            AddColumn("Status", "الحالة", 80);
            AddColumn("Reason", "سبب الطلب", 220);
            AddColumn("RequestedBy", "طلبها", 120);
            AddColumn("DecidedBy", "قررها", 120);
            AddColumn("DecideReason", "سبب القرار", 160);
            AddColumn("CreatedAt", "أُنشئ في", 120);

            Controls.Add(_grid);
            Controls.Add(toolbar);
            Controls.Add(header);

            _btnNew.Enabled = AppContext.HasPermission("Schedule.Edit");
            _btnDecideApprove.Enabled = AppContext.HasPermission("Schedule.Approve");
            _btnDecideReject.Enabled = AppContext.HasPermission("Schedule.Approve");
            _btnExecute.Enabled = AppContext.HasPermission("Schedule.Unlock");

            Load += delegate { Reload(); };
        }

        private void AddColumn(string name, string headerText, int fillWeight)
        {
            DataGridViewTextBoxColumn column = new DataGridViewTextBoxColumn();
            column.Name = name;
            column.HeaderText = headerText;
            column.ReadOnly = true;
            column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            column.FillWeight = fillWeight;
            column.SortMode = DataGridViewColumnSortMode.NotSortable;
            _grid.Columns.Add(column);
        }

        private static string StatusKeyFromIndex(int index)
        {
            string[] keys = new string[] { null, "Pending", "Approved", "Executed", "Rejected" };
            return keys[index];
        }

        private void Reload()
        {
            try
            {
                if (_userNames == null)
                {
                    _userNames = new Dictionary<int, string>();
                    foreach (UserView user in UserBLL.ListUsers())
                    {
                        _userNames[user.UserId] = string.IsNullOrWhiteSpace(user.FullName) ? user.Username : user.FullName;
                    }
                }
                string statusKey = StatusKeyFromIndex(_cboStatus.SelectedIndex < 0 ? 0 : _cboStatus.SelectedIndex);
                List<ScheduleChangeRequest> requests = ScheduleChangeBLL.List(statusKey);
                _grid.Rows.Clear();
                foreach (ScheduleChangeRequest request in requests)
                {
                    int rowIndex = _grid.Rows.Add();
                    DataGridViewRow row = _grid.Rows[rowIndex];
                    row.Tag = request;
                    row.Cells["RequestId"].Value = request.RequestId;
                    row.Cells["Month"].Value = request.YearVal + "-" + request.MonthVal.ToString("00");
                    row.Cells["Status"].Value = ScheduleChangeBLL.StatusAr(request.Status);
                    row.Cells["Reason"].Value = request.Reason;
                    row.Cells["RequestedBy"].Value = UserName(request.RequestedBy);
                    row.Cells["DecidedBy"].Value = UserName(request.DecidedBy);
                    row.Cells["DecideReason"].Value = request.DecideReason;
                    row.Cells["CreatedAt"].Value = request.CreatedAt == null ? "" : request.CreatedAt.Value.ToString("yyyy-MM-dd HH:mm");
                }
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmScheduleChanges.Reload");
                Theme.ShowError("حدث خطأ أثناء تحميل الطلبات.");
            }
        }

        private string UserName(int? userId)
        {
            if (userId == null || _userNames == null || !_userNames.ContainsKey(userId.Value))
            {
                return "";
            }
            return _userNames[userId.Value];
        }

        private ScheduleChangeRequest SelectedRequest()
        {
            if (_grid.CurrentRow == null)
            {
                return null;
            }
            return _grid.CurrentRow.Tag as ScheduleChangeRequest;
        }

        private void OnNewClick()
        {
            try
            {
                using (CreateDialog dialog = new CreateDialog())
                {
                    dialog.ShowDialog(this);
                }
                Reload();
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmScheduleChanges.OnNewClick");
                Theme.ShowError("حدث خطأ أثناء إنشاء الطلب.");
            }
        }

        private void OnDecideClick(bool approve)
        {
            try
            {
                ScheduleChangeRequest request = SelectedRequest();
                if (request == null)
                {
                    Theme.ShowInfo("اختر طلباً من القائمة أولاً.");
                    return;
                }
                string action = approve ? "اعتماد" : "رفض";
                if (!Theme.Confirm("هل تريد " + action + " الطلب رقم " + request.RequestId +
                    " (" + request.YearVal + "-" + request.MonthVal.ToString("00") + ")؟"))
                {
                    return;
                }
                string reason = null;
                if (!approve)
                {
                    reason = ReasonDialog.Show(this, "سبب رفض الطلب", "سبب الرفض (إلزامي):", "رفض الطلب");
                    if (reason == null)
                    {
                        return;
                    }
                }
                ScheduleChangeBLL.Decide(request.RequestId, approve, reason);
                Theme.ShowInfo(approve ? "اعتُمد الطلب — يمكن تنفيذه الآن." : "رُفض الطلب.");
                Reload();
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmScheduleChanges.OnDecideClick");
                Theme.ShowError("حدث خطأ أثناء البت في الطلب.");
            }
        }

        private void OnExecuteClick()
        {
            try
            {
                ScheduleChangeRequest request = SelectedRequest();
                if (request == null)
                {
                    Theme.ShowInfo("اختر طلباً من القائمة أولاً.");
                    return;
                }
                List<ScheduleChangeItem> items = ScheduleChangeBLL.Items(request.RequestId);
                if (!Theme.Confirm("تنفيذ الطلب رقم " + request.RequestId + "؟" +
                    "\nسيتم تطبيق " + items.Count + " بند على جدول " + request.YearVal + "-" +
                    request.MonthVal.ToString("00") + " مباشرة، وتُسجَّل العملية في سجل العمليات."))
                {
                    return;
                }
                ScheduleChangeBLL.Execute(request.RequestId);
                Theme.ShowInfo("نُفِّذ الطلب وحُدِّث الجدول الشهري.");
                Reload();
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmScheduleChanges.OnExecuteClick");
                Theme.ShowError("حدث خطأ أثناء تنفيذ الطلب.");
            }
        }

        private void OnItemsClick()
        {
            try
            {
                ScheduleChangeRequest request = SelectedRequest();
                if (request == null)
                {
                    Theme.ShowInfo("اختر طلباً من القائمة أولاً.");
                    return;
                }
                List<ScheduleChangeItem> items = ScheduleChangeBLL.Items(request.RequestId);
                Dictionary<int, Employee> employees = new Dictionary<int, Employee>();
                Dictionary<int, Shift> shifts = ScheduleBLL.LoadMonth(request.YearVal, request.MonthVal, null, null).Shifts;
                DataGridView grid = new DataGridView();
                Theme.StyleGrid(grid);
                grid.Dock = DockStyle.Fill;
                grid.ReadOnly = true;
                grid.AllowUserToAddRows = false;
                grid.AllowUserToDeleteRows = false;
                grid.RowHeadersVisible = false;
                grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
                string[] headers = new string[] { "الموظف", "اليوم", "الوردية القديمة", "الوردية الجديدة" };
                for (int i = 0; i < headers.Length; i++)
                {
                    DataGridViewTextBoxColumn column = new DataGridViewTextBoxColumn();
                    column.Name = "c" + i;
                    column.HeaderText = headers[i];
                    column.ReadOnly = true;
                    column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                    column.FillWeight = 100;
                    grid.Columns.Add(column);
                }
                foreach (ScheduleChangeItem item in items)
                {
                    Employee employee = EmployeeBLL.GetById(item.EmployeeId);
                    int rowIndex = grid.Rows.Add();
                    DataGridViewRow row = grid.Rows[rowIndex];
                    row.Cells[0].Value = employee == null ? "#" + item.EmployeeId : employee.EmployeeCode + " - " + employee.FullNameAr;
                    row.Cells[1].Value = item.DayNumber;
                    row.Cells[2].Value = ShiftName(shifts, item.OldShiftId);
                    row.Cells[3].Value = ShiftName(shifts, item.NewShiftId);
                }
                Form dialog = new Form();
                Theme.Apply(dialog);
                dialog.Text = "بنود الطلب رقم " + request.RequestId + " — " + request.YearVal + "-" + request.MonthVal.ToString("00");
                dialog.ClientSize = new Size(640, 460);
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.Controls.Add(grid);
                dialog.ShowDialog(this);
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmScheduleChanges.OnItemsClick");
                Theme.ShowError("حدث خطأ أثناء عرض البنود.");
            }
        }

        private static string ShiftName(Dictionary<int, Shift> shifts, int? shiftId)
        {
            if (shiftId == null)
            {
                return "غير مجدول";
            }
            Shift shift;
            if (shifts.TryGetValue(shiftId.Value, out shift))
            {
                return shift.ShiftCode + " (" + shift.ShiftName + ")";
            }
            return "#" + shiftId.Value;
        }

        /// <summary>Modal creator for a balanced group of shift-change items.</summary>
        private class CreateDialog : Form
        {
            private NumericUpDown _numYear;
            private ComboBox _cboMonth;
            private TextBox _txtReason;
            private TextBox _txtEmployee;
            private Label _lblEmployeeName;
            private NumericUpDown _numDay;
            private ComboBox _cboShift;
            private Button _btnResolve;
            private Button _btnAddItem;
            private Button _btnRemoveItem;
            private Button _btnOk;
            private DataGridView _grid;
            private Label _lblBalance;
            private MonthData _month;
            private Employee _employee;

            internal CreateDialog()
            {
                Theme.Apply(this);
                Text = "طلب تعديل ورديات — مجموعة بنود متوازنة";
                ClientSize = new Size(700, 620);
                StartPosition = FormStartPosition.CenterParent;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MinimizeBox = false;
                MaximizeBox = false;
                ShowInTaskbar = false;

                FlowLayoutPanel top = new FlowLayoutPanel();
                top.Dock = DockStyle.Top;
                top.Height = 140;
                top.FlowDirection = FlowDirection.LeftToRight;
                top.WrapContents = true;
                top.Padding = new Padding(6, 8, 6, 4);

                Label lblYear = MakeLabel("السنة:");
                _numYear = new NumericUpDown();
                _numYear.Minimum = DateTime.Now.Year - 3;
                _numYear.Maximum = DateTime.Now.Year + 1;
                _numYear.Value = DateTime.Now.Year;
                _numYear.Width = 70;

                Label lblMonth = MakeLabel("الشهر:");
                _cboMonth = new ComboBox();
                _cboMonth.DropDownStyle = ComboBoxStyle.DropDownList;
                _cboMonth.Width = 110;
                for (int month = 1; month <= 12; month++)
                {
                    _cboMonth.Items.Add(month);
                }
                _cboMonth.SelectedIndex = DateTime.Now.Month - 1;

                Label lblReason = MakeLabel("سبب الطلب (إلزامي):");
                _txtReason = new TextBox();
                _txtReason.Width = 420;

                Label lblEmployee = MakeLabel("الموظف (كود/اسم):");
                _txtEmployee = new TextBox();
                _txtEmployee.Width = 150;
                _lblEmployeeName = MakeLabel("");
                _lblEmployeeName.ForeColor = Theme.HeaderTextColor;

                _btnResolve = new Button();
                _btnResolve.Text = "تحديد الموظف";
                _btnResolve.Height = 30;
                _btnResolve.Click += delegate { ResolveEmployee(); };

                Label lblDay = MakeLabel("اليوم:");
                _numDay = new NumericUpDown();
                _numDay.Minimum = 1;
                _numDay.Maximum = 31;
                _numDay.Width = 56;

                Label lblShift = MakeLabel("الوردية الجديدة:");
                _cboShift = new ComboBox();
                _cboShift.DropDownStyle = ComboBoxStyle.DropDownList;
                _cboShift.Width = 180;

                _btnAddItem = new Button();
                _btnAddItem.Text = "إضافة بند";
                _btnAddItem.Height = 30;
                _btnAddItem.Click += delegate { OnAddItemClick(); };

                _btnRemoveItem = new Button();
                _btnRemoveItem.Text = "حذف البند المحدد";
                _btnRemoveItem.Height = 30;
                _btnRemoveItem.Click += delegate { OnRemoveItemClick(); };

                _btnOk = new Button();
                _btnOk.Text = "إرسال الطلب";
                _btnOk.Height = 30;
                _btnOk.Click += delegate { OnOkClick(); };

                top.Controls.Add(lblYear);
                top.Controls.Add(_numYear);
                top.Controls.Add(lblMonth);
                top.Controls.Add(_cboMonth);
                top.Controls.Add(lblReason);
                top.Controls.Add(_txtReason);
                top.Controls.Add(lblEmployee);
                top.Controls.Add(_txtEmployee);
                top.Controls.Add(_lblEmployeeName);
                top.Controls.Add(_btnResolve);
                top.Controls.Add(lblDay);
                top.Controls.Add(_numDay);
                top.Controls.Add(lblShift);
                top.Controls.Add(_cboShift);
                top.Controls.Add(_btnAddItem);
                top.Controls.Add(_btnRemoveItem);
                top.Controls.Add(_btnOk);

                _lblBalance = new Label();
                _lblBalance.Text = "مجموع القديم: 0.0 ساعة — مجموع الجديد: 0.0 ساعة";
                _lblBalance.Dock = DockStyle.Top;
                _lblBalance.Height = 26;
                _lblBalance.TextAlign = ContentAlignment.MiddleCenter;

                _grid = new DataGridView();
                Theme.StyleGrid(_grid);
                _grid.Dock = DockStyle.Fill;
                _grid.ReadOnly = true;
                _grid.AllowUserToAddRows = false;
                _grid.AllowUserToDeleteRows = false;
                _grid.RowHeadersVisible = false;
                _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
                _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
                string[] headers = new string[] { "الموظف", "اليوم", "الوردية الجديدة", "ساعات" };
                for (int i = 0; i < headers.Length; i++)
                {
                    DataGridViewTextBoxColumn column = new DataGridViewTextBoxColumn();
                    column.Name = "c" + i;
                    column.HeaderText = headers[i];
                    column.ReadOnly = true;
                    column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                    column.FillWeight = 100;
                    _grid.Columns.Add(column);
                }

                Controls.Add(_grid);
                Controls.Add(_lblBalance);
                Controls.Add(top);

                _numYear.ValueChanged += delegate { LoadMonth(); };
                _cboMonth.SelectedIndexChanged += delegate { LoadMonth(); };
                Load += delegate { LoadMonth(); };
            }

            private static Label MakeLabel(string text)
            {
                Label label = new Label();
                label.Text = text;
                label.AutoSize = true;
                label.Margin = new Padding(6, 10, 3, 0);
                return label;
            }

            private void LoadMonth()
            {
                try
                {
                    int year = (int)_numYear.Value;
                    int month = _cboMonth.SelectedIndex + 1;
                    _month = ScheduleBLL.LoadMonth(year, month, null, null);
                    List<Shift> options = new List<Shift>(_month.Shifts.Values);
                    options.Sort(delegate(Shift a, Shift b)
                    {
                        return string.Compare(a.ShiftCode, b.ShiftCode, StringComparison.Ordinal);
                    });
                    _cboShift.DataSource = options;
                    _cboShift.DisplayMember = "ShiftCode";
                    _cboShift.ValueMember = "ShiftId";
                    _numDay.Maximum = DateTime.DaysInMonth(year, month);
                    UpdateBalance();
                }
                catch (BusinessException expected)
                {
                    Theme.ShowError(expected.Message);
                }
                catch (Exception unexpected)
                {
                    Logger.LogError(unexpected, "FrmScheduleChanges.CreateDialog.LoadMonth");
                    Theme.ShowError("حدث خطأ أثناء تحميل الشهر.");
                }
            }

            /// <summary>
            /// Shared EmployeeResolve.FromText (Phase 3, R-01): exact code
            /// first, unique name, explicit ambiguity error. Shift-change
            /// requests target active staff only, so an archived match is
            /// rejected here (preserving this window’s original rule) while
            /// the shared resolver keeps its own general semantics.
            /// </summary>
            private void ResolveEmployee()
            {
                try
                {
                    string resolveError;
                    Employee resolved = EmployeeResolve.FromText(_txtEmployee.Text, out resolveError);
                    if (resolved == null)
                    {
                        Theme.ShowInfo(resolveError ?? "اكتب رقم الموظف أو اسمه أولاً.");
                        return;
                    }
                    if (resolved.IsDeleted == true)
                    {
                        Theme.ShowInfo("لم يتم العثور على موظف مطابق.");
                        return;
                    }
                    _employee = EmployeeBLL.GetById(resolved.EmployeeId);
                    _lblEmployeeName.Text = _employee.EmployeeCode + " - " + _employee.FullNameAr;
                }
                catch (BusinessException expected)
                {
                    Theme.ShowError(expected.Message);
                }
                catch (Exception unexpected)
                {
                    Logger.LogError(unexpected, "FrmScheduleChanges.CreateDialog.ResolveEmployee");
                    Theme.ShowError("حدث خطأ أثناء تحديد الموظف.");
                }
            }

            private void OnAddItemClick()
            {
                try
                {
                    if (_month == null)
                    {
                        Theme.ShowInfo("حمّل الشهر أولاً.");
                        return;
                    }
                    if (_employee == null)
                    {
                        Theme.ShowInfo("حدد الموظف أولاً.");
                        return;
                    }
                    object shiftValue = _cboShift.SelectedValue;
                    if (!(shiftValue is int))
                    {
                        Theme.ShowInfo("اختر الوردية الجديدة أولاً.");
                        return;
                    }
                    int day = (int)_numDay.Value;
                    int newShiftId = (int)shiftValue;
                    // duplicate employee+day guard (mirror of the BLL rule, for UX)
                    for (int i = 0; i < _grid.Rows.Count; i++)
                    {
                        object tagEmployeeId = _grid.Rows[i].Tag;
                        object tagDay = _grid.Rows[i].Cells["c1"].Value;
                        if (tagEmployeeId != null && tagDay != null &&
                            (int)tagEmployeeId == _employee.EmployeeId && (int)tagDay == day)
                        {
                            Theme.ShowInfo("يوجد بند لنفس الموظف ونفس اليوم — احذفه وعدّله.");
                            return;
                        }
                    }
                    Shift newShift = _month.Shifts[newShiftId];
                    int rowIndex = _grid.Rows.Add();
                    DataGridViewRow row = _grid.Rows[rowIndex];
                    row.Tag = _employee.EmployeeId;
                    row.Cells[0].Value = _employee.EmployeeCode + " - " + _employee.FullNameAr;
                    row.Cells[1].Value = day;
                    row.Cells[2].Value = newShift.ShiftCode + " (" + newShift.ShiftName + ")";
                    row.Cells[2].Tag = newShiftId;
                    row.Cells[3].Value = (newShift.DurationHrs ?? newShift.NormalHours ?? 8f).ToString("0.#");
                    UpdateBalance();
                }
                catch (BusinessException expected)
                {
                    Theme.ShowError(expected.Message);
                }
                catch (Exception unexpected)
                {
                    Logger.LogError(unexpected, "FrmScheduleChanges.CreateDialog.OnAddItemClick");
                    Theme.ShowError("حدث خطأ أثناء إضافة البند.");
                }
            }

            private void OnRemoveItemClick()
            {
                if (_grid.CurrentRow == null)
                {
                    Theme.ShowInfo("اختر بنداً لحذفه.");
                    return;
                }
                _grid.Rows.Remove(_grid.CurrentRow);
                UpdateBalance();
            }

            /// <summary>Live old/new hour totals (client-side preview of the BLL balance rule).</summary>
            private void UpdateBalance()
            {
                try
                {
                    if (_month == null)
                    {
                        return;
                    }
                    double oldHours = 0;
                    double newHours = 0;
                    foreach (DataGridViewRow row in _grid.Rows)
                    {
                        object employeeIdRaw = row.Tag;
                        object dayRaw = row.Cells["c1"].Value;
                        object newShiftIdRaw = row.Cells["c2"].Tag;
                        if (employeeIdRaw == null || dayRaw == null || !(newShiftIdRaw is int))
                        {
                            continue;
                        }
                        int employeeId = (int)employeeIdRaw;
                        int day = (int)dayRaw;
                        Dictionary<int, int> days;
                        int oldShiftId;
                        if (_month.Cells.TryGetValue(employeeId, out days) && days.TryGetValue(day, out oldShiftId))
                        {
                            Shift oldShift;
                            if (_month.Shifts.TryGetValue(oldShiftId, out oldShift))
                            {
                                oldHours += oldShift.DurationHrs ?? oldShift.NormalHours ?? 8f;
                            }
                        }
                        Shift newShift;
                        if (_month.Shifts.TryGetValue((int)newShiftIdRaw, out newShift))
                        {
                            newHours += newShift.DurationHrs ?? newShift.NormalHours ?? 8f;
                        }
                    }
                    _lblBalance.Text = "مجموع القديم: " + oldHours.ToString("0.#") + " ساعة — مجموع الجديد: " +
                        newHours.ToString("0.#") + " ساعة" +
                        (Math.Abs(newHours - oldHours) > 0.01 ? "  ⚠ غير متوازن" : "  ✔ متوازن");
                }
                catch (Exception unexpected)
                {
                    Logger.LogError(unexpected, "FrmScheduleChanges.CreateDialog.UpdateBalance");
                }
            }

            private void OnOkClick()
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(_txtReason.Text))
                    {
                        Theme.ShowError("سبب الطلب إلزامي.");
                        return;
                    }
                    if (_month == null)
                    {
                        Theme.ShowInfo("حمّل الشهر أولاً.");
                        return;
                    }
                    if (_grid.Rows.Count == 0)
                    {
                        Theme.ShowInfo("أضف بنداً واحداً على الأقل.");
                        return;
                    }
                    int year = (int)_numYear.Value;
                    int month = _cboMonth.SelectedIndex + 1;
                    List<ScheduleChangeItem> items = new List<ScheduleChangeItem>();
                    foreach (DataGridViewRow row in _grid.Rows)
                    {
                        object employeeIdRaw = row.Tag;
                        object dayRaw = row.Cells["c1"].Value;
                        object newShiftIdRaw = row.Cells["c2"].Tag;
                        if (employeeIdRaw == null || dayRaw == null || !(newShiftIdRaw is int))
                        {
                            continue;
                        }
                        ScheduleChangeItem item = new ScheduleChangeItem();
                        item.EmployeeId = (int)employeeIdRaw;
                        item.DayNumber = (int)dayRaw;
                        item.NewShiftId = (int)newShiftIdRaw;
                        items.Add(item);
                    }
                    ScheduleChangeBLL.Create(year, month, _txtReason.Text, items);
                    Theme.ShowInfo("أُنشئ الطلب وهو الآن معلق بانتظار قرار الاعتماد.");
                    Close();
                }
                catch (BusinessException expected)
                {
                    Theme.ShowError(expected.Message);
                }
                catch (Exception unexpected)
                {
                    Logger.LogError(unexpected, "FrmScheduleChanges.CreateDialog.OnOkClick");
                    Theme.ShowError("حدث خطأ أثناء إرسال الطلب.");
                }
            }
        }
    }
}
