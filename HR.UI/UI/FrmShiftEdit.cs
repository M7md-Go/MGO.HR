using System;
using System.Drawing;
using System.Windows.Forms;
using HR.BLL;
using HR.Entities;
using HR.Helpers;

namespace HR.UI.UI
{
    /// <summary>
    /// Shift add/edit dialog (rule 6): code, name, start/end times, cross-day,
    /// duration, normal hours, break minutes, category (BR-09 anti-mixing),
    /// require-check and active flags. CrossDay and DurationHrs are derived by
    /// ShiftBLL.Validate (BR-06) — the UI never invents them, so end-before-start
    /// is allowed only for genuine cross-day shifts and durations can never
    /// exceed 24 continuous hours. Rest shifts (code FR) carry zero duration.
    /// </summary>
    public class FrmShiftEdit : Form
    {
        private static readonly string[] CategoryLabels = new string[] { "مكتبي", "ميداني", "ليلي", "راحة" };
        private static readonly string[] CategoryValues = new string[] { "Office", "Field", "Night", "Rest" };

        private readonly int? _shiftId;
        private TextBox _txtCode;
        private TextBox _txtName;
        private TextBox _txtStart;
        private TextBox _txtEnd;
        private Label _lblDuration;
        private NumericUpDown _numNormal;
        private NumericUpDown _numBreak;
        private ComboBox _cboCategory;
        private CheckBox _chkRequireCheck;
        private CheckBox _chkActive;

        public FrmShiftEdit(int? shiftId)
        {
            _shiftId = shiftId;
            Theme.Apply(this);
            Text = shiftId == null ? "إضافة وردية جديدة" : "تعديل وردية";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(480, 360);

            Label lblCode = new Label();
            lblCode.Text = "كود الوردية *:";
            lblCode.Location = new Point(20, 22);
            lblCode.Size = new Size(130, 22);

            _txtCode = new TextBox();
            _txtCode.Location = new Point(155, 18);
            _txtCode.Size = new Size(160, 26);
            _txtCode.RightToLeft = RightToLeft.No;
            _txtCode.MaxLength = 10;

            Label lblName = new Label();
            lblName.Text = "اسم الوردية *:";
            lblName.Location = new Point(20, 57);
            lblName.Size = new Size(130, 22);

            _txtName = new TextBox();
            _txtName.Location = new Point(155, 53);
            _txtName.Size = new Size(300, 26);
            _txtName.MaxLength = 60;

            Label lblStart = new Label();
            lblStart.Text = "وقت البداية (HH:mm) *:";
            lblStart.Location = new Point(20, 92);
            lblStart.Size = new Size(130, 22);

            _txtStart = new TextBox();
            _txtStart.Location = new Point(155, 88);
            _txtStart.Size = new Size(90, 26);
            _txtStart.RightToLeft = RightToLeft.No;
            _txtStart.MaxLength = 5;
            _txtStart.TextChanged += delegate { UpdateDurationLabel(); };

            Label lblEnd = new Label();
            lblEnd.Text = "وقت النهاية (HH:mm) *:";
            lblEnd.Location = new Point(20, 127);
            lblEnd.Size = new Size(130, 22);

            _txtEnd = new TextBox();
            _txtEnd.Location = new Point(155, 123);
            _txtEnd.Size = new Size(90, 26);
            _txtEnd.RightToLeft = RightToLeft.No;
            _txtEnd.MaxLength = 5;
            _txtEnd.TextChanged += delegate { UpdateDurationLabel(); };

            _lblDuration = new Label();
            _lblDuration.Text = "المدة: —";
            _lblDuration.Location = new Point(260, 127);
            _lblDuration.Size = new Size(195, 22);
            _lblDuration.ForeColor = Theme.StatusOk;

            Label lblNormal = new Label();
            lblNormal.Text = "الساعات الطبيعية:";
            lblNormal.Location = new Point(20, 162);
            lblNormal.Size = new Size(130, 22);

            _numNormal = new NumericUpDown();
            _numNormal.Location = new Point(155, 158);
            _numNormal.Size = new Size(90, 26);
            _numNormal.Minimum = 0;
            _numNormal.Maximum = 24;
            _numNormal.Increment = 0.5m;
            _numNormal.DecimalPlaces = 2;

            Label lblBreak = new Label();
            lblBreak.Text = "راحة داخلية (دقيقة):";
            lblBreak.Location = new Point(20, 197);
            lblBreak.Size = new Size(130, 22);

            _numBreak = new NumericUpDown();
            _numBreak.Location = new Point(155, 193);
            _numBreak.Size = new Size(90, 26);
            _numBreak.Minimum = 0;
            _numBreak.Maximum = 720;
            _numBreak.Increment = 5m;

            Label lblCategory = new Label();
            lblCategory.Text = "صنف الوردية *:";
            lblCategory.Location = new Point(20, 232);
            lblCategory.Size = new Size(130, 22);

            _cboCategory = new ComboBox();
            _cboCategory.Location = new Point(155, 228);
            _cboCategory.Size = new Size(160, 26);
            _cboCategory.DropDownStyle = ComboBoxStyle.DropDownList;

            _chkRequireCheck = new CheckBox();
            _chkRequireCheck.Text = "بصمة إلزامية لهذه الوردية";
            _chkRequireCheck.Location = new Point(155, 263);
            _chkRequireCheck.AutoSize = true;

            _chkActive = new CheckBox();
            _chkActive.Text = "نشطة";
            _chkActive.Location = new Point(155, 290);
            _chkActive.AutoSize = true;

            Button btnOk = new Button();
            btnOk.Text = "حفظ";
            btnOk.Location = new Point(210, 325);
            btnOk.Size = new Size(110, 28);
            btnOk.Click += OnOkClick;

            Button btnCancel = new Button();
            btnCancel.Text = "إلغاء";
            btnCancel.Location = new Point(330, 325);
            btnCancel.Size = new Size(110, 28);
            btnCancel.Click += delegate { DialogResult = DialogResult.Cancel; };

            Controls.Add(lblCode);
            Controls.Add(_txtCode);
            Controls.Add(lblName);
            Controls.Add(_txtName);
            Controls.Add(lblStart);
            Controls.Add(_txtStart);
            Controls.Add(lblEnd);
            Controls.Add(_txtEnd);
            Controls.Add(_lblDuration);
            Controls.Add(lblNormal);
            Controls.Add(_numNormal);
            Controls.Add(lblBreak);
            Controls.Add(_numBreak);
            Controls.Add(lblCategory);
            Controls.Add(_cboCategory);
            Controls.Add(_chkRequireCheck);
            Controls.Add(_chkActive);
            Controls.Add(btnOk);
            Controls.Add(btnCancel);

            for (int index = 0; index < CategoryLabels.Length; index++)
            {
                _cboCategory.Items.Add(CategoryLabels[index]);
            }
            _cboCategory.SelectedIndexChanged += HandleCategoryChanged;
            _cboCategory.SelectedIndex = 0;
            _chkRequireCheck.Checked = true;
            _chkActive.Checked = true;

            if (shiftId != null)
            {
                LoadShift(shiftId.Value);
            }
        }

        private string CategoryToken()
        {
            int index = _cboCategory.SelectedIndex;
            if (index < 0)
            {
                return null;
            }
            return CategoryValues[index];
        }

        private void HandleCategoryChanged(object sender, EventArgs args)
        {
            bool rest = CategoryToken() == "Rest";
            _txtStart.Enabled = !rest;
            _txtEnd.Enabled = !rest;
            if (rest)
            {
                _txtStart.Text = string.Empty;
                _txtEnd.Text = string.Empty;
                _numNormal.Value = 0;
            }
            UpdateDurationLabel();
        }

        /// <summary>Shows the live duration and cross-day state derived from the typed times.</summary>
        private void UpdateDurationLabel()
        {
            if (CategoryToken() == "Rest")
            {
                _lblDuration.Text = "وردية راحة — بلا وقت";
                return;
            }
            int? start = TimeHelper.ParseMinutes(_txtStart.Text.Trim());
            int? end = TimeHelper.ParseMinutes(_txtEnd.Text.Trim());
            if (start == null || end == null)
            {
                _lblDuration.Text = "المدة: —";
                return;
            }
            int minutes = TimeHelper.DurationMinutes(start.Value, end.Value);
            if (minutes > 24 * 60)
            {
                _lblDuration.ForeColor = Theme.StatusBad;
                _lblDuration.Text = "المدة: غير صالحة — تتجاوز 24 ساعة متواصلة (BR-06)";
                return;
            }
            string cross = end.Value < start.Value ? " — تمتد لليوم التالي" : string.Empty;
            _lblDuration.ForeColor = Theme.StatusOk;
            _lblDuration.Text = "المدة: " + (minutes / 60f).ToString("0.##") + " ساعة" + cross;
            if (_numNormal.Value == 0)
            {
                decimal suggested = Math.Round(Convert.ToDecimal(minutes) / 60m, 2);
                if (suggested > 0 && suggested <= _numNormal.Maximum)
                {
                    _numNormal.Value = suggested;
                }
            }
        }

        private void LoadShift(int shiftId)
        {
            Shift shift = ShiftBLL.GetById(shiftId);
            if (shift == null)
            {
                Theme.ShowError("الوردية غير موجودة.");
                Close();
                return;
            }
            _txtCode.Text = shift.ShiftCode ?? string.Empty;
            _txtCode.Enabled = false;
            _txtName.Text = shift.ShiftName ?? string.Empty;
            for (int index = 0; index < CategoryValues.Length; index++)
            {
                if (string.Equals(shift.CategoryCode, CategoryValues[index], StringComparison.Ordinal))
                {
                    _cboCategory.SelectedIndex = index;
                    break;
                }
            }
            if (CategoryToken() != "Rest")
            {
                _txtStart.Text = shift.StartTime ?? string.Empty;
                _txtEnd.Text = shift.EndTime ?? string.Empty;
            }
            if (shift.NormalHours != null && shift.NormalHours.Value > 0)
            {
                _numNormal.Value = Convert.ToDecimal(shift.NormalHours.Value);
            }
            if (shift.BreakMinutes != null)
            {
                _numBreak.Value = Convert.ToDecimal(shift.BreakMinutes.Value);
            }
            _chkRequireCheck.Checked = shift.RequireCheck != false;
            _chkActive.Checked = shift.IsActive != false;
            UpdateDurationLabel();
        }

        private void OnOkClick(object sender, EventArgs args)
        {
            try
            {
                bool isNew = _shiftId == null;
                Shift shift;
                if (isNew)
                {
                    shift = new Shift();
                }
                else
                {
                    shift = ShiftBLL.GetById(_shiftId.Value);
                    if (shift == null)
                    {
                        Theme.ShowError("الوردية غير موجودة.");
                        return;
                    }
                }
                shift.ShiftCode = _txtCode.Text.Trim();
                shift.ShiftName = _txtName.Text.Trim();
                if (CategoryToken() != "Rest")
                {
                    shift.StartTime = EmptyToNull(_txtStart.Text);
                    shift.EndTime = EmptyToNull(_txtEnd.Text);
                    shift.CrossDay = null;
                    shift.DurationHrs = null;
                    shift.NormalHours = _numNormal.Value == 0 ? (float?)null : (float)_numNormal.Value;
                }
                else
                {
                    shift.StartTime = null;
                    shift.EndTime = null;
                    shift.CrossDay = false;
                    shift.DurationHrs = 0f;
                    shift.NormalHours = 0f;
                }
                shift.BreakMinutes = (int)_numBreak.Value;
                shift.CategoryCode = CategoryToken();
                shift.RequireCheck = _chkRequireCheck.Checked;
                shift.IsActive = _chkActive.Checked;

                if (isNew)
                {
                    ShiftBLL.Create(shift);
                }
                else
                {
                    ShiftBLL.Update(shift);
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
                Logger.LogError(unexpected, "FrmShiftEdit.OnOkClick");
                Theme.ShowError("حدث خطأ أثناء الحفظ. تم حفظ التفاصيل في السجلات.");
            }
        }

        private static string EmptyToNull(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }
            return text.Trim();
        }
    }
}
