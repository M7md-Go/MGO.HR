using System;
using System.Drawing;
using System.Windows.Forms;
using HR.BLL;
using HR.Entities;
using HR.Helpers;

namespace HR.UI.UI
{
    /// <summary>Small add/rename dialog for one role (name only, validation in UserBLL).</summary>
    public class FrmRoleEdit : Form
    {
        private readonly Role _existing;
        private TextBox _txtName;

        public FrmRoleEdit(Role existing)
        {
            _existing = existing;
            Theme.Apply(this);
            Text = existing == null ? "دور جديد" : "إعادة تسمية الدور";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(380, 160);

            Label lblName = new Label();
            lblName.Text = "اسم الدور *:";
            lblName.Location = new Point(20, 26);
            lblName.Size = new Size(110, 22);

            _txtName = new TextBox();
            _txtName.Location = new Point(140, 22);
            _txtName.Size = new Size(210, 26);
            _txtName.MaxLength = 50;
            if (existing != null)
            {
                _txtName.Text = existing.RoleName ?? "";
                _txtName.SelectAll();
            }

            Button btnSave = new Button();
            btnSave.Text = "حفظ";
            btnSave.Location = new Point(220, 100);
            btnSave.Size = new Size(130, 34);
            btnSave.Click += delegate { OnSaveClick(); };

            Button btnCancel = new Button();
            btnCancel.Text = "إلغاء";
            btnCancel.Location = new Point(30, 100);
            btnCancel.Size = new Size(120, 34);
            btnCancel.DialogResult = DialogResult.Cancel;

            Controls.Add(lblName);
            Controls.Add(_txtName);
            Controls.Add(btnSave);
            Controls.Add(btnCancel);
            AcceptButton = btnSave;
            CancelButton = btnCancel;
        }

        private void OnSaveClick()
        {
            try
            {
                string roleName = (_txtName.Text ?? string.Empty).Trim();
                if (_existing == null)
                {
                    UserBLL.CreateRole(roleName);
                }
                else
                {
                    UserBLL.RenameRole(_existing.RoleId, roleName);
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
                Logger.LogError(unexpected, "FrmRoleEdit.OnSaveClick");
                Theme.ShowError("حدث خطأ أثناء حفظ الدور.");
            }
        }
    }
}
