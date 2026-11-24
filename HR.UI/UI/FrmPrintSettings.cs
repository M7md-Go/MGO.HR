using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using HR.BLL;
using HR.Helpers;

namespace HR.UI.UI
{
    /// <summary>
    /// Print settings screen (permission "Settings.Manage", rule 36 — all
    /// print values are AppSettings keys, nothing hardcoded): report logos on
    /// the right and left of the header (one or more per side), logo height,
    /// company header line, custom footer, print date / page number toggles
    /// and the signature area on the last page. Saving goes through
    /// SettingsBLL.Create (new keys) / SettingsBLL.Save (existing keys) so
    /// every change is validated and audited; the image cache is invalidated
    /// afterwards so the next print reads the new configuration. The live
    /// preview button prints a sample document from the CURRENT form values,
    /// before saving.
    /// </summary>
    public class FrmPrintSettings : Form
    {
        private List<string> _right;
        private List<string> _left;
        private CheckedListBox _lstRight;
        private CheckedListBox _lstLeft;
        private PictureBox _picRight;
        private PictureBox _picLeft;
        private NumericUpDown _numHeight;
        private TextBox _txtHeader;
        private TextBox _txtFooter;
        private TextBox _txtSignature;
        private CheckBox _chkDate;
        private CheckBox _chkPage;
        private CheckBox _chkSignature;

        public FrmPrintSettings()
        {
            Theme.Apply(this, false); // MDI child: mirrored layout is not applied — RTL flow comes from the containers
            Text = "\u0625\u0639\u062f\u0627\u062f\u0627\u062a \u0627\u0644\u0637\u0628\u0627\u0639\u0629";
            ClientSize = new Size(960, 640);
            StartPosition = FormStartPosition.CenterParent;

            Panel header = new Panel();
            Label lblTitle = new Label();
            lblTitle.Text = "\u0625\u0639\u062f\u0627\u062f\u0627\u062a \u0627\u0644\u0637\u0628\u0627\u0639\u0629 \u2014 \u062a\u0646\u0637\u0628\u0642 \u0639\u0644\u0649 \u062c\u0645\u064a\u0639 \u0627\u0644\u062a\u0642\u0627\u0631\u064a\u0631 \u0648\u0627\u0644\u0642\u0648\u0627\u0626\u0645 \u0627\u0644\u0645\u0637\u0628\u0648\u0639\u0629";
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

            Button btnSave = new Button();
            btnSave.Text = "\u062d\u0641\u0638 \u0627\u0644\u0625\u0639\u062f\u0627\u062f\u0627\u062a";
            btnSave.Height = 30;
            btnSave.Click += delegate { OnSaveClick(); };
            toolbar.Controls.Add(btnSave);

            Button btnPreview = new Button();
            btnPreview.Text = "\u0645\u0639\u0627\u064a\u0646\u0629 \u0644\u064a\u0641\u0629 (\u0642\u0628\u0644 \u0627\u0644\u062d\u0641\u0638)";
            btnPreview.Height = 30;
            btnPreview.Click += delegate { OnPreviewClick(); };
            toolbar.Controls.Add(btnPreview);

            _right = new List<string>();
            _left = new List<string>();

            TableLayoutPanel main = new TableLayoutPanel();
            main.Dock = DockStyle.Fill;
            main.ColumnCount = 2;
            main.RowCount = 2;
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 300f));
            main.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            main.Controls.Add(BuildSideGroup("\u0634\u0639\u0627\u0631\u0627\u062a \u0627\u0644\u064a\u0645\u064a\u0646 \u0645\u0646 \u0627\u0644\u062a\u0642\u0631\u064a\u0631", true), 0, 0);
            main.Controls.Add(BuildSideGroup("\u0634\u0639\u0627\u0631\u0627\u062a \u0627\u0644\u064a\u0633\u0627\u0631 \u0645\u0646 \u0627\u0644\u062a\u0642\u0631\u064a\u0631", false), 1, 0);
            Control options = BuildOptionsGroup();
            main.Controls.Add(options, 0, 1);
            main.SetColumnSpan(options, 2);

            Controls.Add(main);
            Controls.Add(toolbar);
            Controls.Add(header);
            LoadFromSettings();
        }

        /// <summary>Builds the logo group of one side (list + add/remove + live thumbnail).</summary>
        private GroupBox BuildSideGroup(string caption, bool rightSide)
        {
            GroupBox group = new GroupBox();
            group.Text = caption + " \u2014 \u064a\u0645\u0643\u0646 \u0625\u0636\u0627\u0641\u0629 \u0623\u0643\u062b\u0631 \u0645\u0646 \u0634\u0639\u0627\u0631";
            group.Dock = DockStyle.Fill;
            group.Padding = new Padding(8);

            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.ColumnCount = 1;
            layout.RowCount = 3;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 110f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40f));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            CheckedListBox list = new CheckedListBox();
            list.Dock = DockStyle.Fill;
            list.CheckOnClick = true;
            list.IntegralHeight = false;
            list.Font = Theme.Font(8.5f);
            layout.Controls.Add(list, 0, 0);

            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Fill;
            buttons.FlowDirection = FlowDirection.LeftToRight; // mirrors with the RTL group box
            buttons.WrapContents = false;
            buttons.Padding = new Padding(0, 4, 0, 0);

            Button btnAdd = new Button();
            btnAdd.Text = "\u0625\u0636\u0627\u0641\u0629 \u0634\u0639\u0627\u0631\u2026";
            btnAdd.Width = 100;
            btnAdd.Height = 28;
            btnAdd.Click += delegate { OnAddLogoClick(rightSide); };
            buttons.Controls.Add(btnAdd);

            Button btnRemove = new Button();
            btnRemove.Text = "\u0625\u0632\u0627\u0644\u0629 \u0627\u0644\u0645\u062d\u062f\u062f";
            btnRemove.Width = 100;
            btnRemove.Height = 28;
            btnRemove.Click += delegate { OnRemoveLogoClick(rightSide); };
            buttons.Controls.Add(btnRemove);

            Button btnUp = new Button();
            btnUp.Text = "\u062a\u0642\u062f\u064a\u0645";
            btnUp.Width = 70;
            btnUp.Height = 28;
            btnUp.Click += delegate { OnMoveClick(rightSide, -1); };
            buttons.Controls.Add(btnUp);

            Button btnDown = new Button();
            btnDown.Text = "\u062a\u0623\u062e\u064a\u0631";
            btnDown.Width = 70;
            btnDown.Height = 28;
            btnDown.Click += delegate { OnMoveClick(rightSide, 1); };
            buttons.Controls.Add(btnDown);
            layout.Controls.Add(buttons, 0, 1);

            PictureBox preview = new PictureBox();
            preview.Dock = DockStyle.Fill;
            preview.SizeMode = PictureBoxSizeMode.Zoom;
            preview.BorderStyle = BorderStyle.FixedSingle;
            layout.Controls.Add(preview, 0, 2);

            group.Controls.Add(layout);
            if (rightSide)
            {
                _lstRight = list;
                _picRight = preview;
            }
            else
            {
                _lstLeft = list;
                _picLeft = preview;
            }
            return group;
        }

        /// <summary>Builds the options group: logo height, header/footer text, toggles.</summary>
        private GroupBox BuildOptionsGroup()
        {
            GroupBox group = new GroupBox();
            group.Text = "\u062e\u064a\u0627\u0631\u0627\u062a \u0627\u0644\u0637\u0628\u0627\u0639\u0629";
            group.Dock = DockStyle.Fill;
            group.Padding = new Padding(8);

            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.ColumnCount = 2;
            layout.RowCount = 6;
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220f));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            for (int r = 0; r < 5; r++)
            {
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42f));
            }
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            Label lblHeight = NewRowLabel("\u0627\u0631\u062a\u0641\u0627\u0639 \u0627\u0644\u0634\u0639\u0627\u0631 (\u0628\u0643\u0633\u0644 24\u2013120)");
            _numHeight = new NumericUpDown();
            _numHeight.Minimum = 24;
            _numHeight.Maximum = 120;
            _numHeight.Value = 48;
            _numHeight.Width = 100;
            _numHeight.TextAlign = HorizontalAlignment.Center;
            layout.Controls.Add(lblHeight, 0, 0);
            layout.Controls.Add(_numHeight, 1, 0);

            Label lblHeader = NewRowLabel("\u0646\u0635 \u0623\u0639\u0644\u0649 \u0627\u0644\u062a\u0642\u0631\u064a\u0631 (\u0627\u0633\u0645 \u0627\u0644\u0634\u0631\u0643\u0629)");
            _txtHeader = new TextBox();
            _txtHeader.MaxLength = 100;
            _txtHeader.Dock = DockStyle.Fill;
            layout.Controls.Add(lblHeader, 0, 1);
            layout.Controls.Add(_txtHeader, 1, 1);

            Label lblFooter = NewRowLabel("\u0646\u0635 \u0623\u0633\u0641\u0644 \u0627\u0644\u062a\u0642\u0631\u064a\u0631 (\u0627\u0644\u0627\u0641\u062a\u0631\u0627\u0636\u064a Modern Time)");
            _txtFooter = new TextBox();
            _txtFooter.MaxLength = 100;
            _txtFooter.Dock = DockStyle.Fill;
            layout.Controls.Add(lblFooter, 0, 2);
            layout.Controls.Add(_txtFooter, 1, 2);

            Label lblSignature = NewRowLabel("\u0639\u0646\u0648\u0627\u0646 \u0645\u0646\u0637\u0642\u0629 \u0627\u0644\u062a\u0648\u0642\u064a\u0639");
            _txtSignature = new TextBox();
            _txtSignature.MaxLength = 60;
            _txtSignature.Dock = DockStyle.Fill;
            layout.Controls.Add(lblSignature, 0, 3);
            layout.Controls.Add(_txtSignature, 1, 3);

            _chkSignature = new CheckBox();
            _chkSignature.Text = "\u0625\u0636\u0627\u0641\u0629 \u0645\u0646\u0637\u0642\u0629 \u062a\u0648\u0642\u064a\u0639 \u0648\u0627\u0639\u062a\u0645\u0627\u062f \u0641\u064a \u0622\u062e\u0631 \u0635\u0641\u062d\u0629";
            _chkSignature.AutoSize = true;
            _chkDate = new CheckBox();
            _chkDate.Text = "\u0625\u0638\u0647\u0627\u0631 \u062a\u0627\u0631\u064a\u062e \u0627\u0644\u0637\u0628\u0627\u0639\u0629 \u0641\u064a \u0627\u0644\u062a\u0631\u0648\u064a\u0633\u0629";
            _chkDate.AutoSize = true;
            _chkPage = new CheckBox();
            _chkPage.Text = "\u0625\u0638\u0647\u0627\u0631 \u0631\u0642\u0645 \u0627\u0644\u0635\u0641\u062d\u0629 \u0641\u064a \u0627\u0644\u062a\u0630\u064a\u064a\u0644";
            _chkPage.AutoSize = true;
            FlowLayoutPanel checks = new FlowLayoutPanel();
            checks.Dock = DockStyle.Fill;
            checks.FlowDirection = FlowDirection.LeftToRight; // mirrors with the RTL group box
            checks.WrapContents = true;
            checks.Controls.Add(_chkSignature);
            checks.Controls.Add(_chkDate);
            checks.Controls.Add(_chkPage);
            layout.Controls.Add(checks, 0, 4);
            layout.SetColumnSpan(checks, 2);

            group.Controls.Add(layout);
            return group;
        }

        private static Label NewRowLabel(string text)
        {
            Label label = new Label();
            label.Text = text;
            label.AutoSize = false;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.Dock = DockStyle.Fill;
            return label;
        }

        /// <summary>Loads the current Print.* settings into the form controls.</summary>
        private void LoadFromSettings()
        {
            _right = ParsePaths(SettingsBLL.GetString(PrintOptions.KeyLogoRight, string.Empty));
            _left = ParsePaths(SettingsBLL.GetString(PrintOptions.KeyLogoLeft, string.Empty));
            int height = SettingsBLL.GetInt(PrintOptions.KeyLogoHeight, 48);
            _numHeight.Value = height < 24 ? 24 : (height > 120 ? 120 : height);
            _txtHeader.Text = SettingsBLL.GetString(PrintOptions.KeyHeaderText, string.Empty) ?? string.Empty;
            _txtFooter.Text = SettingsBLL.GetString(PrintOptions.KeyFooterText, string.Empty) ?? string.Empty;
            _chkDate.Checked = SettingsBLL.GetBool(PrintOptions.KeyShowDate, true);
            _chkPage.Checked = SettingsBLL.GetBool(PrintOptions.KeyShowPage, true);
            _chkSignature.Checked = SettingsBLL.GetBool(PrintOptions.KeyShowSignature, false);
            string signature = SettingsBLL.GetString(PrintOptions.KeySignatureText, string.Empty);
            _txtSignature.Text = string.IsNullOrEmpty(signature)
                ? "\u0627\u0644\u062a\u0648\u0642\u064a\u0639 \u0648\u0627\u0644\u0627\u0639\u062a\u0645\u0627\u062f"
                : signature;
            ReloadSide(true);
            ReloadSide(false);
        }

        private static List<string> ParsePaths(string settingValue)
        {
            List<string> paths = new List<string>();
            if (settingValue == null || settingValue.Trim().Length == 0)
            {
                return paths;
            }
            string[] parts = settingValue.Split(';');
            for (int i = 0; i < parts.Length; i++)
            {
                string path = parts[i] != null ? parts[i].Trim() : string.Empty;
                if (path.Length > 0)
                {
                    paths.Add(path);
                }
            }
            return paths;
        }

        private static string JoinPaths(List<string> paths)
        {
            return string.Join(";", paths.ToArray());
        }

        /// <summary>Refreshes the CheckedListBox + thumbnail of one side.</summary>
        private void ReloadSide(bool rightSide)
        {
            List<string> paths = rightSide ? _right : _left;
            CheckedListBox list = rightSide ? _lstRight : _lstLeft;
            PictureBox preview = rightSide ? _picRight : _picLeft;
            list.Items.Clear();
            foreach (string path in paths)
            {
                list.Items.Add(path, true);
            }
            preview.Image = null;
            if (paths.Count > 0 && File.Exists(paths[0]))
            {
                try
                {
                    byte[] bytes = File.ReadAllBytes(paths[0]);
                    preview.Image = new Bitmap(new MemoryStream(bytes));
                }
                catch (Exception)
                {
                    preview.Image = null;
                }
            }
        }

        private void OnAddLogoClick(bool rightSide)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "\u0627\u062e\u062a\u064a\u0627\u0631 \u0635\u0648\u0631\u0629 \u0627\u0644\u0634\u0639\u0627\u0631";
                dialog.Filter = "\u0635\u0648\u0631 (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|\u0643\u0644 \u0627\u0644\u0645\u0644\u0641\u0627\u062a (*.*)|*.*";
                dialog.Multiselect = true;
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
                List<string> paths = rightSide ? _right : _left;
                foreach (string fileName in dialog.FileNames)
                {
                    if (!paths.Contains(fileName))
                    {
                        paths.Add(fileName);
                    }
                }
                ReloadSide(rightSide);
            }
        }

        private void OnRemoveLogoClick(bool rightSide)
        {
            List<string> paths = rightSide ? _right : _left;
            CheckedListBox list = rightSide ? _lstRight : _lstLeft;
            for (int i = list.Items.Count - 1; i >= 0; i--)
            {
                if (list.GetItemChecked(i))
                {
                    paths.RemoveAt(i);
                }
            }
            ReloadSide(rightSide);
        }

        /// <summary>Reorders one side's logo list (checked-in or not): -1 forward, +1 backward.</summary>
        private void OnMoveClick(bool rightSide, int delta)
        {
            List<string> paths = rightSide ? _right : _left;
            CheckedListBox list = rightSide ? _lstRight : _lstLeft;
            if (list.SelectedIndex < 0)
            {
                return;
            }
            int index = list.SelectedIndex;
            int target = index + delta;
            if (target < 0 || target >= paths.Count)
            {
                return;
            }
            string moved = paths[index];
            paths.RemoveAt(index);
            paths.Insert(target, moved);
            ReloadSide(rightSide);
            list.SelectedIndex = target;
        }

        /// <summary>Persists every Print.* key through SettingsBLL (Create new / Save existing).</summary>
        private void OnSaveClick()
        {
            try
            {
                string rightValue = JoinPaths(_right);
                string leftValue = JoinPaths(_left);
                SaveKey(PrintOptions.KeyLogoRight, rightValue,
                    "\u0645\u0633\u0627\u0631\u0627\u062a \u0634\u0639\u0627\u0631\u0627\u062a \u064a\u0645\u064a\u0646 \u0627\u0644\u062a\u0642\u0631\u064a\u0631 \u0645\u0641\u0635\u0648\u0644\u0629 \u0628\u0641\u0627\u0635\u0644\u0629 \u0645\u0646\u0642\u0648\u0637\u0629");
                SaveKey(PrintOptions.KeyLogoLeft, leftValue,
                    "\u0645\u0633\u0627\u0631\u0627\u062a \u0634\u0639\u0627\u0631\u0627\u062a \u064a\u0633\u0627\u0631 \u0627\u0644\u062a\u0642\u0631\u064a\u0631 \u0645\u0641\u0635\u0648\u0644\u0629 \u0628\u0641\u0627\u0635\u0644\u0629 \u0645\u0646\u0642\u0648\u0637\u0629");
                SaveKey(PrintOptions.KeyLogoHeight, _numHeight.Value.ToString(), "\u0627\u0631\u062a\u0641\u0627\u0639 \u0627\u0644\u0634\u0639\u0627\u0631 \u0641\u064a \u062a\u0631\u0648\u064a\u0633\u0629 \u0627\u0644\u062a\u0642\u0631\u064a\u0631");
                SaveKey(PrintOptions.KeyHeaderText, _txtHeader.Text != null ? _txtHeader.Text.Trim() : string.Empty,
                    "\u0646\u0635 \u0623\u0639\u0644\u0649 \u0645\u062d\u062a\u0648\u0649 \u0627\u0644\u062a\u0642\u0631\u064a\u0631 (\u0627\u0633\u0645 \u0627\u0644\u0634\u0631\u0643\u0629)");
                SaveKey(PrintOptions.KeyFooterText, _txtFooter.Text != null ? _txtFooter.Text.Trim() : string.Empty,
                    "\u0646\u0635 \u062a\u0630\u064a\u064a\u0644 \u0627\u0644\u062a\u0642\u0631\u064a\u0631\u060c \u0627\u0644\u0627\u0641\u062a\u0631\u0627\u0636\u064a Modern Time");
                SaveKey(PrintOptions.KeyShowDate, _chkDate.Checked ? "1" : "0", "\u0625\u0638\u0647\u0627\u0631 \u062a\u0627\u0631\u064a\u062e \u0627\u0644\u0637\u0628\u0627\u0639\u0629 \u0641\u064a \u0627\u0644\u062a\u0631\u0648\u064a\u0633\u0629 (1/0)");
                SaveKey(PrintOptions.KeyShowPage, _chkPage.Checked ? "1" : "0", "\u0625\u0638\u0647\u0627\u0631 \u0631\u0642\u0645 \u0627\u0644\u0635\u0641\u062d\u0629 \u0641\u064a \u0627\u0644\u062a\u0630\u064a\u064a\u0644 (1/0)");
                SaveKey(PrintOptions.KeyShowSignature, _chkSignature.Checked ? "1" : "0",
                    "\u0625\u0636\u0627\u0641\u0629 \u0645\u0646\u0637\u0642\u0629 \u062a\u0648\u0642\u064a\u0639 \u0641\u064a \u0622\u062e\u0631 \u0635\u0641\u062d\u0629 (1/0)");
                SaveKey(PrintOptions.KeySignatureText, _txtSignature.Text != null ? _txtSignature.Text.Trim() : string.Empty,
                    "\u0639\u0646\u0648\u0627\u0646 \u0645\u0646\u0637\u0642\u0629 \u0627\u0644\u062a\u0648\u0642\u064a\u0639");
                PrintOptions.InvalidateCache();
                Theme.ShowInfo("\u062a\u0645 \u062d\u0641\u0638 \u0625\u0639\u062f\u0627\u062f\u0627\u062a \u0627\u0644\u0637\u0628\u0627\u0639\u0629. \u0633\u062a\u0637\u0628\u0642 \u0639\u0644\u0649 \u062c\u0645\u064a\u0639 \u0627\u0644\u062a\u0642\u0627\u0631\u064a\u0631 \u0645\u0646 \u0627\u0644\u0645\u0639\u0627\u064a\u0646\u0629 \u0627\u0644\u062a\u0627\u0644\u064a\u0629.");
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmPrintSettings.OnSaveClick");
                Theme.ShowError("\u062d\u062f\u062b \u062e\u0637\u0623 \u0623\u062b\u0646\u0627\u0621 \u062d\u0641\u0638 \u0625\u0639\u062f\u0627\u062f\u0627\u062a \u0627\u0644\u0637\u0628\u0627\u0639\u0629.");
            }
        }

        /// <summary>Creates the key when missing (Create wants a non-empty value) or saves it in place.</summary>
        private static void SaveKey(string key, string value, string description)
        {
            string existing = SettingsBLL.GetString(key, null);
            if (string.Equals(existing, value, StringComparison.Ordinal))
            {
                return;
            }
            if (existing == null)
            {
                if (value == null || value.Trim().Length == 0)
                {
                    return; // nothing stored yet and nothing to store
                }
                SettingsBLL.Create(key, value, description);
            }
            else
            {
                SettingsBLL.Save(key, value);
            }
        }

        /// <summary>Opens a real print preview of a sample document using the CURRENT form values (no save needed).</summary>
        private void OnPreviewClick()
        {
            using (DataGridView grid = new DataGridView())
            {
                grid.Columns.Add("c1", "\u0627\u0644\u0645\u0648\u0638\u0641");
                grid.Columns.Add("c2", "\u0627\u0644\u0642\u0633\u0645");
                grid.Columns.Add("c3", "\u0627\u0644\u062d\u0636\u0648\u0631");
                grid.Rows.Add("\u0639\u0645\u0648\u062f \u0627\u0644\u0645\u0639\u0627\u064a\u0646\u0629", "\u0642\u0633\u0645 \u0645\u062b\u0627\u0644\u064a", "\u062d\u0627\u0636\u0631");
                grid.Rows.Add("\u0635\u0641 \u062a\u0648\u0636\u064a\u062d\u064a\u064a", "\u0642\u0633\u0645 \u0645\u062b\u0627\u0644\u064a", "\u0645\u062a\u0623\u062e\u0631 10 \u062f");
                grid.Rows.Add("\u0635\u0641 \u062a\u0648\u0636\u064a\u062d\u064a\u064a", "\u0642\u0633\u0645 \u0645\u062b\u0627\u0644\u064a", "\u063a\u0627\u0626\u0628");
                GridPrintHelper.Print(grid, "\u0645\u0639\u0627\u064a\u0646\u0629 \u0625\u0639\u062f\u0627\u062f\u0627\u062a \u0627\u0644\u0637\u0628\u0627\u0639\u0629", "\u0645\u0633\u0648\u062f\u0629 \u0645\u0646 \u0627\u0644\u0646\u0645\u0648\u0630\u062c \u0627\u0644\u0645\u062d\u0648\u0631\u064a \u0644\u0644\u062a\u0642\u0631\u064a\u0631", BuildProfile());
            }
        }

        /// <summary>Snapshot of the CURRENT form values as a PrintProfile (used by the live preview).</summary>
        private PrintProfile BuildProfile()
        {
            PrintProfile profile = new PrintProfile();
            profile.LogosRight = PrintOptions.LoadImages(JoinPaths(_right));
            profile.LogosLeft = PrintOptions.LoadImages(JoinPaths(_left));
            profile.LogoHeight = (int)_numHeight.Value;
            profile.HeaderText = _txtHeader.Text != null ? _txtHeader.Text.Trim() : string.Empty;
            profile.FooterText = _txtFooter.Text != null ? _txtFooter.Text.Trim() : string.Empty;
            profile.ShowDate = _chkDate.Checked;
            profile.ShowPage = _chkPage.Checked;
            profile.ShowSignature = _chkSignature.Checked;
            profile.SignatureText = _txtSignature.Text != null && _txtSignature.Text.Trim().Length > 0
                ? _txtSignature.Text.Trim()
                : "\u0627\u0644\u062a\u0648\u0642\u064a\u0639 \u0648\u0627\u0644\u0627\u0639\u062a\u0645\u0627\u062f";
            return profile;
        }
    }
}
