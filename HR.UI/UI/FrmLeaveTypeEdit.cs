using System;
using System.Drawing;
using System.Windows.Forms;
using HR.BLL;
using HR.Entities;
using HR.Helpers;

namespace HR.UI.UI
{
    /// <summary>Modal add/edit form for one leave type (code, name, quota, flags).</summary>
    public class FrmLeaveTypeEdit : Form
    {
        private readonly int? _leaveTypeId;
        private readonly TextBox _txtCode;
        private readonly TextBox _txtName;
        private readonly TextBox _txtAnnualDays;
        private readonly CheckBox _chkPaid;
        private readonly CheckBox _chkRequiresDoc;

        public FrmLeaveTypeEdit(int? leaveTypeId)
        {
            _leaveTypeId = leaveTypeId;
            Theme.Apply(this);
            Text = leaveTypeId == null ? "نوع إجازة جديد" : "تعديل نوع إجازة";
            ClientSize = new Size(440, 260);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;

            Label lblCode = new Label();
            lblCode.Text = "الكود (حتى 10 حروف، فريد):";
            lblCode.AutoSize = true;
            lblCode.Location = new Point(20, 18);

            _txtCode = new TextBox();
            _txtCode.Location = new Point(20, 40);
            _txtCode.Size = new Size(400, 23);
            _txtCode.MaxLength = 10;
            _txtCode.RightToLeft = RightToLeft.No;

            Label lblName = new Label();
            lblName.Text = "المسمى (حتى 50 حرفًا):";
            lblName.AutoSize = true;
            lblName.Location = new Point(20, 72);

            _txtName = new TextBox();
            _txtName.Location = new Point(20, 94);
            _txtName.Size = new Size(400, 23);
            _txtName.MaxLength = 50;

            Label lblAnnual = new Label();
            lblAnnual.Text = "الرصيد السنوي (أيام — فارغ = غير محدود):";
            lblAnnual.AutoSize = true;
            lblAnnual.Location = new Point(20, 126);

            _txtAnnualDays = new TextBox();
            _txtAnnualDays.Location = new Point(20, 148);
            _txtAnnualDays.Size = new Size(120, 23);
            _txtAnnualDays.RightToLeft = RightToLeft.No;

            _chkPaid = new CheckBox();
            _chkPaid.Text = "إجازة مدفوعة الراتب";
            _chkPaid.AutoSize = true;
            _chkPaid.Location = new Point(20, 176);
            _chkPaid.Checked = true;

            _chkRequiresDoc = new CheckBox();
            _chkRequiresDoc.Text = "تتطلب مستندًا (شهادة/تقرير طبي)";
            _chkRequiresDoc.AutoSize = true;
            _chkRequiresDoc.Location = new Point(20, 200);

            Button btnOk = new Button();
            btnOk.Text = "حفظ";
            btnOk.Width = 90;
            btnOk.Location = new Point(330, 226);
            btnOk.Font = Theme.Font(9f);
            btnOk.Click += delegate { OnSaveClick(); };

            Button btnCancel = new Button();
            btnCancel.Text = "إلغاء";
            btnCancel.Width = 90;
            btnCancel.Location = new Point(230, 226);
            btnCancel.DialogResult = DialogResult.Cancel;

            Controls.Add(lblCode);
            Controls.Add(_txtCode);
            Controls.Add(lblName);
            Controls.Add(_txtName);
            Controls.Add(lblAnnual);
            Controls.Add(_txtAnnualDays);
            Controls.Add(_chkPaid);
            Controls.Add(_chkRequiresDoc);
            Controls.Add(btnOk);
            Controls.Add(btnCancel);
            CancelButton = btnCancel;

            if (leaveTypeId != null)
            {
                LoadType(leaveTypeId.Value);
            }
        }

        private void LoadType(int leaveTypeId)
        {
            try
            {
                LeaveType stored = LeaveTypeBLL.GetById(leaveTypeId);
                if (stored == null)
                {
                    Theme.ShowError("نوع الإجازة غير موجود.");
                    Close();
                    return;
                }
                _txtCode.Text = stored.LeaveTypeCode;
                _txtName.Text = stored.TypeName;
                _txtAnnualDays.Text = stored.AnnualDays == null ? "" : stored.AnnualDays.Value.ToString();
                _chkPaid.Checked = stored.IsPaid == true;
                _chkRequiresDoc.Checked = stored.RequiresDoc == true;
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmLeaveTypeEdit.LoadType");
                Theme.ShowError("حدث خطأ أثناء تحميل بيانات نوع الإجازة.");
            }
        }

        private void OnSaveClick()
        {
            try
            {
                string code = (_txtCode.Text ?? string.Empty).Trim();
                string name = (_txtName.Text ?? string.Empty).Trim();
                int? annualDays = null;
                string annualText = (_txtAnnualDays.Text ?? string.Empty).Trim();
                if (annualText.Length > 0)
                {
                    int parsed;
                    if (!int.TryParse(annualText, out parsed) || parsed < 0 || parsed > 365)
                    {
                        Theme.ShowError("الرصيد السنوي رقم بين 0 و 365.");
                        return;
                    }
                    annualDays = parsed;
                }
                if (_leaveTypeId == null)
                {
                    LeaveTypeBLL.Create(code, name, annualDays, _chkPaid.Checked, _chkRequiresDoc.Checked);
                }
                else
                {
                    LeaveTypeBLL.Update(_leaveTypeId.Value, code, name, annualDays, _chkPaid.Checked, _chkRequiresDoc.Checked);
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
                Logger.LogError(unexpected, "FrmLeaveTypeEdit.OnSaveClick");
                Theme.ShowError("حدث خطأ أثناء حفظ نوع الإجازة.");
            }
        }
    }
}
