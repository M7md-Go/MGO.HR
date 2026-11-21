using System;
using System.Drawing;
using System.Windows.Forms;
using HR.Helpers;

namespace HR.UI.UI
{
    /// <summary>Small modal for creating a new system setting (key, description, value).</summary>
    public class FrmSettingEdit : Form
    {
        private readonly TextBox _txtKey;
        private readonly TextBox _txtDesc;
        private readonly TextBox _txtValue;

        public FrmSettingEdit()
        {
            Theme.Apply(this);
            Text = "إضافة إعداد جديد";
            ClientSize = new Size(440, 210);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;

            Label lblKey = new Label();
            lblKey.Text = "المفتاح (حتى 50 حرفًا):";
            lblKey.AutoSize = true;
            lblKey.Location = new Point(20, 18);

            _txtKey = new TextBox();
            _txtKey.Location = new Point(20, 40);
            _txtKey.Size = new Size(400, 23);
            _txtKey.MaxLength = 50;
            _txtKey.RightToLeft = RightToLeft.No;

            Label lblDesc = new Label();
            lblDesc.Text = "الوصف العربي (حتى 200 حرف):";
            lblDesc.AutoSize = true;
            lblDesc.Location = new Point(20, 72);

            _txtDesc = new TextBox();
            _txtDesc.Location = new Point(20, 94);
            _txtDesc.Size = new Size(400, 23);
            _txtDesc.MaxLength = 200;

            Label lblValue = new Label();
            lblValue.Text = "القيمة (حتى 200 حرف):";
            lblValue.AutoSize = true;
            lblValue.Location = new Point(20, 126);

            _txtValue = new TextBox();
            _txtValue.Location = new Point(20, 148);
            _txtValue.Size = new Size(400, 23);
            _txtValue.MaxLength = 200;

            Button btnOk = new Button();
            btnOk.Text = "إضافة";
            btnOk.Width = 90;
            btnOk.Location = new Point(330, 178);
            btnOk.DialogResult = DialogResult.OK;
            btnOk.Font = Theme.Font(9f);

            Button btnCancel = new Button();
            btnCancel.Text = "إلغاء";
            btnCancel.Width = 90;
            btnCancel.Location = new Point(230, 178);
            btnCancel.DialogResult = DialogResult.Cancel;

            Controls.Add(lblKey);
            Controls.Add(_txtKey);
            Controls.Add(lblDesc);
            Controls.Add(_txtDesc);
            Controls.Add(lblValue);
            Controls.Add(_txtValue);
            Controls.Add(btnOk);
            Controls.Add(btnCancel);
            AcceptButton = btnOk;
            CancelButton = btnCancel;
        }

        public string SettingKey
        {
            get { return (_txtKey.Text ?? string.Empty).Trim(); }
        }

        public string SettingDesc
        {
            get { return (_txtDesc.Text ?? string.Empty).Trim(); }
        }

        public string SettingValue
        {
            get { return (_txtValue.Text ?? string.Empty).Trim(); }
        }
    }
}
