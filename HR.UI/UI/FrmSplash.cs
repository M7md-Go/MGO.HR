using System;
using System.Drawing;
using System.Windows.Forms;
using HR.Entities;

namespace HR.UI.UI
{
    /// <summary>
    /// Commercial splash screen: system name, version, last automatic backup date.
    /// Closes itself after ~1.2 seconds; shown before the login loop.
    /// </summary>
    public class FrmSplash : Form
    {
        private readonly Timer _closeTimer;

        public FrmSplash()
        {
            Theme.Apply(this); // top-level dialog: mirrored RTL works here
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            ShowInTaskbar = false;
            ClientSize = new Size(560, 300);
            BackColor = Theme.HeaderColor;

            Label title = new Label();
            title.Text = "Modern Time";
            title.Font = Theme.Font(24f);
            title.ForeColor = Color.White;
            title.Dock = DockStyle.Fill;
            title.TextAlign = ContentAlignment.MiddleCenter;

            Label subtitle = new Label();
            subtitle.Text = "نظام إدارة الموارد البشرية والحضور والانصراف";
            subtitle.Font = Theme.Font(11f);
            subtitle.ForeColor = Color.FromArgb(214, 226, 244);
            subtitle.Height = 34;
            subtitle.Dock = DockStyle.Bottom;
            subtitle.TextAlign = ContentAlignment.MiddleCenter;

            Label lastBackup = new Label();
            lastBackup.Text = "آخر نسخة احتياطية: " + DescribeLastBackup();
            lastBackup.Font = Theme.Font(8.75f);
            lastBackup.ForeColor = Color.FromArgb(178, 198, 226);
            lastBackup.Height = 24;
            lastBackup.Dock = DockStyle.Bottom;
            lastBackup.TextAlign = ContentAlignment.MiddleCenter;

            Controls.Add(title);
            Controls.Add(subtitle);
            Controls.Add(lastBackup);

            Panel versionBand = new Panel();
            versionBand.Height = 30;
            versionBand.Dock = DockStyle.Bottom;
            versionBand.BackColor = Color.FromArgb(33, 68, 122);
            Label version = new Label();
            version.Dock = DockStyle.Fill;
            version.TextAlign = ContentAlignment.MiddleCenter;
            version.ForeColor = Color.White;
            version.Font = Theme.Font(8.75f);
            Version assemblyVersion = typeof(FrmAbout).Assembly.GetName().Version;
            version.Text = "الإصدار " + assemblyVersion.ToString(3);
            versionBand.Controls.Add(version);
            Controls.Add(versionBand);

            _closeTimer = new Timer();
            _closeTimer.Interval = 1200;
            _closeTimer.Tick += delegate
            {
                _closeTimer.Stop();
                Close();
            };
            Shown += delegate { _closeTimer.Start(); };
        }

        private static string DescribeLastBackup()
        {
            try
            {
                System.Collections.Generic.List<BackupLog> log = BackupBLL.GetLog(1);
                if (log != null && log.Count > 0 && log[0].CreatedAt.HasValue)
                {
                    return "آخر نسخة احتياطية: " + log[0].CreatedAt.Value.ToString("yyyy-MM-dd HH:mm");
                }
            }
            catch (Exception)
            {
                // splash must never block startup — unknown backup date is fine here
            }
            return "آخر نسخة احتياطية: لا يوجد سجل";
        }
    }
}
