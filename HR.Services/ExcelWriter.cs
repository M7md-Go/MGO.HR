using System;
using System.Globalization;
using System.IO;
using System.Security;
using System.Text;
using HR.Entities;

namespace HR.Services
{
    /// <summary>
    /// SpreadsheetML 2003 writer (rules 20, D5): exports a ReportData as a
    /// single-sheet XML workbook with UTF-8 encoding, right-to-left sheet
    /// layout, styled header/totals and per-column widths. No external
    /// libraries — plain System.* — and the result opens directly in Excel
    /// 2007 and later. All cell values are emitted as strings; Excel shows
    /// them exactly, and Arabic survives the round trip through the BOM.
    /// </summary>
    public static class ExcelWriter
    {
        /// <summary>Writes the report to disk as SpreadsheetML and returns the path.</summary>
        public static string Export(ReportData report, string filePath)
        {
            if (report == null)
            {
                throw new ArgumentNullException("report");
            }
            StringBuilder xml = new StringBuilder();
            xml.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n");
            xml.Append("<?mso-application progid=\"Excel.Sheet\"?>\n");
            xml.Append("<Workbook xmlns=\"urn:schemas-microsoft-com:office:spreadsheet\"\n");
            xml.Append(" xmlns:o=\"urn:schemas-microsoft-com:office:office\"\n");
            xml.Append(" xmlns:x=\"urn:schemas-microsoft-com:office:excel\"\n");
            xml.Append(" xmlns:ss=\"urn:schemas-microsoft-com:office:spreadsheet\">\n");

            AppendStyles(xml);

            xml.Append("<Worksheet ss:Name=\"");
            xml.Append(Escape(SheetName(report.Title)));
            xml.Append("\">\n");
            xml.Append("<Table>\n");
            foreach (ReportColumn column in report.Columns)
            {
                xml.Append("<Column ss:Width=\"");
                xml.Append(ClampWidth(column.Width));
                xml.Append("\"/>\n");
            }

            AppendRow(xml, string.Empty, report.Title, false);
            AppendRow(xml, string.Empty, report.PeriodText, false);
            xml.Append("<Row ss:Height=\"12\"><Cell><Data ss:Type=\"String\"> </Data></Cell></Row>\n");

            StringBuilder headerCells = new StringBuilder();
            foreach (ReportColumn column in report.Columns)
            {
                AppendCell(headerCells, column.Header, "hdr");
            }
            xml.Append("<Row ss:StyleID=\"hdrrow\">").Append(headerCells).Append("</Row>\n");

            foreach (System.Collections.Generic.List<string> row in report.Rows)
            {
                StringBuilder cells = new StringBuilder();
                for (int i = 0; i < report.Columns.Count; i++)
                {
                    AppendCell(cells, i < row.Count ? row[i] : string.Empty, null);
                }
                xml.Append("<Row>").Append(cells).Append("</Row>\n");
            }

            if (report.TotalsRow != null)
            {
                StringBuilder cells = new StringBuilder();
                for (int i = 0; i < report.Columns.Count; i++)
                {
                    AppendCell(cells, i < report.TotalsRow.Count ? report.TotalsRow[i] : string.Empty, "tot");
                }
                xml.Append("<Row>").Append(cells).Append("</Row>\n");
            }

            xml.Append("</Table>\n");
            xml.Append("<WorksheetOptions xmlns=\"urn:schemas-microsoft-com:office:excel\">\n");
            xml.Append("<DisplayRightToLeft/>\n");
            xml.Append("<SplitHorizontal>6</SplitHorizontal>\n");
            xml.Append("<TopRowBottomPane>6</TopRowBottomPane>\n");
            xml.Append("<FreezePanes/>\n");
            xml.Append("<FrozenNoSplit/>\n");
            xml.Append("<ActivePane>2</ActivePane>\n");
            xml.Append("</WorksheetOptions>\n");
            xml.Append("</Worksheet>\n");
            xml.Append("</Workbook>\n");

            using (StreamWriter writer = new StreamWriter(filePath, false, new UTF8Encoding(true)))
            {
                writer.Write(xml.ToString());
            }
            return filePath;
        }

        /// <summary>Workbook styles: default font, title, header, totals.</summary>
        private static void AppendStyles(StringBuilder xml)
        {
            xml.Append("<Styles>\n");
            xml.Append("<Style ss:ID=\"Default\" ss:Name=\"Normal\">\n");
            xml.Append("<Alignment ss:Vertical=\"Center\" ss:ReadingOrder=\"2\"/>\n");
            xml.Append("<Font ss:FontName=\"Arial\" ss:Size=\"10\"/>\n");
            xml.Append("<Borders>\n");
            xml.Append("<Border ss:Position=\"Bottom\" ss:LineStyle=\"Continuous\" ss:Weight=\"1\" ss:Color=\"#D9D9D9\"/>\n");
            xml.Append("<Border ss:Position=\"Top\" ss:LineStyle=\"Continuous\" ss:Weight=\"1\" ss:Color=\"#D9D9D9\"/>\n");
            xml.Append("</Borders>\n");
            xml.Append("</Style>\n");
            xml.Append("<Style ss:ID=\"title\">\n");
            xml.Append("<Font ss:FontName=\"Arial\" ss:Size=\"14\" ss:Bold=\"1\" ss:Color=\"#1F6FB5\"/>\n");
            xml.Append("<Alignment ss:Horizontal=\"Center\" ss:Vertical=\"Center\" ss:ReadingOrder=\"2\"/>\n");
            xml.Append("</Style>\n");
            xml.Append("<Style ss:ID=\"period\">\n");
            xml.Append("<Font ss:FontName=\"Arial\" ss:Size=\"10\" ss:Color=\"#666666\"/>\n");
            xml.Append("<Alignment ss:Horizontal=\"Center\" ss:Vertical=\"Center\" ss:ReadingOrder=\"2\"/>\n");
            xml.Append("</Style>\n");
            xml.Append("<Style ss:ID=\"hdrrow\">\n");
            xml.Append("<Alignment ss:Horizontal=\"Center\" ss:Vertical=\"Center\" ss:ReadingOrder=\"2\"/>\n");
            xml.Append("</Style>\n");
            xml.Append("<Style ss:ID=\"hdr\">\n");
            xml.Append("<Font ss:FontName=\"Arial\" ss:Size=\"10\" ss:Bold=\"1\" ss:Color=\"#FFFFFF\"/>\n");
            xml.Append("<Interior ss:Color=\"#1F6FB5\" ss:Pattern=\"Solid\"/>\n");
            xml.Append("<Alignment ss:Horizontal=\"Center\" ss:Vertical=\"Center\" ss:ReadingOrder=\"2\"/>\n");
            xml.Append("<Borders>\n");
            xml.Append("<Border ss:Position=\"Bottom\" ss:LineStyle=\"Continuous\" ss:Weight=\"1\" ss:Color=\"#FFFFFF\"/>\n");
            xml.Append("</Borders>\n");
            xml.Append("</Style>\n");
            xml.Append("<Style ss:ID=\"tot\">\n");
            xml.Append("<Font ss:FontName=\"Arial\" ss:Size=\"10\" ss:Bold=\"1\"/>\n");
            xml.Append("<Interior ss:Color=\"#E8F0FE\" ss:Pattern=\"Solid\"/>\n");
            xml.Append("<Alignment ss:Vertical=\"Center\" ss:ReadingOrder=\"2\"/>\n");
            xml.Append("</Style>\n");
            xml.Append("</Styles>\n");
        }

        /// <summary>Title and period rows (merged across the table width).</summary>
        private static void AppendRow(StringBuilder xml, string styleId, string value, bool data)
        {
            xml.Append("<Row>");
            xml.Append("<Cell");
            if (styleId.Length > 0)
            {
                xml.Append(" ss:StyleID=\"").Append(styleId).Append("\"");
            }
            xml.Append("><Data ss:Type=\"String\">").Append(Escape(value ?? string.Empty)).Append("</Data></Cell>");
            xml.Append("</Row>\n");
        }

        private static void AppendCell(StringBuilder cells, string value, string styleId)
        {
            cells.Append("<Cell");
            if (!string.IsNullOrEmpty(styleId))
            {
                cells.Append(" ss:StyleID=\"").Append(styleId).Append("\"");
            }
            cells.Append("><Data ss:Type=\"String\">").Append(Escape(value ?? string.Empty)).Append("</Data></Cell>");
        }

        /// <summary>Excel sheet names: max 31 chars, no : \ / ? * [ ].</summary>
        private static string SheetName(string title)
        {
            string name = title ?? "Report";
            foreach (char forbidden in new char[] { ':', '\\', '/', '?', '*', '[', ']' })
            {
                name = name.Replace(forbidden, '-');
            }
            return name.Length > 31 ? name.Substring(0, 31) : name;
        }

        private static int ClampWidth(int width)
        {
            if (width < 30)
            {
                width = 30;
            }
            if (width > 400)
            {
                width = 400;
            }
            return (int)((width + 4) * 5.5);
        }

        /// <summary>XML escaping for every emitted value.</summary>
        private static string Escape(string value)
        {
            return SecurityElement.Escape(value);
        }
    }
}
