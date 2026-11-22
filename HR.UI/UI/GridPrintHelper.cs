using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.Windows.Forms;

namespace HR.UI.UI
{
    /// <summary>
    /// Generic commercial-grade printing for any themed DataGridView: RTL snapshot,
    /// content-proportional scaled columns, multi-page rows, header/title/footer.
    /// Uses PrintDocument + PrintPreviewDialog only (no extra references).
    /// </summary>
    public static class GridPrintHelper
    {
        /// <summary>Opens a print preview for the grid with an Arabic report title.</summary>
        public static void Print(DataGridView grid, string title)
        {
            if (grid == null || grid.Columns.Count == 0 || grid.RowCount == 0)
            {
                Theme.ShowInfo("لا توجد بيانات لطباعتها.");
                return;
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
            public string[] Headers;
            public List<string[]> Rows;
            public PrintDocument Document;
            public int NextRow;
            public int PageIndex;
            public Font HeaderFont;
            public Font CellFont;
            public Font TitleFont;
            public Font FooterFont;
            public float[] Widths;
            public float RowHeight;
            public bool LaidOut;
        }

        /// <summary>Prints one page; returns true when more rows remain.</summary>
        private static bool PrintPage(PrintState state, PrintPageEventArgs args)
        {
            Graphics graphics = args.Graphics;
            RectangleF bounds = args.MarginBounds;
            if (!state.LaidOut)
            {
                state.HeaderFont = Theme.Font(9f);
                state.CellFont = Theme.Font(8.5f);
                state.TitleFont = Theme.Font(13f);
                state.FooterFont = Theme.Font(8f);
                LayoutColumns(state, graphics, bounds);
            }

            state.PageIndex++;
            StringFormat rtl = new StringFormat();
            rtl.FormatFlags = StringFormatFlags.DirectionRightToLeft;
            rtl.LineAlignment = StringAlignment.Center;
            rtl.Trimming = StringTrimming.EllipsisCharacter;

            using (SolidBrush black = new SolidBrush(Color.Black))
            {
                StringFormat center = new StringFormat();
                center.Alignment = StringAlignment.Center;
                center.FormatFlags = StringFormatFlags.DirectionRightToLeft;
                graphics.DrawString(state.Title, state.TitleFont, black,
                    new RectangleF(bounds.Left, bounds.Top, bounds.Width, 30f), center);
                graphics.DrawString("تاريخ الطباعة: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                    state.FooterFont, black,
                    new RectangleF(bounds.Left, bounds.Top + 30f, bounds.Width, 16f), center);
            }

            float cursorY = bounds.Top + 52f;
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
            while (state.NextRow < state.Rows.Count && cursorY + state.RowHeight <= bounds.Bottom)
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
                string footer = "Modern Time" + "  —  صفحة " + state.PageIndex;
                graphics.DrawString(footer, state.FooterFont, footerFore,
                    new RectangleF(bounds.Left, bounds.Bottom - 18f, bounds.Width, 16f), center);
            }

            args.HasMorePages = state.NextRow < state.Rows.Count;
            if (!args.HasMorePages)
            {
                // reset so a second preview of the same document starts over
                state.LaidOut = false;
                state.NextRow = 0;
                state.PageIndex = 0;
                if (state.HeaderFont != null) { state.HeaderFont.Dispose(); state.HeaderFont = null; }
                if (state.CellFont != null) { state.CellFont.Dispose(); state.CellFont = null; }
                if (state.TitleFont != null) { state.TitleFont.Dispose(); state.TitleFont = null; }
                if (state.FooterFont != null) { state.FooterFont.Dispose(); state.FooterFont = null; }
            }
            return args.HasMorePages;
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
            state.LaidOut = true;
        }
    }
}
