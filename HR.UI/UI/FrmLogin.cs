using System;
using System.Drawing;
using System.Windows.Forms;
using HR.BLL;
using HR.Entities;
using HR.Helpers;

namespace HR.UI.UI
{
    /// <summary>Login screen (rule 21/W4): SHA-256 verification handled in UserBLL.</summary>
    public class FrmLogin : Form
    {
        private TextBox _txtUsername;
        private TextBox _txtPassword;
        private Button _btnLogin;
        private Button _btnClose;
        private Panel _header;

        public FrmLogin()
        {
            Theme.Apply(this);
            Text = "تسجيل الدخول";
            Icon = Theme.AppIcon;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(400, 230);

            _header = new Panel();
            Label lblBrand = new Label();
            lblBrand.Text = "Modern Time";
            lblBrand.ForeColor = Theme.HeaderTextColor;
            lblBrand.Font = Theme.Font(14f);
            lblBrand.Dock = DockStyle.Fill;
            lblBrand.TextAlign = ContentAlignment.MiddleCenter;
            Theme.StyleHeader(_header);
            _header.Controls.Add(lblBrand);
            _header.Dock = DockStyle.Top;

            Label lblUser = new Label();
            lblUser.Text = "اسم المستخدم:";
            lblUser.Location = new Point(30, 70);
            lblUser.Size = new Size(110, 24);

            _txtUsername = new TextBox();
            _txtUsername.Location = new Point(150, 67);
            _txtUsername.Size = new Size(220, 26);
            _txtUsername.RightToLeft = RightToLeft.No;

            Label lblPassword = new Label();
            lblPassword.Text = "كلمة المرور:";
            lblPassword.Location = new Point(30, 110);
            lblPassword.Size = new Size(110, 24);

            _txtPassword = new TextBox();
            _txtPassword.Location = new Point(150, 107);
            _txtPassword.Size = new Size(220, 26);
            _txtPassword.PasswordChar = '●';
            _txtPassword.RightToLeft = RightToLeft.No;

            _btnLogin = new Button();
            _btnLogin.Text = "دخول";
            _btnLogin.Location = new Point(150, 160);
            _btnLogin.Size = new Size(105, 34);
            _btnLogin.BackColor = Theme.HeaderColor;
            _btnLogin.ForeColor = Color.White;
            _btnLogin.FlatStyle = FlatStyle.Flat;
            _btnLogin.Click += OnLoginClick;

            _btnClose = new Button();
            _btnClose.Text = "إغلاق";
            _btnClose.Location = new Point(265, 160);
            _btnClose.Size = new Size(105, 34);
            _btnClose.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };

            AcceptButton = _btnLogin;
            CancelButton = _btnClose;

            Controls.Add(lblUser);
            Controls.Add(_txtUsername);
            Controls.Add(lblPassword);
            Controls.Add(_txtPassword);
            Controls.Add(_btnLogin);
            Controls.Add(_btnClose);
            Controls.Add(_header);
        }

        private void OnLoginClick(object sender, EventArgs args)
        {
            try
            {
                string enteredPassword = _txtPassword.Text;
                User user = UserBLL.Login(_txtUsername.Text, enteredPassword);
                _txtPassword.Clear();

                // S-01: no default/flagged password reaches the application
                // shell — the change dialog is mandatory and blocking.
                if (user.MustChangePassword == true)
                {
                    Theme.ShowInfo("كلمة المرور الحالية افتراضية أو معلَّمة للتغيير الإلزامي —\nيجب تعيين كلمة مرور جديدة قبل الدخول إلى النظام.");
                    using (FrmChangePassword change = new FrmChangePassword(enteredPassword))
                    {
                        change.ShowDialog(this);
                    }
                    if (UserBLL.MustStillChangePassword(user.UserId))
                    {
                        AppContext.ClearSession();
                        Theme.ShowError("لم تُنجَز عملية تغيير كلمة المرور — لن يُسمح بالدخول بكلمة المرور الافتراضية.");
                        return;
                    }
                }

                Theme.ShowInfo("مرحبًا " + (string.IsNullOrEmpty(user.FullName) ? user.Username : user.FullName));
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmLogin.OnLoginClick");
                Theme.ShowError("حدث خطأ أثناء تسجيل الدخول. تم حفظ التفاصيل في ملف السجلات.");
            }
        }
    }
}
