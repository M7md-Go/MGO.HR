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

        private static Icon _appIcon;

        /// <summary>
        /// Application icon (embedded in ModernTime.exe via ApplicationIcon).
        /// Extracted lazily once; never throws — a null result simply leaves the
        /// form with the default system icon.
        /// </summary>
        public static Icon AppIcon
        {
            get
            {
                if (_appIcon == null)
                {
                    try
                    {
                        _appIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                    }
                    catch (Exception)
                    {
                        _appIcon = null;
                    }
                }
                return _appIcon;
            }
        }

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
            grid.Paint += GridEmptyStatePaint; // unified Arabic empty state for every themed grid
        }

        /// <summary>Draws a centered Arabic hint when a themed grid has no rows yet.</summary>
        private static void GridEmptyStatePaint(object sender, PaintEventArgs args)
        {
            DataGridView grid = sender as DataGridView;
            if (grid == null || grid.RowCount > 0 || grid.Columns.Count == 0 || grid.IsDisposed)
            {
                return;
            }
            Rectangle area = grid.ClientRectangle;
            area.Y += grid.ColumnHeadersHeight;
            area.Height -= grid.ColumnHeadersHeight;
            if (area.Height <= 0)
            {
                return;
            }
            args.Graphics.FillRectangle(Brushes.White, area);
            StringFormat format = new StringFormat();
            format.Alignment = StringAlignment.Center;
            format.LineAlignment = StringAlignment.Center;
            format.FormatFlags = StringFormatFlags.DirectionRightToLeft;
            using (SolidBrush brush = new SolidBrush(Color.FromArgb(140, 150, 165)))
            {
                args.Graphics.DrawString("لا توجد بيانات لعرضها", Font(11f), brush, area, format);
            }
        }

        /// <summary>
        /// Status color pill: colors the cells of a translated-Arabic status column
        /// (معلق = warning, معتمد = ok, مرفوض = bad). Call once after building columns.
        /// </summary>
        public static void StyleStatusColumn(DataGridView grid, string columnName)
        {
            grid.CellFormatting += delegate(object sender, DataGridViewCellFormattingEventArgs args)
            {
                if (args.RowIndex < 0)
                {
                    return;
                }
                DataGridView view = sender as DataGridView;
                if (view == null || view.Columns[args.ColumnIndex].Name != columnName)
                {
                    return;
                }
                string value = args.Value != null ? args.Value.ToString() : null;
                Color? pill = StatusPillColor(value);
                if (pill.HasValue)
                {
                    args.CellStyle.BackColor = pill.Value;
                    args.CellStyle.ForeColor = Color.White;
                    args.CellStyle.SelectionBackColor = pill.Value;
                    args.CellStyle.SelectionForeColor = Color.White;
                }
            };
        }

        /// <summary>Maps a translated Arabic status word to its pill color; null = no pill.</summary>
        public static Color? StatusPillColor(string status)
        {
            if (string.IsNullOrEmpty(status))
            {
                return null;
            }
            if (status == "معتمد" || status == "نشط" || status == "مطبق")
            {
                return StatusOk;
            }
            if (status == "معلق" || status == "بانتظار الاعتماد" || status == "قيد الانتظار")
            {
                return StatusWarn;
            }
            if (status == "مرفوض" || status == "مؤرشف" || status == "ملغي")
            {
                return StatusBad;
            }
            return null;
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
