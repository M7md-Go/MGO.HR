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
    /// Leave request add/edit dialog (rule 15): employee resolved by code or
    /// name, leave type from LeaveBLL.Types, inclusive start/end dates, reason
    /// and a live balance readout (quota / used / remaining for the selected
    /// type and start year). Validation and balance checks live in LeaveBLL.
    /// </summary>
    public class FrmLeaveEdit : Form
    {
        private readonly int? _leaveId;
        private int _employeeId;
        private TextBox _txtEmployee;
        private Label _lblEmployeeName;
        private ComboBox _cboType;
        private Dictionary<int, string> _typeNames;
        private DateTimePicker _dtpStart;
        private DateTimePicker _dtpEnd;
        private TextBox _txtReason;
        private Label _lblBalance;
        private Label _lblStatus;
        private Button _btnSave;
        private Button _btnCancel;

        public FrmLeaveEdit(int? leaveId)
        {
            _leaveId = leaveId;
            _employeeId = 0;
            Theme.Apply(this);
            Text = leaveId == null ? "طلب إجازة جديد" : "تعديل طلب إجازة";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(470, 400);

            Label lblEmployee = new Label();
            lblEmployee.Text = "الموظف *:";
            lblEmployee.Location = new Point(20, 22);
            lblEmployee.Size = new Size(130, 22);

            _txtEmployee = new TextBox();
            _txtEmployee.Location = new Point(155, 18);
            _txtEmployee.Size = new Size(280, 26);

            _lblEmployeeName = new Label();
            _lblEmployeeName.Location = new Point(155, 45);
            _lblEmployeeName.Size = new Size(280, 20);
            _lblEmployeeName.ForeColor = Theme.HeaderTextColor;

            Label lblType = new Label();
            lblType.Text = "نوع الإجازة *:";
            lblType.Location = new Point(20, 77);
            lblType.Size = new Size(130, 22);

            _cboType = new ComboBox();
            _cboType.DropDownStyle = ComboBoxStyle.DropDownList;
            _cboType.Location = new Point(155, 73);
            _cboType.Size = new Size(280, 26);

            Label lblStart = new Label();
            lblStart.Text = "تاريخ البداية *:";
            lblStart.Location = new Point(20, 112);
            lblStart.Size = new Size(130, 22);

            _dtpStart = new DateTimePicker();
            _dtpStart.Format = DateTimePickerFormat.Short;
            _dtpStart.Location = new Point(155, 108);
            _dtpStart.Size = new Size(280, 26);

            Label lblEnd = new Label();
            lblEnd.Text = "تاريخ النهاية *:";
            lblEnd.Location = new Point(20, 147);
            lblEnd.Size = new Size(130, 22);

            _dtpEnd = new DateTimePicker();
            _dtpEnd.Format = DateTimePickerFormat.Short;
            _dtpEnd.Location = new Point(155, 143);
            _dtpEnd.Size = new Size(280, 26);

            Label lblReason = new Label();
            lblReason.Text = "السبب:";
            lblReason.Location = new Point(20, 182);
            lblReason.Size = new Size(130, 22);

            _txtReason = new TextBox();
            _txtReason.Location = new Point(155, 178);
            _txtReason.Size = new Size(280, 60);
            _txtReason.Multiline = true;

            _lblBalance = new Label();
            _lblBalance.Location = new Point(20, 250);
            _lblBalance.Size = new Size(415, 40);
            _lblBalance.ForeColor = Theme.HeaderTextColor;

            _lblStatus = new Label();
            _lblStatus.Location = new Point(20, 295);
            _lblStatus.Size = new Size(415, 40);
            _lblStatus.ForeColor = Theme.StatusBad;

            _btnSave = new Button();
            _btnSave.Text = "حفظ";
            _btnSave.Location = new Point(305, 340);
            _btnSave.Size = new Size(130, 34);
            _btnSave.Click += delegate { OnSaveClick(); };

            _btnCancel = new Button();
            _btnCancel.Text = "إلغاء";
            _btnCancel.Location = new Point(20, 340);
            _btnCancel.Size = new Size(130, 34);
            _btnCancel.DialogResult = DialogResult.Cancel;

            _cboType.SelectedIndexChanged += delegate { RefreshBalance(); };
            _dtpStart.ValueChanged += delegate { RefreshBalance(); };

            Controls.Add(lblEmployee);
            Controls.Add(_txtEmployee);
            Controls.Add(_lblEmployeeName);
            Controls.Add(lblType);
            Controls.Add(_cboType);
            Controls.Add(lblStart);
            Controls.Add(_dtpStart);
            Controls.Add(lblEnd);
            Controls.Add(_dtpEnd);
            Controls.Add(lblReason);
            Controls.Add(_txtReason);
            Controls.Add(_lblBalance);
            Controls.Add(_lblStatus);
            Controls.Add(_btnSave);
            Controls.Add(_btnCancel);

            AcceptButton = _btnSave;
            CancelButton = _btnCancel;

            LoadTypes();
            if (_leaveId != null)
            {
                LoadLeave();
            }
            else
            {
                _dtpEnd.Value = DateTime.Now.Date;
                _dtpStart.Value = DateTime.Now.Date;
            }
            RefreshBalance();
        }

        private void LoadTypes()
        {
            try
            {
                _typeNames = new Dictionary<int, string>();
                foreach (LeaveType type in LeaveBLL.Types())
                {
                    _typeNames[type.LeaveTypeId] = type.TypeName + " (" + type.LeaveTypeCode + ")";
                }
                _cboType.DataSource = new BindingSource(_typeNames, null);
                _cboType.DisplayMember = "Value";
                _cboType.ValueMember = "Key";
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmLeaveEdit.LoadTypes");
                Theme.ShowError("حدث خطأ أثناء تحميل أنواع الإجازات.");
            }
        }

        private void LoadLeave()
        {
            try
            {
                LeaveRequestView stored = LeaveBLL.GetById(_leaveId.Value);
                if (stored == null)
                {
                    Theme.ShowError("طلب الإجازة غير موجود.");
                    return;
                }
                _employeeId = stored.EmployeeId ?? 0;
                _txtEmployee.Text = stored.EmployeeCode;
                _lblEmployeeName.Text = stored.EmployeeName;
                if (_typeNames.ContainsKey(stored.LeaveTypeId ?? 0))
                {
                    _cboType.SelectedValue = stored.LeaveTypeId ?? 0;
                }
                _dtpStart.Value = stored.StartDate ?? DateTime.Now.Date;
                _dtpEnd.Value = stored.EndDate ?? DateTime.Now.Date;
                _txtReason.Text = stored.Reason;
                if (stored.Status != null && stored.Status != LeaveBLL.StatusPending)
                {
                    _txtEmployee.Enabled = false;
                    _cboType.Enabled = false;
                    _dtpStart.Enabled = false;
                    _dtpEnd.Enabled = false;
                    _btnSave.Enabled = false;
                    _lblStatus.Text = "حالة الطلب: " + (stored.Status == LeaveBLL.StatusApproved ? "معتمد" : "مرفوض") +
                        " — البيانات مقفلة." +
                        (string.IsNullOrEmpty(stored.RejectReason) ? string.Empty : " سبب الرفض: " + stored.RejectReason);
                    Text = "عرض طلب الإجازة";
                }
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmLeaveEdit.LoadLeave");
                Theme.ShowError("حدث خطأ أثناء تحميل بيانات الطلب.");
            }
        }

        private void RefreshBalance()
        {
            _lblBalance.Text = "";
            int employeeId = _employeeId;
            if (employeeId <= 0)
            {
                return;
            }
            object selected = _cboType.SelectedValue;
            int leaveTypeId = selected is int ? (int)selected : 0;
            if (leaveTypeId <= 0)
            {
                return;
            }
            try
            {
                LeaveBalance balance = LeaveBLL.GetOrInitBalance(employeeId,
                    _dtpStart.Value.Date.Year, leaveTypeId);
                _lblBalance.Text = "الرصيد السنوي: الحصة " + (balance.QuotaDays ?? 0) +
                    " يومًا | المستخدم " + (balance.UsedDays ?? 0) +
                    " يومًا | المتبقي " + (balance.Remaining ?? 0) + " يومًا";
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmLeaveEdit.RefreshBalance");
            }
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

        private void OnSaveClick()
        {
            try
            {
                Employee employee = ResolveEmployee();
                if (employee == null)
                {
                    Theme.ShowError("لم يتم العثور على الموظف — أدخل كودًا أو اسمًا صحيحًا.");
                    return;
                }
                _employeeId = employee.EmployeeId;
                _lblEmployeeName.Text = employee.FullNameAr + " (" + employee.EmployeeCode + ")";
                object selected = _cboType.SelectedValue;
                int leaveTypeId = selected is int ? (int)selected : 0;

                LeaveRequest request = new LeaveRequest();
                request.EmployeeId = _employeeId;
                request.LeaveTypeId = leaveTypeId;
                request.StartDate = _dtpStart.Value.Date;
                request.EndDate = _dtpEnd.Value.Date;
                request.Reason = (_txtReason.Text ?? string.Empty).Trim();

                if (_leaveId == null)
                {
                    LeaveBLL.Create(request);
                }
                else
                {
                    LeaveBLL.Update(_leaveId.Value, request);
                }
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmLeaveEdit.OnSaveClick");
                Theme.ShowError("حدث خطأ أثناء حفظ طلب الإجازة.");
            }
        }
    }
}
