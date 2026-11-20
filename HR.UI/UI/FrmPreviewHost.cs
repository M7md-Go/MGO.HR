#if DEBUG
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using HR.Helpers;

namespace HR.UI.UI
{
    /// <summary>
    /// DEBUG-only visual preview host (preview loop for code-built forms).
    /// Launch the application with /preview in Debug configuration; never
    /// compiled into Release builds and fully excluded from production runs.
    /// </summary>
    internal class FrmPreviewHost : Form
    {
        private class PreviewItem
        {
            public string Id;
            public string Label;

            public PreviewItem(string id, string label)
            {
                Id = id;
                Label = label;
            }

            public override string ToString()
            {
                return Label;
            }
        }

        private ComboBox _combo;
        private readonly List<Form> _openPreviews = new List<Form>();

        public FrmPreviewHost()
        {
            Theme.Apply(this);
            Text = "معرض معاينة النوافذ (وضع DEBUG)";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(430, 160);

            Label caption = new Label();
            caption.Text = "اختر النافذة ثم اضغط عرض:";
            caption.Location = new Point(20, 18);
            caption.Size = new Size(380, 22);
            caption.TextAlign = ContentAlignment.MiddleRight;
            Controls.Add(caption);

            _combo = new ComboBox();
            _combo.DropDownStyle = ComboBoxStyle.DropDownList;
            _combo.Location = new Point(20, 46);
            _combo.Size = new Size(380, 26);
            BuildItems();
            Controls.Add(_combo);

            Button show = new Button();
            show.Text = "عرض";
            show.Location = new Point(245, 82);
            show.Size = new Size(155, 32);
            show.Click += OnShowClick;
            Controls.Add(show);

            Button closeAll = new Button();
            closeAll.Text = "إغلاق المعروض";
            closeAll.Location = new Point(20, 82);
            closeAll.Size = new Size(155, 32);
            closeAll.Click += delegate { CloseAllPreviews(); };
            Controls.Add(closeAll);

            FormClosing += OnHostClosing;
        }

        private void BuildItems()
        {
            List<PreviewItem> items = new List<PreviewItem>();
            items.Add(new PreviewItem("dashboard", "لوحة التحكم"));
            items.Add(new PreviewItem("employees", "الموظفون"));
            items.Add(new PreviewItem("employeeEdit", "نافذة إدخال موظف (إضافة)"));
            items.Add(new PreviewItem("org", "الهيكل التنظيمي"));
            items.Add(new PreviewItem("shifts", "الورديات"));
            items.Add(new PreviewItem("shiftEdit", "نافذة إدخال وردية (إضافة)"));
            items.Add(new PreviewItem("monthly", "الجدول الشهري"));
            items.Add(new PreviewItem("attengine", "محرك الحضور"));
            items.Add(new PreviewItem("devices", "البصمة والحضور"));
            items.Add(new PreviewItem("deviceEdit", "نافذة إدخال جهاز (إضافة)"));
            items.Add(new PreviewItem("fingerprintLogs", "سجل البصمات الخام"));
            items.Add(new PreviewItem("leaves", "الإجازات"));
            items.Add(new PreviewItem("leaveEdit", "نافذة إدخال إجازة (إضافة)"));
            items.Add(new PreviewItem("leaveBalances", "أرصدة الإجازات"));
            items.Add(new PreviewItem("missions", "المأموريات"));
            items.Add(new PreviewItem("missionEdit", "نافذة إدخال مأمورية (إضافة)"));
            items.Add(new PreviewItem("penalties", "الجزاءات"));
            items.Add(new PreviewItem("penaltyEdit", "نافذة إدخال جزاء (إضافة)"));
            items.Add(new PreviewItem("reports", "التقارير"));
            items.Add(new PreviewItem("users", "المستخدمون والصلاحيات"));
            items.Add(new PreviewItem("permissions", "تفويض الصلاحيات"));
            items.Add(new PreviewItem("audit", "سجل العمليات"));
            items.Add(new PreviewItem("logins", "سجل الدخول"));
            items.Add(new PreviewItem("backup", "النسخ الاحتياطي"));
            items.Add(new PreviewItem("archive", "الأرشفة"));
            items.Add(new PreviewItem("settings", "الإعدادات"));

            foreach (PreviewItem item in items)
            {
                _combo.Items.Add(item);
            }
            if (_combo.Items.Count > 0)
            {
                _combo.SelectedIndex = 0;
            }
        }

        private void OnShowClick(object sender, EventArgs args)
        {
            try
            {
                PreviewItem selected = _combo.SelectedItem as PreviewItem;
                if (selected == null)
                {
                    return;
                }
                Form form = CreateForm(selected.Id);
                if (form == null)
                {
                    Theme.ShowInfo("هذه النافذة تتطلب سياقًا حقيقيًا لا يمكن معاينتها.");
                    return;
                }
                form.FormClosed += delegate { _openPreviews.Remove(form); };
                _openPreviews.Add(form);
                form.Show(this);
            }
            catch (Exception previewError)
            {
                Logger.LogError(previewError, "FrmPreviewHost.OnShowClick");
                Theme.ShowError("تعذر عرض هذه النافذة في وضع المعاينة. راجع السجلات.");
            }
        }

        private Form CreateForm(string id)
        {
            switch (id)
            {
                case "dashboard": return new FrmDashboard();
                case "employees": return new FrmEmployees();
                case "employeeEdit": return new FrmEmployeeEdit(null);
                case "org": return new FrmLookupManager();
                case "shifts": return new FrmShifts();
                case "shiftEdit": return new FrmShiftEdit(null);
                case "monthly": return new FrmMonthlySchedule();
                case "attengine": return new FrmAttendanceCalc();
                case "devices": return new FrmDevices();
                case "deviceEdit": return new FrmDeviceEdit(null);
                case "fingerprintLogs": return new FrmFingerprintLogs();
                case "leaves": return new FrmLeaves();
                case "leaveEdit": return new FrmLeaveEdit(null);
                case "leaveBalances": return new FrmLeaveBalances(null);
                case "missions": return new FrmMissions();
                case "missionEdit": return new FrmMissionEdit(null);
                case "penalties": return new FrmPenalties();
                case "penaltyEdit": return new FrmPenaltyEdit(null);
                case "reports": return new FrmReports();
                case "users": return new FrmUsers();
                case "permissions": return new FrmPermissions();
                case "audit": return new FrmAuditLog();
                case "logins": return new FrmLoginHistory();
                case "backup": return new FrmBackupRestore();
                case "archive": return new FrmArchive();
                case "settings": return new FrmSettings();
                default: return null;
            }
        }

        private void CloseAllPreviews()
        {
            for (int i = _openPreviews.Count - 1; i >= 0; i--)
            {
                Form preview = _openPreviews[i];
                if (!preview.IsDisposed)
                {
                    preview.Close();
                }
            }
            _openPreviews.Clear();
        }

        private void OnHostClosing(object sender, FormClosingEventArgs args)
        {
            CloseAllPreviews();
        }
    }
}
#endif
