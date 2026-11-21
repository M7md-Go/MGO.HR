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
    /// Audit log browser (rule 22, permission "Audit.View"): filters by
    /// action / table / date range over the newest 1000 entries. Every row
    /// shows who, what, when, on which machine - old and new values for inspection.
    /// 
    /// word-wrap for the serialized snapshots.
    /// </summary>
    public class FrmAuditLog : Form
    {
        private TextBox _txtAction;
        private TextBox _txtTable;
        private DateTimePicker _dtpFrom;
        private DateTimePicker _dtpTo;
        private Button _btnRefresh;
        private DataGridView _grid;
        private List<AuditLog> _rows;

        public FrmAuditLog()
        {
            Theme.Apply(this, false); // MDI child: mirrored layout is not applied — RTL flow comes from the containers
            Text = "سجل العمليات";
            ClientSize = new Size(1050, 600);
            StartPosition = FormStartPosition.CenterParent;

            Panel header = new Panel();
            Label lblTitle = new Label();
            lblTitle.Text = "سجل العمليات (Audit Log) — آخر 1000 عملية";
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

            Label lblAction = new Label();
            lblAction.Text = "العملية:";
            lblAction.AutoSize = true;
            lblAction.Margin = new Padding(3, 10, 3, 0);

            _txtAction = new TextBox();
            _txtAction.Width = 120;
            _txtAction.Margin = new Padding(3, 8, 3, 0);

            Label lblTable = new Label();
            lblTable.Text = "الجدول:";
            lblTable.AutoSize = true;
            lblTable.Margin = new Padding(3, 10, 3, 0);

            _txtTable = new TextBox();
            _txtTable.Width = 110;
            _txtTable.Margin = new Padding(3, 8, 3, 0);

            Label lblFrom = new Label();
            lblFrom.Text = "من:";
            lblFrom.AutoSize = true;
            lblFrom.Margin = new Padding(3, 10, 3, 0);

            _dtpFrom = new DateTimePicker();
            _dtpFrom.Format = DateTimePickerFormat.Short;
            _dtpFrom.Width = 110;
            _dtpFrom.ShowCheckBox = true;
            _dtpFrom.Value = DateTime.Now.Date.AddDays(-7);
            _dtpFrom.Margin = new Padding(3, 8, 3, 0);

            Label lblTo = new Label();
            lblTo.Text = "إلى:";
            lblTo.AutoSize = true;
            lblTo.Margin = new Padding(3, 10, 3, 0);

            _dtpTo = new DateTimePicker();
            _dtpTo.Format = DateTimePickerFormat.Short;
            _dtpTo.Width = 110;
            _dtpTo.ShowCheckBox = true;
            _dtpTo.Value = DateTime.Now.Date;
            _dtpTo.Margin = new Padding(3, 8, 3, 0);

            _btnRefresh = new Button();
            _btnRefresh.Text = "عرض";
            _btnRefresh.Height = 30;
            _btnRefresh.Click += delegate { Reload(); };

            toolbar.Controls.Add(lblAction);
            toolbar.Controls.Add(_txtAction);
            toolbar.Controls.Add(lblTable);
            toolbar.Controls.Add(_txtTable);
            toolbar.Controls.Add(lblFrom);
            toolbar.Controls.Add(_dtpFrom);
            toolbar.Controls.Add(lblTo);
            toolbar.Controls.Add(_dtpTo);
            toolbar.Controls.Add(_btnRefresh);

            _grid = new DataGridView();
            Theme.StyleGrid(_grid);
            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            Controls.Add(_grid);
            Controls.Add(toolbar);
            Controls.Add(header);
            Reload();
        }

        private void Reload()
        {
            try
            {
                string actionName = (_txtAction.Text ?? string.Empty).Trim();
                string tableName = (_txtTable.Text ?? string.Empty).Trim();
                DateTime? fromDate = _dtpFrom.Checked ? (DateTime?)_dtpFrom.Value.Date : null;
                DateTime? toDate = _dtpTo.Checked ? (DateTime?)_dtpTo.Value.Date : null;
                _rows = AuditBLL.Search(
                    actionName.Length == 0 ? null : actionName,
                    tableName.Length == 0 ? null : tableName,
                    fromDate, toDate, 1000);
                _grid.Columns.Clear();
                _grid.Rows.Clear();

                AddColumn("ActionTime", "الوقت", 110);
                AddColumn("Username", "المستخدم", 90);
                AddColumn("ActionName", "العملية", 120);
                AddColumn("TableName", "الجدول", 90);
                AddColumn("RecordId", "السجل", 60);
                AddColumn("OldValue", "القيمة القديمة", 140);
                AddColumn("NewValue", "القيمة الجديدة", 140);
                AddColumn("MachineName", "الجهاز", 80);
                AddColumn("IpAddress", "IP", 70);

                foreach (AuditLog audit in _rows)
                {
                    int rowIndex = _grid.Rows.Add();
                    DataGridViewRow row = _grid.Rows[rowIndex];
                    row.Tag = audit;
                    row.Cells["ActionTime"].Value = audit.ActionTime == null ? "-" :
                        audit.ActionTime.Value.ToString("yyyy-MM-dd HH:mm");
                    row.Cells["Username"].Value = audit.Username;
                    row.Cells["ActionName"].Value = audit.ActionName;
                    row.Cells["TableName"].Value = audit.TableName;
                    row.Cells["RecordId"].Value = audit.RecordId;
                    row.Cells["OldValue"].Value = audit.OldValue;
                    row.Cells["NewValue"].Value = audit.NewValue;
                    row.Cells["MachineName"].Value = audit.MachineName;
                    row.Cells["IpAddress"].Value = audit.IpAddress;
                }
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmAuditLog.Reload");
                Theme.ShowError("حدث خطأ أثناء تحميل سجل العمليات.");
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
