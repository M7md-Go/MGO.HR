using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using HR.BLL;
using HR.Entities;
using HR.Helpers;

namespace HR.UI.UI
{
    /// <summary>
    /// Employee attachments manager (add / view / delete / open): files are copied
    /// into the application "Attachments" folder and stored as relative paths, so
    /// the installation stays portable and backups stay consistent.
    /// </summary>
    public class FrmEmployeeFiles : Form
    {
        private readonly int _employeeId;
        private readonly bool _canManage;
        private DataGridView _grid;
        private List<EmployeeFile> _rows;
        private TextBox _txtCategory;
        private TextBox _txtNotes;

        public FrmEmployeeFiles(int employeeId, string employeeName)
        {
            _employeeId = employeeId;
            _canManage = AppContext.HasPermission("Employees.Edit");

            Theme.Apply(this); // dialog under an MDI shell: mirrored RTL layout works here
            Text = "مرفقات الموظف: " + employeeName;
            ClientSize = new Size(640, 500);
            StartPosition = FormStartPosition.CenterParent;

            Panel header = new Panel();
            Label lblTitle = new Label();
            lblTitle.Text = "مرفقات الموظف: " + employeeName;
            lblTitle.ForeColor = Theme.HeaderTextColor;
            lblTitle.Font = Theme.Font(11.5f);
            lblTitle.Dock = DockStyle.Fill;
            lblTitle.TextAlign = ContentAlignment.MiddleCenter;
            Theme.StyleHeader(header);
            header.Controls.Add(lblTitle);
            header.Dock = DockStyle.Top;

            FlowLayoutPanel toolbar = new FlowLayoutPanel();
            toolbar.Dock = DockStyle.Top;
            toolbar.Height = 96;
            toolbar.BackColor = Theme.ToolbarColor;
            // FlowLayoutPanel: RTL=Yes (inherited from the form) already reverses the flow;
            // an explicit RightToLeft cancelled that reversal and rendered the bar left-to-right.
            toolbar.FlowDirection = FlowDirection.LeftToRight;
            toolbar.WrapContents = false;
            toolbar.Padding = new Padding(6, 8, 6, 4);

            Label lblCategory = new Label();
            lblCategory.Text = "التصنيف:";
            lblCategory.AutoSize = true;
            lblCategory.Margin = new Padding(3, 10, 3, 0);

            _txtCategory = new TextBox();
            _txtCategory.Width = 130;
            _txtCategory.MaxLength = 50;
            _txtCategory.Margin = new Padding(3, 8, 3, 0);

            Label lblNotes = new Label();
            lblNotes.Text = "ملاحظات:";
            lblNotes.AutoSize = true;
            lblNotes.Margin = new Padding(3, 10, 3, 0);

            _txtNotes = new TextBox();
            _txtNotes.Width = 220;
            _txtNotes.MaxLength = 500;
            _txtNotes.Margin = new Padding(3, 8, 3, 0);

            Button btnAdd = new Button();
            btnAdd.Text = "إضافة ملف…";
            btnAdd.Height = 30;
            btnAdd.Width = 110;
            btnAdd.Click += OnAddClick;
            btnAdd.Enabled = _canManage;

            Button btnDelete = new Button();
            btnDelete.Text = "حذف";
            btnDelete.Height = 30;
            btnDelete.Width = 80;
            btnDelete.Click += OnDeleteClick;
            btnDelete.Enabled = _canManage;

            Button btnOpen = new Button();
            btnOpen.Text = "فتح الملف";
            btnOpen.Height = 30;
            btnOpen.Width = 90;
            btnOpen.Click += OnOpenClick;

            Button btnClose = new Button();
            btnClose.Text = "إغلاق";
            btnClose.Height = 30;
            btnClose.Width = 80;
            btnClose.Click += delegate { Close(); };

            toolbar.Controls.Add(lblCategory);
            toolbar.Controls.Add(_txtCategory);
            toolbar.Controls.Add(lblNotes);
            toolbar.Controls.Add(_txtNotes);
            toolbar.Controls.Add(btnAdd);
            toolbar.Controls.Add(btnDelete);
            toolbar.Controls.Add(btnOpen);
            toolbar.Controls.Add(btnClose);

            _grid = new DataGridView();
            Theme.StyleGrid(_grid);
            _grid.Dock = DockStyle.Fill;
            _grid.ColumnHeadersVisible = true;
            _grid.AutoGenerateColumns = false;
            DataGridViewTextBoxColumn colCategory = new DataGridViewTextBoxColumn();
            colCategory.Name = "Category";
            colCategory.HeaderText = "التصنيف";
            colCategory.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            colCategory.FillWeight = 20;
            DataGridViewTextBoxColumn colPath = new DataGridViewTextBoxColumn();
            colPath.Name = "FilePath";
            colPath.HeaderText = "الملف";
            colPath.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            colPath.FillWeight = 45;
            DataGridViewTextBoxColumn colNotes = new DataGridViewTextBoxColumn();
            colNotes.Name = "Notes";
            colNotes.HeaderText = "ملاحظات";
            colNotes.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            colNotes.FillWeight = 25;
            DataGridViewTextBoxColumn colCreated = new DataGridViewTextBoxColumn();
            colCreated.Name = "CreatedAt";
            colCreated.HeaderText = "أضيف في";
            colCreated.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            colCreated.FillWeight = 10;
            _grid.Columns.AddRange(new DataGridViewColumn[] { colCategory, colPath, colNotes, colCreated });
            _grid.CellDoubleClick += delegate { OnOpenClick(null, null); };

            Controls.Add(_grid);
            Controls.Add(toolbar);
            Controls.Add(header);

            Shown += delegate { Reload(); };
        }

        private void Reload()
        {
            try
            {
                _rows = EmployeeFileBLL.List(_employeeId);
                if (IsDisposed || _grid == null || _grid.IsDisposed)
                {
                    return;
                }
                _grid.Rows.Clear();
                foreach (EmployeeFile file in _rows)
                {
                    _grid.Rows.Add(file.Category ?? "", file.FilePath, file.Notes ?? "",
                        file.CreatedAt.HasValue ? file.CreatedAt.Value.ToString("yyyy-MM-dd HH:mm") : "");
                }
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmEmployeeFiles.Reload");
                Theme.ShowError("حدث خطأ غير متوقع أثناء تحميل المرفقات.");
            }
        }

        private void OnAddClick(object sender, EventArgs args)
        {
            try
            {
                using (OpenFileDialog dialog = new OpenFileDialog())
                {
                    dialog.Title = "اختيار مرفق للموظف";
                    dialog.Multiselect = false;
                    if (dialog.ShowDialog(this) != DialogResult.OK)
                    {
                        return;
                    }
                    string sourcePath = dialog.FileName;
                    string folder = EmployeeFileBLL.AttachmentsFolder();
                    if (!Directory.Exists(folder))
                    {
                        Directory.CreateDirectory(folder);
                    }
                    string safeName = _employeeId + "_" + DateTime.Now.ToString("yyyyMMddHHmmss") +
                        Path.GetExtension(sourcePath);
                    string targetPath = Path.Combine(folder, safeName);
                    File.Copy(sourcePath, targetPath, true);
                    EmployeeFileBLL.Add(_employeeId, Path.Combine("Attachments", safeName),
                        _txtCategory.Text, _txtNotes.Text);
                }
                Reload();
                Theme.ShowInfo("تمت إضافة المرفق بنجاح.");
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmEmployeeFiles.OnAddClick");
                Theme.ShowError("حدث خطأ أثناء إضافة المرفق. تم حفظ التفاصيل في السجلات.");
            }
        }

        private void OnDeleteClick(object sender, EventArgs args)
        {
            try
            {
                if (_grid.CurrentRow == null || _rows == null || _grid.CurrentRow.Index >= _rows.Count)
                {
                    return;
                }
                EmployeeFile selected = _rows[_grid.CurrentRow.Index];
                if (!Theme.Confirm("هل تريد حذف المرفق «" + selected.FilePath + "»؟"))
                {
                    return;
                }
                EmployeeFileBLL.Delete(selected.FileId, _employeeId);
                Reload();
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmEmployeeFiles.OnDeleteClick");
                Theme.ShowError("حدث خطأ أثناء حذف المرفق. تم حفظ التفاصيل في السجلات.");
            }
        }

        private void OnOpenClick(object sender, EventArgs args)
        {
            try
            {
                if (_grid.CurrentRow == null || _rows == null || _grid.CurrentRow.Index >= _rows.Count)
                {
                    return;
                }
                string path = EmployeeFileBLL.ResolvePath(_rows[_grid.CurrentRow.Index].FilePath);
                if (path == null)
                {
                    Theme.ShowError("لم يتم العثور على الملف الفعلي. ربما تم نقله أو حذفه من مجلد المرفقات.");
                    return;
                }
                System.Diagnostics.Process.Start(path);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmEmployeeFiles.OnOpenClick");
                Theme.ShowError("تعذر فتح الملف. تحقق من وجود برنامج مطابق لنوع الملف.");
            }
        }
    }
}
