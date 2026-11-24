using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using HR.BLL;
using HR.Helpers;

namespace HR.UI.UI
{
    /// <summary>
    /// Read-only structure dialog of one table: columns (name, Jet type,
    /// length, nullability, primary key) and its indexes. Absolute key
    /// relationships come from MSysRelationships when Jet permissions allow.
    /// </summary>
    public class FrmDbStructure : Form
    {
        private readonly string _tableName;
        private DataGridView _gridColumns;
        private DataGridView _gridIndexes;
        private Label _lblRelations;

        public FrmDbStructure(string tableName)
        {
            _tableName = tableName;
            Theme.Apply(this);
            Text = "هيكل الجدول: " + tableName;
            FormBorderStyle = FormBorderStyle.Sizable;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(760, 560);
            MinimizeBox = false;

            Label lblColumns = new Label();
            lblColumns.Text = "الأعمدة:";
            lblColumns.Location = new Point(20, 16);
            lblColumns.AutoSize = true;

            _gridColumns = new DataGridView();
            Theme.StyleGrid(_gridColumns);
            _gridColumns.Location = new Point(20, 40);
            _gridColumns.Size = new Size(720, 220);
            _gridColumns.ReadOnly = true;
            _gridColumns.AllowUserToAddRows = false;
            _gridColumns.AllowUserToDeleteRows = false;
            _gridColumns.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _gridColumns.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            Label lblIndexes = new Label();
            lblIndexes.Text = "الفهارس والمفاتيح:";
            lblIndexes.Location = new Point(20, 274);
            lblIndexes.AutoSize = true;

            _gridIndexes = new DataGridView();
            Theme.StyleGrid(_gridIndexes);
            _gridIndexes.Location = new Point(20, 298);
            _gridIndexes.Size = new Size(720, 150);
            _gridIndexes.ReadOnly = true;
            _gridIndexes.AllowUserToAddRows = false;
            _gridIndexes.AllowUserToDeleteRows = false;
            _gridIndexes.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _gridIndexes.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            _lblRelations = new Label();
            _lblRelations.Text = "";
            _lblRelations.Location = new Point(20, 462);
            _lblRelations.Size = new Size(720, 56);
            _lblRelations.ForeColor = Theme.HeaderTextColor;

            Button btnClose = new Button();
            btnClose.Text = "إغلاق";
            btnClose.Location = new Point(610, 522);
            btnClose.Size = new Size(130, 32);
            btnClose.DialogResult = DialogResult.Cancel;
            btnClose.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;

            Controls.Add(lblColumns);
            Controls.Add(_gridColumns);
            Controls.Add(lblIndexes);
            Controls.Add(_gridIndexes);
            Controls.Add(_lblRelations);
            Controls.Add(btnClose);
            CancelButton = btnClose;

            Reload();
        }

        private void Reload()
        {
            try
            {
                _gridColumns.Columns.Clear();
                _gridColumns.Rows.Clear();
                AddHead(_gridColumns, "Name", "العمود", 180);
                AddHead(_gridColumns, "Type", "النوع", 150);
                AddHead(_gridColumns, "Len", "الطول", 70);
                AddHead(_gridColumns, "Null", "يقبل فراغ", 80);
                AddHead(_gridColumns, "Pk", "مفتاح أساسي", 90);
                foreach (DbSchema.ColumnInfo column in DatabaseBLL.GetColumns(_tableName))
                {
                    int rowIndex = _gridColumns.Rows.Add();
                    DataGridViewRow row = _gridColumns.Rows[rowIndex];
                    row.Cells["Name"].Value = column.Name;
                    row.Cells["Type"].Value = column.TypeName;
                    row.Cells["Len"].Value = column.MaxChars == null ? "—" : column.MaxChars.Value.ToString();
                    row.Cells["Null"].Value = column.Nullable ? "نعم" : "لا";
                    row.Cells["Pk"].Value = column.PrimaryKey ? "✔" : "";
                }

                _gridIndexes.Columns.Clear();
                _gridIndexes.Rows.Clear();
                AddHead(_gridIndexes, "Name", "الفهرس", 200);
                AddHead(_gridIndexes, "Column", "العمود", 180);
                AddHead(_gridIndexes, "Unique", "فريد", 70);
                AddHead(_gridIndexes, "Primary", "أساسي", 70);
                foreach (DbSchema.IndexInfo index in DatabaseBLL.GetIndexes(_tableName))
                {
                    int rowIndex = _gridIndexes.Rows.Add();
                    DataGridViewRow row = _gridIndexes.Rows[rowIndex];
                    row.Cells["Name"].Value = index.IndexName;
                    row.Cells["Column"].Value = index.ColumnName;
                    row.Cells["Unique"].Value = index.Unique ? "✔" : "";
                    row.Cells["Primary"].Value = index.Primary ? "✔" : "";
                }

                List<string> relations = DatabaseBLL.Relationships();
                if (relations == null)
                {
                    _lblRelations.Text = "العلاقات المرجعية: غير متاحة للعرض ضمن أذونات Jet الافتراضية " +
                        "(القيود مفروضة ضمن السكيما في Database/ModernTime_Schema.sql).";
                }
                else if (relations.Count == 0)
                {
                    _lblRelations.Text = "العلاقات المرجعية: لا توجد قيود مرجعية مسجّلة.";
                }
                else
                {
                    _lblRelations.Text = "العلاقات المرجعية (" + relations.Count + "): " +
                        string.Join("، ", relations.ToArray());
                }
            }
            catch (BusinessException expected)
            {
                Theme.ShowError(expected.Message);
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmDbStructure.Reload");
                Theme.ShowError("حدث خطأ أثناء قراءة هيكل الجدول.");
            }
        }

        private static void AddHead(DataGridView grid, string name, string header, int fillWeight)
        {
            DataGridViewTextBoxColumn column = new DataGridViewTextBoxColumn();
            column.Name = name;
            column.HeaderText = header;
            column.FillWeight = fillWeight;
            column.SortMode = DataGridViewColumnSortMode.NotSortable;
            grid.Columns.Add(column);
        }
    }
}
