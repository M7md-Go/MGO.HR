using System;
using System.Drawing;
using System.Windows.Forms;
using HR.Helpers;

namespace HR.UI.UI
{
    /// <summary>
    /// Shared modal mandatory-reason dialog (Phase 3, R-04). Used by the
    /// monthly-schedule unlock and rejection flows and by schedule-change
    /// rejections — one implementation instead of two near-identical ones.
    /// Returns the trimmed reason, or null when cancelled.
    /// </summary>
    internal class ReasonDialog : Form
    {
        private TextBox _txtReason;
        private Button _btnOk;
        private Label _lblReason;
        private bool _confirmed;

        private ReasonDialog()
        {
            Theme.Apply(this);
            Text = "السبب إلزامي";
            ClientSize = new Size(440, 155);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;

            _lblReason = new Label();
            _lblReason.Text = "السبب (إلزامي، يُسجَّل في سجل العمليات):";
            _lblReason.Location = new Point(16, 14);
            _lblReason.Size = new Size(408, 22);

            _txtReason = new TextBox();
            _txtReason.Location = new Point(16, 44);
            _txtReason.Size = new Size(408, 26);
            _txtReason.MaxLength = 300;

            _btnOk = new Button();
            _btnOk.Text = "موافق";
            _btnOk.Location = new Point(294, 90);
            _btnOk.Size = new Size(130, 32);
            _btnOk.DialogResult = DialogResult.None;
            _btnOk.Click += delegate
            {
                if (string.IsNullOrWhiteSpace(_txtReason.Text))
                {
                    Theme.ShowError("السبب إلزامي.");
                    return;
                }
                _confirmed = true;
                Close();
            };

            Button btnCancel = new Button();
            btnCancel.Text = "إلغاء";
            btnCancel.Location = new Point(16, 90);
            btnCancel.Size = new Size(130, 32);
            btnCancel.DialogResult = DialogResult.Cancel;
            btnCancel.Click += delegate { Close(); };

            Controls.Add(_lblReason);
            Controls.Add(_txtReason);
            Controls.Add(_btnOk);
            Controls.Add(btnCancel);
            AcceptButton = _btnOk;
            CancelButton = btnCancel;
        }

        /// <summary>Default flow: unlock-month reason (title/labels of the original dialog).</summary>
        public static string Show(IWin32Window owner)
        {
            return Show(owner, "سبب فتح الشهر المقفل",
                "سبب فتح الشهر المقفل (إلزامي، يُسجَّل في سجل العمليات):", "فتح الشهر");
        }

        /// <summary>Generalized mandatory-reason dialog (unlock / schedule rejection).</summary>
        public static string Show(IWin32Window owner, string title, string labelText, string okText)
        {
            using (ReasonDialog dialog = new ReasonDialog())
            {
                dialog.Text = title;
                dialog._lblReason.Text = labelText;
                dialog._btnOk.Text = okText;
                dialog.ShowDialog(owner);
                if (!dialog._confirmed)
                {
                    return null;
                }
                return dialog._txtReason.Text.Trim();
            }
        }
    }
}
