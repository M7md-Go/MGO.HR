using System;
using System.Drawing;
using System.Windows.Forms;
using HR.BLL;
using HR.Entities;
using HR.Helpers;

namespace HR.UI.UI
{
    /// <summary>
    /// Device add/edit dialog (rule 9): name, IP, port, serial, location and
    /// active flag. Validation lives in DeviceBLL (unique name, parseable IPv4,
    /// port range); the serial may stay empty — the first successful connection
    /// binds it automatically from the physical device.
    /// </summary>
    public class FrmDeviceEdit : Form
    {
        private readonly int? _deviceId;
        private TextBox _txtName;
        private TextBox _txtIp;
        private NumericUpDown _numPort;
        private TextBox _txtSerial;
        private TextBox _txtLocation;
        private CheckBox _chkActive;
        private Button _btnSave;
        private Button _btnCancel;

        public FrmDeviceEdit(int? deviceId)
        {
            _deviceId = deviceId;
            Theme.Apply(this);
            Text = deviceId == null ? "إضافة جهاز بصمة" : "تعديل جهاز بصمة";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(470, 300);

            Label lblName = new Label();
            lblName.Text = "اسم الجهاز *:";
            lblName.Location = new Point(20, 22);
            lblName.Size = new Size(130, 22);

            _txtName = new TextBox();
            _txtName.Location = new Point(155, 18);
            _txtName.Size = new Size(280, 26);
            _txtName.MaxLength = 60;

            Label lblIp = new Label();
            lblIp.Text = "عنوان IP *:";
            lblIp.Location = new Point(20, 57);
            lblIp.Size = new Size(130, 22);

            _txtIp = new TextBox();
            _txtIp.Location = new Point(155, 53);
            _txtIp.Size = new Size(280, 26);
            _txtIp.MaxLength = 15;
            _txtIp.RightToLeft = RightToLeft.No;

            Label lblPort = new Label();
            lblPort.Text = "المنفذ *:";
            lblPort.Location = new Point(20, 92);
            lblPort.Size = new Size(130, 22);

            _numPort = new NumericUpDown();
            _numPort.Location = new Point(155, 88);
            _numPort.Size = new Size(120, 26);
            _numPort.Minimum = 1;
            _numPort.Maximum = 65535;
            _numPort.Value = 4370;

            Label lblSerial = new Label();
            lblSerial.Text = "الرقم التسلسلي:";
            lblSerial.Location = new Point(20, 127);
            lblSerial.Size = new Size(130, 22);

            _txtSerial = new TextBox();
            _txtSerial.Location = new Point(155, 123);
            _txtSerial.Size = new Size(280, 26);
            _txtSerial.MaxLength = 40;
            _txtSerial.RightToLeft = RightToLeft.No;

            Label lblLocation = new Label();
            lblLocation.Text = "الموقع:";
            lblLocation.Location = new Point(20, 162);
            lblLocation.Size = new Size(130, 22);

            _txtLocation = new TextBox();
            _txtLocation.Location = new Point(155, 158);
            _txtLocation.Size = new Size(280, 26);
            _txtLocation.MaxLength = 100;

            _chkActive = new CheckBox();
            _chkActive.Text = "الجهاز نشط";
            _chkActive.Location = new Point(155, 196);
            _chkActive.AutoSize = true;
            _chkActive.Checked = true;

            _btnSave = new Button();
            _btnSave.Text = "حفظ";
            _btnSave.Location = new Point(305, 240);
            _btnSave.Size = new Size(130, 34);
            _btnSave.Click += delegate { OnSaveClick(); };

            _btnCancel = new Button();
            _btnCancel.Text = "إلغاء";
            _btnCancel.Location = new Point(20, 240);
            _btnCancel.Size = new Size(130, 34);
            _btnCancel.DialogResult = DialogResult.Cancel;

            Controls.Add(lblName);
            Controls.Add(_txtName);
            Controls.Add(lblIp);
            Controls.Add(_txtIp);
            Controls.Add(lblPort);
            Controls.Add(_numPort);
            Controls.Add(lblSerial);
            Controls.Add(_txtSerial);
            Controls.Add(lblLocation);
            Controls.Add(_txtLocation);
            Controls.Add(_chkActive);
            Controls.Add(_btnSave);
            Controls.Add(_btnCancel);

            AcceptButton = _btnSave;
            CancelButton = _btnCancel;

            if (_deviceId != null)
            {
                LoadDevice();
            }
        }

        private void LoadDevice()
        {
            Device device = DeviceBLL.GetById(_deviceId.Value);
            if (device == null)
            {
                throw new BusinessException("الجهاز غير موجود.");
            }
            _txtName.Text = device.DeviceName;
            _txtIp.Text = device.IpAddress;
            _numPort.Value = device.Port == null ? 4370 : device.Port.Value;
            _txtSerial.Text = device.SerialNumber;
            _txtLocation.Text = device.Location;
            _chkActive.Checked = device.IsActive == true;
        }

        private void OnSaveClick()
        {
            try
            {
                Device device = _deviceId == null ? new Device() : DeviceBLL.GetById(_deviceId.Value);
                if (device == null)
                {
                    Theme.ShowError("الجهاز غير موجود (رُبما حُذف من محطة أخرى).");
                    return;
                }
                device.DeviceName = _txtName.Text;
                device.IpAddress = _txtIp.Text;
                device.Port = (int)_numPort.Value;
                device.SerialNumber = _txtSerial.Text.Trim();
                device.Location = _txtLocation.Text.Trim();
                device.IsActive = _chkActive.Checked;
                if (_deviceId == null)
                {
                    DeviceBLL.Add(device);
                }
                else
                {
                    DeviceBLL.Update(device);
                }
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmDeviceEdit.OnSaveClick");
                Theme.ShowError("حدث خطأ أثناء حفظ بيانات الجهاز.");
            }
        }
    }
}
