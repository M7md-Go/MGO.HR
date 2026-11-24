using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using HR.BLL;
using HR.Helpers;

namespace HR.UI.UI
{
    /// <summary>
    /// Guarded old-log cleanup tool (DB tab, "Settings.Manage"): shows live
    /// row counts of the whitelisted log tables, picks a "before" date, and
    /// deletes only after an explicit confirmation. DatabaseBLL always takes a
    /// PreCleanup safety backup first, so the action stays reversible via
    /// the backup screen. Core HR tables are never purgeable here.
    /// </summary>
    public class FrmDataCleanup : Form
    {
        private CheckedListBox _listTables;
        private DateTimePicker _dtpBefore;
        private Dictionary<string, long> _counts;

        public FrmDataCleanup()
        {
            Theme.Apply(this);
            Text = "تنظيف بيانات السجلات القديمة";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(520, 430);

            Label lblTitle = new Label();
            lblTitle.Text = "حذف سجلات العمليات التشغيلية القديمة (بصمات خام / عمليات / دخول)";
            lblTitle.ForeColor = Theme.HeaderTextColor;
            lblTitle.Font = Theme.Font(11f);
            lblTitle.Location = new Point(20, 16);
            lblTitle.Size = new Size(480, 40);

            Label lblTables = new Label();
            lblTables.Text = "الجداول المسموحة للتنظيف:";
            lblTables.Location = new Point(20, 66);
            lblTables.Size = new Size(480, 22);

            _listTables = new CheckedListBox();
            _listTables.Location = new Point(20, 90);
            _listTables.Size = new Size(480, 110);
            _listTables.CheckOnClick = true;

            Label lblBefore = new Label();
            lblBefore.Text = "حذف ما قبل التاريخ:";
            lblBefore.Location = new Point(20, 214);
            lblBefore.Size = new Size(150, 22);

            _dtpBefore = new DateTimePicker();
            _dtpBefore.Format = DateTimePickerFormat.Short;
            _dtpBefore.Location = new Point(175, 210);
            _dtpBefore.Size = new Size(150, 26);

            Label lblWarn = new Label();
            lblWarn.Text = "⚠ قبل التنفيذ تؤخذ نسخة أمان كاملة تلقائيًا (PreCleanup)،\nوكل عملية تُسجَّل في سجل العمليات. الجداول الأساسية لا تُمس هنا.";
            lblWarn.Location = new Point(20, 250);
            lblWarn.Size = new Size(480, 44);
            lblWarn.ForeColor = Theme.StatusWarn;

            Button btnRun = new Button();
            btnRun.Text = "تنظيف باتَّخاذ نسخة الأمان";
            btnRun.Location = new Point(155, 360);
            btnRun.Size = new Size(200, 36);
            btnRun.Click += delegate { OnRunClick(); };

            Button btnCancel = new Button();
            btnCancel.Text = "إلغاء";
            btnCancel.Location = new Point(20, 360);
            btnCancel.Size = new Size(120, 36);
            btnCancel.DialogResult = DialogResult.Cancel;

            Controls.Add(lblTitle);
            Controls.Add(lblTables);
            Controls.Add(_listTables);
            Controls.Add(lblBefore);
            Controls.Add(_dtpBefore);
            Controls.Add(lblWarn);
            Controls.Add(btnRun);
            Controls.Add(btnCancel);
            CancelButton = btnCancel;

            _dtpBefore.Value = DateTime.Now.AddMonths(-6);
            LoadCounts();
        }

        private void LoadCounts()
        {
            try
            {
                _counts = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
                _listTables.Items.Clear();
                foreach (KeyValuePair<string, long> candidate in DatabaseBLL.CleanupCandidates())
                {
                    _counts[candidate.Key] = candidate.Value;
                    _listTables.Items.Add(candidate.Key + "  (" + candidate.Value + " سجل)", false);
                }
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
                Close();
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmDataCleanup.LoadCounts");
                Theme.ShowError("حدث خطأ أثناء عدّ سجلات الجداول.");
                Close();
            }
        }

        private void OnRunClick()
        {
            try
            {
                List<string> selected = new List<string>();
                foreach (string item in _listTables.CheckedItems)
                {
                    string tableName = item.Split(new string[] { "  (" }, StringSplitOptions.None)[0].Trim();
                    selected.Add(tableName);
                }
                if (selected.Count == 0)
                {
                    Theme.ShowInfo("علّم جدولاً واحداً على الأقل للتنظيف.");
                    return;
                }
                if (!Theme.Confirm("تأكيد نهائي: حذف السجلات الأقدم من " + _dtpBefore.Value.ToString("yyyy-MM-dd") +
                    " من:\n" + string.Join("، ", selected.ToArray()) +
                    "\n\nستؤخذ نسخة أمان أولاً ويُسجَّل كل شيء في سجل العمليات. هل أنت متأكد؟"))
                {
                    return;
                }
                Dictionary<string, int> deleted = DatabaseBLL.CleanupOlderThan(selected, _dtpBefore.Value);
                List<string> summary = new List<string>();
                foreach (KeyValuePair<string, int> entry in deleted)
                {
                    summary.Add(entry.Key + ": " + entry.Value + " سجل محذوف");
                }
                Theme.ShowInfo("تم التنظيف بنجاح:\n" + string.Join("\n", summary.ToArray()) +
                    "\n\nإن رغبت بالرجوع: استعِد نسخة Pre-Cleanup من شاشة النسخ الاحتياطي.");
                LoadCounts();
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmDataCleanup.OnRunClick");
                Theme.ShowError("حدث خطأ أثناء التنظيف — البيانات لم تُمس إلا ما تم قبل الخطأ.");
            }
        }
    }
}
