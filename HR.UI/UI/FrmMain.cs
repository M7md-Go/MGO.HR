using System;
using System.Drawing;
using System.Windows.Forms;
using HR.BLL;
using HR.Helpers;

namespace HR.UI.UI
{
    /// <summary>
    /// Main shell: permission-aware side navigation, open-window tab strip
    /// (with per-tab close), Arabic menu bar, status bar, MDI container.
    /// Opens the Dashboard automatically at startup.
    /// </summary>
    public class FrmMain : Form
    {
        public bool UserLoggedOut { get; private set; }

        private TabControl _tabs;
        private bool _syncingTabs;
        private Timer _backupTimer;

        public FrmMain()
        {
            Theme.Apply(this);
            Text = Theme.AppTitle;
            IsMdiContainer = true;
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(1200, 720);
            WindowState = FormWindowState.Maximized;

            FormClosing += OnFormClosingUserCheck;
            MdiChildActivate += delegate { SyncTabs(); };

            BuildStatusBar();
            BuildNav();
            BuildTabs();
            BuildMenu();

            _backupTimer = new Timer();
            _backupTimer.Interval = 60000;
            _backupTimer.Tick += delegate { BackupBLL.RunAutoBackupIfDue(); };
            _backupTimer.Start();
        }

        protected override void OnShown(EventArgs args)
        {
            base.OnShown(args);
            OpenModule("dashboard");
        }

        // ---------------- navigation items ----------------

        /// <summary>One side-nav entry; Perms = any-of keys (empty/null = always visible).</summary>
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

        private void BuildNav()
        {
            Panel nav = new Panel();
            nav.Dock = DockStyle.Left;
            nav.Width = 178;
            nav.BackColor = Color.FromArgb(225, 231, 240);

            FlowLayoutPanel list = new FlowLayoutPanel();
            list.Dock = DockStyle.Fill;
            list.FlowDirection = FlowDirection.TopDown;
            list.WrapContents = false;
            list.AutoScroll = true;
            list.BackColor = nav.BackColor;

            Label brand = new Label();
            brand.Text = "Modern Time";
            brand.Width = 166;
            brand.Height = 36;
            brand.TextAlign = ContentAlignment.MiddleCenter;
            brand.ForeColor = Theme.HeaderColor;
            brand.Font = Theme.Font(11f);
            list.Controls.Add(MarginWrap(brand));

            AddNavSection(list, "الرئيسية");
            AddNavItem(list, new NavItem("dashboard", "لوحة التحكم", null));

            AddNavSection(list, "العمليات");
            AddNavItem(list, new NavItem("employees", "الموظفون",
                new string[] { "Employees.Add", "Employees.Edit", "Employees.Archive", "Employees.Delete" }));
            AddNavItem(list, new NavItem("org", "الهيكل التنظيمي",
                new string[] { "Org.Manage" }));
            AddNavItem(list, new NavItem("shifts", "الورديات",
                new string[] { "Org.Manage" }));
            AddNavItem(list, new NavItem("monthly", "الجدول الشهري",
                new string[] { "Schedule.Edit", "Schedule.Approve", "Schedule.Unlock" }));
            AddNavItem(list, new NavItem("attengine", "محرك الحضور",
                new string[] { "Attendance.Edit" }));
            AddNavItem(list, new NavItem("devices", "البصمة والحضور",
                new string[] { "Devices.Manage", "Fingerprint.Download", "Fingerprint.ReadUsers" }));

            AddNavSection(list, "الخدمات");
            AddNavItem(list, new NavItem("leaves", "الإجازات",
                new string[] { "Leaves.Approve", "Leave.Create" }));
            AddNavItem(list, new NavItem("longleaves", "الإجازات طويلة المدة",
                new string[] { "Leaves.Approve", "Leave.Create" }));
            AddNavItem(list, new NavItem("missions", "المأموريات",
                new string[] { "Missions.Approve", "Mission.Create" }));
            AddNavItem(list, new NavItem("penalties", "الجزاءات",
                new string[] { "Penalties.Add" }));

            AddNavSection(list, "التقارير");
            AddNavItem(list, new NavItem("reports", "التقارير",
                new string[] { "Reports.View" }));

            AddNavSection(list, "الإدارة");
            AddNavItem(list, new NavItem("users", "المستخدمون والصلاحيات",
                new string[] { "Users.Manage" }));
            AddNavItem(list, new NavItem("audit", "سجل العمليات",
                new string[] { "Audit.View" }));
            AddNavItem(list, new NavItem("logins", "سجل الدخول",
                new string[] { "Audit.View" }));
            AddNavItem(list, new NavItem("backup", "النسخ الاحتياطي",
                new string[] { "Backup.Execute" }));
            AddNavItem(list, new NavItem("archive", "الأرشفة",
                new string[] { "Archive.Execute" }));
            AddNavItem(list, new NavItem("settings", "الإعدادات",
                new string[] { "Settings.Manage" }));

            nav.Controls.Add(list);
            Controls.Add(nav);
        }

        private Control MarginWrap(Control inner)
        {
            Panel wrap = new Panel();
            wrap.Width = 166;
            wrap.Height = inner.Height;
            wrap.Margin = new Padding(6, 4, 6, 2);
            inner.Dock = DockStyle.Fill;
            wrap.Controls.Add(inner);
            return wrap;
        }

        private void AddNavSection(FlowLayoutPanel list, string title)
        {
            Label section = new Label();
            section.Text = title;
            section.Width = 166;
            section.Height = 24;
            section.TextAlign = ContentAlignment.MiddleRight;
            section.ForeColor = Color.FromArgb(90, 100, 115);
            section.Font = Theme.Font(8.5f);
            section.Margin = new Padding(6, 8, 6, 2);
            list.Controls.Add(section);
        }

        private void AddNavItem(FlowLayoutPanel list, NavItem item)
        {
            if (!AnyPermitted(item.Perms))
            {
                return;
            }
            Button button = new Button();
            button.Text = item.Label;
            button.Width = 166;
            button.Height = 34;
            button.TextAlign = ContentAlignment.MiddleRight;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(203, 216, 234);
            button.BackColor = list.BackColor;
            button.ForeColor = Color.FromArgb(45, 55, 70);
            button.Font = Theme.Font(9.5f);
            button.Margin = new Padding(6, 1, 6, 1);
            button.Tag = item;
            button.Click += delegate { OpenModule(item.Id); };
            list.Controls.Add(button);
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

        private void OpenModule(string id)
        {
            Form form;
            switch (id)
            {
                case "dashboard": form = new FrmDashboard(); break;
                case "employees": form = new FrmEmployees(); break;
                case "org": form = new FrmLookupManager(); break;
                case "shifts": form = new FrmShifts(); break;
                case "monthly": form = new FrmMonthlySchedule(); break;
                case "attengine": form = new FrmAttendanceCalc(); break;
                case "devices": form = new FrmDevices(); break;
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
                default: return;
            }
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
            form.FormClosed += delegate { SyncTabs(); };
            form.Show();
            SyncTabs();
        }

        // ---------------- menu + status bar ----------------

        private void BuildMenu()
        {
            MenuStrip menu = new MenuStrip();
            menu.BackColor = Theme.ToolbarColor;

            ToolStripMenuItem miSystem = new ToolStripMenuItem("النظام");
            ToolStripMenuItem miLogout = new ToolStripMenuItem("تسجيل الخروج");
            ToolStripMenuItem miChangePassword = new ToolStripMenuItem("تغيير كلمة المرور");
            ToolStripMenuItem miExit = new ToolStripMenuItem("إنهاء البرنامج");
            miSystem.DropDownItems.Add(miLogout);
            miSystem.DropDownItems.Add(miChangePassword);
            miSystem.DropDownItems.Add(new ToolStripSeparator());
            miSystem.DropDownItems.Add(miExit);

            ToolStripMenuItem miModules = new ToolStripMenuItem("الوحدات");
            foreach (ToolStripMenuItem moduleMenu in BuildModuleMenuItems())
            {
                miModules.DropDownItems.Add(moduleMenu);
            }

            miLogout.Click += OnLogoutClick;
            miChangePassword.Click += OnChangePasswordClick;
            miExit.Click += delegate { Close(); };

            menu.Items.Add(miSystem);
            menu.Items.Add(miModules);
            MainMenuStrip = menu;
            Controls.Add(menu);
        }

        /// <summary>Module menu mirrors the side nav (same permission filtering).</summary>
        private System.Collections.Generic.List<ToolStripMenuItem> BuildModuleMenuItems()
        {
            System.Collections.Generic.List<ToolStripMenuItem> result =
                new System.Collections.Generic.List<ToolStripMenuItem>();
            result.Add(StoreAndMenu(new NavItem("dashboard", "لوحة التحكم", null)));
            result.Add(StoreAndMenu(new NavItem("employees", "الموظفون",
                new string[] { "Employees.Add", "Employees.Edit", "Employees.Archive", "Employees.Delete" })));
            result.Add(StoreAndMenu(new NavItem("org", "الهيكل التنظيمي", new string[] { "Org.Manage" })));
            result.Add(StoreAndMenu(new NavItem("shifts", "الورديات", new string[] { "Org.Manage" })));
            result.Add(StoreAndMenu(new NavItem("monthly", "الجدول الشهري",
                new string[] { "Schedule.Edit", "Schedule.Approve", "Schedule.Unlock" })));
            result.Add(StoreAndMenu(new NavItem("attengine", "محرك الحضور",
                new string[] { "Attendance.Edit" })));
            result.Add(StoreAndMenu(new NavItem("devices", "البصمة والحضور",
                new string[] { "Devices.Manage", "Fingerprint.Download", "Fingerprint.ReadUsers" })));
            result.Add(StoreAndMenu(new NavItem("leaves", "الإجازات",
                new string[] { "Leaves.Approve", "Leave.Create" })));
            result.Add(StoreAndMenu(new NavItem("longleaves", "الإجازات طويلة المدة",
                new string[] { "Leaves.Approve", "Leave.Create" })));
            result.Add(StoreAndMenu(new NavItem("missions", "المأموريات",
                new string[] { "Missions.Approve", "Mission.Create" })));
            result.Add(StoreAndMenu(new NavItem("penalties", "الجزاءات",
                new string[] { "Penalties.Add" })));
            result.Add(StoreAndMenu(new NavItem("reports", "التقارير",
                new string[] { "Reports.View" })));
            result.Add(StoreAndMenu(new NavItem("users", "المستخدمون والصلاحيات",
                new string[] { "Users.Manage" })));
            result.Add(StoreAndMenu(new NavItem("audit", "سجل العمليات",
                new string[] { "Audit.View" })));
            result.Add(StoreAndMenu(new NavItem("logins", "سجل الدخول",
                new string[] { "Audit.View" })));
            result.Add(StoreAndMenu(new NavItem("backup", "النسخ الاحتياطي",
                new string[] { "Backup.Execute" })));
            result.Add(StoreAndMenu(new NavItem("archive", "الأرشفة",
                new string[] { "Archive.Execute" })));
            result.Add(StoreAndMenu(new NavItem("settings", "الإعدادات",
                new string[] { "Settings.Manage" })));
            return result;
        }

        private ToolStripMenuItem StoreAndMenu(NavItem item)
        {
            ToolStripMenuItem menu = new ToolStripMenuItem(item.Label);
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

        private void BuildStatusBar()
        {
            StatusStrip status = new StatusStrip();
            ToolStripStatusLabel statusUser = new ToolStripStatusLabel();
            ToolStripStatusLabel statusDate = new ToolStripStatusLabel();
            statusUser.Text = "المستخدم: " + (AppContext.CurrentUser != null ?
                (AppContext.CurrentUser.FullName ?? AppContext.CurrentUser.Username) : "") +
                (AppContext.IsCurrentUserAdmin ? " (مسؤول النظام)" : "");
            statusDate.Text = "الجهاز: " + AppContext.MachineName;
            status.Items.Add(statusUser);
            status.Items.Add(new ToolStripStatusLabel("            "));
            status.Items.Add(statusDate);
            Controls.Add(status);
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
