using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using HR.BLL;
using HR.Helpers;

namespace HR.UI.UI
{
    /// <summary>
    /// Database monitoring screen (tab "إدارة قاعدة البيانات", permission
    /// "Settings.Manage"): live facts about the MDB file, connection state,
    /// backup status and the slow-query log measured by the DAL layer.
    /// Everything here is read-only by design.
    /// </summary>
    public class FrmDbInfo : Form
    {
        private DataGridView _gridFacts;
        private DataGridView _gridSlow;
        private List<DatabaseBLL.SlowQueryView> _slow;

        public FrmDbInfo()
        {
            Theme.Apply(this, false); // MDI child: mirrored layout is not applied — RTL flow comes from the containers
            Text = "مراقبة قاعدة البيانات";
            ClientSize = new Size(1000, 600);
            StartPosition = FormStartPosition.CenterParent;

            Panel header = new Panel();
            Label lblTitle = new Label();
            lblTitle.Text = "معلومات ومراقبة قاعدة البيانات — قراءة فقط (لا تعديل هيكل من هذا القسم)";
            lblTitle.ForeColor = Theme.HeaderTextColor;
            lblTitle.Font = Theme.Font(12f);
            lblTitle.Dock = DockStyle.Fill;
            lblTitle.TextAlign = ContentAlignment.MiddleCenter;
            Theme.StyleHeader(header);
            header.Controls.Add(lblTitle);
            header.Dock = DockStyle.Top;

            SplitContainer split = new SplitContainer();
            split.Dock = DockStyle.Fill;
            split.Orientation = Orientation.Vertical;
            split.SplitterDistance = 300;

            Panel factsHost = new Panel();
            factsHost.Dock = DockStyle.Fill;

            FlowLayoutPanel factsBar = new FlowLayoutPanel();
            factsBar.Dock = DockStyle.Top;
            factsBar.Height = 46;
            factsBar.BackColor = Theme.ToolbarColor;
            // FlowLayoutPanel: RTL=Yes (inherited from the form) already reverses the flow;
            // an explicit RightToLeft cancelled that reversal and rendered the bar left-to-right.
            factsBar.FlowDirection = FlowDirection.LeftToRight;
            factsBar.WrapContents = false;
            factsBar.Padding = new Padding(6, 6, 6, 2);

            Button btnRefresh = new Button();
            btnRefresh.Text = "تحديث";
            btnRefresh.Height = 30;
            btnRefresh.Click += delegate { Reload(); };

            Button btnTest = new Button();
            btnTest.Text = "اختبار الاتصال";
            btnTest.Height = 30;
            btnTest.Click += delegate { OnTestClick(); };

            factsBar.Controls.Add(btnRefresh);
            factsBar.Controls.Add(btnTest);

            _gridFacts = new DataGridView();
            Theme.StyleGrid(_gridFacts);
            _gridFacts.Dock = DockStyle.Fill;
            _gridFacts.ReadOnly = true;
            _gridFacts.AllowUserToAddRows = false;
            _gridFacts.AllowUserToDeleteRows = false;
            _gridFacts.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            factsHost.Controls.Add(_gridFacts);
            factsHost.Controls.Add(factsBar);
            split.Panel1.Controls.Add(factsHost);

            Panel slowHost = new Panel();
            slowHost.Dock = DockStyle.Fill;

            FlowLayoutPanel slowBar = new FlowLayoutPanel();
            slowBar.Dock = DockStyle.Top;
            slowBar.Height = 46;
            slowBar.BackColor = Theme.ToolbarColor;
            // FlowLayoutPanel: same flow note as above.
            slowBar.FlowDirection = FlowDirection.LeftToRight;
            slowBar.WrapContents = false;
            slowBar.Padding = new Padding(6, 6, 6, 2);

            Label lblSlow = new Label();
            lblSlow.Text = "أبطأ استعلامات مُقاسة (العتبة عبر الإعداد Performance.SlowQueryMs، افتراضي 500ms):";
            lblSlow.AutoSize = true;
            lblSlow.Margin = new Padding(3, 10, 3, 0);

            Button btnClearSlow = new Button();
            btnClearSlow.Text = "مسح السجل";
            btnClearSlow.Height = 30;
            btnClearSlow.Click += delegate { OnClearSlowClick(); };

            slowBar.Controls.Add(lblSlow);
            slowBar.Controls.Add(btnClearSlow);

            _gridSlow = new DataGridView();
            Theme.StyleGrid(_gridSlow);
            _gridSlow.Dock = DockStyle.Fill;
            _gridSlow.ReadOnly = true;
            _gridSlow.AllowUserToAddRows = false;
            _gridSlow.AllowUserToDeleteRows = false;
            _gridSlow.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            slowHost.Controls.Add(_gridSlow);
            slowHost.Controls.Add(slowBar);
            split.Panel2.Controls.Add(slowHost);

            Controls.Add(split);
            Controls.Add(header);

            Reload();
        }

        private void Reload()
        {
            try
            {
                DatabaseBLL.DatabaseInfo info = DatabaseBLL.GetInfo();
                _gridFacts.Columns.Clear();
                _gridFacts.Rows.Clear();

                AddFactColumn();
                AddFact("مسار قاعدة البيانات", info.Path);
                AddFact("الحجم", FormatSize(info.SizeBytes));
                AddFact("عدد الجداول", info.TableCount.ToString());
                AddFact("حالة الاتصال", string.IsNullOrEmpty(info.ConnectionError) ?
                    "متصلة ✔" : "خطأ: " + info.ConnectionError);
                AddFact("مجلد النسخ الاحتياطي", info.BackupFolder);
                AddFact("آخر نسخة يدوية", info.LastManualBackup == null ? "لا توجد" :
                    info.LastManualBackup.Value.ToString("yyyy-MM-dd HH:mm"));
                AddFact("آخر نسخة تلقائية", info.LastAutoBackup == null ? "لا توجد" :
                    info.LastAutoBackup.Value.ToString("yyyy-MM-dd HH:mm"));
                AddFact("مدة الاحتفاظ بالنسخ (يوم)", info.RetentionDays <= 0 ?
                    "بدون حذف (دائم)" : info.RetentionDays.ToString());
                AddFact("عتبة الاستعلام البطيء (ms)", info.SlowQueryThresholdMs.ToString());

                _slow = DatabaseBLL.SlowQueries();
                _gridSlow.Columns.Clear();
                _gridSlow.Rows.Clear();
                AddSlowColumn("At", "الوقت", 140);
                AddSlowColumn("Elapsed", "الزمن (ms)", 90);
                AddSlowColumn("Sql", "الاستعلام", 700);
                foreach (DatabaseBLL.SlowQueryView query in _slow)
                {
                    int rowIndex = _gridSlow.Rows.Add();
                    DataGridViewRow row = _gridSlow.Rows[rowIndex];
                    row.Cells["At"].Value = query.At.ToString("yyyy-MM-dd HH:mm:ss");
                    row.Cells["Elapsed"].Value = query.ElapsedMs.ToString();
                    row.Cells["Sql"].Value = query.Sql;
                }
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmDbInfo.Reload");
                Theme.ShowError("حدث خطأ أثناء قراءة معلومات قاعدة البيانات.");
            }
        }

        private void OnTestClick()
        {
            try
            {
                string error = DatabaseBLL.TestConnection();
                if (error == null)
                {
                    Theme.ShowInfo("الاتصال بقاعدة البيانات سليم ✔");
                }
                else
                {
                    Theme.ShowError("فشل الاتصال: " + error);
                }
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmDbInfo.OnTestClick");
                Theme.ShowError("حدث خطأ أثناء اختبار الاتصال.");
            }
        }

        private void OnClearSlowClick()
        {
            try
            {
                DatabaseBLL.ClearSlowLog();
                Reload();
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmDbInfo.OnClearSlowClick");
                Theme.ShowError("حدث خطأ أثناء مسح السجل.");
            }
        }

        private static string FormatSize(long bytes)
        {
            if (bytes >= 1048576)
            {
                return (bytes / 1048576d).ToString("0.##") + " MB";
            }
            if (bytes >= 1024)
            {
                return (bytes / 1024d).ToString("0.##") + " KB";
            }
            return bytes + " بايت";
        }

        private void AddFactColumn()
        {
            DataGridViewTextBoxColumn key = new DataGridViewTextBoxColumn();
            key.Name = "Key";
            key.HeaderText = "البند";
            key.FillWeight = 220;
            key.SortMode = DataGridViewColumnSortMode.NotSortable;
            _gridFacts.Columns.Add(key);
            DataGridViewTextBoxColumn value = new DataGridViewTextBoxColumn();
            value.Name = "Value";
            value.HeaderText = "القيمة";
            value.FillWeight = 700;
            value.SortMode = DataGridViewColumnSortMode.NotSortable;
            _gridFacts.Columns.Add(value);
        }

        private void AddFact(string key, string value)
        {
            int rowIndex = _gridFacts.Rows.Add();
            _gridFacts.Rows[rowIndex].Cells["Key"].Value = key;
            _gridFacts.Rows[rowIndex].Cells["Value"].Value = value;
        }

        private void AddSlowColumn(string name, string header, int fillWeight)
        {
            DataGridViewTextBoxColumn column = new DataGridViewTextBoxColumn();
            column.Name = name;
            column.HeaderText = header;
            column.FillWeight = fillWeight;
            column.SortMode = DataGridViewColumnSortMode.NotSortable;
            _gridSlow.Columns.Add(column);
        }
    }
}
