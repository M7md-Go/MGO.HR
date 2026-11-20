using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using HR.BLL;
using HR.Entities;
using HR.Helpers;

namespace HR.UI.UI
{
    /// <summary>
    /// System settings screen (rule 11): shows every AppSettings key with its
    /// attached Arabic description and lets a user holding "Settings.Manage"
    /// edit values inline. Saving goes through SettingsBLL.Save so each change
    /// is validated, cached and audited (rule 22).
    /// </summary>
    public class FrmSettings : Form
    {
        private const int MaxRows = 200;
        private DataGridView _grid;
        private Dictionary<string, string> _originalValues;
        private Button _btnSave;

        public FrmSettings()
        {
            Theme.Apply(this);
            Text = "الإعدادات";
            ClientSize = new Size(1000, 600);
            StartPosition = FormStartPosition.CenterParent;

            Panel header = new Panel();
            Label lblTitle = new Label();
            lblTitle.Text = "إعدادات النظام — جميع القواعد قابلة للتعديل من هنا";
            lblTitle.ForeColor = Theme.HeaderTextColor;
            lblTitle.Font = Theme.Font(12f);
            lblTitle.Dock = DockStyle.Fill;
            lblTitle.TextAlign = ContentAlignment.MiddleCenter;
            Theme.StyleHeader(header);
            header.Controls.Add(lblTitle);
            header.Dock = DockStyle.Top;

            FlowLayoutPanel toolbar = new FlowLayoutPanel();
            toolbar.Dock = DockStyle.Top;
            toolbar.Height = 52;
            toolbar.BackColor = Theme.ToolbarColor;
            toolbar.FlowDirection = FlowDirection.RightToLeft;
            toolbar.WrapContents = false;
            toolbar.Padding = new Padding(6, 8, 6, 4);

            _btnSave = new Button();
            _btnSave.Text = "حفظ التعديلات";
            _btnSave.Height = 30;
            _btnSave.Click += delegate { OnSaveClick(); };
            toolbar.Controls.Add(_btnSave);

            _grid = new DataGridView();
            Theme.StyleGrid(_grid);
            _grid.Dock = DockStyle.Fill;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            DataGridViewTextBoxColumn colKey = new DataGridViewTextBoxColumn();
            colKey.Name = "SettingKey";
            colKey.HeaderText = "المفتاح";
            colKey.ReadOnly = true;
            colKey.FillWeight = 30;

            DataGridViewTextBoxColumn colDesc = new DataGridViewTextBoxColumn();
            colDesc.Name = "SettingDesc";
            colDesc.HeaderText = "الوصف";
            colDesc.ReadOnly = true;
            colDesc.FillWeight = 42;

            DataGridViewTextBoxColumn colValue = new DataGridViewTextBoxColumn();
            colValue.Name = "SettingValue";
            colValue.HeaderText = "القيمة";
            colValue.FillWeight = 18;

            DataGridViewTextBoxColumn colUpdated = new DataGridViewTextBoxColumn();
            colUpdated.Name = "UpdatedAt";
            colUpdated.HeaderText = "آخر تحديث";
            colUpdated.ReadOnly = true;
            colUpdated.FillWeight = 10;

            _grid.Columns.Add(colKey);
            _grid.Columns.Add(colDesc);
            _grid.Columns.Add(colValue);
            _grid.Columns.Add(colUpdated);

            Controls.Add(_grid);
            Controls.Add(toolbar);
            Controls.Add(header);

            _btnSave.Enabled = AppContext.HasPermission("Settings.Manage");
            Reload();
        }

        private void Reload()
        {
            try
            {
                _originalValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                List<AppSetting> settings = SettingsBLL.ListSettings();
                _grid.Rows.Clear();
                foreach (AppSetting setting in settings)
                {
                    _originalValues[setting.SettingKey] = setting.SettingValue ?? string.Empty;
                    int rowIndex = _grid.Rows.Add();
                    DataGridViewRow row = _grid.Rows[rowIndex];
                    row.Cells["SettingKey"].Value = setting.SettingKey;
                    row.Cells["SettingDesc"].Value = setting.SettingDesc;
                    row.Cells["SettingValue"].Value = setting.SettingValue;
                    row.Cells["UpdatedAt"].Value = setting.UpdatedAt == null ? "-" :
                        setting.UpdatedAt.Value.ToString("yyyy-MM-dd HH:mm");
                }
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmSettings.Reload");
                Theme.ShowError("حدث خطأ أثناء تحميل الإعدادات.");
            }
        }

        private void OnSaveClick()
        {
            try
            {
                int savedCount = 0;
                for (int i = 0; i < _grid.Rows.Count && i < MaxRows; i++)
                {
                    DataGridViewRow row = _grid.Rows[i];
                    string key = row.Cells["SettingKey"].Value as string;
                    if (string.IsNullOrEmpty(key))
                    {
                        continue;
                    }
                    string newValue = row.Cells["SettingValue"].Value as string ?? string.Empty;
                    string oldValue;
                    if (!_originalValues.TryGetValue(key, out oldValue) ||
                        string.Equals(oldValue, newValue, StringComparison.Ordinal))
                    {
                        continue;
                    }
                    SettingsBLL.Save(key, newValue);
                    _originalValues[key] = newValue;
                    savedCount++;
                }
                if (savedCount == 0)
                {
                    Theme.ShowInfo("لا توجد تعديلات للحفظ.");
                    return;
                }
                SettingsBLL.Refresh();
                Theme.ShowInfo("تم حفظ " + savedCount + " إعدادًا بنجاح وتسجيل العملية.");
                Reload();
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmSettings.OnSaveClick");
                Theme.ShowError("حدث خطأ أثناء حفظ الإعدادات.");
            }
        }
    }
}
