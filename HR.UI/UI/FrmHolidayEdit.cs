using System;
using System.Drawing;
using System.Windows.Forms;
using HR.BLL;
using HR.Entities;
using HR.Helpers;

namespace HR.UI.UI
{
    /// <summary>Modal add/edit form for one official holiday.</summary>
    public class FrmHolidayEdit : Form
    {
        private readonly int? _holidayId;
        private readonly DateTimePicker _dtpDate;
        private readonly TextBox _txtName;
        private readonly CheckBox _chkRecurring;

        public FrmHolidayEdit(int? holidayId)
        {
            _holidayId = holidayId;
            Theme.Apply(this);
            Text = holidayId == null ? "عطلة رسمية جديدة" : "تعديل عطلة رسمية";
            ClientSize = new Size(440, 210);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;

            Label lblDate = new Label();
            lblDate.Text = "التاريخ (للعطلة السنوية يُستخدم الشهر واليوم فقط):";
            lblDate.AutoSize = true;
            lblDate.Location = new Point(20, 18);

            _dtpDate = new DateTimePicker();
            _dtpDate.Format = DateTimePickerFormat.Short;
            _dtpDate.Location = new Point(20, 40);
            _dtpDate.Width = 160;
            _dtpDate.Value = holidayId == null ? DateTime.Now.Date.AddMonths(1) : DateTime.Now.Date;

            Label lblName = new Label();
            lblName.Text = "الاسم (حتى 100 حرف):";
            lblName.AutoSize = true;
            lblName.Location = new Point(20, 72);

            _txtName = new TextBox();
            _txtName.Location = new Point(20, 94);
            _txtName.Size = new Size(400, 23);
            _txtName.MaxLength = 100;

            _chkRecurring = new CheckBox();
            _chkRecurring.Text = "عطلة سنوية متكررة كل عام (مثل ذكرى وطنية ثابتة)";
            _chkRecurring.AutoSize = true;
            _chkRecurring.Location = new Point(20, 126);

            Button btnOk = new Button();
            btnOk.Text = "حفظ";
            btnOk.Width = 90;
            btnOk.Location = new Point(330, 172);
            btnOk.Font = Theme.Font(9f);
            btnOk.Click += delegate { OnSaveClick(); };

            Button btnCancel = new Button();
            btnCancel.Text = "إلغاء";
            btnCancel.Width = 90;
            btnCancel.Location = new Point(230, 172);
            btnCancel.DialogResult = DialogResult.Cancel;

            Controls.Add(lblDate);
            Controls.Add(_dtpDate);
            Controls.Add(lblName);
            Controls.Add(_txtName);
            Controls.Add(_chkRecurring);
            Controls.Add(btnOk);
            Controls.Add(btnCancel);
            CancelButton = btnCancel;

            if (holidayId != null)
            {
                LoadHoliday(holidayId.Value);
            }
        }

        private void LoadHoliday(int holidayId)
        {
            try
            {
                PublicHoliday stored = PublicHolidayBLL.GetById(holidayId);
                if (stored == null)
                {
                    Theme.ShowError("العطلة غير موجودة.");
                    Close();
                    return;
                }
                _dtpDate.Value = stored.HolidayDate ?? DateTime.Now.Date;
                _txtName.Text = stored.HolidayName;
                _chkRecurring.Checked = stored.IsRecurring == true;
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmHolidayEdit.LoadHoliday");
                Theme.ShowError("حدث خطأ أثناء تحميل بيانات العطلة.");
            }
        }

        private void OnSaveClick()
        {
            try
            {
                string name = (_txtName.Text ?? string.Empty).Trim();
                if (_holidayId == null)
                {
                    PublicHolidayBLL.Create(_dtpDate.Value.Date, name, _chkRecurring.Checked);
                }
                else
                {
                    PublicHolidayBLL.Update(_holidayId.Value, _dtpDate.Value.Date, name, _chkRecurring.Checked);
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
                Logger.LogError(unexpected, "FrmHolidayEdit.OnSaveClick");
                Theme.ShowError("حدث خطأ أثناء حفظ العطلة.");
            }
        }
    }
}
