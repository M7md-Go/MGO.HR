using System;
using System.Drawing;
using System.Windows.Forms;
using HR.BLL;
using HR.Entities;
using HR.Helpers;

namespace HR.UI.UI
{
    /// <summary>Modal add/edit form for one long-leave type.</summary>
    public class FrmLongLeaveTypeEdit : Form
    {
        private readonly int? _typeId;
        private readonly TextBox _txtCode;
        private readonly TextBox _txtName;
        private readonly CheckBox _chkLimited;
        private readonly TextBox _txtMaxMonths;
        private readonly CheckBox _chkActive;
        private readonly TextBox _txtNotes;

        public FrmLongLeaveTypeEdit(int? typeId)
        {
            _typeId = typeId;
            Theme.Apply(this);
            Text = typeId == null ? "نوع إجازة طويلة جديد" : "تعديل نوع الإجازة الطويلة";
            ClientSize = new Size(460, 330);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;

            Label lblCode = new Label();
            lblCode.Text = "الكود (حتى 20 حرفًا، فريد):";
            lblCode.AutoSize = true;
            lblCode.Location = new Point(20, 18);

            _txtCode = new TextBox();
            _txtCode.Location = new Point(20, 40);
            _txtCode.Size = new Size(420, 23);
            _txtCode.MaxLength = 20;
            _txtCode.RightToLeft = RightToLeft.No;

            Label lblName = new Label();
            lblName.Text = "المسمى (حتى 80 حرفًا):";
            lblName.AutoSize = true;
            lblName.Location = new Point(20, 72);

            _txtName = new TextBox();
            _txtName.Location = new Point(20, 94);
            _txtName.Size = new Size(420, 23);
            _txtName.MaxLength = 80;

            Label lblLimited = new Label();
            lblLimited.Text = "حد المدة (بالأشهر):";
            lblLimited.AutoSize = true;
            lblLimited.Location = new Point(20, 126);

            _chkLimited = new CheckBox();
            _chkLimited.Text = "مدة محدودة";
            _chkLimited.AutoSize = true;
            _chkLimited.Location = new Point(20, 148);
            _chkLimited.CheckedChanged += delegate { _txtMaxMonths.Enabled = _chkLimited.Checked; };

            _txtMaxMonths = new TextBox();
            _txtMaxMonths.Location = new Point(150, 146);
            _txtMaxMonths.Size = new Size(70, 23);
            _txtMaxMonths.RightToLeft = RightToLeft.No;
            _txtMaxMonths.Enabled = false;

            _chkActive = new CheckBox();
            _chkActive.Text = "نشط (متاح لاختياره في طلبات الإجازات الطويلة)";
            _chkActive.AutoSize = true;
            _chkActive.Location = new Point(20, 174);
            _chkActive.Checked = true;

            Label lblNotes = new Label();
            lblNotes.Text = "ملاحظات (حتى 500 حرف، إجراءات التقديم والأوثاق):";
            lblNotes.AutoSize = true;
            lblNotes.Location = new Point(20, 202);

            _txtNotes = new TextBox();
            _txtNotes.Location = new Point(20, 224);
            _txtNotes.Size = new Size(420, 50);
            _txtNotes.Multiline = true;
            _txtNotes.MaxLength = 500;

            Button btnOk = new Button();
            btnOk.Text = "حفظ";
            btnOk.Width = 90;
            btnOk.Location = new Point(350, 286);
            btnOk.Font = Theme.Font(9f);
            btnOk.Click += delegate { OnSaveClick(); };

            Button btnCancel = new Button();
            btnCancel.Text = "إلغاء";
            btnCancel.Width = 90;
            btnCancel.Location = new Point(250, 286);
            btnCancel.DialogResult = DialogResult.Cancel;

            Controls.Add(lblCode);
            Controls.Add(_txtCode);
            Controls.Add(lblName);
            Controls.Add(_txtName);
            Controls.Add(lblLimited);
            Controls.Add(_chkLimited);
            Controls.Add(_txtMaxMonths);
            Controls.Add(_chkActive);
            Controls.Add(lblNotes);
            Controls.Add(_txtNotes);
            Controls.Add(btnOk);
            Controls.Add(btnCancel);
            CancelButton = btnCancel;

            if (typeId != null)
            {
                LoadType(typeId.Value);
            }
        }

        private void LoadType(int typeId)
        {
            try
            {
                LongLeaveType stored = LongLeaveTypeBLL.GetById(typeId);
                if (stored == null)
                {
                    Theme.ShowError("النوع غير موجود.");
                    Close();
                    return;
                }
                _txtCode.Text = stored.TypeCode;
                _txtName.Text = stored.TypeName;
                _chkLimited.Checked = stored.IsLimited == true;
                _txtMaxMonths.Text = stored.MaxMonths == null ? "" : stored.MaxMonths.Value.ToString();
                _txtMaxMonths.Enabled = _chkLimited.Checked;
                _chkActive.Checked = stored.IsActive != false;
                _txtNotes.Text = stored.Notes;
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmLongLeaveTypeEdit.LoadType");
                Theme.ShowError("حدث خطأ أثناء تحميل بيانات النوع.");
            }
        }

        private void OnSaveClick()
        {
            try
            {
                string code = (_txtCode.Text ?? string.Empty).Trim();
                string name = (_txtName.Text ?? string.Empty).Trim();
                int? maxMonths = null;
                if (_chkLimited.Checked)
                {
                    string monthsText = (_txtMaxMonths.Text ?? string.Empty).Trim();
                    int parsed;
                    if (!int.TryParse(monthsText, out parsed) || parsed <= 0 || parsed > 120)
                    {
                        Theme.ShowError("عدد أشهر الحد رقم بين 1 و 120.");
                        return;
                    }
                    maxMonths = parsed;
                }
                if (_typeId == null)
                {
                    LongLeaveTypeBLL.Create(code, name, _chkLimited.Checked, maxMonths,
                        (_txtNotes.Text ?? string.Empty).Trim());
                }
                else
                {
                    LongLeaveTypeBLL.Update(_typeId.Value, code, name, _chkLimited.Checked, maxMonths,
                        _chkActive.Checked, (_txtNotes.Text ?? string.Empty).Trim());
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
                Logger.LogError(unexpected, "FrmLongLeaveTypeEdit.OnSaveClick");
                Theme.ShowError("حدث خطأ أثناء حفظ النوع.");
            }
        }
    }
}
