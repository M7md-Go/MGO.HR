using System;
using System.Drawing;
using System.Windows.Forms;
using HR.BLL;
using HR.Helpers;

namespace HR.UI.UI
{
    /// <summary>
    /// Commercial main shell: six top menu tabs (RTL) instead of a side navigation,
    /// permission-filtered professional screens under each tab, open-window tab strip
    /// with per-tab close, MDI children always open maximized filling the parent,
    /// status bar, and background timers (auto backup / auto fingerprint download).
    /// Opens the Dashboard automatically at startup.
    /// </summary>
    public class FrmMain : Form
    {
        public bool UserLoggedOut { get; private set; }

        private TabControl _tabs;
        private bool _syncingTabs;
        private Timer _backupTimer;
        private ToolStripStatusLabel _statusAlerts;

        public FrmMain()
        {
            Theme.Apply(this, false); // MDI parent: Windows ignores mirrored layout here — explicit RTL containers used instead
            Text = Theme.AppTitle;
            IsMdiContainer = true;
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(1200, 720);
            WindowState = FormWindowState.Maximized;

            FormClosing += OnFormClosingUserCheck;
            MdiChildActivate += delegate { SyncTabs(); };

            BuildStatusBar();
            BuildTabs();
            BuildMenu();

            _backupTimer = new Timer();
            _backupTimer.Interval = 60000;
            _backupTimer.Tick += delegate
            {
                BackupBLL.RunAutoBackupIfDue();
                FingerprintBLL.RunAutoDownloadIfDue();
                RefreshAlertsCounter();
            };
            _backupTimer.Start();
        }

        protected override void OnShown(EventArgs args)
        {
            base.OnShown(args);
            OpenModule("dashboard");
        }

        // ---------------- menu item model ----------------

        /// <summary>One menu entry under a top tab; Perms = any-of keys (empty/null = always visible).</summary>
        private class NavItem
        {
            public string Id;
            public string Label;
            public string[] Perms;

            public NavItem(string id, string label, string[] perms)
            {
                Id = id;
                Label = label;
                Perms = perms;
            }
        }

        private static bool AnyPermitted(string[] keys)
        {
            if (keys == null || keys.Length == 0)
            {
                return true;
            }
            foreach (string key in keys)
            {
                if (AppContext.HasPermission(key))
                {
                    return true;
                }
            }
            return false;
        }

        // ---------------- top menu tabs ----------------

        private void BuildMenu()
        {
            MenuStrip menu = new MenuStrip();
            menu.BackColor = Theme.ToolbarColor;
            menu.RightToLeft = RightToLeft.Yes; // RTL menu bar (top-level; mirrored layout is not applicable here)

            // Tab 1: ملف — dashboard, backup, users, settings, logs, session.
            ToolStripMenuItem miFile = new ToolStripMenuItem("ملف");
            miFile.DropDownItems.Add(StoreAndMenu(new NavItem("dashboard", "لوحة التحكم", null), Keys.F5));
            miFile.DropDownItems.Add(StoreAndMenu(new NavItem("alerts", "مركز التنبيهات", null), Keys.F7));
            miFile.DropDownItems.Add(new ToolStripSeparator());
            miFile.DropDownItems.Add(StoreAndMenu(new NavItem("backup", "النسخ الاحتياطي والاستعادة",
                new string[] { "Backup.Execute" }), null));
            miFile.DropDownItems.Add(StoreAndMenu(new NavItem("users", "المستخدمون والصلاحيات",
                new string[] { "Users.Manage" }), null));
            miFile.DropDownItems.Add(StoreAndMenu(new NavItem("settings", "الإعدادات",
                new string[] { "Settings.Manage" }), null));
            miFile.DropDownItems.Add(new ToolStripSeparator());
            miFile.DropDownItems.Add(StoreAndMenu(new NavItem("logins", "سجل الدخول",
                new string[] { "Audit.View" }), null));
            miFile.DropDownItems.Add(StoreAndMenu(new NavItem("audit", "سجل العمليات",
                new string[] { "Audit.View" }), null));
            miFile.DropDownItems.Add(new ToolStripSeparator());

            ToolStripMenuItem miChangePassword = new ToolStripMenuItem("تغيير كلمة المرور");
            miChangePassword.Click += OnChangePasswordClick;
            miFile.DropDownItems.Add(miChangePassword);

            ToolStripMenuItem miLogout = new ToolStripMenuItem("تسجيل الخروج");
            miLogout.Click += OnLogoutClick;
            miFile.DropDownItems.Add(miLogout);

            ToolStripMenuItem miExit = new ToolStripMenuItem("إنهاء البرنامج");
            miExit.Click += delegate { Close(); };
            miFile.DropDownItems.Add(miExit);

            // Tab 2: الموظفين — full employee management.
            ToolStripMenuItem miEmployees = new ToolStripMenuItem("الموظفين");
            miEmployees.DropDownItems.Add(StoreAndMenu(new NavItem("employees", "إدارة الموظفين",
                new string[] { "Employees.Add", "Employees.Edit", "Employees.Archive", "Employees.Delete" }), null));
            miEmployees.DropDownItems.Add(StoreAndMenu(new NavItem("archive", "أرشيف الموظفين",
                new string[] { "Archive.Execute" }), null));
            miEmployees.DropDownItems.Add(new ToolStripSeparator());
            miEmployees.DropDownItems.Add(StoreAndMenu(new NavItem("org", "الهيكل التنظيمي",
                new string[] { "Org.Manage" }), null));
            miEmployees.DropDownItems.Add(StoreAndMenu(new NavItem("shifts", "الورديات",
                new string[] { "Org.Manage" }), null));
            miEmployees.DropDownItems.Add(new ToolStripSeparator());
            miEmployees.DropDownItems.Add(StoreAndMenu(new NavItem("emphistory", "سجل تاريخ الموظف",
                new string[] { "Employees.Add", "Employees.Edit", "Employees.Archive", "Employees.Delete" }), null));

            // Tab 3: الإجازات — requests and approvals management.
            ToolStripMenuItem miLeaves = new ToolStripMenuItem("الإجازات");
            miLeaves.DropDownItems.Add(StoreAndMenu(new NavItem("leaves", "طلبات الإجازات",
                new string[] { "Leaves.Approve", "Leave.Create" }), null));
            miLeaves.DropDownItems.Add(StoreAndMenu(new NavItem("longleaves", "الإجازات طويلة المدة",
                new string[] { "Leaves.Approve", "Leave.Create" }), null));
            miLeaves.DropDownItems.Add(new ToolStripSeparator());
            miLeaves.DropDownItems.Add(StoreAndMenu(new NavItem("missions", "المأموريات",
                new string[] { "Missions.Approve", "Mission.Create" }), null));
            miLeaves.DropDownItems.Add(StoreAndMenu(new NavItem("penalties", "الجزاءات",
                new string[] { "Penalties.Add" }), null));
            miLeaves.DropDownItems.Add(new ToolStripSeparator());
            miLeaves.DropDownItems.Add(StoreAndMenu(new NavItem("leavetypes", "أنواع الإجازات",
                new string[] { "Org.Manage" }), null));
            miLeaves.DropDownItems.Add(StoreAndMenu(new NavItem("longleavetypes", "أنواع الإجازات الطويلة",
                new string[] { "Org.Manage" }), null));

            // Tab 4: الجداول الشهرية.
            ToolStripMenuItem miSchedule = new ToolStripMenuItem("الجداول الشهرية");
            miSchedule.DropDownItems.Add(StoreAndMenu(new NavItem("monthly", "الجدول الشهري (توزيع الورديات على الموظفين)",
                new string[] { "Schedule.Edit", "Schedule.Approve", "Schedule.Unlock" }), Keys.F6));

            // Tab 5: الحضور والانصراف والإعدادات.
            ToolStripMenuItem miAttendance = new ToolStripMenuItem("الحضور والانصراف والإعدادات");
            miAttendance.DropDownItems.Add(StoreAndMenu(new NavItem("attengine", "محرك الحضور",
                new string[] { "Attendance.Edit" }), null));
            miAttendance.DropDownItems.Add(StoreAndMenu(new NavItem("devices", "الأجهزة والبصمة",
                new string[] { "Devices.Manage", "Fingerprint.Download", "Fingerprint.ReadUsers" }), null));
            miAttendance.DropDownItems.Add(StoreAndMenu(new NavItem("fplogs", "سجلات البصمات",
                new string[] { "Devices.Manage", "Fingerprint.Download", "Fingerprint.ReadUsers" }), null));
            miAttendance.DropDownItems.Add(new ToolStripSeparator());
            miAttendance.DropDownItems.Add(StoreAndMenu(new NavItem("holidays", "العطل الرسمية",
                new string[] { "Attendance.Edit", "Attendance.ApproveAdjust", "Reports.View" }), null));
            miAttendance.DropDownItems.Add(StoreAndMenu(new NavItem("overtime", "العمل الإضافي",
                new string[] { "Attendance.Edit", "Attendance.ApproveAdjust", "Reports.View" }), null));
            miAttendance.DropDownItems.Add(StoreAndMenu(new NavItem("adjustments", "تصحيحات الحضور اليدوية",
                new string[] { "Attendance.Edit", "Attendance.ApproveAdjust", "Reports.View" }), null));
            miAttendance.DropDownItems.Add(StoreAndMenu(new NavItem("attexceptions", "سجل استثناءات الحضور",
                new string[] { "Attendance.Edit", "Attendance.ApproveAdjust", "Reports.View" }), null));
            miAttendance.DropDownItems.Add(new ToolStripSeparator());
            miAttendance.DropDownItems.Add(StoreAndMenu(new NavItem("reports", "التقارير",
                new string[] { "Reports.View" }), null));

            // Tab 6: عن النظام والمطور.
            ToolStripMenuItem miAbout = new ToolStripMenuItem("عن النظام والمطور");
            ToolStripMenuItem miAboutSystem = new ToolStripMenuItem("عن النظام");
            miAboutSystem.Click += OnAboutClick;
            miAbout.DropDownItems.Add(miAboutSystem);
            ToolStripMenuItem miAboutDeveloper = new ToolStripMenuItem("المطور");
            miAboutDeveloper.Click += OnDeveloperClick;
            miAbout.DropDownItems.Add(miAboutDeveloper);

            menu.Items.Add(miFile);
            menu.Items.Add(miEmployees);
            menu.Items.Add(miLeaves);
            menu.Items.Add(miSchedule);
            menu.Items.Add(miAttendance);
            menu.Items.Add(miAbout);
            MainMenuStrip = menu;
            Controls.Add(menu);
        }

        /// <summary>Permission-gated menu entry that opens a module by id.</summary>
        private ToolStripMenuItem StoreAndMenu(NavItem item, Keys? shortcut)
        {
            ToolStripMenuItem menu = new ToolStripMenuItem(item.Label);
            if (shortcut.HasValue)
            {
                menu.ShortcutKeys = shortcut.Value;
            }
            if (AnyPermitted(item.Perms))
            {
                menu.Click += delegate { OpenModule(item.Id); };
            }
            else
            {
                menu.Enabled = false;
            }
            return menu;
        }

        // ---------------- open-window tab strip ----------------

        private void BuildTabs()
        {
            _tabs = new TabControl();
            _tabs.Dock = DockStyle.Top;
            _tabs.Height = 30;
            _tabs.Visible = false;
            _tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
            _tabs.SizeMode = TabSizeMode.Normal;
            _tabs.ItemSize = new Size(190, 28);
            _tabs.Padding = new Point(6, 3);
            _tabs.DrawItem += TabsDrawItem;
            _tabs.MouseClick += TabsMouseClick;
            _tabs.SelectedIndexChanged += delegate
            {
                if (_syncingTabs || _tabs.SelectedTab == null)
                {
                    return;
                }
                Form child = _tabs.SelectedTab.Tag as Form;
                if (child != null && !child.IsDisposed && child != ActiveMdiChild)
                {
                    child.Activate();
                }
            };
            Controls.Add(_tabs);
        }

        private void SyncTabs()
        {
            _syncingTabs = true;
            try
            {
                for (int i = _tabs.TabPages.Count - 1; i >= 0; i--)
                {
                    Form existing = _tabs.TabPages[i].Tag as Form;
                    if (existing == null || existing.IsDisposed || Array.IndexOf(MdiChildren, existing) < 0)
                    {
                        _tabs.TabPages.RemoveAt(i);
                    }
                }
                Form active = ActiveMdiChild;
                int selectedIndex = -1;
                foreach (Form child in MdiChildren)
                {
                    TabPage page = null;
                    foreach (TabPage candidate in _tabs.TabPages)
                    {
                        if (object.ReferenceEquals(candidate.Tag, child))
                        {
                            page = candidate;
                            break;
                        }
                    }
                    if (page == null)
                    {
                        page = new TabPage();
                        page.Tag = child;
                        page.Text = child.Text;
                        _tabs.TabPages.Add(page);
                    }
                    else if (!string.Equals(page.Text, child.Text, StringComparison.Ordinal))
                    {
                        page.Text = child.Text;
                    }
                    if (child == active)
                    {
                        selectedIndex = _tabs.TabPages.IndexOf(page);
                    }
                }
                if (_tabs.TabPages.Count == 0)
                {
                    _tabs.Visible = false;
                }
                else
                {
                    _tabs.Visible = true;
                    if (selectedIndex >= 0 && _tabs.SelectedIndex != selectedIndex)
                    {
                        _tabs.SelectedIndex = selectedIndex;
                    }
                }
            }
            finally
            {
                _syncingTabs = false;
            }
        }

        private void TabsDrawItem(object sender, DrawItemEventArgs e)
        {
            Rectangle bounds = e.Bounds;
            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            using (SolidBrush back = new SolidBrush(selected ? Theme.HeaderColor : Theme.ToolbarColor))
            {
                e.Graphics.FillRectangle(back, bounds);
            }
            Rectangle box = new Rectangle(bounds.Left + 5, bounds.Top + (bounds.Height - 13) / 2, 13, 13);
            using (Pen pen = new Pen(selected ? Color.White : Color.FromArgb(120, 120, 120), 1.5f))
            {
                e.Graphics.DrawLine(pen, box.Left + 3, box.Top + 3, box.Right - 3, box.Bottom - 3);
                e.Graphics.DrawLine(pen, box.Right - 3, box.Top + 3, box.Left + 3, box.Bottom - 3);
            }
            Rectangle textRect = new Rectangle(bounds.Left + 22, bounds.Top, bounds.Width - 26, bounds.Height);
            Color foreColor = selected ? Theme.HeaderTextColor : Color.FromArgb(55, 65, 80);
            using (SolidBrush fore = new SolidBrush(foreColor))
            {
                StringFormat format = new StringFormat();
                format.Alignment = StringAlignment.Far;
                format.LineAlignment = StringAlignment.Center;
                format.FormatFlags = StringFormatFlags.DirectionRightToLeft;
                format.Trimming = StringTrimming.EllipsisCharacter;
                e.Graphics.DrawString(_tabs.TabPages[e.Index].Text, Theme.Font(8.75f), fore, textRect, format);
            }
        }

        private void TabsMouseClick(object sender, MouseEventArgs e)
        {
            for (int i = 0; i < _tabs.TabPages.Count; i++)
            {
                Rectangle rect = _tabs.GetTabRect(i);
                Rectangle box = new Rectangle(rect.Left + 5, rect.Top + (rect.Height - 13) / 2, 13, 13);
                if (box.Contains(e.Location))
                {
                    Form child = _tabs.TabPages[i].Tag as Form;
                    if (child != null && !child.IsDisposed)
                    {
                        child.Close();
                    }
                    return;
                }
            }
        }

        // ---------------- module opening ----------------

        public void OpenModule(string id)
        {
            foreach (Form child in MdiChildren)
            {
                if (Equals(child.Tag, id))
                {
                    child.Activate();
                    return;
                }
            }
            Form form;
            switch (id)
            {
                case "dashboard": form = new FrmDashboard(); break;
                case "alerts": form = new FrmAlerts(); break;
                case "employees": form = new FrmEmployees(); break;
                case "org": form = new FrmLookupManager(); break;
                case "shifts": form = new FrmShifts(); break;
                case "monthly": form = new FrmMonthlySchedule(); break;
                case "attengine": form = new FrmAttendanceCalc(); break;
                case "devices": form = new FrmDevices(); break;
                case "fplogs": form = new FrmFingerprintLogs(); break;
                case "leaves": form = new FrmLeaves(); break;
                case "longleaves": form = new FrmLongLeaves(); break;
                case "missions": form = new FrmMissions(); break;
                case "penalties": form = new FrmPenalties(); break;
                case "reports": form = new FrmReports(); break;
                case "users": form = new FrmUsers(); break;
                case "audit": form = new FrmAuditLog(); break;
                case "logins": form = new FrmLoginHistory(); break;
                case "backup": form = new FrmBackupRestore(); break;
                case "archive": form = new FrmArchive(); break;
                case "settings": form = new FrmSettings(); break;
                case "leavetypes": form = new FrmLeaveTypes(); break;
                case "longleavetypes": form = new FrmLongLeaveTypes(); break;
                case "holidays": form = new FrmPublicHolidays(); break;
                case "overtime": form = new FrmOvertime(); break;
                case "adjustments": form = new FrmAdjustments(); break;
                case "attexceptions": form = new FrmAttendanceExceptions(); break;
                case "emphistory": form = new FrmEmployeeHistory(); break;
                default: return;
            }
            form.Tag = id;
            ShowForm(form);
        }

        private void ShowForm(Form form)
        {
            foreach (Form child in MdiChildren)
            {
                if (child.GetType() == form.GetType())
                {
                    form.Dispose();
                    child.Activate();
                    return;
                }
            }
            form.MdiParent = this;
            form.WindowState = FormWindowState.Maximized; // every screen opens filling the parent window
            form.FormClosed += delegate { SyncTabs(); RefreshAlertsCounter(); };
            form.Show();
            SyncTabs();
        }

        // ---------------- about ----------------

        private void OnAboutClick(object sender, EventArgs args)
        {
            using (FrmAbout dialog = new FrmAbout())
            {
                dialog.ShowDialog(this);
            }
        }

        private void OnDeveloperClick(object sender, EventArgs args)
        {
            try
            {
                System.Diagnostics.Process.Start("https://github.com/M7md-Go/MGO.HR");
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmMain.OnDeveloperClick");
                Theme.ShowError("لم يتم فتح الرابط. افتح المتصفح يدويًا: https://github.com/M7md-Go/MGO.HR");
            }
        }

        // ---------------- status bar ----------------

        private void BuildStatusBar()
        {
            StatusStrip status = new StatusStrip();
            ToolStripStatusLabel statusUser = new ToolStripStatusLabel();
            ToolStripStatusLabel statusDate = new ToolStripStatusLabel();
            statusUser.Text = "المستخدم: " + (AppContext.CurrentUser != null ?
                (AppContext.CurrentUser.FullName ?? AppContext.CurrentUser.Username) : "") +
                (AppContext.IsCurrentUserAdmin ? " (مسؤول النظام)" : "");
            statusDate.Text = "الجهاز: " + AppContext.MachineName;

            _statusAlerts = new ToolStripStatusLabel();
            _statusAlerts.Text = "تنبيهات: 0";
            _statusAlerts.ForeColor = Theme.StatusWarn;
            _statusAlerts.DoubleClickEnabled = true;
            _statusAlerts.Click += delegate { OpenModule("alerts"); };

            status.Items.Add(statusUser);
            status.Items.Add(new ToolStripStatusLabel("            "));
            status.Items.Add(statusDate);
            status.Items.Add(new ToolStripStatusLabel("            "));
            status.Items.Add(_statusAlerts);
            Controls.Add(status);
            RefreshAlertsCounter();
        }

        /// <summary>Refreshes the alerts count in the status bar (never throws).</summary>
        private void RefreshAlertsCounter()
        {
            if (_statusAlerts == null || IsDisposed)
            {
                return;
            }
            try
            {
                int count = AlertsBLL.Summary().Count;
                _statusAlerts.Text = "تنبيهات: " + count;
                _statusAlerts.ForeColor = count > 0 ? Theme.StatusWarn : SystemColors.ControlText;
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmMain.RefreshAlertsCounter");
            }
        }

        // ---------------- lifecycle ----------------

        private void OnLogoutClick(object sender, EventArgs args)
        {
            if (!Theme.Confirm("هل تريد تسجيل الخروج؟"))
            {
                return;
            }
            UserBLL.Logout();
            UserLoggedOut = true;
            Close();
        }

        private void OnChangePasswordClick(object sender, EventArgs args)
        {
            try
            {
                ShowChangePassword();
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmMain.OnChangePasswordClick");
                Theme.ShowError("حدث خطأ غير متوقع. تم حفظ التفاصيل في السجلات.");
            }
        }

        private void ShowChangePassword()
        {
            using (FrmChangePassword dialog = new FrmChangePassword())
            {
                dialog.ShowDialog(this);
            }
        }

        private void OnFormClosingUserCheck(object sender, FormClosingEventArgs args)
        {
            if (UserLoggedOut)
            {
                return;
            }
            if (!Theme.Confirm("هل تريد إنهاء البرنامج؟"))
            {
                args.Cancel = true;
            }
            else
            {
                if (AppContext.IsAuthenticated)
                {
                    UserBLL.Logout();
                }
            }
        }
    }
}
