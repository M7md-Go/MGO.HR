using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using HR.BLL;
using HR.Helpers;

namespace HR.UI.UI
{
    /// <summary>
    /// Demo-data generation screen (permission "Settings.Manage"). Explains
    /// exactly what will be created, refuses politely when the database is not
    /// empty (the generator only ever runs on a fresh database, per the
    /// master-prompt guard "لا تستخدم بيانات تجريبية باعتبارها بيانات فعلية"),
    /// asks for a double confirmation, then reports the per-table counts and
    /// any notes from DemoDataBLL.
    /// </summary>
    public class FrmDemoData : Form
    {
        private Button _btnGenerate;
        private Label _lblStatus;
        private TextBox _txtResult;

        public FrmDemoData()
        {
            Theme.Apply(this, false); // MDI child: mirrored layout is not applied — RTL flow comes from the containers
            Text = "\u0628\u064a\u0627\u0646\u0627\u062a \u062a\u062c\u0631\u064a\u0628\u064a\u0629";
            ClientSize = new Size(860, 560);
            StartPosition = FormStartPosition.CenterParent;

            Panel header = new Panel();
            Label lblTitle = new Label();
            lblTitle.Text = "\u062a\u0648\u0644\u064a\u062f \u0628\u064a\u0627\u0646\u0627\u062a \u062a\u062c\u0631\u064a\u0628\u064a\u0629 \u0644\u0644\u0645\u0634\u0631\u0648\u0639 \u0643\u0627\u0645\u0644\u0627\u064b \u2014 \u0644\u0644\u062a\u062f\u0631\u064a\u0628 \u0648\u0627\u0644\u062a\u0642\u064a\u064a\u0645 \u0641\u0642\u0637";
            lblTitle.ForeColor = Theme.HeaderTextColor;
            lblTitle.Font = Theme.Font(12f);
            lblTitle.Dock = DockStyle.Fill;
            lblTitle.TextAlign = ContentAlignment.MiddleCenter;
            Theme.StyleHeader(header);
            header.Controls.Add(lblTitle);
            header.Dock = DockStyle.Top;

            FlowLayoutPanel toolbar = new FlowLayoutPanel();
            toolbar.Dock = DockStyle.Top;
            toolbar.Height = 52;
            toolbar.BackColor = Theme.ToolbarColor;
            // FlowLayoutPanel: RTL=Yes (inherited from the form) already reverses the flow;
            // an explicit RightToLeft cancelled that reversal and rendered the bar left-to-right.
            toolbar.FlowDirection = FlowDirection.LeftToRight;
            toolbar.WrapContents = false;
            toolbar.Padding = new Padding(6, 8, 6, 4);

            _btnGenerate = new Button();
            _btnGenerate.Text = "\u062a\u0648\u0644\u064a\u062f \u0627\u0644\u0628\u064a\u0627\u0646\u0627\u062a \u0627\u0644\u062a\u062c\u0631\u064a\u0628\u064a\u0629";
            _btnGenerate.Height = 30;
            _btnGenerate.Click += delegate { OnGenerateClick(); };
            toolbar.Controls.Add(_btnGenerate);

            _lblStatus = new Label();
            _lblStatus.AutoSize = true;
            _lblStatus.ForeColor = Color.FromArgb(150, 90, 30);
            _lblStatus.Font = Theme.Font(9f);
            _lblStatus.Margin = new Padding(12, 10, 6, 0);
            toolbar.Controls.Add(_lblStatus);

            TextBox intro = new TextBox();
            intro.Multiline = true;
            intro.ReadOnly = true;
            intro.ScrollBars = ScrollBars.Vertical;
            intro.Dock = DockStyle.Fill;
            intro.BackColor = Color.White;
            intro.Font = Theme.Font(9.5f);
            intro.Text =
                "\u0633\u064a\u062a\u0645 \u0625\u0646\u0634\u0627\u0621 \u0645\u062c\u0645\u0648\u0639\u0629 \u0645\u062a\u0643\u0627\u0645\u0644\u0629 \u0645\u0646 \u0627\u0644\u0628\u064a\u0627\u0646\u0627\u062a \u0627\u0644\u062a\u062c\u0631\u064a\u0628\u064a\u0629 \u0641\u064a \u0643\u0627\u0641\u0629 \u0623\u062c\u0632\u0627\u0621 \u0627\u0644\u0646\u0637\u0627\u0645:\n\n" +
                "\u2022 \u0627\u0644\u0647\u064a\u0643\u0644 \u0627\u0644\u062a\u0646\u0638\u064a\u0645\u064a: \u0634\u0631\u0643\u0629 + \u0641\u0631\u0639\u0627\u0646 + 4 \u0623\u0642\u0633\u0627\u0645 + \u0642\u0633\u0645\u0627\u0646 \u0641\u0631\u0639\u064a\u0627\u0646 + 5 \u0645\u0633\u0645\u064a\u0627\u062a \u0648\u0638\u064a\u0641\u064a\u0629 + \u0645\u0624\u0647\u0644\u0627\u062a\n" +
                "\u2022 24 \u0645\u0648\u0638\u0641\u0627\u064b \u0628\u0623\u0633\u0645\u0627\u0621 \u0639\u0631\u0628\u064a\u0629 \u0648\u0631\u0648\u0627\u062a\u0628 \u0648\u062a\u0648\u0631\u064a\u062f\u0627\u062a \u0645\u062e\u062a\u0644\u0641\u0629 (\u0623\u0643\u0648\u0627\u062f EMP-0001..EMP-0024)\n" +
                "\u2022 4 \u0645\u0633\u062a\u062e\u062f\u0645\u064a\u0646 \u062a\u062c\u0631\u064a\u0628\u064a\u064a\u0646 (manager / supervisor / hr / disabled) \u0628\u0643\u0644\u0645\u0629 \u0645\u0631\u0648\u0631 Demo@123\n" +
                "\u2022 \u062c\u0647\u0627\u0632\u0627 \u0628\u0635\u0645\u0629 \u062a\u062c\u0631\u064a\u0628\u064a\u0627\u0646 + \u0628\u0635\u0645\u0627\u062a \u0648\u0627\u0642\u0639\u064a\u0629 \u0644\u0622\u062e\u0631 14 \u064a\u0648\u0645\u0627\u064b \u0645\u0639 \u0633\u064a\u0646\u0627\u0631\u064a\u0648\u0647\u0627\u062a \u062a\u0623\u062e\u064a\u0631/\u063a\u064a\u0627\u0628/\u0628\u0635\u0645\u0629 \u0646\u0627\u0642\u0635\u0629\n" +
                "\u2022 \u062c\u062f\u0648\u0644\u0627\u0646 \u0634\u0647\u0631\u064a\u0627\u0646 \u0645\u0639\u062a\u0645\u062f\u0627\u0646 (\u0627\u0644\u0634\u0647\u0631 \u0627\u0644\u062d\u0627\u0644\u064a \u0648\u0627\u0644\u0633\u0627\u0628\u0642) \u0644\u0643\u0644 \u0627\u0644\u0645\u0648\u0638\u0641\u064a\u0646 \u0645\u0639 \u0631\u0627\u062d\u0629 \u0627\u0644\u062c\u0645\u0639\u0629\n" +
                "\u2022 \u0623\u0631\u0635\u064a\u062f\u0629 \u0625\u062c\u0627\u0632\u0627\u062a + 8 \u0637\u0644\u0628\u0627\u062a \u0625\u062c\u0627\u0632\u0627\u062a + \u0625\u062c\u0627\u0632\u062a\u0627\u0646 \u0637\u0648\u064a\u0644\u062a\u0627\u0646 + 6 \u0645\u0623\u0645\u0648\u0631\u064a\u0627\u062a + 5 \u062c\u0632\u0627\u0621\u0627\u062a + 6 \u0623\u0639\u0645\u0627\u0644 \u0625\u0636\u0627\u0641\u064a\u0629 + 3 \u062a\u0635\u062d\u064a\u062d\u0627\u062a \u0645\u0639\u0644\u0651\u0642\u0629\n" +
                "\u2022 \u0639\u0637\u0644\u062a\u0627\u0646 \u0631\u0633\u0645\u064a\u062a\u0627\u0646 \u0645\u062a\u0643\u0631\u0631\u062a\u0627\u0646 + 3 \u0645\u0644\u0641\u0627\u062a \u0645\u0648\u0638\u0641\u064a\u0646 \u062a\u062c\u0631\u064a\u0628\u064a\u0629 + \u0633\u062c\u0644 \u062a\u0627\u0631\u064a\u062e \u0648\u062a\u0633\u062c\u064a\u0644 \u062f\u062e\u0648\u0644\n\n" +
                "\u062b\u0645 \u064a\u064f\u0634\u063a\u0651\u0644 \u0645\u062d\u0631\u0643 \u0627\u0644\u062d\u0636\u0648\u0631 \u0627\u0644\u062d\u0642\u064a\u0642\u064a \u0639\u0644\u0649 \u0627\u0644\u0628\u0635\u0645\u0627\u062a \u0644\u064a\u0646\u062a\u062c \u0633\u062c\u0644 \u0627\u0644\u062d\u0636\u0648\u0631 \u0648\u0627\u0644\u062a\u0623\u062e\u064a\u0631 \u0648\u0627\u0644\u0627\u0633\u062a\u062b\u0646\u0627\u0621\u0627\u062a.\n\n" +
                "\u062a\u062d\u0630\u064a\u0631 \u0645\u0647\u0645 \u0641\u064a \u0635\u0644\u0629 \u0627\u0644\u0645\u062e\u0637\u0637: \u0627\u0644\u062a\u0648\u0644\u064a\u062f \u064a\u0639\u0645\u0644 \u0639\u0644\u0649 \u0642\u0627\u0639\u062f\u0629 \u0628\u064a\u0627\u0646\u0627\u062a \u0641\u0627\u0631\u063a\u0629 \u0641\u0642\u0637. \u0625\u0630\u0627 \u0643\u0627\u0646\u062a \u0647\u0646\u0627\u0643 \u0628\u064a\u0627\u0646\u0627\u062a \u0641\u0639\u0644\u064a\u0629 \u0641\u064a \u0647\u0630\u0647 \u0627\u0644\u0642\u0627\u0639\u062f\u0629 \u0641\u0633\u0648\u0641 \u064a\u0631\u0641\u0636 \u0627\u0644\u0646\u0637\u0627\u0645 \u0627\u0644\u062a\u0648\u0644\u064a\u062f \u062d\u0645\u0627\u064a\u0629\u064b \u0644\u0644\u0628\u064a\u0627\u0646\u0627\u062a.";

            _txtResult = new TextBox();
            _txtResult.Multiline = true;
            _txtResult.ReadOnly = true;
            _txtResult.ScrollBars = ScrollBars.Vertical;
            _txtResult.Dock = DockStyle.Fill;
            _txtResult.BackColor = Color.White;
            _txtResult.Font = Theme.Font(9.5f);
            _txtResult.Visible = false;

            SplitContainer split = new SplitContainer();
            split.Dock = DockStyle.Fill;
            split.Orientation = Orientation.Horizontal;
            split.Panel1.Controls.Add(intro);
            split.Panel2.Controls.Add(_txtResult);
            split.Panel1MinSize = 120;
            split.Panel2MinSize = 60;
            split.SplitterDistance = 380;

            Controls.Add(split);
            Controls.Add(toolbar);
            Controls.Add(header);
        }

        private void OnGenerateClick()
        {
            if (!DemoDataBLL.IsFreshDatabase())
            {
                Theme.ShowError("\u0642\u0627\u0639\u062f\u0629 \u0627\u0644\u0628\u064a\u0627\u0646\u0627\u062a \u0644\u064a\u0633\u062a \u0641\u0627\u0631\u063a\u0629 (\u064a\u0648\u062c\u062f \u0645\u0648\u0637\u0641\u0648\u0646) \u2014 \u0627\u0644\u062a\u0648\u0644\u064a\u062f \u064a\u0639\u0645\u0644 \u0639\u0644\u0649 \u0642\u0627\u0639\u062f\u0629 \u062c\u062f\u064a\u062f\u0629 \u0641\u0627\u0631\u063a\u0629 \u0641\u0642\u0637.\n" +
                    "\u0623\u0631\u062c\u0648\u0627 \u0623\u0646 \u062a\u0623\u062e\u0630 \u0646\u0633\u062e\u0629 \u0627\u062d\u062a\u064a\u0627\u0637\u064a\u0629 \u0648\u062a\u062d\u062f\u062b \u0642\u0627\u0639\u062f\u0629 \u0645\u0646\u0641\u0635\u0644\u0629 \u0644\u0644\u062a\u062c\u0631\u0628\u0629.");
                return;
            }
            if (!Theme.Confirm("\u0633\u064a\u062a\u0645 \u0625\u0646\u0634\u0627\u0621 \u0645\u062c\u0645\u0648\u0639\u0629 \u0643\u0627\u0645\u0644\u0629 \u0645\u0646 \u0627\u0644\u0628\u064a\u0627\u0646\u0627\u062a \u0627\u0644\u062a\u062c\u0631\u064a\u0628\u064a\u0629 \u0641\u064a \u0647\u0630\u0647 \u0627\u0644\u0642\u0627\u0639\u062f\u0629.\n" +
                "\u0644\u0646 \u064a\u064f\u062d\u0630\u0641 \u0623\u064a \u0634\u064a\u0621\u060c \u0644\u0643\u0646 \u0627\u0644\u0639\u0645\u0644\u064a\u0629 \u062a\u0636\u064a\u0641 \u0645\u0626\u0627\u062a \u0627\u0644\u0633\u062c\u0644\u0627\u062a \u0648\u0644\u0627 \u064a\u0645\u0643\u0646 \u0627\u0644\u062a\u0631\u0627\u062c\u0639 \u0639\u0646\u0647\u0627 \u0627\u0644\u0622\u0646 \u0633\u0648\u0649 \u0645\u0646 \u062e\u0644\u0627\u0644 \u0627\u0633\u062a\u0639\u0627\u062f\u0629 \u0646\u0633\u062e\u0629 \u0627\u062d\u062a\u064a\u0627\u0637\u064a\u0629.\n\n" +
                "\u0647\u0644 \u0623\u0646\u062a \u0645\u062a\u0623\u0643\u062f \u0623\u0646\u0643 \u062a\u0639\u0645\u0644 \u0639\u0644\u0649 \u0642\u0627\u0639\u062f\u0629 \u062a\u062c\u0631\u064a\u0628\u064a\u0629 \u0648\u062a\u0631\u064a\u062f \u0627\u0644\u0645\u062a\u0627\u0628\u0639\u0629\u061f"))
            {
                return;
            }

            try
            {
                _btnGenerate.Enabled = false;
                _lblStatus.Text = "\u062c\u0627\u0631\u064d \u0627\u0644\u062a\u0648\u0644\u064a\u062f\u2026";
                Cursor = Cursors.WaitCursor;
                DemoDataBLL.SeedSummary summary = DemoDataBLL.Generate();
                System.Text.StringBuilder report = new System.Text.StringBuilder();
                report.AppendLine("\u062a\u0645 \u062a\u0648\u0644\u064a\u062f \u0627\u0644\u0628\u064a\u0627\u0646\u0627\u062a \u0627\u0644\u062a\u062c\u0631\u064a\u0628\u064a\u0629 \u0628\u0646\u062c\u0627\u062d:");
                foreach (KeyValuePair<string, int> entry in summary.Counts)
                {
                    report.AppendLine("  \u2022 " + entry.Key + ": " + entry.Value + " \u0633\u062c\u0644");
                }
                foreach (string note in summary.Notes)
                {
                    report.AppendLine("  \u2139 " + note);
                }
                report.AppendLine();
                report.AppendLine("\u0645\u0633\u062a\u062e\u062f\u0645\u0648 \u0627\u0644\u062a\u062c\u0631\u0628\u0629: manager / supervisor / hr / disabled(disabled) \u2014 \u0643\u0644\u0645\u0629 \u0627\u0644\u0645\u0631\u0648\u0631: Demo@123");
                _txtResult.Text = report.ToString();
                _txtResult.Visible = true;
                _lblStatus.Text = "\u062a\u0645 \u0627\u0644\u062a\u0648\u0644\u064a\u062f \u0628\u0646\u062c\u0627\u062d \u2714";
                Theme.ShowInfo("\u062a\u0645 \u062a\u0648\u0644\u064a\u062f \u0627\u0644\u0628\u064a\u0627\u0646\u0627\u062a \u0627\u0644\u062a\u062c\u0631\u064a\u0628\u064a\u0629 \u0644\u0644\u0645\u0634\u0631\u0648\u0639 \u0643\u0627\u0645\u0644\u0627\u064b.\n\u0641\u064a\u0647\u0627 \u062a\u0642\u0631\u064a\u0631 \u0627\u0644\u062a\u0641\u0627\u0635\u064a\u0644 \u0623\u062f\u0646\u0627\u0647.");
            }
            catch (BusinessException expected)
            {
                _lblStatus.Text = string.Empty;
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                _lblStatus.Text = string.Empty;
                Logger.LogError(unexpected, "FrmDemoData.OnGenerateClick");
                Theme.ShowError("\u062d\u062f\u062b \u062e\u0637\u0623 \u0623\u062b\u0646\u0627\u0621 \u062a\u0648\u0644\u064a\u062f \u0627\u0644\u0628\u064a\u0627\u0646\u0627\u062a \u0627\u0644\u062a\u062c\u0631\u064a\u0628\u064a\u0629.\n" +
                    "\u0623\u062c\u0632\u0627\u0621 \u062a\u0627\u0645\u0629 \u0642\u062f \u062a\u0643\u0648\u0646 \u0642\u062f \u0623\u064f\u062f\u062e\u0644\u062a \u0642\u0628\u0644 \u0627\u0644\u062e\u0637\u0623 \u2014 \u0627\u0633\u062a\u0631\u062c\u0639 \u0646\u0633\u062e\u0629 \u0627\u062d\u062a\u064a\u0627\u0637\u064a\u0629 \u0648\u0623\u0639\u062f \u0627\u0644\u0645\u062d\u0627\u0648\u0644\u0629 \u0639\u0644\u0649 \u0642\u0627\u0639\u062f\u0629 \u062c\u062f\u064a\u062f\u0629.");
            }
            finally
            {
                Cursor = Cursors.Default;
                _btnGenerate.Enabled = true;
            }
        }
    }
}
