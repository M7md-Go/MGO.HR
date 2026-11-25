using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using HR.BLL;
using HR.Entities;
using HR.Helpers;

namespace HR.UI.UI
{
    /// <summary>
    /// Backup / restore screen (rule 23, permission "Backup Execute" in
    /// BackupBLL). Top: destination folder + manual backup. Middle: the
    /// backup files in the folder with restore. Bottom: the BackupLogs
    /// history. Automatic backup settings live in System Settings
    /// (Backup.AutoEnabled / Backup.AutoTime — no hardcoded values).
    /// </summary>
    public class FrmBackupRestore : Form
    {
        private TextBox _txtFolder;
        private Button _btnBrowse;
        private Button _btnBackupNow;
        private DataGridView _gridFiles;
        private Button _btnRestore;
        private DataGridView _gridLog;

        public FrmBackupRestore()
        {
            Theme.Apply(this, false); // MDI child: mirrored layout is not applied — RTL flow comes from the containers
            Text = "النسخ الاحتياطي والاستعادة";
            ClientSize = new Size(980, 660);
            StartPosition = FormStartPosition.CenterParent;

            Panel header = new Panel();
            Label lblTitle = new Label();
            lblTitle.Text = "النسخ الاحتياطي لملف قاعدة البيانات (.mdb) — يدوي وتلقائي مع السجل والاستعادة";
            lblTitle.ForeColor = Theme.HeaderTextColor;
            lblTitle.Font = Theme.Font(12f);
            lblTitle.Dock = DockStyle.Fill;
            lblTitle.TextAlign = ContentAlignment.MiddleCenter;
            Theme.StyleHeader(header);
            header.Controls.Add(lblTitle);
            header.Dock = DockStyle.Top;

            FlowLayoutPanel backupBar = new FlowLayoutPanel();
            backupBar.Dock = DockStyle.Top;
            backupBar.Height = 52;
            backupBar.BackColor = Theme.ToolbarColor;
            // FlowLayoutPanel: RTL=Yes (inherited from the form) already reverses the flow;
            // an explicit RightToLeft cancelled that reversal and rendered the bar left-to-right.
            backupBar.FlowDirection = FlowDirection.LeftToRight;
            backupBar.WrapContents = false;
            backupBar.Padding = new Padding(6, 8, 6, 4);

            Label lblFolder = new Label();
            lblFolder.Text = "مجلد النسخ:";
            lblFolder.AutoSize = true;
            lblFolder.Margin = new Padding(3, 10, 3, 0);

            _txtFolder = new TextBox();
            _txtFolder.Width = 420;
            _txtFolder.Text = BackupBLL.DefaultBackupFolder();
            _txtFolder.Margin = new Padding(3, 8, 3, 0);

            _btnBrowse = new Button();
            _btnBrowse.Text = "استعراض";
            _btnBrowse.Height = 30;
            _btnBrowse.Click += delegate { OnBrowseClick(); };

            _btnBackupNow = new Button();
            _btnBackupNow.Text = "نسخ احتياطي الآن";
            _btnBackupNow.Height = 30;
            _btnBackupNow.Click += delegate { OnBackupNowClick(); };

            Button btnCompact = new Button();
            btnCompact.Text = "ضغط قاعدة البيانات";
            btnCompact.Height = 30;
            btnCompact.Click += delegate { OnCompactClick(); };

            Label lblDbSize = new Label();
            lblDbSize.AutoSize = true;
            lblDbSize.ForeColor = Color.FromArgb(60, 72, 90);
            lblDbSize.Margin = new Padding(12, 10, 12, 0);
            lblDbSize.Text = "حجم قاعدة البيانات: " + (BackupBLL.DatabaseSizeBytes() / 1024.0 / 1024.0).ToString("0.0") + " م.ب";

            backupBar.Controls.Add(lblFolder);
            backupBar.Controls.Add(_txtFolder);
            backupBar.Controls.Add(_btnBrowse);
            backupBar.Controls.Add(_btnBackupNow);
            backupBar.Controls.Add(btnCompact);
            backupBar.Controls.Add(lblDbSize);

            Label lblFiles = new Label();
            lblFiles.Text = "ملفات النسخ في المجلد — حدد ملفًا لاستعادته ( سيُنشأ قبل ذلك نسخ أمان تلقائي )";
            lblFiles.Dock = DockStyle.Top;
            lblFiles.Height = 24;
            lblFiles.TextAlign = ContentAlignment.MiddleLeft;

            _gridFiles = BuildGrid();
            BuildFileColumns();
            _gridFiles.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _gridFiles.MultiSelect = false;

            Button restoreHost = new Button();
            restoreHost.Text = "استعادة المحدد";
            restoreHost.Dock = DockStyle.Top;
            restoreHost.Height = 34;
            restoreHost.Click += delegate { OnRestoreClick(); };

            Panel filesPanel = new Panel();
            filesPanel.Dock = DockStyle.Fill;
            filesPanel.Controls.Add(_gridFiles);
            filesPanel.Controls.Add(restoreHost);
            filesPanel.Controls.Add(lblFiles);

            Label lblLog = new Label();
            lblLog.Text = "سجل عمليات النسخ والاستعادة (BackupLogs)";
            lblLog.Dock = DockStyle.Top;
            lblLog.Height = 24;
            lblLog.TextAlign = ContentAlignment.MiddleLeft;

            _gridLog = BuildGrid();
            BuildLogColumns();
            Panel logPanel = new Panel();
            logPanel.Dock = DockStyle.Bottom;
            logPanel.Height = 240;
            logPanel.Controls.Add(_gridLog);
            logPanel.Controls.Add(lblLog);

            Controls.Add(filesPanel);
            Controls.Add(logPanel);
            Controls.Add(backupBar);
            Controls.Add(header);

            bool canRun = AppContext.HasAnyPermission(new string[] { "Backup.Execute", "Backup.Restore" });
            bool canRestore = AppContext.HasAnyPermission(new string[] { "Backup.Restore", "Backup.Execute" });
            _btnBackupNow.Enabled = AppContext.HasPermission("Backup.Execute");
            btnCompact.Enabled = AppContext.HasPermission("Backup.Execute");
            RegisterRestoreButton(restoreHost, canRestore);
            LoadFiles();
            LoadLog();
        }

        private void RegisterRestoreButton(Button restoreHost, bool canRun)
        {
            restoreHost.Enabled = canRun;
            _btnRestore = restoreHost;
        }

        private void OnBrowseClick()
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "اختر مجلد النسخ الاحتياطي";
                dialog.SelectedPath = _txtFolder.Text;
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    _txtFolder.Text = dialog.SelectedPath;
                    LoadFiles();
                }
            }
        }

        private void OnBackupNowClick()
        {
            if (!Theme.Confirm("إنشاء نسخة احتياطية من قاعدة البيانات الآن في:\n" + _txtFolder.Text + "؟"))
            {
                return;
            }
            try
            {
                string path = BackupBLL.Backup(_txtFolder.Text, BackupBLL.TypeManual);
                Theme.ShowInfo("تم إنشاء النسخة الاحتياطية:\n" + path);
                LoadFiles();
                LoadLog();
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmBackupRestore.OnBackupNowClick");
                Theme.ShowError("حدث خطأ أثناء النسخ الاحتياطي.");
            }
        }

        private string SelectedFile()
        {
            if (_gridFiles.CurrentRow == null)
            {
                return null;
            }
            return _gridFiles.CurrentRow.Tag as string;
        }

        private void OnRestoreClick()
        {
            string filePath = SelectedFile();
            if (filePath == null)
            {
                Theme.ShowInfo("اختر ملف نسخة احتياطية من القائمة أولاً.");
                return;
            }
            if (!Theme.Confirm("تحذير: الاستعادة ستستبدل قاعدة البيانات الحالية بالكامل بـ:\n" + filePath +
                "\n\nسيتم حذف أي بيانات أُضيفت بعد لحظة إنشاء هذه النسخة.\nتأكد من إغلاق البرنامج على بقية الأجهزة قبل المتابعة.\n\nهل تريد المتابعة؟"))
            {
                return;
            }
            try
            {
                string safetyPath = BackupBLL.Restore(filePath);
                Theme.ShowInfo("تمت الاستعادة بنجاح.\nنسخة أمان من الحالة قبل الاستعادة:\n" + safetyPath +
                    "\n\nيُنصح بإعادة تشغيل البرنامج على جميع الأجهزة.");
                LoadFiles();
                LoadLog();
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmBackupRestore.OnRestoreClick");
                Theme.ShowError("حدث خطأ أثناء الاستعادة.");
            }
        }

        private void LoadFiles()
        {
            try
            {
                List<FileInfo> files = BackupBLL.ListBackupFiles(_txtFolder.Text);
                _gridFiles.Rows.Clear();
                foreach (FileInfo file in files)
                {
                    int rowIndex = _gridFiles.Rows.Add();
                    DataGridViewRow row = _gridFiles.Rows[rowIndex];
                    row.Tag = file.FullName;
                    row.Cells["FileName"].Value = file.Name;
                    row.Cells["FileSize"].Value = (file.Length / 1024).ToString("N0") + " KB";
                    row.Cells["Modified"].Value = file.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss");
                }
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmBackupRestore.LoadFiles");
                Theme.ShowError("حدث خطأ أثناء قراءة مجلد النسخ.");
            }
        }

        private void LoadLog()
        {
            try
            {
                List<BackupLog> rows = BackupBLL.GetLog(200);
                _gridLog.Rows.Clear();
                foreach (BackupLog backup in rows)
                {
                    int rowIndex = _gridLog.Rows.Add();
                    DataGridViewRow row = _gridLog.Rows[rowIndex];
                    row.Cells["BackupType"].Value = backup.BackupType;
                    row.Cells["FilePath"].Value = backup.FilePath;
                    row.Cells["FileSize"].Value = backup.FileSize == null ? "-" :
                        ((backup.FileSize.Value / 1024).ToString("N0") + " KB");
                    row.Cells["CreatedAt"].Value = backup.CreatedAt == null ? "-" :
                        backup.CreatedAt.Value.ToString("yyyy-MM-dd HH:mm:ss");
                    row.Cells["Status"].Value = backup.Status;
                    row.Cells["RestorePath"].Value = backup.RestorePath;
                }
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmBackupRestore.LoadLog");
                Theme.ShowError("حدث خطأ أثناء تحميل سجل النسخ.");
            }
        }

        private DataGridView BuildGrid()
        {
            DataGridView grid = new DataGridView();
            Theme.StyleGrid(grid);
            grid.Dock = DockStyle.Fill;
            grid.ReadOnly = true;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            return grid;
        }

        private void BuildFileColumns()
        {
            AddColumn(_gridFiles, "FileName", "الملف", 260);
            AddColumn(_gridFiles, "FileSize", "الحجم", 80);
            AddColumn(_gridFiles, "Modified", "آخر تعديل", 130);
        }

        private void BuildLogColumns()
        {
            AddColumn(_gridLog, "BackupType", "النوع", 70);
            AddColumn(_gridLog, "FilePath", "المسار", 250);
            AddColumn(_gridLog, "FileSize", "الحجم", 70);
            AddColumn(_gridLog, "CreatedAt", "الوقت", 110);
            AddColumn(_gridLog, "Status", "الحالة", 70);
            AddColumn(_gridLog, "RestorePath", "استُعيد من", 150);
        }

        private void AddColumn(DataGridView grid, string name, string header, int fillWeight)
        {
            DataGridViewTextBoxColumn column = new DataGridViewTextBoxColumn();
            column.Name = name;
            column.HeaderText = header;
            column.FillWeight = fillWeight;
            column.SortMode = DataGridViewColumnSortMode.NotSortable;
            grid.Columns.Add(column);
        }

        /// <summary>Compacts the Access database (defrags pages, reclaims deleted space).</summary>
        private void OnCompactClick()
        {
            if (!Theme.Confirm("سيتم فتح قاعدة البيانات وإعادة بنائها ملفًا جديدًا. يجب إغلاق النوافذ والاتصالات الأخرى." +
                "\n\nهل تريد تنفيذ الضغط الآن؟"))
            {
                return;
            }
            try
            {
                BackupBLL.CompactDatabase();
                Theme.ShowInfo("تم ضغط قاعدة البيانات بنجاح.");
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmBackupRestore.OnCompactClick");
                Theme.ShowError("حدث خطأ أثناء الضغط. تم حفظ التفاصيل في السجلات.");
            }
        }
    }
}
