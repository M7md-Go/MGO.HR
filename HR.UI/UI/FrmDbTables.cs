using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using HR.BLL;
using HR.Helpers;

namespace HR.UI.UI
{
    /// <summary>
    /// User-tables browser (DB tab, "Settings.Manage"): lists every user
    /// table with its live row count, opens the read-only structure dialog or
    /// the data browser, and exports table data to Excel. Editing happens
    /// only through each table's own dedicated management screen — never raw.
    /// </summary>
    public class FrmDbTables : Form
    {
        private DataGridView _grid;
        private List<string> _tables;

        public FrmDbTables()
        {
            Theme.Apply(this, false); // MDI child: mirrored layout is not applied — RTL flow comes from the containers
            Text = "استعراض جداول قاعدة البيانات";
            ClientSize = new Size(820, 580);
            StartPosition = FormStartPosition.CenterParent;

            Panel header = new Panel();
            Label lblTitle = new Label();
            lblTitle.Text = "جداول النظام — الهيكل والبيانات (قراءة فقط، التعديل من نوافذ الإدارة المخصصة)";
            lblTitle.ForeColor = Theme.HeaderTextColor;
            lblTitle.Font = Theme.Font(12f);
            lblTitle.Dock = DockStyle.Fill;
            lblTitle.TextAlign = ContentAlignment.MiddleCenter;
            Theme.StyleHeader(header);
            header.Controls.Add(lblTitle);
            header.Dock = DockStyle.Top;

            FlowLayoutPanel toolbar = new FlowLayoutPanel();
            toolbar.Dock = DockStyle.Top;
            toolbar.Height = 60;
            toolbar.BackColor = Theme.ToolbarColor;
            // FlowLayoutPanel: RTL=Yes (inherited from the form) already reverses the flow;
            // an explicit RightToLeft cancelled that reversal and rendered the bar left-to-right.
            toolbar.FlowDirection = FlowDirection.LeftToRight;
            toolbar.WrapContents = false;
            toolbar.Padding = new Padding(6, 6, 6, 2);

            Button btnRefresh = new Button();
            btnRefresh.Text = "تحديث";
            btnRefresh.Height = 30;
            btnRefresh.Click += delegate { Reload(); };

            Button btnStructure = new Button();
            btnStructure.Text = "هيكل الجدول";
            btnStructure.Height = 30;
            btnStructure.Click += delegate { OnStructureClick(); };

            Button btnData = new Button();
            btnData.Text = "استعراض البيانات";
            btnData.Height = 30;
            btnData.Click += delegate { OnDataClick(); };

            toolbar.Controls.Add(btnRefresh);
            toolbar.Controls.Add(btnStructure);
            toolbar.Controls.Add(btnData);

            _grid = new DataGridView();
            Theme.StyleGrid(_grid);
            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _grid.CellDoubleClick += delegate { OnDataClick(); };

            ContextMenuStrip gridMenu = new ContextMenuStrip();
            gridMenu.RightToLeft = RightToLeft.Yes;
            gridMenu.Font = Theme.Font(9.5f);
            ToolStripMenuItem miStructure = new ToolStripMenuItem("هيكل الجدول");
            miStructure.Click += delegate { OnStructureClick(); };
            ToolStripMenuItem miData = new ToolStripMenuItem("استعراض البيانات");
            miData.Click += delegate { OnDataClick(); };
            gridMenu.Items.Add(miStructure);
            gridMenu.Items.Add(miData);
            _grid.ContextMenuStrip = gridMenu;

            Controls.Add(_grid);
            Controls.Add(toolbar);
            Controls.Add(header);

            Reload();
        }

        private string SelectedTable()
        {
            if (_grid.CurrentRow == null)
            {
                return null;
            }
            return _grid.CurrentRow.Cells["TableName"].Value as string;
        }

        private void Reload()
        {
            try
            {
                _tables = DatabaseBLL.ListTables();
                _grid.Columns.Clear();
                _grid.Rows.Clear();

                DataGridViewTextBoxColumn name = new DataGridViewTextBoxColumn();
                name.Name = "TableName";
                name.HeaderText = "الجدول";
                name.FillWeight = 260;
                name.SortMode = DataGridViewColumnSortMode.NotSortable;
                _grid.Columns.Add(name);
                DataGridViewTextBoxColumn rows = new DataGridViewTextBoxColumn();
                rows.Name = "Rows";
                rows.HeaderText = "عدد السجلات";
                rows.FillWeight = 120;
                rows.SortMode = DataGridViewColumnSortMode.NotSortable;
                _grid.Columns.Add(rows);
                DataGridViewTextBoxColumn purpose = new DataGridViewTextBoxColumn();
                purpose.Name = "Purpose";
                purpose.HeaderText = "المورد الرئيسي";
                purpose.FillWeight = 420;
                purpose.SortMode = DataGridViewColumnSortMode.NotSortable;
                _grid.Columns.Add(purpose);

                foreach (string table in _tables)
                {
                    int rowIndex = _grid.Rows.Add();
                    DataGridViewRow row = _grid.Rows[rowIndex];
                    row.Cells["TableName"].Value = table;
                    row.Cells["Purpose"].Value = DescribeTable(table);
                    try
                    {
                        row.Cells["Rows"].Value = DatabaseBLL.CountRows(table).ToString();
                    }
                    catch (Exception countFailure)
                    {
                        Logger.LogError(countFailure, "FrmDbTables.CountRows: " + table);
                        row.Cells["Rows"].Value = "—";
                    }
                }
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmDbTables.Reload");
                Theme.ShowError("حدث خطأ أثناء تعداد جداول قاعدة البيانات.");
            }
        }

        private void OnStructureClick()
        {
            string tableName = SelectedTable();
            if (string.IsNullOrEmpty(tableName))
            {
                Theme.ShowInfo("اختر جدولاً من القائمة أولاً.");
                return;
            }
            FrmDbStructure dialog = new FrmDbStructure(tableName);
            dialog.ShowDialog(this);
        }

        private void OnDataClick()
        {
            string tableName = SelectedTable();
            if (string.IsNullOrEmpty(tableName))
            {
                Theme.ShowInfo("اختر جدولاً من القائمة أولاً.");
                return;
            }
            FrmDbTableData dialog = new FrmDbTableData(tableName);
            dialog.ShowDialog(this);
        }

        /// <summary>One-line purpose hint so the tables list is meaningful in Arabic.</summary>
        private static string DescribeTable(string table)
        {
            switch (table)
            {
                case "Employees": return "ملفات الموظفين (الجداول المركزية)";
                case "Attendance": return "سجل الحضور والانصراف النهائي";
                case "AttendanceAdjustments": return "تصحيحات الحضور اليدوية";
                case "AttendanceExceptions": return "استثناءات محرك الحضور";
                case "AuditLogs": return "سجل العمليات";
                case "BackupLogs": return "سجل النسخ الاحتياطي";
                case "Branches": return "الفروع";
                case "Companies": return "الشركة";
                case "Departments": return "الأقسام";
                case "Sections": return "الأقسام الفرعية";
                case "Devices": return "أجهزة البصمة";
                case "EmployeeFiles": return "مرفقات ودفاتر الموظف";
                case "EmployeeHistory": return "تاريخ تغييرات الموظف";
                case "EmployeeStatus": return "حالات الموظف (مرجعي)";
                case "FingerprintLogs": return "السجلات الخام للبصمات";
                case "JobTitles": return "المسميات الوظيفية";
                case "ActualJobs": return "الوظائف الفعلية";
                case "Qualifications": return "المؤهلات";
                case "LeaveBalances": return "أرصدة الإجازات";
                case "LeaveRequests": return "طلبات الإجازات";
                case "LeaveTypes": return "أنواع الإجازات";
                case "LongLeaves": return "الإجازات الطويلة";
                case "LongLeaveTypes": return "أنواع الإجازات الطويلة";
                case "LoginHistory": return "سجل تسجيل الدخول";
                case "Missions": return "المأموريات";
                case "MonthlySchedules": return "الجداول الشهرية";
                case "MonthlyShiftLocks": return "أقفال الأشهر المعتمدة";
                case "Overtime": return "العمل الإضافي";
                case "Penalties": return "الجزاءات";
                case "Permissions": return "كتالوج الصلاحيات";
                case "PublicHolidays": return "العطل الرسمية";
                case "RolePermissions": return "منح الصلاحيات للأدوار";
                case "Roles": return "أدوار المستخدمين";
                case "ScheduleDetails": return "تفاصيل توزيع الورديات الشهرية";
                case "Shifts": return "الورديات";
                case "UserPermissions": return "تجاوزات صلاحيات المستخدمين";
                case "Users": return "حسابات المستخدمين";
                case "AppSettings": return "إعدادات النظام المركزية";
                case "ArchiveLogs": return "سجل عمليات الأرشفة";
                default: return "";
            }
        }
    }
}
