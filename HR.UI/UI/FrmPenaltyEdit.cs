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
    /// Penalty add/edit dialog (rule 17): employee resolved by code or name,
    /// violation date, penalty date, type (Warning/Deduction/Penalty/
    /// Absence/Violation with Arabic labels), optional amount and reason.
    /// Validation and permission checks live in PenaltyBLL.
    /// </summary>
    public class FrmPenaltyEdit : Form
    {
        private readonly int? _penaltyId;
        private int _employeeId;
        private TextBox _txtEmployee;
        private Label _lblEmployeeName;
        private DateTimePicker _dtpAtt;
        private DateTimePicker _dtpPenalty;
        private ComboBox _cboType;
        private Dictionary<string, string> _types;
        private NumericUpDown _numAmount;
        private TextBox _txtReason;
        private Button _btnSave;
        private Button _btnCancel;

        public FrmPenaltyEdit(int? penaltyId)
        {
            _penaltyId = penaltyId;
            _employeeId = 0;
            Theme.Apply(this);
            Text = penaltyId == null ? "جزاء جديد" : "تعديل جزاء";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(470, 420);

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

            Label lblAttDate = new Label();
            lblAttDate.Text = "تاريخ المخالفة:";
            lblAttDate.Location = new Point(20, 77);
            lblAttDate.Size = new Size(130, 22);

            _dtpAtt = new DateTimePicker();
            _dtpAtt.Format = DateTimePickerFormat.Short;
            _dtpAtt.Location = new Point(155, 73);
            _dtpAtt.Size = new Size(280, 26);
            _dtpAtt.ShowCheckBox = true;

            Label lblPenaltyDate = new Label();
            lblPenaltyDate.Text = "تاريخ الجزاء *:";
            lblPenaltyDate.Location = new Point(20, 112);
            lblPenaltyDate.Size = new Size(130, 22);

            _dtpPenalty = new DateTimePicker();
            _dtpPenalty.Format = DateTimePickerFormat.Short;
            _dtpPenalty.Location = new Point(155, 108);
            _dtpPenalty.Size = new Size(280, 26);

            Label lblType = new Label();
            lblType.Text = "النوع *:";
            lblType.Location = new Point(20, 147);
            lblType.Size = new Size(130, 22);

            _cboType = new ComboBox();
            _cboType.DropDownStyle = ComboBoxStyle.DropDownList;
            _cboType.Location = new Point(155, 143);
            _cboType.Size = new Size(280, 26);

            Label lblAmount = new Label();
            lblAmount.Text = "القيمة:";
            lblAmount.Location = new Point(20, 182);
            lblAmount.Size = new Size(130, 22);

            _numAmount = new NumericUpDown();
            _numAmount.Location = new Point(155, 178);
            _numAmount.Size = new Size(150, 26);
            _numAmount.DecimalPlaces = 2;
            _numAmount.Minimum = 0;
            _numAmount.Maximum = 10000000;
            _numAmount.Value = 0;

            Label lblReason = new Label();
            lblReason.Text = "السبب:";
            lblReason.Location = new Point(20, 217);
            lblReason.Size = new Size(130, 22);

            _txtReason = new TextBox();
            _txtReason.Location = new Point(155, 213);
            _txtReason.Size = new Size(280, 60);
            _txtReason.Multiline = true;

            _btnSave = new Button();
            _btnSave.Text = "حفظ";
            _btnSave.Location = new Point(305, 360);
            _btnSave.Size = new Size(130, 34);
            _btnSave.Click += delegate { OnSaveClick(); };

            _btnCancel = new Button();
            _btnCancel.Text = "إلغاء";
            _btnCancel.Location = new Point(20, 360);
            _btnCancel.Size = new Size(130, 34);
            _btnCancel.DialogResult = DialogResult.Cancel;

            Controls.Add(lblEmployee);
            Controls.Add(_txtEmployee);
            Controls.Add(_lblEmployeeName);
            Controls.Add(lblAttDate);
            Controls.Add(_dtpAtt);
            Controls.Add(lblPenaltyDate);
            Controls.Add(_dtpPenalty);
            Controls.Add(lblType);
            Controls.Add(_cboType);
            Controls.Add(lblAmount);
            Controls.Add(_numAmount);
            Controls.Add(lblReason);
            Controls.Add(_txtReason);
            Controls.Add(_btnSave);
            Controls.Add(_btnCancel);

            AcceptButton = _btnSave;
            CancelButton = _btnCancel;

            LoadTypes();
            if (_penaltyId != null)
            {
                LoadPenalty();
            }
            else
            {
                _dtpAtt.Checked = false;
                _dtpPenalty.Value = DateTime.Now.Date;
            }
        }

        private void LoadTypes()
        {
            try
            {
                _types = new Dictionary<string, string>();
                _types[PenaltyBLL.TypeWarning] = "إنذار";
                _types[PenaltyBLL.TypeDeduction] = "خصم";
                _types[PenaltyBLL.TypePenalty] = "جزاء";
                _types[PenaltyBLL.TypeAbsence] = "غياب";
                _types[PenaltyBLL.TypeViolation] = "مخالفة";
                _cboType.DataSource = new BindingSource(_types, null);
                _cboType.DisplayMember = "Value";
                _cboType.ValueMember = "Key";
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmPenaltyEdit.LoadTypes");
                Theme.ShowError("حدث خطأ أثناء تحميل أنواع الجزاءات.");
            }
        }

        private void LoadPenalty()
        {
            try
            {
                Penalty stored = PenaltyBLL.GetById(_penaltyId.Value);
                if (stored == null)
                {
                    Theme.ShowError("الجزاء غير موجود.");
                    return;
                }
                _employeeId = stored.EmployeeId ?? 0;
                Employee loaded = ResolveEmployeeById(_employeeId);
                if (loaded != null)
                {
                    _txtEmployee.Text = loaded.EmployeeCode;
                    _lblEmployeeName.Text = loaded.FullNameAr;
                }
                if (stored.AttDate != null)
                {
                    _dtpAtt.Checked = true;
                    _dtpAtt.Value = stored.AttDate.Value;
                }
                _dtpPenalty.Value = stored.PenaltyDate ?? DateTime.Now.Date;
                if (_types.ContainsKey(stored.TypeCode))
                {
                    _cboType.SelectedValue = stored.TypeCode;
                }
                _numAmount.Value = stored.Amount ?? 0;
                _txtReason.Text = stored.Reason;
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmPenaltyEdit.LoadPenalty");
                Theme.ShowError("حدث خطأ أثناء تحميل بيانات الجزاء.");
            }
        }

        /// <summary>Looks up one employee by id (resolved again by code text so
        /// the search path stays identical to the other screens).</summary>
        private static Employee ResolveEmployeeById(int employeeId)
        {
            EmployeeSearchFilter filter = new EmployeeSearchFilter();
            filter.Text = employeeId.ToString();
            filter.IncludeArchived = true;
            filter.PageSize = 1;
            List<Employee> matches = EmployeeBLL.Search(filter);
            return matches.Count > 0 ? matches[0] : null;
        }

        private Employee ResolveEmployee()
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
            return matches.Count > 0 ? matches[0] : null;
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

                Penalty penalty = new Penalty();
                penalty.EmployeeId = _employeeId;
                penalty.AttDate = _dtpAtt.Checked ? (DateTime?)_dtpAtt.Value.Date : null;
                penalty.PenaltyDate = _dtpPenalty.Value.Date;
                penalty.TypeCode = Convert.ToString(_cboType.SelectedValue);
                penalty.Amount = _numAmount.Value > 0 ? (decimal?)_numAmount.Value : null;
                penalty.Reason = (_txtReason.Text ?? string.Empty).Trim();

                if (_penaltyId == null)
                {
                    PenaltyBLL.Create(penalty);
                }
                else
                {
                    PenaltyBLL.Update(_penaltyId.Value, penalty);
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
                Logger.LogError(unexpected, "FrmPenaltyEdit.OnSaveClick");
                Theme.ShowError("حدث خطأ أثناء حفظ الجزاء.");
            }
        }
    }
}
