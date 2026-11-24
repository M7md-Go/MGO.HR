using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using HR.BLL;
using HR.Entities;
using HR.Helpers;
using HR.Services;

namespace HR.UI.UI
{
    /// <summary>
    /// Read-only data browser for one table (DB tab): top 500 rows in a bound
    /// grid, client-side text filter over all columns, refresh and Excel
    /// export through the shared SpreadsheetML writer. No editing path exists
    /// here at all (protection of core tables — constraint 2 of the order).
    /// </summary>
    public class FrmDbTableData : Form
    {
        private const int MaxRows = 500;
        private readonly string _tableName;
        private DataGridView _grid;
        private DataTable _data;
        private BindingSource _binding;
        private TextBox _txtFilter;
        private Label _lblInfo;

        public FrmDbTableData(string tableName)
        {
            _tableName = tableName;
            Theme.Apply(this);
            Text = "بيانات الجدول: " + tableName + " (قراءة فقط)";
            FormBorderStyle = FormBorderStyle.Sizable;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(900, 560);

            Panel header = new Panel();
            Label lblTitle = new Label();
            lblTitle.Text = "أول " + MaxRows + " سجل من [" + tableName + "] — استعراض فقط";
            lblTitle.ForeColor = Theme.HeaderTextColor;
            lblTitle.Font = Theme.Font(12f);
            lblTitle.Dock = DockStyle.Fill;
            lblTitle.TextAlign = ContentAlignment.MiddleCenter;
            Theme.StyleHeader(header);
            header.Controls.Add(lblTitle);
            header.Height = 40;
            header.Dock = DockStyle.Top;

            FlowLayoutPanel toolbar = new FlowLayoutPanel();
            toolbar.Dock = DockStyle.Top;
            toolbar.Height = 56;
            toolbar.BackColor = Theme.ToolbarColor;
            // FlowLayoutPanel: RTL=Yes (inherited from the form) already reverses the flow;
            // an explicit RightToLeft cancelled that reversal and rendered the bar left-to-right.
            toolbar.FlowDirection = FlowDirection.LeftToRight;
            toolbar.WrapContents = false;
            toolbar.Padding = new Padding(6, 8, 6, 4);

            Label lblSearch = new Label();
            lblSearch.Text = "بحث في كل الأعمدة:";
            lblSearch.AutoSize = true;
            lblSearch.Margin = new Padding(3, 10, 3, 0);

            _txtFilter = new TextBox();
            _txtFilter.Width = 220;
            _txtFilter.Margin = new Padding(3, 6, 3, 0);
            _txtFilter.KeyDown += delegate(object sender, KeyEventArgs args)
            {
                if (args.KeyCode == Keys.Enter)
                {
                    ApplyFilter();
                    args.Handled = true;
                    args.SuppressKeyPress = true;
                }
            };

            Button btnFilter = new Button();
            btnFilter.Text = "تصفية";
            btnFilter.Height = 30;
            btnFilter.Click += delegate { ApplyFilter(); };

            Button btnClear = new Button();
            btnClear.Text = "إلغاء التصفية";
            btnClear.Height = 30;
            btnClear.Click += delegate { _txtFilter.Text = ""; ApplyFilter(); };

            Button btnReload = new Button();
            btnReload.Text = "تحديث";
            btnReload.Height = 30;
            btnReload.Click += delegate { Reload(); };

            Button btnExport = new Button();
            btnExport.Text = "تصدير Excel";
            btnExport.Height = 30;
            btnExport.Click += delegate { OnExportClick(); };

            toolbar.Controls.Add(lblSearch);
            toolbar.Controls.Add(_txtFilter);
            toolbar.Controls.Add(btnFilter);
            toolbar.Controls.Add(btnClear);
            toolbar.Controls.Add(btnReload);
            toolbar.Controls.Add(btnExport);

            _lblInfo = new Label();
            _lblInfo.AutoSize = true;
            _lblInfo.ForeColor = Theme.HeaderTextColor;
            _lblInfo.Margin = new Padding(6, 10, 3, 0);

            toolbar.Controls.Add(_lblInfo);

            _grid = new DataGridView();
            Theme.StyleGrid(_grid);
            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
            _binding = new BindingSource();

            Controls.Add(_grid);
            Controls.Add(toolbar);
            Controls.Add(header);

            Reload();
        }

        private void Reload()
        {
            try
            {
                _data = DatabaseBLL.ReadRows(_tableName, MaxRows);
                _binding.DataSource = _data;
                _grid.DataSource = _binding;
                _lblInfo.Text = "معروض: " + _data.Rows.Count + " من " +
                    DatabaseBLL.CountRows(_tableName) + " سجل";
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmDbTableData.Reload");
                Theme.ShowError("حدث خطأ أثناء قراءة بيانات الجدول.");
            }
        }

        /// <summary>Client-side Contains filter over every string representation of the loaded page.</summary>
        private void ApplyFilter()
        {
            try
            {
                string text = (_txtFilter.Text ?? string.Empty).Trim().Replace("'", "''");
                if (text.Length == 0)
                {
                    _binding.Filter = null;
                }
                else
                {
                    List<string> parts = new List<string>();
                    foreach (DataColumn column in _data.Columns)
                    {
                        parts.Add("CONVERT(" + column.ColumnName + ", 'System.String') LIKE '%" + text + "%'");
                    }
                    _binding.Filter = string.Join(" OR ", parts.ToArray());
                }
                _lblInfo.Text = "مُصفّى: " + _binding.Count + " من " + _data.Rows.Count + " سجل (صفحة أول 500)";
            }
            catch (Exception filterFailure)
            {
                Logger.LogError(filterFailure, "FrmDbTableData.ApplyFilter");
                Theme.ShowError("صيغة البحث غير قابلة للتطبيق على هذه الأعمدة.");
            }
        }

        private void OnExportClick()
        {
            try
            {
                SaveFileDialog dialog = new SaveFileDialog();
                dialog.Filter = "ملف Excel XML|*.xml";
                dialog.FileName = _tableName + "_" + DateTime.Now.ToString("yyyyMMdd_HHmm") + ".xml";
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
                ReportData report = new ReportData();
                report.Title = "بيانات الجدول: " + _tableName;
                report.PeriodText = "حد أقصى " + MaxRows + " سجل — " + DateTime.Now.ToString("yyyy-MM-dd HH:mm");
                foreach (DataColumn column in _data.Columns)
                {
                    report.AddColumn(column.ColumnName, column.ColumnName, 18);
                }
                foreach (DataRow row in _data.Rows)
                {
                    string[] cells = new string[_data.Columns.Count];
                    for (int i = 0; i < _data.Columns.Count; i++)
                    {
                        cells[i] = row[i] == DBNull.Value ? "" : Convert.ToString(row[i]);
                    }
                    report.Rows.Add(new List<string>(cells));
                }
                string path = ExcelWriter.Export(report, dialog.FileName);
                Theme.ShowInfo("تم التصدير إلى:\n" + path);
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmDbTableData.OnExportClick");
                Theme.ShowError("حدث خطأ أثناء التصدير.");
            }
        }
    }
}
