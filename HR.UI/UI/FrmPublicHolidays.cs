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
    /// Official holiday calendar management ([PublicHolidays]) — the attendance
    /// engine counts these days as paid non-working days. Year filter plus
    /// add/edit/delete; writes are gated by "Attendance.Edit" inside the BLL.
    /// </summary>
    public class FrmPublicHolidays : Form
    {
        private ComboBox _cboYear;
        private CheckBox _chkAll;
        private Button _btnRefresh;
        private Button _btnAdd;
        private Button _btnEdit;
        private Button _btnDelete;
        private DataGridView _grid;
        private List<PublicHoliday> _rows;

        public FrmPublicHolidays()
        {
            Theme.Apply(this, false); // MDI child: mirrored layout is not applied — RTL flow comes from the containers
            Text = "العطل الرسمية";
            ClientSize = new Size(880, 560);
            StartPosition = FormStartPosition.CenterParent;

            Panel header = new Panel();
            Label lblTitle = new Label();
            lblTitle.Text = "العطل الرسمية — يقفزها محرك الحضور ولا تُحسب غيابًا";
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
            toolbar.WrapContents = false;
            toolbar.Padding = new Padding(6, 8, 6, 4);

            Label lblYear = new Label();
            lblYear.Text = "السنة:";
            lblYear.AutoSize = true;
            lblYear.Margin = new Padding(3, 10, 3, 0);

            _cboYear = new ComboBox();
            _cboYear.DropDownStyle = ComboBoxStyle.DropDownList;
            _cboYear.Width = 90;
            for (int year = DateTime.Now.Year + 1; year >= DateTime.Now.Year - 8; year--)
            {
                _cboYear.Items.Add(year);
            }
            _cboYear.SelectedIndex = 1; // current year
            _cboYear.Margin = new Padding(3, 8, 3, 0);

            _chkAll = new CheckBox();
            _chkAll.Text = "كل السنوات (مع السنوية المتكررة)";
            _chkAll.AutoSize = true;
            _chkAll.Margin = new Padding(6, 10, 3, 0);

            _btnRefresh = new Button();
            _btnRefresh.Text = "تحديث";
            _btnRefresh.Height = 30;
            _btnRefresh.Click += delegate { Reload(); };

            _btnAdd = new Button();
            _btnAdd.Text = "عطلة جديدة";
            _btnAdd.Height = 30;
            _btnAdd.Click += delegate { OnAddClick(); };

            _btnEdit = new Button();
            _btnEdit.Text = "تعديل";
            _btnEdit.Height = 30;
            _btnEdit.Click += delegate { OnEditClick(); };

            _btnDelete = new Button();
            _btnDelete.Text = "حذف";
            _btnDelete.Height = 30;
            _btnDelete.Click += delegate { OnDeleteClick(); };

            toolbar.Controls.Add(lblYear);
            toolbar.Controls.Add(_cboYear);
            toolbar.Controls.Add(_chkAll);
            toolbar.Controls.Add(_btnRefresh);
            toolbar.Controls.Add(_btnAdd);
            toolbar.Controls.Add(_btnEdit);
            toolbar.Controls.Add(_btnDelete);

            _grid = new DataGridView();
            Theme.StyleGrid(_grid);
            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _grid.CellDoubleClick += delegate { OnEditClick(); };

            // management quick menu on the grid
            ContextMenuStrip gridMenu = new ContextMenuStrip();
            gridMenu.RightToLeft = RightToLeft.Yes;
            gridMenu.Font = Theme.Font(9.5f);
            ToolStripMenuItem miAdd = new ToolStripMenuItem("عطلة جديدة");
            miAdd.Click += delegate { OnAddClick(); };
            ToolStripMenuItem miEdit = new ToolStripMenuItem("تعديل");
            miEdit.Click += delegate { OnEditClick(); };
            ToolStripMenuItem miDelete = new ToolStripMenuItem("حذف");
            miDelete.Click += delegate { OnDeleteClick(); };
            ToolStripMenuItem miPrint = new ToolStripMenuItem("طباعة");
            miPrint.Click += delegate { GridPrintHelper.Print(_grid, "العطل الرسمية"); };
            gridMenu.Items.Add(miAdd);
            gridMenu.Items.Add(miEdit);
            gridMenu.Items.Add(miDelete);
            gridMenu.Items.Add(new ToolStripSeparator());
            gridMenu.Items.Add(miPrint);
            _grid.ContextMenuStrip = gridMenu;

            Controls.Add(_grid);
            Controls.Add(toolbar);
            Controls.Add(header);

            bool canManage = AppContext.HasAnyPermission(new string[] { "Attendance.Holidays", "Attendance.Edit" });
            _btnAdd.Enabled = canManage;
            _btnEdit.Enabled = canManage;
            _btnDelete.Enabled = canManage;
            Reload();
        }

        private PublicHoliday SelectedHoliday()
        {
            if (_grid.CurrentRow == null)
            {
                return null;
            }
            return _grid.CurrentRow.Tag as PublicHoliday;
        }

        private void Reload()
        {
            try
            {
                DateTime? from = null;
                DateTime? to = null;
                if (!_chkAll.Checked && _cboYear.SelectedItem != null)
                {
                    int year = (int)_cboYear.SelectedItem;
                    from = new DateTime(year, 1, 1);
                    to = new DateTime(year, 12, 31);
                }
                _rows = PublicHolidayBLL.List(from, to);
                _grid.Columns.Clear();
                _grid.Rows.Clear();

                AddColumn("Date", "التاريخ", 90);
                AddColumn("Name", "الاسم", 220);
                AddColumn("Recurring", "سنوية متكررة", 90);
                AddColumn("DayName", "اليوم", 90);

                foreach (PublicHoliday holiday in _rows)
                {
                    int rowIndex = _grid.Rows.Add();
                    DataGridViewRow row = _grid.Rows[rowIndex];
                    row.Tag = holiday;
                    bool recurring = holiday.IsRecurring == true;
                    row.Cells["Date"].Value = recurring ? "كل سنة — " +
                        (holiday.HolidayDate == null ? "" : holiday.HolidayDate.Value.ToString("MM-dd")) :
                        (holiday.HolidayDate == null ? "" : holiday.HolidayDate.Value.ToString("yyyy-MM-dd"));
                    row.Cells["Name"].Value = holiday.HolidayName;
                    row.Cells["Recurring"].Value = recurring ? "نعم" : "لا";
                    row.Cells["DayName"].Value = recurring || holiday.HolidayDate == null ? "—" :
                        DescribeDayName(holiday.HolidayDate.Value.DayOfWeek);
                }
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmPublicHolidays.Reload");
                Theme.ShowError("حدث خطأ أثناء تحميل العطل الرسمية.");
            }
        }

        private void OnAddClick()
        {
            FrmHolidayEdit dialog = new FrmHolidayEdit(null);
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                Reload();
            }
        }

        private void OnEditClick()
        {
            PublicHoliday holiday = SelectedHoliday();
            if (holiday == null)
            {
                Theme.ShowInfo("اختر عطلة من القائمة أولاً.");
                return;
            }
            FrmHolidayEdit dialog = new FrmHolidayEdit(holiday.HolidayId);
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                Reload();
            }
        }

        private void OnDeleteClick()
        {
            PublicHoliday holiday = SelectedHoliday();
            if (holiday == null)
            {
                Theme.ShowInfo("اختر عطلة من القائمة أولاً.");
                return;
            }
            if (!Theme.Confirm("حذف العطلة '" + holiday.HolidayName + "'؟ سيعيد الحساب اعتبار يومها يوم عمل عادي."))
            {
                return;
            }
            try
            {
                PublicHolidayBLL.Delete(holiday.HolidayId);
                Reload();
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmPublicHolidays.OnDeleteClick");
                Theme.ShowError("حدث خطأ أثناء حذف العطلة.");
            }
        }

        /// <summary>Arabic weekday name for the grid.</summary>
        private static string DescribeDayName(DayOfWeek day)
        {
            string[] arabic = new string[] { "الأحد", "الاثنين", "الثلاثاء", "الأربعاء", "الخميس", "الجمعة", "السبت" };
            return arabic[(int)day];
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
    }
}
