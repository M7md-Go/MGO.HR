using System;
using System.Drawing;
using System.Windows.Forms;
using HR.BLL;
using HR.Helpers;

namespace HR.UI.UI
{
    /// <summary>Main shell: Arabic navigation, status bar, MDI container for module screens.</summary>
    public class FrmMain : Form
    {
        public bool UserLoggedOut { get; private set; }

        public FrmMain()
        {
            Theme.Apply(this);
            Text = Theme.AppTitle;
            IsMdiContainer = true;
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(1120, 700);
            WindowState = FormWindowState.Maximized;

            FormClosing += OnFormClosingUserCheck;

            BuildMenu();
            BuildStatusBar();
        }

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
            ToolStripMenuItem miEmployees = new ToolStripMenuItem("الموظفون");
            ToolStripMenuItem miOrg = new ToolStripMenuItem("الهيكل التنظيمي");
            ToolStripMenuItem miShifts = new ToolStripMenuItem("الورديات");
            ToolStripMenuItem miDashboard = new ToolStripMenuItem("لوحة التحكم");
            ToolStripMenuItem miMonthly = new ToolStripMenuItem("الجدول الشهري");
            ToolStripMenuItem miAttEngine = new ToolStripMenuItem("محرك الحضور");
            ToolStripMenuItem miFingerprint = new ToolStripMenuItem("البصمة والحضور");
            ToolStripMenuItem miReports = new ToolStripMenuItem("التقارير");
            ToolStripMenuItem miAdmin = new ToolStripMenuItem("الإدارة");
            ToolStripMenuItem miUsers = new ToolStripMenuItem("المستخدمون والصلاحيات");
            ToolStripMenuItem miBackup = new ToolStripMenuItem("النسخ الاحتياطي");
            ToolStripMenuItem miSettings = new ToolStripMenuItem("الإعدادات");
            miAdmin.DropDownItems.Add(miUsers);
            miAdmin.DropDownItems.Add(miBackup);
            miAdmin.DropDownItems.Add(miSettings);

            miEmployees.Click += delegate { ShowForm(new FrmEmployees()); };
            miOrg.Click += delegate { ShowForm(new FrmLookupManager()); };
            miShifts.Click += delegate { ShowForm(new FrmShifts()); };
            miMonthly.Click += delegate { ShowForm(new FrmMonthlySchedule()); };
            miAttEngine.Click += delegate { ShowForm(new FrmAttendanceCalc()); };
            miFingerprint.Click += delegate { ShowForm(new FrmDevices()); };
            miDashboard.Click += delegate { ShowPhaseNotice(11); };
            miReports.Click += delegate { ShowPhaseNotice(9); };
            miUsers.Click += delegate { ShowPhaseNotice(10); };
            miBackup.Click += delegate { ShowPhaseNotice(12); };
            miSettings.Click += delegate { ShowPhaseNotice(2); };
            miLogout.Click += OnLogoutClick;
            miChangePassword.Click += OnChangePasswordClick;
            miExit.Click += delegate { Close(); };

            miModules.DropDownItems.Add(miEmployees);
            miModules.DropDownItems.Add(miOrg);
            miModules.DropDownItems.Add(miShifts);
            miModules.DropDownItems.Add(new ToolStripSeparator());
            miModules.DropDownItems.Add(miDashboard);
            miModules.DropDownItems.Add(miMonthly);
            miModules.DropDownItems.Add(miAttEngine);
            miModules.DropDownItems.Add(miFingerprint);
            miModules.DropDownItems.Add(new ToolStripSeparator());
            miModules.DropDownItems.Add(miReports);
            miModules.DropDownItems.Add(miAdmin);

            menu.Items.Add(miSystem);
            menu.Items.Add(miModules);
            MainMenuStrip = menu;
            Controls.Add(menu);
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

        private void ShowForm(Form form)
        {
            form.MdiParent = this;
            form.Show();
        }

        private static void ShowPhaseNotice(int phaseNumber)
        {
            Theme.ShowInfo("هذه الوحدة مُبرمجة للتنفيذ في المرحلة رقم " + phaseNumber +
                " وفق خطة التنفيذ المعتمدة ولم تُبنَ بعد.");
        }

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

        protected override void OnResize(EventArgs args)
        {
            base.OnResize(args);
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
