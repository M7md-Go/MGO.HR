using System;
using System.Drawing;
using System.Windows.Forms;
using HR.Helpers;

namespace HR.UI.UI
{
    /// <summary>
    /// About dialog: system identity, stack description, version, and developer credit.
    /// </summary>
    public class FrmAbout : Form
    {
        public FrmAbout()
        {
            Theme.Apply(this); // dialog: mirrored RTL layout works here (non-MDI)
            Text = "عن النظام";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(520, 430);

            Panel header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = 74;
            header.BackColor = Theme.HeaderColor;

            Label title = new Label();
            title.Text = "Modern Time";
            title.Dock = DockStyle.Fill;
            title.TextAlign = ContentAlignment.MiddleCenter;
            title.ForeColor = Color.White;
            title.Font = Theme.Font(16f);
            header.Controls.Add(title);

            Label subtitle = new Label();
            subtitle.Text = "إدارة الموارد البشرية والحضور والانصراف";
            subtitle.Dock = DockStyle.Bottom;
            subtitle.Height = 26;
            subtitle.TextAlign = ContentAlignment.MiddleCenter;
            subtitle.ForeColor = Color.FromArgb(214, 226, 244);
            subtitle.Font = Theme.Font(9.5f);
            header.Controls.Add(subtitle);

            TextBox info = new TextBox();
            info.Multiline = true;
            info.ReadOnly = true;
            info.ScrollBars = ScrollBars.Vertical;
            info.Dock = DockStyle.Fill;
            info.BackColor = Color.White;
            info.Font = Theme.Font(9.75f);
            Version version = typeof(FrmMain).Assembly.GetName().Version;
            info.Text =
                "نظام Modern Time لإدارة الموارد البشرية والحضور والانصراف.\r\n" +
                "\r\n" +
                "الإصدار: " + version.ToString(3) + "\r\n" +
                "\r\n" +
                "المزايا الرئيسية:\r\n" +
                "• إدارة الموظفين والهيكل التنظيمي والورديات\r\n" +
                "• الجداول الشهرية مع محرك الحضور والانصراف\r\n" +
                "• الإجازات والمأموريات والجزاءات ودورات الاعتماد\r\n" +
                "• تكامل أجهزة البصمة (ZKTeco) مع تنزيل مجدول\r\n" +
                "• النسخ الاحتياطي التلقائي وسجل العمليات والصلاحيات\r\n" +
                "\r\n" +
                "البنية التقنية: C# / WinForms / .NET Framework 4.0\r\n" +
                "قاعدة البيانات: Microsoft Access (Jet OLEDB 4.0)\r\n" +
                "\r\n" +
                "المطور: M7md-Go\r\n" +
                "github.com/M7md-Go/MGO.HR";

            Panel buttons = new Panel();
            buttons.Dock = DockStyle.Bottom;
            buttons.Height = 48;

            Button btnClose = new Button();
            btnClose.Text = "إغلاق";
            btnClose.Width = 110;
            btnClose.Height = 32;
            btnClose.Font = Theme.Font(9.5f);
            btnClose.Left = 16;
            btnClose.Top = 8;
            btnClose.Click += delegate { Close(); };
            buttons.Controls.Add(btnClose);

            Button btnRepo = new Button();
            btnRepo.Text = "صفحة المشروع";
            btnRepo.Width = 140;
            btnRepo.Height = 32;
            btnRepo.Font = Theme.Font(9.5f);
            btnRepo.Left = 140;
            btnRepo.Top = 8;
            btnRepo.Click += delegate
            {
                try
                {
                    System.Diagnostics.Process.Start("https://github.com/M7md-Go/MGO.HR");
                }
                catch (Exception unexpected)
                {
                    Logger.LogError(unexpected, "FrmAbout.OpenRepo");
                    Theme.ShowError("لم يتم فتح الرابط. افتح المتصفح يدويًا: https://github.com/M7md-Go/MGO.HR");
                }
            };
            buttons.Controls.Add(btnRepo);

            Controls.Add(info);
            Controls.Add(buttons);
            Controls.Add(header);
            AcceptButton = btnClose;
            CancelButton = btnClose;
        }
    }
}
