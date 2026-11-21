using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace HR.UI.UI
{
    /// <summary>
    /// Shared UI theme (rule 25): Arabic RTL, Cairo font with graceful fallback,
    /// calm colors, standardized dialogs.
    /// </summary>
    public static class Theme
    {
        public const string AppTitle = "Modern Time — إدارة الموارد البشرية والحضور";

        public static Color HeaderColor { get { return Color.FromArgb(43, 87, 154); } }
        public static Color HeaderTextColor { get { return Color.White; } }
        public static Color RowAlternateColor { get { return Color.FromArgb(240, 245, 250); } }
        public static Color ToolbarColor { get { return Color.FromArgb(238, 242, 247); } }
        public static Color StatusOk { get { return Color.FromArgb(46, 128, 89); } }
        public static Color StatusWarn { get { return Color.FromArgb(196, 133, 29); } }
        public static Color StatusBad { get { return Color.FromArgb(178, 55, 55); } }

        /// <summary>Cairo font when installed, otherwise Segoe UI of the same size.</summary>
        public static Font Font(float size)
        {
            try
            {
                foreach (FontFamily family in FontFamily.Families)
                {
                    if (string.Equals(family.Name, "Times New Roman", StringComparison.OrdinalIgnoreCase))
                    {
                        return new Font(family, size, FontStyle.Regular);
                    }
                }
            }
            catch (Exception)
            {
                // Font enumeration failure is non-fatal; fall through to Segoe UI.
            }
            return new Font("Segoe UI", size, FontStyle.Regular);
        }

        /// <summary>Applies RTL + Arabic font to a TOP-LEVEL dialog form, where Windows
        /// full-layout mirroring (RightToLeftLayout) works correctly.</summary>
        public static void Apply(Form form)
        {
            Apply(form, true);
        }

        /// <summary>mirrored=false is required for the MDI shell and MDI children:
        /// Windows does not apply mirrored layout (WS_EX_LAYOUTRTL) to MDI parent or
        /// child windows, and owner-drawn chrome is undefined under mirroring. Those
        /// screens get right-to-left flow from their containers instead (see the
        /// FlowLayoutPanel note: RTL=Yes already reverses FlowLayoutPanel flow).</summary>
        public static void Apply(Form form, bool mirrored)
        {
            form.RightToLeft = RightToLeft.Yes;
            form.RightToLeftLayout = mirrored;
            form.Font = Font(9.75f);
            form.Icon = null;
        }

        public static void StyleHeader(Panel header)
        {
            header.BackColor = HeaderColor;
            header.Height = 44;
        }

        public static void StyleGrid(DataGridView grid)
        {
            grid.RightToLeft = RightToLeft.Yes;
            grid.BackgroundColor = Color.White;
            grid.BorderStyle = BorderStyle.FixedSingle;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AllowUserToResizeRows = false;
            grid.ReadOnly = true;
            grid.MultiSelect = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.RowHeadersVisible = false;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersDefaultCellStyle.BackColor = HeaderColor;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = HeaderTextColor;
            grid.ColumnHeadersDefaultCellStyle.Font = Font(9.5f);
            grid.ColumnHeadersHeight = 32;
            grid.DefaultCellStyle.Font = Font(9.5f);
            grid.RowTemplate.Height = 26;
            grid.AlternatingRowsDefaultCellStyle.BackColor = RowAlternateColor;
        }

        public static void ShowError(string message)
        {
            MessageBox.Show(null, message, AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Error,
                MessageBoxDefaultButton.Button1, MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
        }

        public static void ShowInfo(string message)
        {
            MessageBox.Show(null, message, AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Information,
                MessageBoxDefaultButton.Button1, MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
        }

        public static bool Confirm(string message)
        {
            DialogResult result = MessageBox.Show(null, message, AppTitle, MessageBoxButtons.YesNo,
                MessageBoxIcon.Question, MessageBoxDefaultButton.Button2,
                MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
            return result == DialogResult.Yes;
        }

        /// <summary>
        /// Binds a Key/Value dictionary to a combo. An empty dictionary is left
        /// unbound: WinForms cannot resolve display/value member names on an
        /// empty dictionary and throws "Cannot bind to the new display member".
        /// </summary>
        public static void BindDictionary(ComboBox combo, Dictionary<int, string> options)
        {
            if (options == null || options.Count == 0)
            {
                combo.DataSource = null;
                combo.SelectedIndex = -1;
                return;
            }
            combo.DataSource = new BindingSource(options, null);
            combo.DisplayMember = "Value";
            combo.ValueMember = "Key";
        }
    }
}
