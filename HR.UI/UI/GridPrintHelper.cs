using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.Windows.Forms;

namespace HR.UI.UI
{
    /// <summary>
    /// Generic commercial-grade printing for any themed DataGridView: RTL snapshot,
    /// content-proportional scaled columns, multi-page rows, and a configurable
    /// header band (one or more logos on the right, one or more on the left,
    /// centered title + optional company line + optional print date), a
    /// configurable footer (text, page numbers, printed-by) and an optional
    /// signature area on the last page. Logo layout comes from PrintOptions
    /// (AppSettings Print.* keys — rule 36) or from an explicit profile used by
    /// the print-settings live preview. Uses PrintDocument + PrintPreviewDialog
    /// only (no extra references).
    /// </summary>
    public static class GridPrintHelper
    {
        /// <summary>Opens the print preview with the shared PrintOptions configuration.</summary>
        public static void Print(DataGridView grid, string title)
        {
            Print(grid, title, null);
        }

        /// <summary>Opens the print preview with an optional subtitle (e.g. report period).</summary>
        public static void Print(DataGridView grid, string title, string subtitle)
        {
            Print(grid, title, subtitle, PrintOptions.Snapshot());
        }

        /// <summary>Opens the print preview with an explicit profile (live preview from FrmPrintSettings).</summary>
        public static void Print(DataGridView grid, string title, string subtitle, PrintProfile profile)
        {
            if (grid == null || grid.Columns.Count == 0 || grid.RowCount == 0)
            {
                Theme.ShowInfo("\u0644\u0627 \u062a\u0648\u062c\u062f \u0628\u064a\u0627\u0646\u0627\u062a \u0644\u0637\u0628\u0627\u0639\u062a\u0647\u0627.");
                return;
            }
            if (profile == null)
            {
                profile = PrintOptions.Snapshot();
            }

            // snapshot at call time: stable even if the grid reloads during preview
            List<int> visibleColumns = new List<int>();
            for (int i = 0; i < grid.Columns.Count; i++)
            {
                if (grid.Columns[i].Visible)
                {
                    visibleColumns.Add(i);
                }
            }
            string[] headers = new string[visibleColumns.Count];
            for (int c = 0; c < visibleColumns.Count; c++)
            {
                headers[c] = grid.Columns[visibleColumns[c]].HeaderText;
            }
            List<string[]> rows = new List<string[]>();
            foreach (DataGridViewRow row in grid.Rows)
            {
                if (row.IsNewRow)
                {
                    continue;
                }
                string[] cells = new string[visibleColumns.Count];
                for (int c = 0; c < visibleColumns.Count; c++)
                {
                    object value = row.Cells[visibleColumns[c]].Value;
                    cells[c] = value != null ? value.ToString() : string.Empty;
                }
                rows.Add(cells);
            }

            PrintState state = new PrintState();
            state.Headers = headers;
            state.Rows = rows;
            state.Title = title;
            state.Subtitle = subtitle != null ? subtitle : string.Empty;
            state.Profile = profile;

            using (PrintDocument document = new PrintDocument())
            {
                document.DocumentName = title;
                document.DefaultPageSettings.Landscape = visibleColumns.Count > 6;
                document.PrintPage += delegate(object sender, PrintPageEventArgs args)
                {
                    args.HasMorePages = PrintPage(state, args);
                };
                state.Document = document;
                using (PrintPreviewDialog preview = new PrintPreviewDialog())
                {
                    preview.Document = document;
                    preview.RightToLeft = RightToLeft.Yes;
                    preview.WindowState = FormWindowState.Maximized;
                    preview.ShowDialog();
                }
            }
        }

        /// <summary>Mutable print state: fonts/layout computed once at the first page.</summary>
        private class PrintState
        {
            public string Title;
            public string Subtitle;
            public string[] Headers;
            public List<string[]> Rows;
            public PrintProfile Profile;
            public PrintDocument Document;
            public int NextRow;
            public int PageIndex;
            public Font HeaderFont;
            public Font CellFont;
            public Font TitleFont;
            public Font SubtitleFont;
            public Font FooterFont;
            public float[] Widths;
            public float RowHeight;
            public float BandHeight;
            public bool LaidOut;
        }

        /// <summary>Prints one page; returns true when more rows remain.</summary>
        private static bool PrintPage(PrintState state, PrintPageEventArgs args)
        {
            Graphics graphics = args.Graphics;
            RectangleF bounds = args.MarginBounds;
            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            if (!state.LaidOut)
            {
                state.HeaderFont = Theme.Font(9f);
                state.CellFont = Theme.Font(8.5f);
                state.TitleFont = Theme.Font(13f);
                state.SubtitleFont = Theme.Font(10f);
                state.FooterFont = Theme.Font(8f);
                LayoutColumns(state, graphics, bounds);
            }

            state.PageIndex++;
            StringFormat rtl = new StringFormat();
            rtl.FormatFlags = StringFormatFlags.DirectionRightToLeft;
            rtl.LineAlignment = StringAlignment.Center;
            rtl.Trimming = StringTrimming.EllipsisCharacter;

            using (SolidBrush black = new SolidBrush(Color.Black))
            using (SolidBrush subFore = new SolidBrush(Color.FromArgb(90, 100, 115)))
            {
                // -- header band: logos at both edges, centered title stack --
                DrawLogoZone(graphics, state.Profile.LogosRight, state, bounds, false);
                DrawLogoZone(graphics, state.Profile.LogosLeft, state, bounds, true);

                RectangleF titleRect = new RectangleF(bounds.Left, bounds.Top, bounds.Width, 26f);
                StringFormat center = new StringFormat();
                center.Alignment = StringAlignment.Center;
                center.FormatFlags = StringFormatFlags.DirectionRightToLeft;
                graphics.DrawString(state.Title, state.TitleFont, black, titleRect, center);
                float nextY = bounds.Top + 28f;
                if (state.Profile.HeaderText != null && state.Profile.HeaderText.Trim().Length > 0)
                {
                    graphics.DrawString(state.Profile.HeaderText, state.SubtitleFont, subFore,
                        new RectangleF(bounds.Left, nextY, bounds.Width, 18f), center);
                    nextY += 20f;
                }
                if (state.Subtitle.Length > 0)
                {
                    graphics.DrawString(state.Subtitle, state.SubtitleFont, subFore,
                        new RectangleF(bounds.Left, nextY, bounds.Width, 18f), center);
                    nextY += 20f;
                }
                if (state.Profile.ShowDate)
                {
                    graphics.DrawString("\u062a\u0627\u0631\u064a\u062e \u0627\u0644\u0637\u0628\u0627\u0639\u0629: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                        state.FooterFont, subFore,
                        new RectangleF(bounds.Left, nextY, bounds.Width, 16f), center);
                }
            }

            float cursorY = bounds.Top + state.BandHeight;
            float[] widths = state.Widths;

            // column header band, drawn right-to-left: first logical column sits at the right
            using (SolidBrush headerBack = new SolidBrush(Theme.HeaderColor))
            using (SolidBrush headerFore = new SolidBrush(Color.White))
            {
                float xRight = bounds.Right;
                for (int c = 0; c < state.Headers.Length; c++)
                {
                    float width = widths[c];
                    RectangleF cell = new RectangleF(xRight - width, cursorY, width, state.RowHeight);
                    graphics.FillRectangle(headerBack, cell);
                    graphics.DrawString(state.Headers[c], state.HeaderFont, headerFore, cell, rtl);
                    xRight -= width;
                }
            }
            cursorY += state.RowHeight;

            int rowsPrinted = 0;
            while (state.NextRow < state.Rows.Count && cursorY + state.RowHeight <= bounds.Bottom - 20f)
            {
                string[] cells = state.Rows[state.NextRow];
                state.NextRow++;
                rowsPrinted++;
                if (rowsPrinted % 2 == 0)
                {
                    using (SolidBrush alt = new SolidBrush(Theme.RowAlternateColor))
                    {
                        graphics.FillRectangle(alt, bounds.Left, cursorY, bounds.Width, state.RowHeight);
                    }
                }
                float xRight = bounds.Right;
                for (int c = 0; c < cells.Length; c++)
                {
                    float width = widths[c];
                    RectangleF cell = new RectangleF(xRight - width, cursorY, width, state.RowHeight);
                    using (SolidBrush fore = new SolidBrush(Color.FromArgb(45, 55, 70)))
                    {
                        graphics.DrawString(cells[c], state.CellFont, fore, cell, rtl);
                    }
                    using (Pen line = new Pen(Color.FromArgb(210, 218, 228)))
                    {
                        graphics.DrawLine(line, cell.Left, cell.Top, cell.Left, cell.Bottom);
                        graphics.DrawLine(line, cell.Left, cell.Bottom, cell.Right, cell.Bottom);
                    }
                    xRight -= width;
                }
                cursorY += state.RowHeight;
            }

            using (SolidBrush footerFore = new SolidBrush(Color.FromArgb(110, 120, 135)))
            {
                StringFormat center = new StringFormat();
                center.Alignment = StringAlignment.Center;
                center.FormatFlags = StringFormatFlags.DirectionRightToLeft;
                List<string> parts = new List<string>();
                string footerText = state.Profile.FooterText != null ? state.Profile.FooterText.Trim() : string.Empty;
                parts.Add(footerText.Length > 0 ? footerText : "Modern Time");
                if (state.Profile.ShowPage)
                {
                    parts.Add("\u0635\u0641\u062d\u0629 " + state.PageIndex);
                }
                string printedBy = "\u0637\u064e\u0628\u0639\u064e\u0647\u0627: " + CurrentUserName();
                parts.Add(printedBy);
                graphics.DrawString(string.Join("  \u2014  ", parts.ToArray()), state.FooterFont, footerFore,
                    new RectangleF(bounds.Left, bounds.Bottom - 18f, bounds.Width, 16f), center);
            }

            args.HasMorePages = state.NextRow < state.Rows.Count;
            if (!args.HasMorePages && state.Profile.ShowSignature)
            {
                // signature area on the last page only, just above the footer
                using (SolidBrush sigFore = new SolidBrush(Color.FromArgb(60, 70, 85)))
                using (Pen sigLine = new Pen(Color.FromArgb(140, 150, 165)))
                {
                    StringFormat rightAligned = new StringFormat();
                    rightAligned.Alignment = StringAlignment.Near;
                    rightAligned.FormatFlags = StringFormatFlags.DirectionRightToLeft;
                    string label = state.Profile.SignatureText != null && state.Profile.SignatureText.Trim().Length > 0
                        ? state.Profile.SignatureText.Trim()
                        : "\u0627\u0644\u062a\u0648\u0642\u064a\u0639 \u0648\u0627\u0644\u0627\u0639\u062a\u0645\u0627\u062f";
                    graphics.DrawString(label + ": ", state.FooterFont, sigFore,
                        new RectangleF(bounds.Right - 300f, bounds.Bottom - 44f, 300f, 16f), rightAligned);
                    graphics.DrawLine(sigLine, bounds.Right - 200f, bounds.Bottom - 24f, bounds.Right, bounds.Bottom - 24f);
                }
            }

            if (!args.HasMorePages)
            {
                // reset so a second preview of the same document starts over
                state.LaidOut = false;
                state.NextRow = 0;
                state.PageIndex = 0;
                if (state.HeaderFont != null) { state.HeaderFont.Dispose(); state.HeaderFont = null; }
                if (state.CellFont != null) { state.CellFont.Dispose(); state.CellFont = null; }
                if (state.TitleFont != null) { state.TitleFont.Dispose(); state.TitleFont = null; }
                if (state.SubtitleFont != null) { state.SubtitleFont.Dispose(); state.SubtitleFont = null; }
                if (state.FooterFont != null) { state.FooterFont.Dispose(); state.FooterFont = null; }
            }
            return args.HasMorePages;
        }

        /// <summary>
        /// Draws one side of the header logo zone. Logos are scaled to the
        /// configured height keeping their aspect ratio and laid horizontally:
        /// "forward" (left edge of the paper) runs inward right; the right
        /// edge runs inward left, so the first configured logo is always the
        /// outermost one on its side.
        /// </summary>
        private static void DrawLogoZone(Graphics graphics, List<Image> logos, PrintState state,
            RectangleF bounds, bool forward)
        {
            if (logos == null || logos.Count == 0 || state.Profile.LogoHeight <= 0)
            {
                return;
            }
            float height = state.Profile.LogoHeight;
            float x = forward ? bounds.Left : bounds.Right;
            for (int i = 0; i < logos.Count; i++)
            {
                Image logo = logos[i];
                if (logo == null || logo.Height <= 0)
                {
                    continue;
                }
                float width = logo.Width * (height / logo.Height);
                if (width > bounds.Width / 3f)
                {
                    width = bounds.Width / 3f; // single logo cannot dominate the page
                }
                RectangleF cell = forward
                    ? new RectangleF(x, bounds.Top, width, height)
                    : new RectangleF(x - width, bounds.Top, width, height);
                graphics.DrawImage(logo, cell);
                x = forward ? x + width + 8f : x - width - 8f;
            }
        }

        /// <summary>Current authenticated user display name (empty when unavailable).</summary>
        private static string CurrentUserName()
        {
            try
            {
                if (HR.Helpers.AppContext.IsAuthenticated && HR.Helpers.AppContext.CurrentUser != null
                    && HR.Helpers.AppContext.CurrentUser.FullName != null)
                {
                    return HR.Helpers.AppContext.CurrentUser.FullName;
                }
            }
            catch (Exception)
            {
                // footer attribution is cosmetic — never fail the print
            }
            return "\u063a\u064a\u0631 \u0645\u0639\u0631\u0648\u0641";
        }

        /// <summary>Column widths: content-proportional, scaled once to fit the page width.</summary>
        private static void LayoutColumns(PrintState state, Graphics graphics, RectangleF bounds)
        {
            float[] widths = new float[state.Headers.Length];
            float total = 0f;
            for (int c = 0; c < state.Headers.Length; c++)
            {
                float width = graphics.MeasureString(state.Headers[c], state.HeaderFont).Width + 14f;
                foreach (string[] cells in state.Rows)
                {
                    string text = cells[c] ?? string.Empty;
                    if (text.Length > 0)
                    {
                        width = Math.Max(width, graphics.MeasureString(text, state.CellFont).Width + 12f);
                    }
                    if (width > 220f)
                    {
                        width = 220f; // cap very wide content columns
                    }
                }
                widths[c] = width;
                total += width;
            }
            if (total > bounds.Width && total > 0f)
            {
                float scale = bounds.Width / total;
                for (int c = 0; c < widths.Length; c++)
                {
                    widths[c] = widths[c] * scale;
                }
            }
            state.Widths = widths;
            state.RowHeight = 20f;

            // header band height: title stack (title+company+period+date) vs logo height
            float titleStack = 66f;
            if (state.Profile.HeaderText != null && state.Profile.HeaderText.Trim().Length > 0)
            {
                titleStack += 20f;
            }
            if (state.Subtitle.Length > 0)
            {
                titleStack += 20f;
            }
            state.BandHeight = Math.Max(titleStack, state.Profile.LogoHeight + 6f);

            state.LaidOut = true;
        }
    }
}
