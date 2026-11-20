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
    /// Long-duration leave add/edit dialog: employee resolved by code or name,
    /// type combo (limited types force an end date and enforce the month
    /// limit), required start date, optional end date (checkbox = open-ended),
    /// reason and notes. Validation lives in LongLeaveBLL.
    /// </summary>
    public class FrmLongLeaveEdit : Form
    {
        private readonly int? _longLeaveId;
        private int _employeeId;
        private TextBox _txtEmployee;
        private Label _lblEmployeeName;
        private ComboBox _cboType;
        private List<LongLeaveType> _types;
        private DateTimePicker _dtpFrom;
        private DateTimePicker _dtpTo;
        private TextBox _txtReason;
        private TextBox _txtNotes;
        private Label _lblTypeHint;
        private Label _lblStatus;
        private Button _btnSave;
        private Button _btnCancel;

        public FrmLongLeaveEdit(int? longLeaveId)
        {
            _longLeaveId = longLeaveId;
            _employeeId = 0;
            Theme.Apply(this);
            Text = longLeaveId == null ? "إجازة طويلة المدة — إضافة" : "إجازة طويلة المدة — تعديل";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(470, 470);

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
            lblType.Text = "النوع *:";
            lblType.Location = new Point(20, 77);
            lblType.Size = new Size(130, 22);

            _cboType = new ComboBox();
            _cboType.DropDownStyle = ComboBoxStyle.DropDownList;
            _cboType.Location = new Point(155, 73);
            _cboType.Size = new Size(280, 26);

            _lblTypeHint = new Label();
            _lblTypeHint.Location = new Point(155, 100);
            _lblTypeHint.Size = new Size(280, 20);
            _lblTypeHint.ForeColor = Theme.HeaderTextColor;

            Label lblFrom = new Label();
            lblFrom.Text = "من تاريخ *:";
            lblFrom.Location = new Point(20, 135);
            lblFrom.Size = new Size(130, 22);

            _dtpFrom = new DateTimePicker();
            _dtpFrom.Format = DateTimePickerFormat.Short;
            _dtpFrom.Location = new Point(155, 131);
            _dtpFrom.Size = new Size(280, 26);

            Label lblTo = new Label();
            lblTo.Text = "إلى تاريخ:";
            lblTo.Location = new Point(20, 170);
            lblTo.Size = new Size(130, 22);

            _dtpTo = new DateTimePicker();
            _dtpTo.Format = DateTimePickerFormat.Short;
            _dtpTo.Location = new Point(155, 166);
            _dtpTo.Size = new Size(280, 26);
            _dtpTo.ShowCheckBox = true;

            Label lblReason = new Label();
            lblReason.Text = "السبب:";
            lblReason.Location = new Point(20, 205);
            lblReason.Size = new Size(130, 22);

            _txtReason = new TextBox();
            _txtReason.Location = new Point(155, 201);
            _txtReason.Size = new Size(280, 60);
            _txtReason.Multiline = true;

            Label lblNotes = new Label();
            lblNotes.Text = "ملاحظات:";
            lblNotes.Location = new Point(20, 275);
            lblNotes.Size = new Size(130, 22);

            _txtNotes = new TextBox();
            _txtNotes.Location = new Point(155, 271);
            _txtNotes.Size = new Size(280, 60);
            _txtNotes.Multiline = true;

            _lblStatus = new Label();
            _lblStatus.Location = new Point(20, 340);
            _lblStatus.Size = new Size(415, 30);
            _lblStatus.ForeColor = Theme.StatusBad;

            _btnSave = new Button();
            _btnSave.Text = "حفظ";
            _btnSave.Location = new Point(305, 415);
            _btnSave.Size = new Size(130, 34);
            _btnSave.Click += delegate { OnSaveClick(); };

            _btnCancel = new Button();
            _btnCancel.Text = "إلغاء";
            _btnCancel.Location = new Point(20, 415);
            _btnCancel.Size = new Size(130, 34);
            _btnCancel.DialogResult = DialogResult.Cancel;

            _cboType.SelectedIndexChanged += delegate { RefreshTypeHint(); };

            Controls.Add(lblEmployee);
            Controls.Add(_txtEmployee);
            Controls.Add(_lblEmployeeName);
            Controls.Add(lblType);
            Controls.Add(_cboType);
            Controls.Add(_lblTypeHint);
            Controls.Add(lblFrom);
            Controls.Add(_dtpFrom);
            Controls.Add(lblTo);
            Controls.Add(_dtpTo);
            Controls.Add(lblReason);
            Controls.Add(_txtReason);
            Controls.Add(lblNotes);
            Controls.Add(_txtNotes);
            Controls.Add(_lblStatus);
            Controls.Add(_btnSave);
            Controls.Add(_btnCancel);

            AcceptButton = _btnSave;
            CancelButton = _btnCancel;

            LoadTypes();
            if (_longLeaveId != null)
            {
                LoadLeave();
            }
            else
            {
                _dtpFrom.Value = DateTime.Now.Date;
                _dtpTo.Value = DateTime.Now.Date;
                _dtpTo.Checked = false;
            }
            RefreshTypeHint();
        }

        private void LoadTypes()
        {
            try
            {
                _types = LongLeaveBLL.Types();
                Dictionary<int, string> names = new Dictionary<int, string>();
                foreach (LongLeaveType type in _types)
                {
                    names[type.LongLeaveTypeId] = type.TypeName;
                }
                _cboType.DataSource = new BindingSource(names, null);
                _cboType.DisplayMember = "Value";
                _cboType.ValueMember = "Key";
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmLongLeaveEdit.LoadTypes");
                Theme.ShowError("حدث خطأ أثناء تحميل أنواع الإجازات طويلة المدة.");
            }
        }

        private LongLeaveType SelectedType()
        {
            if (_types == null)
            {
                return null;
            }
            object selected = _cboType.SelectedValue;
            int typeId = selected is int ? (int)selected : 0;
            foreach (LongLeaveType type in _types)
            {
                if (type.LongLeaveTypeId == typeId)
                {
                    return type;
                }
            }
            return null;
        }

        private void RefreshTypeHint()
        {
            LongLeaveType type = SelectedType();
            if (type == null)
            {
                _lblTypeHint.Text = string.Empty;
                return;
            }
            if (type.IsLimited == true && type.MaxMonths != null && type.MaxMonths.Value > 0)
            {
                _lblTypeHint.Text = "نوع محدود — الحد الأقصى " + type.MaxMonths.Value + " شهرًا.";
            }
            else
            {
                _lblTypeHint.Text = "نوع غير محدد المدة — يمكن ترك النهاية فارغة.";
            }
        }

        private void LoadLeave()
        {
            try
            {
                LongLeaveView stored = LongLeaveBLL.GetById(_longLeaveId.Value);
                if (stored == null)
                {
                    Theme.ShowError("طلب الإجازة طويلة المدة غير موجود.");
                    return;
                }
                _employeeId = stored.EmployeeId ?? 0;
                _txtEmployee.Text = stored.EmployeeCode;
                _lblEmployeeName.Text = stored.EmployeeName;
                _cboType.SelectedValue = stored.TypeId ?? 0;
                _dtpFrom.Value = stored.FromDate ?? DateTime.Now.Date;
                if (stored.ToDate != null)
                {
                    _dtpTo.Checked = true;
                    _dtpTo.Value = stored.ToDate.Value;
                }
                else
                {
                    _dtpTo.Checked = false;
                }
                _txtReason.Text = stored.Reason;
                _txtNotes.Text = stored.Notes;
                if (stored.Status != null && stored.Status != LongLeaveBLL.StatusPending &&
                    stored.Status != LongLeaveBLL.StatusRejected)
                {
                    _txtEmployee.Enabled = false;
                    _cboType.Enabled = false;
                    _dtpFrom.Enabled = false;
                    _dtpTo.Enabled = false;
                    _txtReason.Enabled = false;
                    _btnSave.Enabled = false;
                    _lblStatus.Text = "حالة الطلب: " + (stored.Status == LongLeaveBLL.StatusApproved ? "معتمد" : "منتهٍ") +
                        " — البيانات مقفلة.";
                    Text = "عرض الإجازة طويلة المدة";
                }
                else if (stored.Status == LongLeaveBLL.StatusRejected)
                {
                    _lblStatus.ForeColor = Theme.StatusWarn;
                    _lblStatus.Text = "الطلب مرفوض — يمكنك إعادة فتحه بتعديله ثم حذف حالة الرفض عبر طلب جديد أو إعادة الاعتماد.";
                }
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmLongLeaveEdit.LoadLeave");
                Theme.ShowError("حدث خطأ أثناء تحميل بيانات الطلب.");
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
                int typeId = selected is int ? (int)selected : 0;

                LongLeave leave = new LongLeave();
                leave.EmployeeId = _employeeId;
                leave.TypeId = typeId;
                leave.FromDate = _dtpFrom.Value.Date;
                leave.ToDate = _dtpTo.Checked ? (DateTime?)_dtpTo.Value.Date : null;
                leave.Reason = (_txtReason.Text ?? string.Empty).Trim();
                leave.Notes = (_txtNotes.Text ?? string.Empty).Trim();

                if (_longLeaveId == null)
                {
                    LongLeaveBLL.Create(leave);
                }
                else
                {
                    LongLeaveBLL.Update(_longLeaveId.Value, leave);
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
                Logger.LogError(unexpected, "FrmLongLeaveEdit.OnSaveClick");
                Theme.ShowError("حدث خطأ أثناء حفظ الإجازة طويلة المدة.");
            }
        }
    }
}
