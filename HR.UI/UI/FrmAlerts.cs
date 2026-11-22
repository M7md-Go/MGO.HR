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
    /// Alerts center: a single grouped view of everything that needs a decision —
    /// pending leave requests, pending missions, stale fingerprint devices.
    /// Double-clicking an alert opens its owning module (permission re-checked there).
    /// </summary>
    public class FrmAlerts : Form
    {
        private DataGridView _grid;
        private List<AlertItem> _rows;

        public FrmAlerts()
        {
            Theme.Apply(this, false); // MDI child: mirrored layout is not applied — RTL flow comes from the containers
            Text = "مركز التنبيهات";
            ClientSize = new Size(860, 520);
            StartPosition = FormStartPosition.CenterParent;

            Panel header = new Panel();
            Label lblTitle = new Label();
            lblTitle.Text = "مركز التنبيهات — المتطلبات بانتظار الإجراء";
            lblTitle.ForeColor = Theme.HeaderTextColor;
            lblTitle.Font = Theme.Font(12f);
            lblTitle.Dock = DockStyle.Fill;
            lblTitle.TextAlign = ContentAlignment.MiddleCenter;
            Theme.StyleHeader(header);
            header.Controls.Add(lblTitle);
            header.Dock = DockStyle.Top;

            FlowLayoutPanel toolbar = new FlowLayoutPanel();
            toolbar.Dock = DockStyle.Top;
            toolbar.Height = 56;
            toolbar.BackColor = Theme.ToolbarColor;
            // FlowLayoutPanel: RTL=Yes (inherited from the form) already reverses the flow;
            // an explicit RightToLeft cancelled that reversal and rendered the bar left-to-right.
            toolbar.FlowDirection = FlowDirection.LeftToRight;
            toolbar.WrapContents = false;
            toolbar.Padding = new Padding(6, 10, 6, 4);

            Button btnRefresh = new Button();
            btnRefresh.Text = "تحديث";
            btnRefresh.Height = 30;
            btnRefresh.Width = 90;
            btnRefresh.Click += delegate { Reload(); };
            toolbar.Controls.Add(btnRefresh);

            _grid = new DataGridView();
            Theme.StyleGrid(_grid);
            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = true;
            _grid.ColumnHeadersVisible = true;
            _grid.AutoGenerateColumns = false;

            DataGridViewTextBoxColumn colCategory = new DataGridViewTextBoxColumn();
            colCategory.Name = "Category";
            colCategory.HeaderText = "الفئة";
            colCategory.FillWeight = 20;
            colCategory.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            DataGridViewTextBoxColumn colMessage = new DataGridViewTextBoxColumn();
            colMessage.Name = "Message";
            colMessage.HeaderText = "التنبيه";
            colMessage.FillWeight = 60;
            colMessage.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            DataGridViewTextBoxColumn colSeverity = new DataGridViewTextBoxColumn();
            colSeverity.Name = "Severity";
            colSeverity.HeaderText = "الأهمية";
            colSeverity.FillWeight = 20;
            colSeverity.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _grid.Columns.AddRange(new DataGridViewColumn[] { colCategory, colMessage, colSeverity });

            // severity row tint (pill-style row coloring)
            _grid.CellFormatting += delegate(object sender, DataGridViewCellFormattingEventArgs args)
            {
                if (args.RowIndex < 0 || _rows == null || args.RowIndex >= _rows.Count)
                {
                    return;
                }
                if (string.Equals(_rows[args.RowIndex].Severity, "bad", StringComparison.Ordinal))
                {
                    args.CellStyle.BackColor = Color.FromArgb(250, 235, 235);
                    args.CellStyle.ForeColor = Theme.StatusBad;
                }
                else
                {
                    args.CellStyle.BackColor = Color.FromArgb(253, 246, 230);
                    args.CellStyle.ForeColor = Theme.StatusWarn;
                }
            };
            _grid.CellDoubleClick += delegate { OpenAlertModule(); };

            Controls.Add(_grid);
            Controls.Add(toolbar);
            Controls.Add(header);

            Shown += delegate { Reload(); };
        }

        private void Reload()
        {
            _rows = AlertsBLL.Collect();
            if (IsDisposed || _grid == null || _grid.IsDisposed)
            {
                return;
            }
            _grid.Rows.Clear();
            foreach (AlertItem item in _rows)
            {
                _grid.Rows.Add(item.Category, item.Message,
                    string.Equals(item.Severity, "bad", StringComparison.Ordinal) ? "عاجل" : "بحاجة لمتابعة");
            }
        }

        private void OpenAlertModule()
        {
            if (_grid.CurrentRow == null || _rows == null ||
                _grid.CurrentRow.Index >= _rows.Count)
            {
                return;
            }
            string moduleId = _rows[_grid.CurrentRow.Index].ModuleId;
            if (string.IsNullOrEmpty(moduleId))
            {
                return;
            }
            FrmMain main = MdiParent as FrmMain;
            if (main != null)
            {
                main.OpenModule(moduleId);
            }
        }
    }
}
