using System;
using System.Drawing;
using System.Windows.Forms;
using HR.BLL;
using HR.Entities;
using HR.Helpers;

namespace HR.UI.UI
{
    /// <summary>
    /// Mission add/edit dialog (rule 16): employee resolved by code or name,
    /// start/end dates with optional HH:mm times, place, purpose and the
    /// IsExemptAtt flag that makes the attendance engine skip those days.
    /// Validation lives in MissionBLL.
    /// </summary>
    public class FrmMissionEdit : Form
    {
        private readonly int? _missionId;
        private int _employeeId;
        private TextBox _txtEmployee;
        private Label _lblEmployeeName;
        private DateTimePicker _dtpStart;
        private DateTimePicker _dtpEnd;
        private TextBox _txtStartTime;
        private TextBox _txtEndTime;
        private TextBox _txtLocation;
        private TextBox _txtPurpose;
        private CheckBox _chkExempt;
        private Button _btnSave;
        private Button _btnCancel;

        public FrmMissionEdit(int? missionId)
        {
            _missionId = missionId;
            _employeeId = 0;
            Theme.Apply(this);
            Text = missionId == null ? "مأمورية جديدة" : "تعديل مأمورية";
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

            Label lblStart = new Label();
            lblStart.Text = "تاريخ البداية *:";
            lblStart.Location = new Point(20, 77);
            lblStart.Size = new Size(130, 22);

            _dtpStart = new DateTimePicker();
            _dtpStart.Format = DateTimePickerFormat.Short;
            _dtpStart.Location = new Point(155, 73);
            _dtpStart.Size = new Size(280, 26);

            Label lblStartTime = new Label();
            lblStartTime.Text = "وقت البداية:";
            lblStartTime.Location = new Point(20, 112);
            lblStartTime.Size = new Size(130, 22);

            _txtStartTime = new TextBox();
            _txtStartTime.Location = new Point(155, 108);
            _txtStartTime.Size = new Size(120, 26);
            _txtStartTime.RightToLeft = RightToLeft.No;

            Label lblTimeHint1 = new Label();
            lblTimeHint1.Text = "(HH:mm أو فارغ)";
            lblTimeHint1.Location = new Point(285, 112);
            lblTimeHint1.Size = new Size(150, 22);

            Label lblEnd = new Label();
            lblEnd.Text = "تاريخ النهاية *:";
            lblEnd.Location = new Point(20, 147);
            lblEnd.Size = new Size(130, 22);

            _dtpEnd = new DateTimePicker();
            _dtpEnd.Format = DateTimePickerFormat.Short;
            _dtpEnd.Location = new Point(155, 143);
            _dtpEnd.Size = new Size(280, 26);

            Label lblEndTime = new Label();
            lblEndTime.Text = "وقت النهاية:";
            lblEndTime.Location = new Point(20, 182);
            lblEndTime.Size = new Size(130, 22);

            _txtEndTime = new TextBox();
            _txtEndTime.Location = new Point(155, 178);
            _txtEndTime.Size = new Size(120, 26);
            _txtEndTime.RightToLeft = RightToLeft.No;

            Label lblTimeHint2 = new Label();
            lblTimeHint2.Text = "(HH:mm أو فارغ)";
            lblTimeHint2.Location = new Point(285, 182);
            lblTimeHint2.Size = new Size(150, 22);

            Label lblLocation = new Label();
            lblLocation.Text = "المكان:";
            lblLocation.Location = new Point(20, 217);
            lblLocation.Size = new Size(130, 22);

            _txtLocation = new TextBox();
            _txtLocation.Location = new Point(155, 213);
            _txtLocation.Size = new Size(280, 26);
            _txtLocation.MaxLength = 150;

            Label lblPurpose = new Label();
            lblPurpose.Text = "السبب:";
            lblPurpose.Location = new Point(20, 252);
            lblPurpose.Size = new Size(130, 22);

            _txtPurpose = new TextBox();
            _txtPurpose.Location = new Point(155, 248);
            _txtPurpose.Size = new Size(280, 60);
            _txtPurpose.Multiline = true;

            _chkExempt = new CheckBox();
            _chkExempt.Text = "إعفاء من حساب الحضور (تتجاهل الأيام إذا كانت معتمدة)";
            _chkExempt.Location = new Point(155, 320);
            _chkExempt.AutoSize = true;
            _chkExempt.Checked = true;

            _btnSave = new Button();
            _btnSave.Text = "حفظ";
            _btnSave.Location = new Point(305, 410);
            _btnSave.Size = new Size(130, 34);
            _btnSave.Click += delegate { OnSaveClick(); };

            _btnCancel = new Button();
            _btnCancel.Text = "إلغاء";
            _btnCancel.Location = new Point(20, 410);
            _btnCancel.Size = new Size(130, 34);
            _btnCancel.DialogResult = DialogResult.Cancel;

            Controls.Add(lblEmployee);
            Controls.Add(_txtEmployee);
            Controls.Add(_lblEmployeeName);
            Controls.Add(lblStart);
            Controls.Add(_dtpStart);
            Controls.Add(lblStartTime);
            Controls.Add(_txtStartTime);
            Controls.Add(lblTimeHint1);
            Controls.Add(lblEnd);
            Controls.Add(_dtpEnd);
            Controls.Add(lblEndTime);
            Controls.Add(_txtEndTime);
            Controls.Add(lblTimeHint2);
            Controls.Add(lblLocation);
            Controls.Add(_txtLocation);
            Controls.Add(lblPurpose);
            Controls.Add(_txtPurpose);
            Controls.Add(_chkExempt);
            Controls.Add(_btnSave);
            Controls.Add(_btnCancel);

            AcceptButton = _btnSave;
            CancelButton = _btnCancel;

            if (_missionId != null)
            {
                LoadMission();
            }
            else
            {
                _dtpStart.Value = DateTime.Now.Date;
                _dtpEnd.Value = DateTime.Now.Date;
            }
        }

        private void LoadMission()
        {
            try
            {
                MissionView stored = MissionBLL.GetById(_missionId.Value);
                if (stored == null)
                {
                    Theme.ShowError("المأمورية غير موجودة.");
                    return;
                }
                _employeeId = stored.EmployeeId ?? 0;
                _txtEmployee.Text = stored.EmployeeCode;
                _lblEmployeeName.Text = stored.EmployeeName;
                _dtpStart.Value = stored.StartDate ?? DateTime.Now.Date;
                _dtpEnd.Value = stored.EndDate ?? DateTime.Now.Date;
                _txtStartTime.Text = stored.StartTime ?? "";
                _txtEndTime.Text = stored.EndTime ?? "";
                _txtLocation.Text = stored.Location;
                _txtPurpose.Text = stored.Purpose;
                _chkExempt.Checked = stored.IsExemptAtt == true;
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmMissionEdit.LoadMission");
                Theme.ShowError("حدث خطأ أثناء تحميل بيانات المأمورية.");
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

                Mission mission = new Mission();
                mission.EmployeeId = _employeeId;
                mission.StartDate = _dtpStart.Value.Date;
                mission.EndDate = _dtpEnd.Value.Date;
                mission.StartTime = (_txtStartTime.Text ?? string.Empty).Trim();
                mission.EndTime = (_txtEndTime.Text ?? string.Empty).Trim();
                mission.Location = (_txtLocation.Text ?? string.Empty).Trim();
                mission.Purpose = (_txtPurpose.Text ?? string.Empty).Trim();
                mission.IsExemptAtt = _chkExempt.Checked;

                if (_missionId == null)
                {
                    MissionBLL.Create(mission);
                }
                else
                {
                    MissionBLL.Update(_missionId.Value, mission);
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
                Logger.LogError(unexpected, "FrmMissionEdit.OnSaveClick");
                Theme.ShowError("حدث خطأ أثناء حفظ المأمورية: " + unexpected.Message +
                    "\nتم تسجيل التفاصيل في سجل الأخطاء.");
            }
        }
    }
}
