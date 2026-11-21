using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using HR.BLL;
using HR.Entities;
using HR.Helpers;

namespace HR.UI.UI
{
    /// <summary>
    /// Data archiving screen (rule 28): shows the cutoff date derived from
    /// "Arch.ActivePeriodDays", previews how many old rows a table carries,
    /// then exports them to Excel and frees the live MDB. Old history is
    /// never deleted without a preceding export — permission "Archive.Execute".
    /// </summary>
    public class FrmArchive : Form
    {
        private ComboBox _cboTable;
        private Label _lblCutoff;
        private Label _lblPreview;
        private Button _btnPreview;
        private Button _btnArchive;
        private DataGridView _grid;

        public FrmArchive()
        {
            Theme.Apply(this, false); // MDI child: mirrored layout is not applied — RTL flow comes from the containers
            Text = "أرشفة البيانات";
            ClientSize = new Size(980, 580);
            StartPosition = FormStartPosition.CenterParent;

            Panel header = new Panel();
            Label lblTitle = new Label();
            lblTitle.Text = "أرشفة البيانات القديمة — تصدير ثم تفريغ آمن";
            lblTitle.ForeColor = Theme.HeaderTextColor;
            lblTitle.Font = Theme.Font(12f);
            lblTitle.Dock = DockStyle.Fill;
            lblTitle.TextAlign = ContentAlignment.MiddleCenter;
            Theme.StyleHeader(header);
            header.Controls.Add(lblTitle);
            header.Dock = DockStyle.Top;

            FlowLayoutPanel toolbar = new FlowLayoutPanel();
            toolbar.Dock = DockStyle.Top;
            toolbar.Height = 88;
            toolbar.BackColor = Theme.ToolbarColor;
            // FlowLayoutPanel: RTL=Yes (inherited from the form) already reverses the flow;
            // an explicit RightToLeft cancelled that reversal and rendered the bar left-to-right.
            toolbar.FlowDirection = FlowDirection.LeftToRight;
            toolbar.WrapContents = true;
            toolbar.Padding = new Padding(6, 8, 6, 4);

            Label lblTable = new Label();
            lblTable.Text = "الجدول:";
            lblTable.AutoSize = true;
            lblTable.Margin = new Padding(3, 10, 3, 0);

            _cboTable = new ComboBox();
            _cboTable.Width = 200;
            _cboTable.DropDownWidth = 240;
            _cboTable.Margin = new Padding(3, 8, 3, 0);

            _btnPreview = new Button();
            _btnPreview.Text = "حساب المتأخر";
            _btnPreview.Height = 30;
            _btnPreview.Click += delegate { OnPreviewClick(); };
            _btnPreview.Margin = new Padding(8, 7, 3, 0);

            _lblPreview = new Label();
            _lblPreview.Text = "لم يتم الحساب بعد";
            _lblPreview.AutoSize = true;
            _lblPreview.Margin = new Padding(8, 10, 3, 0);

            _btnArchive = new Button();
            _btnArchive.Text = "تنفيذ الأرشفة";
            _btnArchive.Height = 30;
            _btnArchive.Click += delegate { OnArchiveClick(); };
            _btnArchive.Margin = new Padding(8, 7, 3, 0);

            _lblCutoff = new Label();
            _lblCutoff.AutoSize = true;
            _lblCutoff.Margin = new Padding(8, 10, 3, 0);

            toolbar.Controls.Add(lblTable);
            toolbar.Controls.Add(_cboTable);
            toolbar.Controls.Add(_btnPreview);
            toolbar.Controls.Add(_lblPreview);
            toolbar.Controls.Add(_btnArchive);
            toolbar.Controls.Add(_lblCutoff);

            _grid = new DataGridView();
            Theme.StyleGrid(_grid);
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _grid.Dock = DockStyle.Fill;

            Controls.Add(_grid);
            Controls.Add(toolbar);
            Controls.Add(header);

            List<KeyValuePair<string, string>> tables = ArchiveBLL.GetArchiveTables();
            _cboTable.DataSource = new BindingSource(tables, null);
            _cboTable.DisplayMember = "Key";
            _cboTable.ValueMember = "Value";

            DateTime cutoff = ArchiveBLL.GetCutoff();
            _lblCutoff.Text = "البيانات النشطة حتى: " + cutoff.ToString("yyyy-MM-dd") + " (من إعداد Arch.ActivePeriodDays)";

            bool canRun = AppContext.HasPermission("Archive.Execute");
            _btnPreview.Enabled = canRun;
            _btnArchive.Enabled = canRun;
            if (!canRun)
            {
                _lblPreview.Text = "لا تملك صلاحية الأرشفة";
            }
            else
            {
                LoadLog();
            }
        }

        private string SelectedTable()
        {
            return _cboTable.SelectedValue as string;
        }

        private void OnPreviewClick()
        {
            try
            {
                string tableName = SelectedTable();
                if (string.IsNullOrEmpty(tableName))
                {
                    return;
                }
                int rowsCount = ArchiveBLL.PreviewCount(tableName);
                _lblPreview.Text = rowsCount + " سجلًا أقدم من " + ArchiveBLL.GetCutoff().ToString("yyyy-MM-dd");
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmArchive.OnPreviewClick");
                Theme.ShowError("حدث خطأ أثناء حساب البيانات المتأخرة.");
            }
        }

        private void OnArchiveClick()
        {
            try
            {
                string tableName = SelectedTable();
                if (string.IsNullOrEmpty(tableName))
                {
                    return;
                }
                if (!Theme.Confirm("سيتم تصدير البيانات الأقدم من " + ArchiveBLL.GetCutoff().ToString("yyyy-MM-dd") +
                    " إلى ملف Excel ثم حذفها من قاعدة البيانات.\n\nتأكد من أخذ نسخة احتياطية قبل المتابعة. هل تريد الاستمرار؟"))
                {
                    return;
                }
                Theme.ShowInfo(ArchiveBLL.Execute(tableName));
                LoadLog();
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmArchive.OnArchiveClick");
                Theme.ShowError("حدث خطأ أثناء تنفيذ الأرشفة.");
            }
        }

        private void LoadLog()
        {
            try
            {
                List<ArchiveLog> rows = ArchiveBLL.GetLog(200);
                if (_grid.ColumnCount == 0)
                {
                    AddColumn("TableName", "الجدول", 90);
                    AddColumn("ToDate", "حتى تاريخ", 90);
                    AddColumn("RowsCount", "عدد السجلات", 80);
                    AddColumn("ArchiveFile", "ملف الأرشيف", 200);
                    AddColumn("CreatedAt", "وقت العملية", 110);
                }
                _grid.Rows.Clear();
                foreach (ArchiveLog archive in rows)
                {
                    int rowIndex = _grid.Rows.Add();
                    DataGridViewRow row = _grid.Rows[rowIndex];
                    row.Tag = archive;
                    row.Cells["TableName"].Value = archive.TableName;
                    row.Cells["ToDate"].Value = archive.ToDate == null ? "-" :
                        archive.ToDate.Value.ToString("yyyy-MM-dd");
                    row.Cells["RowsCount"].Value = archive.RowsCount;
                    row.Cells["ArchiveFile"].Value = archive.ArchiveFile;
                    row.Cells["CreatedAt"].Value = archive.CreatedAt == null ? "-" :
                        archive.CreatedAt.Value.ToString("yyyy-MM-dd HH:mm");
                }
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmArchive.LoadLog");
                Theme.ShowError("حدث خطأ أثناء تحميل سجل الأرشفة.");
            }
        }

        private void AddColumn(string name, string header, int fillWeight)
        {
            DataGridViewTextBoxColumn column = new DataGridViewTextBoxColumn();
            column.Name = name;
            column.HeaderText = header;
            column.FillWeight = fillWeight;
            column.SortMode = DataGridViewColumnSortMode.NotSortable;
            _grid.Columns.Add(column);
        }
    }
}
