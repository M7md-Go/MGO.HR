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
    /// Shift management screen (rule 6): the Shifts table itself is the shift
    /// template store — every row carries its actual times, duration, cross-day
    /// flag, category and rules, and monthly schedule details reference it.
    /// Add/edit/toggle/delete are permission-gated ("Org.Manage") and audit-logged
    /// inside ShiftBLL; deleting a shift used by schedules or attendance is refused.
    /// </summary>
    public class FrmShifts : Form
    {
        private static readonly string[] CategoryLabels = new string[] { "مكتبي", "ميداني", "ليلي", "راحة" };
        private static readonly string[] CategoryValues = new string[] { "Office", "Field", "Night", "Rest" };

        private CheckBox _chkIncludeInactive;
        private Button _btnRefresh;
        private Button _btnAdd;
        private Button _btnEdit;
        private Button _btnToggle;
        private Button _btnDelete;
        private DataGridView _grid;
        private List<Shift> _rows;

        public FrmShifts()
        {
            Theme.Apply(this, false); // MDI child: mirrored layout is not applied — RTL flow comes from the containers
            Text = "الورديات";
            ClientSize = new Size(1000, 560);
            StartPosition = FormStartPosition.CenterParent;

            Panel header = new Panel();
            Label lblTitle = new Label();
            lblTitle.Text = "إدارة الورديات والقوالب";
            lblTitle.ForeColor = Theme.HeaderTextColor;
            lblTitle.Font = Theme.Font(12f);
            lblTitle.Dock = DockStyle.Fill;
            lblTitle.TextAlign = ContentAlignment.MiddleCenter;
            Theme.StyleHeader(header);
            header.Controls.Add(lblTitle);
            header.Dock = DockStyle.Top;

            FlowLayoutPanel toolbar = new FlowLayoutPanel();
            toolbar.Dock = DockStyle.Top;
            toolbar.Height = 46;
            toolbar.BackColor = Theme.ToolbarColor;
            // FlowLayoutPanel: RTL=Yes (inherited from the form) already reverses the flow;
            // an explicit RightToLeft cancelled that reversal and rendered the bar left-to-right.
            toolbar.FlowDirection = FlowDirection.LeftToRight;
            toolbar.Padding = new Padding(6, 8, 6, 4);

            _chkIncludeInactive = new CheckBox();
            _chkIncludeInactive.Text = "تضمين المعطلة";
            _chkIncludeInactive.AutoSize = true;
            _chkIncludeInactive.Margin = new Padding(3, 8, 3, 0);
            _chkIncludeInactive.CheckedChanged += delegate { Reload(); };

            _btnRefresh = new Button();
            _btnRefresh.Text = "تحديث";
            _btnRefresh.Height = 30;
            _btnRefresh.Click += delegate { Reload(); };

            _btnAdd = new Button();
            _btnAdd.Text = "إضافة وردية";
            _btnAdd.Height = 30;
            _btnAdd.Click += OnAddClick;

            _btnEdit = new Button();
            _btnEdit.Text = "تعديل";
            _btnEdit.Height = 30;
            _btnEdit.Click += OnEditClick;

            _btnToggle = new Button();
            _btnToggle.Text = "تعطيل / تفعيل";
            _btnToggle.Height = 30;
            _btnToggle.Click += OnToggleClick;

            _btnDelete = new Button();
            _btnDelete.Text = "حذف";
            _btnDelete.Height = 30;
            _btnDelete.Click += OnDeleteClick;

            toolbar.Controls.Add(_chkIncludeInactive);
            toolbar.Controls.Add(_btnRefresh);
            toolbar.Controls.Add(_btnAdd);
            toolbar.Controls.Add(_btnEdit);
            toolbar.Controls.Add(_btnToggle);
            toolbar.Controls.Add(_btnDelete);

            _grid = new DataGridView();
            Theme.StyleGrid(_grid);
            _grid.Dock = DockStyle.Fill;
            _grid.CellDoubleClick += delegate(object sender, DataGridViewCellEventArgs e)
            {
                if (e.RowIndex >= 0)
                {
                    OnEditClick(null, null);
                }
            };

            Controls.Add(_grid);
            Controls.Add(toolbar);
            Controls.Add(header);

            ApplyPermissionButtons();
            Shown += delegate { Reload(); };
        }

        /// <summary>Enables the write buttons only for users holding "Org.Manage".</summary>
        private void ApplyPermissionButtons()
        {
            bool canManage = AppContext.HasPermission("Org.Manage");
            _btnAdd.Enabled = canManage;
            _btnEdit.Enabled = canManage;
            _btnToggle.Enabled = canManage;
            _btnDelete.Enabled = canManage;
        }

        private Shift SelectedShift()
        {
            if (_grid.CurrentRow == null || _grid.CurrentRow.Tag == null)
            {
                Theme.ShowInfo("اختر وردية من القائمة أولًا.");
                return null;
            }
            return _grid.CurrentRow.Tag as Shift;
        }

        private static string CategoryText(string categoryCode)
        {
            for (int index = 0; index < CategoryValues.Length; index++)
            {
                if (string.Equals(categoryCode, CategoryValues[index], StringComparison.Ordinal))
                {
                    return CategoryLabels[index];
                }
            }
            return categoryCode;
        }

        private static string YesNo(bool? value)
        {
            return value == true ? "نعم" : "لا";
        }

        private static string TimeText(string value)
        {
            return string.IsNullOrEmpty(value) ? "—" : value;
        }

        private void Reload()
        {
            try
            {
                _rows = ShiftBLL.GetAll(_chkIncludeInactive.Checked == false);
                BindGrid();
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmShifts.Reload");
                Theme.ShowError("خطأ أثناء تحميل الورديات. راجع السجلات للمزيد.");
            }
        }

        private void BindGrid()
        {
            _grid.Columns.Clear();
            _grid.Columns.Add("c0", "الكود");
            _grid.Columns.Add("c1", "الاسم");
            _grid.Columns.Add("c2", "البداية");
            _grid.Columns.Add("c3", "النهاية");
            _grid.Columns.Add("c4", "ليوم تالٍ");
            _grid.Columns.Add("c5", "المدة (س)");
            _grid.Columns.Add("c6", "الطبيعية (س)");
            _grid.Columns.Add("c7", "الراحة (د)");
            _grid.Columns.Add("c8", "الصنف");
            _grid.Columns.Add("c9", "بصمة إلزامية");
            _grid.Columns.Add("c10", "الحالة");
            _grid.Columns[0].FillWeight = 7;
            _grid.Columns[1].FillWeight = 20;
            _grid.Columns[2].FillWeight = 7;
            _grid.Columns[3].FillWeight = 7;
            _grid.Columns[4].FillWeight = 7;
            _grid.Columns[5].FillWeight = 8;
            _grid.Columns[6].FillWeight = 8;
            _grid.Columns[7].FillWeight = 8;
            _grid.Columns[8].FillWeight = 8;
            _grid.Columns[9].FillWeight = 8;
            _grid.Columns[10].FillWeight = 7;
            _grid.Rows.Clear();
            if (_rows == null)
            {
                return;
            }
            foreach (Shift shift in _rows)
            {
                int index = _grid.Rows.Add(
                    shift.ShiftCode,
                    shift.ShiftName,
                    TimeText(shift.StartTime),
                    TimeText(shift.EndTime),
                    shift.CrossDay == true ? "نعم" : "—",
                    FormatHours(shift.DurationHrs),
                    FormatHours(shift.NormalHours),
                    shift.BreakMinutes == null ? "0" : Convert.ToString(shift.BreakMinutes.Value),
                    CategoryText(shift.CategoryCode),
                    YesNo(shift.RequireCheck),
                    shift.IsActive == false ? "معطلة" : "نشطة");
                _grid.Rows[index].Tag = shift;
                if (shift.IsActive == false)
                {
                    _grid.Rows[index].Cells[10].Style.ForeColor = Theme.StatusWarn;
                }
            }
        }

        private static string FormatHours(float? hours)
        {
            if (hours == null)
            {
                return "—";
            }
            return hours.Value.ToString("0.##");
        }

        private void OnAddClick(object sender, EventArgs args)
        {
            try
            {
                using (FrmShiftEdit dialog = new FrmShiftEdit(null))
                {
                    if (dialog.ShowDialog(this) == DialogResult.OK)
                    {
                        Theme.ShowInfo("تمت إضافة الوردية بنجاح.");
                        Reload();
                    }
                }
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmShifts.OnAddClick");
                Theme.ShowError("حدث خطأ أثناء الإضافة. تم حفظ التفاصيل في السجلات.");
            }
        }

        private void OnEditClick(object sender, EventArgs args)
        {
            try
            {
                Shift shift = SelectedShift();
                if (shift == null)
                {
                    return;
                }
                using (FrmShiftEdit dialog = new FrmShiftEdit(shift.ShiftId))
                {
                    if (dialog.ShowDialog(this) == DialogResult.OK)
                    {
                        Theme.ShowInfo("تم حفظ التعديلات بنجاح.");
                        Reload();
                    }
                }
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmShifts.OnEditClick");
                Theme.ShowError("حدث خطأ أثناء التعديل. تم حفظ التفاصيل في السجلات.");
            }
        }

        private void OnToggleClick(object sender, EventArgs args)
        {
            try
            {
                Shift shift = SelectedShift();
                if (shift == null)
                {
                    return;
                }
                Shift fresh = ShiftBLL.GetById(shift.ShiftId);
                if (fresh == null)
                {
                    Theme.ShowError("الوردية غير موجودة.");
                    Reload();
                    return;
                }
                bool activate = fresh.IsActive == false;
                fresh.IsActive = activate;
                ShiftBLL.Update(fresh);
                Theme.ShowInfo(activate ? "تم تفعيل الوردية." : "تم تعطيل الوردية.");
                Reload();
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmShifts.OnToggleClick");
                Theme.ShowError("حدث خطأ أثناء تغيير الحالة. تم حفظ التفاصيل في السجلات.");
            }
        }

        private void OnDeleteClick(object sender, EventArgs args)
        {
            try
            {
                Shift shift = SelectedShift();
                if (shift == null)
                {
                    return;
                }
                bool confirmed = Theme.Confirm("هل تريد حذف الوردية '" + shift.ShiftCode + " - " + shift.ShiftName + "'؟");
                if (!confirmed)
                {
                    return;
                }
                ShiftBLL.Delete(shift.ShiftId);
                Theme.ShowInfo("تم حذف الوردية.");
                Reload();
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmShifts.OnDeleteClick");
                Theme.ShowError("حدث خطأ أثناء الحذف. تم حفظ التفاصيل في السجلات.");
            }
        }
    }
}
