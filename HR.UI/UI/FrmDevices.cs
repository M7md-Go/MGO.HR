using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using HR.BLL;
using HR.Entities;
using HR.Helpers;

namespace HR.UI.UI
{
    /// <summary>
    /// Fingerprint device management (rule 9): registry list, add/edit/toggle,
    /// live connection test, device-user browser, and raw attendance-log
    /// download with exact counters. All work is gated by "Devices.Manage" and
    /// delegated to DeviceBLL / FingerprintBLL — no SQL and no device calls here.
    /// </summary>
    public class FrmDevices : Form
    {
        private CheckBox _chkIncludeInactive;
        private Button _btnRefresh;
        private Button _btnAdd;
        private Button _btnEdit;
        private Button _btnToggle;
        private Button _btnDelete;
        private Button _btnTest;
        private Button _btnUsers;
        private Button _btnDownload;
        private Button _btnDownloadAll;
        private Button _btnLogs;
        private DataGridView _grid;
        private List<Device> _rows;

        public FrmDevices()
        {
            Theme.Apply(this);
            Text = "أجهزة البصمة ZKTeco";
            ClientSize = new Size(1000, 560);
            StartPosition = FormStartPosition.CenterParent;

            Panel header = new Panel();
            Label lblTitle = new Label();
            lblTitle.Text = "إدارة أجهزة البصمة وتنزيل السجلات";
            lblTitle.ForeColor = Theme.HeaderTextColor;
            lblTitle.Font = Theme.Font(12f);
            lblTitle.Dock = DockStyle.Fill;
            lblTitle.TextAlign = ContentAlignment.MiddleCenter;
            Theme.StyleHeader(header);
            header.Controls.Add(lblTitle);
            header.Dock = DockStyle.Top;

            FlowLayoutPanel toolbar = new FlowLayoutPanel();
            toolbar.Dock = DockStyle.Top;
            toolbar.Height = 88;
            toolbar.BackColor = Theme.ToolbarColor;
            toolbar.FlowDirection = FlowDirection.RightToLeft;
            toolbar.WrapContents = true;
            toolbar.Padding = new Padding(6, 8, 6, 4);

            _chkIncludeInactive = new CheckBox();
            _chkIncludeInactive.Text = "تضمين المعطّلة";
            _chkIncludeInactive.AutoSize = true;
            _chkIncludeInactive.Margin = new Padding(3, 8, 3, 0);
            _chkIncludeInactive.CheckedChanged += delegate { Reload(); };

            _btnRefresh = new Button();
            _btnRefresh.Text = "تحديث";
            _btnRefresh.Height = 30;
            _btnRefresh.Click += delegate { Reload(); };

            _btnAdd = new Button();
            _btnAdd.Text = "إضافة جهاز";
            _btnAdd.Height = 30;
            _btnAdd.Click += delegate { OnAddClick(); };

            _btnEdit = new Button();
            _btnEdit.Text = "تعديل";
            _btnEdit.Height = 30;
            _btnEdit.Click += delegate { OnEditClick(); };

            _btnToggle = new Button();
            _btnToggle.Text = "تفعيل / تعطيل";
            _btnToggle.Height = 30;
            _btnToggle.Click += delegate { OnToggleClick(); };

            _btnDelete = new Button();
            _btnDelete.Text = "حذف";
            _btnDelete.Height = 30;
            _btnDelete.Click += delegate { OnDeleteClick(); };

            _btnTest = new Button();
            _btnTest.Text = "اختبار الاتصال";
            _btnTest.Height = 30;
            _btnTest.Click += delegate { OnTestClick(); };

            _btnUsers = new Button();
            _btnUsers.Text = "مستخدمو الجهاز";
            _btnUsers.Height = 30;
            _btnUsers.Click += delegate { OnUsersClick(); };

            _btnDownload = new Button();
            _btnDownload.Text = "تنزيل السجلات";
            _btnDownload.Height = 30;
            _btnDownload.Click += delegate { OnDownloadClick(); };

            _btnDownloadAll = new Button();
            _btnDownloadAll.Text = "تنزيل الكل";
            _btnDownloadAll.Height = 30;
            _btnDownloadAll.Click += delegate { OnDownloadAllClick(); };

            _btnLogs = new Button();
            _btnLogs.Text = "عرض السجلات الخام";
            _btnLogs.Height = 30;
            _btnLogs.Click += delegate { OnLogsClick(); };

            toolbar.Controls.Add(_chkIncludeInactive);
            toolbar.Controls.Add(_btnRefresh);
            toolbar.Controls.Add(_btnAdd);
            toolbar.Controls.Add(_btnEdit);
            toolbar.Controls.Add(_btnToggle);
            toolbar.Controls.Add(_btnDelete);
            toolbar.Controls.Add(_btnTest);
            toolbar.Controls.Add(_btnUsers);
            toolbar.Controls.Add(_btnDownload);
            toolbar.Controls.Add(_btnDownloadAll);
            toolbar.Controls.Add(_btnLogs);

            _grid = new DataGridView();
            Theme.StyleGrid(_grid);
            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            Controls.Add(_grid);
            Controls.Add(toolbar);
            Controls.Add(header);

            ApplyPermissionButtons();
            Reload();
        }

        private bool _canManage;

        private void ApplyPermissionButtons()
        {
            _canManage = AppContext.HasPermission("Devices.Manage");
            _btnAdd.Enabled = _canManage;
            _btnEdit.Enabled = _canManage;
            _btnToggle.Enabled = _canManage;
            _btnDelete.Enabled = _canManage;
            _btnTest.Enabled = _canManage;
            _btnUsers.Enabled = _canManage;
            _btnDownload.Enabled = _canManage;
            _btnDownloadAll.Enabled = _canManage;
        }

        /// <summary>Download buttons travel together and lock during any device session.</summary>
        private void SetDownloadsEnabled(bool downloading)
        {
            bool allow = !downloading && _canManage;
            _btnDownload.Enabled = allow;
            _btnDownloadAll.Enabled = allow;
            UseWaitCursor = downloading;
        }

        private Device SelectedDevice()
        {
            if (_grid.CurrentRow == null)
            {
                return null;
            }
            return _grid.CurrentRow.Tag as Device;
        }

        private void Reload()
        {
            try
            {
                _rows = DeviceBLL.GetAll(_chkIncludeInactive.Checked);
                _grid.Columns.Clear();
                _grid.Rows.Clear();

                AddColumn("DeviceName", "الجهاز", 140);
                AddColumn("Address", "العنوان : المنفذ", 120);
                AddColumn("SerialNumber", "الرقم التسلسلي", 130);
                AddColumn("Location", "الموقع", 100);
                AddColumn("LastSync", "آخر مزامنة", 120);
                AddColumn("LastPingDelay", "زمن الاستجابة", 90);
                AddColumn("LastStatus", "آخر حالة", 90);
                AddColumn("IsActive", "نشط", 55);

                foreach (Device device in _rows)
                {
                    int rowIndex = _grid.Rows.Add();
                    DataGridViewRow row = _grid.Rows[rowIndex];
                    row.Tag = device;
                    row.Cells["DeviceName"].Value = device.DeviceName;
                    row.Cells["Address"].Value = device.IpAddress + ":" + (device.Port ?? 0);
                    row.Cells["SerialNumber"].Value = device.SerialNumber;
                    row.Cells["Location"].Value = device.Location;
                    row.Cells["LastSync"].Value = device.LastSync == null ? null : device.LastSync.Value.ToString("yyyy-MM-dd HH:mm:ss");
                    row.Cells["LastPingDelay"].Value = device.LastPingDelay == null ? null : (object)(device.LastPingDelay.Value + " ms");
                    row.Cells["LastStatus"].Value = TranslateStatus(device.LastStatus);
                    row.Cells["IsActive"].Value = device.IsActive == true ? "نعم" : "لا";
                }
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmDevices.Reload");
                Theme.ShowError("حدث خطأ أثناء تحميل قائمة الأجهزة.");
            }
        }

        private static string TranslateStatus(string status)
        {
            if (string.Equals(status, "Connected", StringComparison.Ordinal))
            {
                return "متصل";
            }
            if (string.Equals(status, "Downloaded", StringComparison.Ordinal))
            {
                return "تم التنزيل";
            }
            if (string.Equals(status, "Failed", StringComparison.Ordinal))
            {
                return "فشل الاتصال";
            }
            return status;
        }

        private void AddColumn(string name, string header, int fillWeight)
        {
            DataGridViewTextBoxColumn column = new DataGridViewTextBoxColumn();
            column.Name = name;
            column.HeaderText = header;
            column.FillWeight = fillWeight;
            column.SortMode = DataGridViewColumnSortMode.NotSortable;
            _grid.Columns.Add(column);
        }

        private void OnAddClick()
        {
            try
            {
                using (FrmDeviceEdit dialog = new FrmDeviceEdit(null))
                {
                    dialog.ShowDialog(this);
                }
                Reload();
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmDevices.OnAddClick");
                Theme.ShowError("حدث خطأ أثناء فتح شاشة إضافة جهاز.");
            }
        }

        private void OnEditClick()
        {
            try
            {
                Device device = SelectedDevice();
                if (device == null)
                {
                    Theme.ShowInfo("اختر جهازًا من القائمة أولًا.");
                    return;
                }
                using (FrmDeviceEdit dialog = new FrmDeviceEdit(device.DeviceId))
                {
                    dialog.ShowDialog(this);
                }
                Reload();
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmDevices.OnEditClick");
                Theme.ShowError("حدث خطأ أثناء فتح شاشة تعديل الجهاز.");
            }
        }

        private void OnToggleClick()
        {
            try
            {
                Device device = SelectedDevice();
                if (device == null)
                {
                    Theme.ShowInfo("اختر جهازًا من القائمة أولًا.");
                    return;
                }
                bool activate = device.IsActive != true;
                if (!Theme.Confirm((activate ? "تفعيل" : "تعطيل") + " الجهاز '" + device.DeviceName + "'؟"))
                {
                    return;
                }
                DeviceBLL.Toggle(device.DeviceId, activate);
                Reload();
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmDevices.OnToggleClick");
                Theme.ShowError("حدث خطأ أثناء تغيير حالة الجهاز.");
            }
        }

        private void OnDeleteClick()
        {
            try
            {
                Device device = SelectedDevice();
                if (device == null)
                {
                    Theme.ShowInfo("اختر جهازًا من القائمة أولًا.");
                    return;
                }
                if (!Theme.Confirm("حذف الجهاز '" + device.DeviceName + "' نهائيًا؟ لا يمكن الحذف إذا كان مرتبطًا بسجلات بصمة."))
                {
                    return;
                }
                DeviceBLL.Delete(device.DeviceId);
                Reload();
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmDevices.OnDeleteClick");
                Theme.ShowError("حدث خطأ أثناء حذف الجهاز.");
            }
        }

        private void OnTestClick()
        {
            try
            {
                Device device = SelectedDevice();
                if (device == null)
                {
                    Theme.ShowInfo("اختر جهازًا من القائمة أولًا.");
                    return;
                }
                string serial = FingerprintBLL.TestConnection(DeviceBLL.GetById(device.DeviceId));
                Theme.ShowInfo("الاتصال ناجح بالجهاز '" + device.DeviceName + "'.\nالرقم التسلسلي: " +
                    (string.IsNullOrEmpty(serial) ? "(لم يُقرأ)" : serial));
                Reload();
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (InvalidOperationException expectedSdk)
            {
                Theme.ShowError(expectedSdk.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmDevices.OnTestClick");
                Theme.ShowError("حدث خطأ أثناء اختبار الاتصال.");
            }
        }

        private void OnUsersClick()
        {
            try
            {
                Device device = SelectedDevice();
                if (device == null)
                {
                    Theme.ShowInfo("اختر جهازًا من القائمة أولًا.");
                    return;
                }
                using (FrmDeviceUsers dialog = new FrmDeviceUsers(device.DeviceId))
                {
                    dialog.ShowDialog(this);
                }
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmDevices.OnUsersClick");
                Theme.ShowError("حدث خطأ أثناء قراءة مستخدمي الجهاز.");
            }
        }

        private void OnDownloadClick()
        {
            try
            {
                Device device = SelectedDevice();
                if (device == null)
                {
                    Theme.ShowInfo("اختر جهازًا من القائمة أولًا.");
                    return;
                }
                if (!Theme.Confirm("تنزيل سجلات البصمة من الجهاز '" + device.DeviceName +
                    "'؟ لا تُفقد السجلات والتكرارات تُتخطى تلقائيًا. قد تستغرق العملية عدة دقائق."))
                {
                    return;
                }
                StartBackgroundDownload(delegate()
                {
                    List<DeviceDownloadResult> results = new List<DeviceDownloadResult>();
                    DeviceDownloadResult result = new DeviceDownloadResult();
                    result.Device = DeviceBLL.GetById(device.DeviceId);
                    result.Summary = FingerprintBLL.DownloadLogs(device.DeviceId);
                    results.Add(result);
                    return results;
                });
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmDevices.OnDownloadClick");
                Theme.ShowError("حدث خطأ أثناء تنزيل سجلات البصمة.");
            }
        }

        private void OnDownloadAllClick()
        {
            try
            {
                if (!Theme.Confirm("تنزيل سجلات البصمة من كل الأجهزة النشطة؟ جهاز لا يستجيب لن يوقف البقية وستظهر نتيجة كل جهاز على حدة. قد تستغرق العملية عدة دقائق."))
                {
                    return;
                }
                StartBackgroundDownload(delegate()
                {
                    return FingerprintBLL.DownloadAllActive();
                });
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmDevices.OnDownloadAllClick");
                Theme.ShowError("حدث خطأ أثناء بدء التنزيل الجماعي.");
            }
        }

        /// <summary>
        /// Runs the device session on a worker thread so the window stays responsive;
        /// the result summary is marshaled back to the UI thread when finished.
        /// </summary>
        private void StartBackgroundDownload(Func<List<DeviceDownloadResult>> work)
        {
            SetDownloadsEnabled(false);
            BackgroundWorker worker = new BackgroundWorker();
            worker.DoWork += delegate(object sender, DoWorkEventArgs args)
            {
                args.Result = work();
            };
            worker.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs args)
            {
                SetDownloadsEnabled(true);
                try
                {
                    if (args.Error != null)
                    {
                        if (args.Error is BusinessException)
                        {
                            Theme.ShowError(args.Error.Message);
                            return;
                        }
                        Logger.LogError(args.Error, "FrmDevices.BackgroundDownload");
                        Theme.ShowError("حدث خطأ أثناء تنزيل سجلات البصمة.");
                        return;
                    }
                    Reload();
                    Theme.ShowInfo(BuildDownloadReport(args.Result as List<DeviceDownloadResult>));
                }
                catch (Exception unexpected)
                {
                    Logger.LogError(unexpected, "FrmDevices.BackgroundDownloadCompleted");
                    Theme.ShowError("حدث خطأ أثناء عرض نتيجة التنزيل.");
                }
            };
            worker.RunWorkerAsync();
        }

        /// <summary>Builds the per-device counters report shown after a session.</summary>
        private string BuildDownloadReport(List<DeviceDownloadResult> results)
        {
            if (results == null || results.Count == 0)
            {
                return "لا يوجد أجهزة نشطة لتنزيل سجلاتها.";
            }
            List<string> lines = new List<string>();
            int ok = 0;
            int failed = 0;
            foreach (DeviceDownloadResult result in results)
            {
                if (result.Success)
                {
                    ok++;
                    lines.Add("✔ '" + result.Device.DeviceName + "' — سجلات الجهاز: " + result.Summary.DeviceLogs +
                        ", جديد: " + result.Summary.NewLogs +
                        ", مكرر: " + result.Summary.DuplicateLogs +
                        ", بلا موظف مطابق: " + result.Summary.UnmappedLogs +
                        ", مستخدمو الجهاز: " + result.Summary.DeviceUsers +
                        ", المدة: " + result.Summary.DurationMs + " ms");
                }
                else
                {
                    failed++;
                    lines.Add("✖ '" + result.Device.DeviceName + "' — فشل: " + result.ErrorMessage);
                }
            }
            lines.Insert(0, "نتيجة التنزيل — أجهزة ناجحة: " + ok + ", متعثرة: " + failed);
            return string.Join("\n", lines.ToArray());
        }

        private void OnLogsClick()
        {
            try
            {
                ShowForm(new FrmFingerprintLogs());
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmDevices.OnLogsClick");
                Theme.ShowError("حدث خطأ أثناء فتح سجلات البصمة الخام.");
            }
        }

        /// <summary>Hosts a child form inside the MDI container when available (FrmMain pattern).</summary>
        private void ShowForm(Form form)
        {
            Form mdiParent = this.MdiParent;
            if (mdiParent != null)
            {
                form.MdiParent = mdiParent;
            }
            form.Show();
        }
    }
}
