using System;
using System.Collections.Generic;

namespace HR.Entities
{
    /// <summary>One column of a report grid / Excel sheet.</summary>
    public class ReportColumn
    {
        /// <summary>Unique column name (grid key).</summary>
        public string Name { get; set; }
        /// <summary>Arabic header shown above the column.</summary>
        public string Header { get; set; }
        /// <summary>Tablet width hint for Excel (characters).</summary>
        public int Width { get; set; }
    }

    /// <summary>
    /// A fully materialized report: title, column set, string rows and an
    /// optional totals row. The engine (HR.Services.ReportEngine) builds it
    /// from BLL data; FrmReports previews it and ExcelWriter exports it as
    /// SpreadsheetML (UTF-8, RTL) for Excel 2007.
    /// </summary>
    public class ReportData
    {
        /// <summary>Report title (Arabic).</summary>
        public string Title { get; set; }
        /// <summary>Period description (Arabic, e.g. from-to dates).</summary>
        public string PeriodText { get; set; }
        /// <summary>Report columns.</summary>
        public List<ReportColumn> Columns { get; set; }
        /// <summary>Data rows (aligned with Columns).</summary>
        public List<List<string>> Rows { get; set; }
        /// <summary>Totals row aligned with Columns, or null when totals do not apply.</summary>
        public List<string> TotalsRow { get; set; }

        public ReportData()
        {
            Columns = new List<ReportColumn>();
            Rows = new List<List<string>>();
        }

        /// <summary>Adds a column and returns the ReportData for chaining.</summary>
        public ReportData AddColumn(string name, string header, int width)
        {
            ReportColumn column = new ReportColumn();
            column.Name = name;
            column.Header = header;
            column.Width = width;
            Columns.Add(column);
            return this;
        }

        /// <summary>Adds a data row from strings.</summary>
        public void AddRow(params string[] cells)
        {
            List<string> row = new List<string>();
            foreach (string cell in cells)
            {
                row.Add(cell);
            }
            Rows.Add(row);
        }
    }
}
