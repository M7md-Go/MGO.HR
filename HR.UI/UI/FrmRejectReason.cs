using System;
using System.Drawing;
using System.Windows.Forms;
using HR.Helpers;

namespace HR.UI.UI
{
    /// <summary>Small modal for entering/updating a leave rejection reason.</summary>
    public class FrmRejectReason : Form
    {
        private readonly TextBox _txtReason;

        public FrmRejectReason(string title, string initialReason)
        {
            Theme.Apply(this);
            Text = title;
            ClientSize = new Size(420, 150);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;

            Label label = new Label();
            label.Text = "سبب الرفض (اختياري):";
            label.AutoSize = true;
            label.Location = new Point(20, 18);

            _txtReason = new TextBox();
            _txtReason.Multiline = true;
            _txtReason.ScrollBars = ScrollBars.Vertical;
            _txtReason.Location = new Point(20, 42);
            _txtReason.Size = new Size(380, 52);
            _txtReason.Text = initialReason ?? string.Empty;

            Button btnOk = new Button();
            btnOk.Text = "موافق";
            btnOk.Width = 90;
            btnOk.Location = new Point(310, 108);
            btnOk.DialogResult = DialogResult.OK;
            btnOk.Font = Theme.Font(9f);

            Button btnCancel = new Button();
            btnCancel.Text = "إلغاء";
            btnCancel.Width = 90;
            btnCancel.Location = new Point(210, 108);
            btnCancel.DialogResult = DialogResult.Cancel;

            Controls.Add(label);
            Controls.Add(_txtReason);
            Controls.Add(btnOk);
            Controls.Add(btnCancel);
            AcceptButton = btnOk;
            CancelButton = btnCancel;
        }

        public string ReasonText
        {
            get { return (_txtReason.Text ?? string.Empty).Trim(); }
        }
    }
}
