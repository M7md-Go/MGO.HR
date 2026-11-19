using System;
using System.Drawing;
using System.Windows.Forms;
using HR.BLL;
using HR.Helpers;

namespace HR.UI.UI
{
    /// <summary>Change-password dialog with masking and confirmation (rule 21).</summary>
    public class FrmChangePassword : Form
    {
        private TextBox _txtCurrent;
        private TextBox _txtNew;
        private TextBox _txtConfirm;

        public FrmChangePassword()
        {
            Theme.Apply(this);
            Text = "تغيير كلمة المرور";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ClientSize = new Size(420, 190);

            _txtCurrent = AddField("كلمة المرور الحالية:", 25, 30);
            _txtNew = AddField("كلمة المرور الجديدة:", 25, 75);
            _txtConfirm = AddField("تأكيد كلمة المرور:", 25, 120);

            Button btnSave = new Button();
            btnSave.Text = "حفظ";
            btnSave.Location = new Point(150, 160);
            btnSave.Size = new Size(120, 30);
            btnSave.DialogResult = DialogResult.OK;
            btnSave.Click += OnSaveClick;
            Controls.Add(btnSave);
            AcceptButton = btnSave;
            CancelButton = new Button { Visible = false };
        }

        private TextBox AddField(string caption, int left, int top)
        {
            Label label = new Label();
            label.Text = caption;
            label.Location = new Point(left, top + 4);
            label.Size = new Size(160, 22);
            TextBox field = new TextBox();
            field.Location = new Point(190, top);
            field.Size = new Size(200, 26);
            field.PasswordChar = '●';
            field.RightToLeft = RightToLeft.No;
            Controls.Add(label);
            Controls.Add(field);
            return field;
        }

        private void OnSaveClick(object sender, EventArgs args)
        {
            try
            {
                if (string.Equals(_txtNew.Text, _txtConfirm.Text, StringComparison.Ordinal) == false)
                {
                    Theme.ShowError("كلمة المرور الجديدة وتأكيدها غير متطابقين.");
                    DialogResult = DialogResult.None;
                    return;
                }
                UserBLL.ChangePassword(AppContext.CurrentUser.UserId, _txtCurrent.Text, _txtNew.Text);
                Theme.ShowInfo("تم تغيير كلمة المرور بنجاح.");
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
                DialogResult = DialogResult.None;
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmChangePassword.OnSaveClick");
                Theme.ShowError("حدث خطأ غير متوقع. تم حفظ التفاصيل في السجلات.");
                DialogResult = DialogResult.None;
            }
        }
    }
}
