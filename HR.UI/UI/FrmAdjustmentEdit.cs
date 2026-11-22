using System;
using System.Drawing;
using System.Windows.Forms;
using HR.BLL;
using HR.Entities;
using HR.Helpers;

namespace HR.UI.UI
{
    /// <summary>
    /// Add/edit dialog for a manual attendance correction. Type "Time" fixes
    /// the punches of one day (HH:mm in / out), type "Status" forces the final
    /// engine status. Reason is mandatory; validation lives in
    /// AttendanceAdjustmentBLL and approval (separate permission) applies the
    /// change to [Attendance].
    /// </summary>
    public class FrmAdjustmentEdit : Form
    {
        private static readonly string[] TypeCodes = new string[] { "Time", "Status" };
        private static readonly string[] StatusCodes =
            new string[] { "", "Present", "Late", "EarlyLeave", "Mission", "Absent", "Leave", "MissingPunch", "Holiday" };

        private readonly int? _adjustmentId;
        private int _employeeId;
        private bool _readOnlyMode;
        private TextBox _txtEmployee;
        private Label _lblEmployeeName;
        private DateTimePicker _dtpAttDate;
        private ComboBox _cboType;
        private TextBox _txtNewIn;
        private TextBox _txtNewOut;
        private ComboBox _cboNewStatus;
        private TextBox _txtReason;
        private Label _lblNewIn;
        private Label _lblNewOut;
        private Label _lblNewStatus;
        private Label _lblTimeHint1;
        private Label _lblTimeHint2;
        private Button _btnSave;

        public FrmAdjustmentEdit(int? adjustmentId)
        {
            _adjustmentId = adjustmentId;
            _employeeId = 0;
            Theme.Apply(this);
            Text = adjustmentId == null ? "تصحيح حضور يدوي جديد" : "تعديل تصحيح حضوري";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(470, 440);

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

            Label lblDate = new Label();
            lblDate.Text = "اليوم المصحَّح *:";
            lblDate.Location = new Point(20, 77);
            lblDate.Size = new Size(130, 22);

            _dtpAttDate = new DateTimePicker();
            _dtpAttDate.Format = DateTimePickerFormat.Short;
            _dtpAttDate.Location = new Point(155, 73);
            _dtpAttDate.Size = new Size(280, 26);

            Label lblType = new Label();
            lblType.Text = "نوع التصحيح *:";
            lblType.Location = new Point(20, 112);
            lblType.Size = new Size(130, 22);

            _cboType = new ComboBox();
            _cboType.DropDownStyle = ComboBoxStyle.DropDownList;
            _cboType.Location = new Point(155, 108);
            _cboType.Size = new Size(280, 26);
            _cboType.Items.Add("تعديل أوقات البصمات (دخول/خروج)");
            _cboType.Items.Add("تغيير الحالة النهائية لليوم");
            _cboType.SelectedIndexChanged += delegate { ApplyTypeVisibility(); };

            _lblNewIn = new Label();
            _lblNewIn.Text = "دخول جديد:";
            _lblNewIn.Location = new Point(20, 147);
            _lblNewIn.Size = new Size(130, 22);

            _txtNewIn = new TextBox();
            _txtNewIn.Location = new Point(155, 143);
            _txtNewIn.Size = new Size(120, 26);
            _txtNewIn.RightToLeft = RightToLeft.No;

            _lblTimeHint1 = new Label();
            _lblTimeHint1.Text = "(HH:mm أو فارغ)";
            _lblTimeHint1.Location = new Point(285, 147);
            _lblTimeHint1.Size = new Size(150, 22);

            _lblNewOut = new Label();
            _lblNewOut.Text = "خروج جديد:";
            _lblNewOut.Location = new Point(20, 182);
            _lblNewOut.Size = new Size(130, 22);

            _txtNewOut = new TextBox();
            _txtNewOut.Location = new Point(155, 178);
            _txtNewOut.Size = new Size(120, 26);
            _txtNewOut.RightToLeft = RightToLeft.No;

            _lblTimeHint2 = new Label();
            _lblTimeHint2.Text = "(HH:mm أو فارغ)";
            _lblTimeHint2.Location = new Point(285, 182);
            _lblTimeHint2.Size = new Size(150, 22);

            _lblNewStatus = new Label();
            _lblNewStatus.Text = "الحالة النهائية الجديدة *:";
            _lblNewStatus.Location = new Point(20, 217);
            _lblNewStatus.Size = new Size(130, 22);

            _cboNewStatus = new ComboBox();
            _cboNewStatus.DropDownStyle = ComboBoxStyle.DropDownList;
            _cboNewStatus.Location = new Point(155, 213);
            _cboNewStatus.Size = new Size(280, 26);
            _cboNewStatus.Items.Add("— (بدون تغيير)");
            _cboNewStatus.Items.Add("حاضر");
            _cboNewStatus.Items.Add("متأخر");
            _cboNewStatus.Items.Add("انصراف مبكر");
            _cboNewStatus.Items.Add("مأمورية");
            _cboNewStatus.Items.Add("غائب");
            _cboNewStatus.Items.Add("إجازة");
            _cboNewStatus.Items.Add("بصمة ناقصة");
            _cboNewStatus.Items.Add("عطلة رسمية");

            Label lblReason = new Label();
            lblReason.Text = "السبب *:";
            lblReason.Location = new Point(20, 252);
            lblReason.Size = new Size(130, 22);

            _txtReason = new TextBox();
            _txtReason.Location = new Point(155, 248);
            _txtReason.Size = new Size(280, 60);
            _txtReason.Multiline = true;
            _txtReason.MaxLength = 500;

            _btnSave = new Button();
            _btnSave.Text = "حفظ";
            _btnSave.Location = new Point(305, 380);
            _btnSave.Size = new Size(130, 34);
            _btnSave.Click += delegate { OnSaveClick(); };

            Button btnCancel = new Button();
            btnCancel.Text = "إلغاء";
            btnCancel.Location = new Point(20, 380);
            btnCancel.Size = new Size(130, 34);
            btnCancel.DialogResult = DialogResult.Cancel;

            Controls.Add(lblEmployee);
            Controls.Add(_txtEmployee);
            Controls.Add(_lblEmployeeName);
            Controls.Add(lblDate);
            Controls.Add(_dtpAttDate);
            Controls.Add(lblType);
            Controls.Add(_cboType);
            Controls.Add(_lblNewIn);
            Controls.Add(_txtNewIn);
            Controls.Add(_lblTimeHint1);
            Controls.Add(_lblNewOut);
            Controls.Add(_txtNewOut);
            Controls.Add(_lblTimeHint2);
            Controls.Add(_lblNewStatus);
            Controls.Add(_cboNewStatus);
            Controls.Add(lblReason);
            Controls.Add(_txtReason);
            Controls.Add(_btnSave);
            Controls.Add(btnCancel);

            AcceptButton = _btnSave;
            CancelButton = btnCancel;

            _cboType.SelectedIndex = 0;

            if (_adjustmentId != null)
            {
                LoadAdjustment();
            }
            else
            {
                _dtpAttDate.Value = DateTime.Now.Date;
            }
        }

        /// <summary>Time rows show the punch boxes; status rows show the status combo.</summary>
        private void ApplyTypeVisibility()
        {
            bool isTime = TypeCodes[_cboType.SelectedIndex >= 0 ? _cboType.SelectedIndex : 0] == "Time";
            _lblNewIn.Visible = isTime;
            _txtNewIn.Visible = isTime;
            _lblTimeHint1.Visible = isTime;
            _lblNewOut.Visible = isTime;
            _txtNewOut.Visible = isTime;
            _lblTimeHint2.Visible = isTime;
            _lblNewStatus.Visible = !isTime;
            _cboNewStatus.Visible = !isTime;
        }

        private void LoadAdjustment()
        {
            try
            {
                AttendanceAdjustment stored = AttendanceAdjustmentBLL.GetById(_adjustmentId.Value);
                if (stored == null)
                {
                    Theme.ShowError("طلب التصحيح غير موجود.");
                    return;
                }
                _employeeId = stored.EmployeeId ?? 0;
                Employee storedEmployee = EmployeeBLL.GetById(_employeeId);
                _txtEmployee.Text = storedEmployee != null ? storedEmployee.EmployeeCode : "";
                _lblEmployeeName.Text = storedEmployee != null ?
                    storedEmployee.FullNameAr + " (" + storedEmployee.EmployeeCode + ")" : "";
                _dtpAttDate.Value = stored.AttDate ?? DateTime.Now.Date;
                _cboType.SelectedIndex = stored.AdjustmentType == AttendanceAdjustmentBLL.TypeStatus ? 1 : 0;
                ApplyTypeVisibility();
                _txtNewIn.Text = stored.NewInTime ?? "";
                _txtNewOut.Text = stored.NewOutTime ?? "";
                int statusIndex = Array.IndexOf(StatusCodes, stored.NewStatus ?? "");
                _cboNewStatus.SelectedIndex = statusIndex >= 0 ? statusIndex : 0;
                _txtReason.Text = stored.Reason ?? "";

                if (stored.Status != AttendanceAdjustmentBLL.StatusPending)
                {
                    // approved/rejected rows are read-only here — approval is what applies them
                    _readOnlyMode = true;
                    Text = "عرض تصحيح حضوري (" + AttendanceAdjustmentBLL.StatusName(stored.Status) + ")";
                    _txtEmployee.Enabled = false;
                    _dtpAttDate.Enabled = false;
                    _cboType.Enabled = false;
                    _txtNewIn.Enabled = false;
                    _txtNewOut.Enabled = false;
                    _cboNewStatus.Enabled = false;
                    _txtReason.Enabled = false;
                    _btnSave.Enabled = false;
                }
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmAdjustmentEdit.LoadAdjustment");
                Theme.ShowError("حدث خطأ أثناء تحميل بيانات التصحيح.");
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
            if (_readOnlyMode)
            {
                Theme.ShowInfo("التصحيحات المعتمدة والمرفوضة للعرض فقط.");
                return;
            }
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

                int typeIndex = _cboType.SelectedIndex >= 0 ? _cboType.SelectedIndex : 0;
                string adjustmentType = TypeCodes[typeIndex];
                string newInTime = (_txtNewIn.Text ?? string.Empty).Trim();
                string newOutTime = (_txtNewOut.Text ?? string.Empty).Trim();
                string newStatus = StatusCodes[_cboNewStatus.SelectedIndex >= 0 ? _cboNewStatus.SelectedIndex : 0];
                string reason = (_txtReason.Text ?? string.Empty).Trim();

                if (_adjustmentId == null)
                {
                    AttendanceAdjustmentBLL.Create(_employeeId, _dtpAttDate.Value.Date, adjustmentType,
                        newInTime, newOutTime, newStatus, reason);
                }
                else
                {
                    AttendanceAdjustmentBLL.Update(_adjustmentId.Value, _employeeId, _dtpAttDate.Value.Date,
                        adjustmentType, newInTime, newOutTime, newStatus, reason);
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
                Logger.LogError(unexpected, "FrmAdjustmentEdit.OnSaveClick");
                Theme.ShowError("حدث خطأ أثناء حفظ التصحيح.");
            }
        }
    }
}
