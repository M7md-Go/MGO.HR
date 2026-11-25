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
    /// Employee add/edit screen (rule 5) with all data fields, organization lookups,
    /// date validation and Arabic validation messages handled by EmployeeBLL.
    /// Lookup fields are driven by Dictionary&lt;int,string&gt; per entity (rules 32/33):
    /// each combo binds the real (Id, Name) pairs through BindingSource, so the saved
    /// value is always the entity Id — never an index, a name match, or reflection.
    /// </summary>
    public class FrmEmployeeEdit : Form
    {
        /// <summary>Sentinel entry filling optional lookup combos (Id 0 = empty).</summary>
        private const string OptionNone = "(بدون)";

        private static readonly string[] GenderLabels = new string[] { "ذكر", "أنثى" };
        private static readonly string[] EmploymentTypeLabels = new string[] { "دائم", "عقد مؤقت", "دوام جزئي", "متدرب" };
        private static readonly string[] EmploymentTypeValues = new string[] { "Permanent", "Contract", "PartTime", "Intern" };

        private readonly int? _employeeId;
        private TextBox _txtCode;
        private TextBox _txtNationalId;
        private TextBox _txtNameAr;
        private TextBox _txtNameEn;
        private ComboBox _cboGender;
        private DateTimePicker _dtpBirthDate;
        private DateTimePicker _dtpHireDate;
        private ComboBox _cboCompany;
        private ComboBox _cboBranch;
        private ComboBox _cboDepartment;
        private ComboBox _cboSection;
        private ComboBox _cboJobTitle;
        private ComboBox _cboActualJob;
        private ComboBox _cboStatus;
        private ComboBox _cboQualification;
        private ComboBox _cboEmploymentType;
        private TextBox _txtPhone;
        private TextBox _txtEmail;
        private TextBox _txtAddress;
        private TextBox _txtEnroll;
        private TextBox _txtShiftGroup;
        private NumericUpDown _numContractHours;
        private NumericUpDown _numSalary;
        private TextBox _txtNotes;
        private DateTimePicker _dtpFirstWork;
        private DateTimePicker _dtpNominalHire;
        private DateTimePicker _dtpJobKeep;
        private PictureBox _photoBox;
        private string _photoPath; // stored relative to the app folder (portable)

        // Full lookup dictionaries: real Id -> display name for every entity.
        private Dictionary<int, string> _companyNames;
        private Dictionary<int, string> _branchNames;
        private Dictionary<int, int> _branchCompanies;
        private Dictionary<int, string> _departmentNames;
        private Dictionary<int, string> _sectionNames;
        private Dictionary<int, int> _sectionDepartments;
        private Dictionary<int, string> _jobTitleNames;
        private Dictionary<int, string> _actualJobNames;
        private Dictionary<int, int> _actualJobDepartments;
        private Dictionary<int, string> _statusNames;
        private Dictionary<int, string> _qualificationNames;

        public FrmEmployeeEdit(int? employeeId)
        {
            _employeeId = employeeId;
            Theme.Apply(this);
            Text = employeeId == null ? "إضافة موظف جديد" : "تعديل بيانات موظف";
            FormBorderStyle = FormBorderStyle.Sizable;
            StartPosition = FormStartPosition.CenterParent;
            // Small screens: the form scrolls instead of pushing the save buttons
            // out of view, and the initial height is clamped to the working area.
            AutoScroll = true;
            int workingHeight = Screen.PrimaryScreen.WorkingArea.Height;
            ClientSize = new Size(760, Math.Min(760, workingHeight - 90));
            MinimumSize = new Size(800, 540);

            BuildControls();
            LoadLookups();
            if (employeeId != null)
            {
                LoadEmployee(employeeId.Value);
            }
        }

        // ---------------- control construction ----------------

        private static Label FieldLabel(string caption, int x, int y)
        {
            Label label = new Label();
            label.Text = caption;
            label.Location = new Point(x, y + 4);
            label.Size = new Size(140, 22);
            return label;
        }

        private static TextBox FieldBox(int x, int y)
        {
            TextBox box = new TextBox();
            box.Location = new Point(x, y);
            box.Size = new Size(200, 26);
            return box;
        }

        private static ComboBox FieldCombo(int x, int y)
        {
            ComboBox combo = new ComboBox();
            combo.Location = new Point(x, y);
            combo.Size = new Size(200, 26);
            combo.DropDownStyle = ComboBoxStyle.DropDownList;
            return combo;
        }

        private static DateTimePicker FieldDate(int x, int y, bool showCheckBox)
        {
            DateTimePicker picker = new DateTimePicker();
            picker.Location = new Point(x, y);
            picker.Size = new Size(200, 26);
            picker.Format = DateTimePickerFormat.Short;
            picker.ShowCheckBox = showCheckBox;
            return picker;
        }

        private void BuildControls()
        {
            int labelX = 20;
            int fieldX = 165;
            int col2LabelX = 390;
            int col2FieldX = 535;

            _txtCode = FieldBox(fieldX, 20);
            _txtCode.RightToLeft = RightToLeft.No;
            _txtCode.MaxLength = 20;
            Controls.Add(FieldLabel("كود الموظف *:", labelX, 20));
            Controls.Add(_txtCode);

            _txtNationalId = FieldBox(fieldX, 55);
            _txtNationalId.RightToLeft = RightToLeft.No;
            _txtNationalId.MaxLength = 20;
            Controls.Add(FieldLabel("الرقم القومي:", labelX, 55));
            Controls.Add(_txtNationalId);

            _txtNameAr = FieldBox(fieldX, 90);
            Controls.Add(FieldLabel("الاسم بالعربية *:", labelX, 90));
            Controls.Add(_txtNameAr);

            _txtNameEn = FieldBox(fieldX, 125);
            _txtNameEn.RightToLeft = RightToLeft.No;
            Controls.Add(FieldLabel("الاسم بالإنجليزية:", labelX, 125));
            Controls.Add(_txtNameEn);

            _cboGender = FieldCombo(fieldX, 160);
            foreach (string gender in GenderLabels)
            {
                _cboGender.Items.Add(gender);
            }
            Controls.Add(FieldLabel("النوع:", labelX, 160));
            Controls.Add(_cboGender);

            _dtpBirthDate = FieldDate(fieldX, 195, true);
            Controls.Add(FieldLabel("تاريخ الميلاد:", labelX, 195));
            Controls.Add(_dtpBirthDate);

            _dtpHireDate = FieldDate(fieldX, 230, false);
            _dtpHireDate.Value = DateTime.Today;
            Controls.Add(FieldLabel("تاريخ التعيين *:", labelX, 230));
            Controls.Add(_dtpHireDate);

            _cboCompany = FieldCombo(fieldX, 265);
            _cboCompany.SelectedIndexChanged += HandleCompanyChanged;
            Controls.Add(FieldLabel("الشركة *:", labelX, 265));
            Controls.Add(_cboCompany);

            _cboBranch = FieldCombo(fieldX, 300);
            Controls.Add(FieldLabel("الفرع:", labelX, 300));
            Controls.Add(_cboBranch);

            _cboDepartment = FieldCombo(fieldX, 335);
            _cboDepartment.SelectedIndexChanged += HandleDepartmentChanged;
            Controls.Add(FieldLabel("القسم *:", labelX, 335));
            Controls.Add(_cboDepartment);

            _cboSection = FieldCombo(fieldX, 370);
            Controls.Add(FieldLabel("القطاع:", labelX, 370));
            Controls.Add(_cboSection);

            _cboJobTitle = FieldCombo(fieldX, 405);
            Controls.Add(FieldLabel("المسمى الوظيفي *:", labelX, 405));
            Controls.Add(_cboJobTitle);

            _cboActualJob = FieldCombo(fieldX, 440);
            Controls.Add(FieldLabel("الوظيفة الفعلية:", labelX, 440));
            Controls.Add(_cboActualJob);

            _cboStatus = FieldCombo(col2FieldX, 20);
            Controls.Add(FieldLabel("حالة الموظف *:", col2LabelX, 20));
            Controls.Add(_cboStatus);

            _cboQualification = FieldCombo(col2FieldX, 55);
            Controls.Add(FieldLabel("المؤهل:", col2LabelX, 55));
            Controls.Add(_cboQualification);

            _cboEmploymentType = FieldCombo(col2FieldX, 90);
            foreach (string label in EmploymentTypeLabels)
            {
                _cboEmploymentType.Items.Add(label);
            }
            _cboEmploymentType.SelectedIndex = 0;
            Controls.Add(FieldLabel("نوع التعيين:", col2LabelX, 90));
            Controls.Add(_cboEmploymentType);

            _txtPhone = FieldBox(col2FieldX, 125);
            _txtPhone.RightToLeft = RightToLeft.No;
            Controls.Add(FieldLabel("الهاتف:", col2LabelX, 125));
            Controls.Add(_txtPhone);

            _txtEmail = FieldBox(col2FieldX, 160);
            _txtEmail.RightToLeft = RightToLeft.No;
            Controls.Add(FieldLabel("البريد الإلكتروني:", col2LabelX, 160));
            Controls.Add(_txtEmail);

            _txtAddress = FieldBox(col2FieldX, 195);
            Controls.Add(FieldLabel("العنوان:", col2LabelX, 195));
            Controls.Add(_txtAddress);

            _txtEnroll = FieldBox(col2FieldX, 230);
            _txtEnroll.RightToLeft = RightToLeft.No;
            Controls.Add(FieldLabel("رقم البصمة:", col2LabelX, 230));
            Controls.Add(_txtEnroll);

            _txtShiftGroup = FieldBox(col2FieldX, 265);
            _txtShiftGroup.RightToLeft = RightToLeft.No;
            Controls.Add(FieldLabel("مجموعة الورديات:", col2LabelX, 265));
            Controls.Add(_txtShiftGroup);

            _numContractHours = new NumericUpDown();
            _numContractHours.Location = new Point(col2FieldX, 300);
            _numContractHours.Size = new Size(200, 26);
            _numContractHours.Minimum = 1;
            _numContractHours.Maximum = 12;
            _numContractHours.Value = 6;
            _numContractHours.DecimalPlaces = 1;
            Controls.Add(FieldLabel("ساعات العقد *:", col2LabelX, 300));
            Controls.Add(_numContractHours);

            _numSalary = new NumericUpDown();
            _numSalary.Location = new Point(col2FieldX, 335);
            _numSalary.Size = new Size(200, 26);
            _numSalary.Minimum = 0;
            _numSalary.Maximum = 999999;
            _numSalary.DecimalPlaces = 2;
            Controls.Add(FieldLabel("الراتب:", col2LabelX, 335));
            Controls.Add(_numSalary);

            _dtpFirstWork = FieldDate(col2FieldX, 370, true);
            Controls.Add(FieldLabel("تاريخ أول استلام عمل:", col2LabelX, 370));
            Controls.Add(_dtpFirstWork);

            _dtpNominalHire = FieldDate(col2FieldX, 405, true);
            Controls.Add(FieldLabel("تاريخ التعيين الفرضي:", col2LabelX, 405));
            Controls.Add(_dtpNominalHire);

            _dtpJobKeep = FieldDate(col2FieldX, 440, true);
            Controls.Add(FieldLabel("تاريخ حفظ الوظيفة:", col2LabelX, 440));
            Controls.Add(_dtpJobKeep);

            _txtNotes = new TextBox();
            _txtNotes.Location = new Point(fieldX, 470);
            _txtNotes.Size = new Size(515, 50);
            _txtNotes.Multiline = true;
            Controls.Add(FieldLabel("ملاحظات:", labelX, 470));
            Controls.Add(_txtNotes);

            // employee photo: preview + pick + clear (stored as relative app path)
            _photoBox = new PictureBox();
            _photoBox.Location = new Point(fieldX, 530);
            _photoBox.Size = new Size(110, 140);
            _photoBox.BorderStyle = BorderStyle.FixedSingle;
            _photoBox.SizeMode = PictureBoxSizeMode.Zoom;
            _photoBox.BackColor = Color.FromArgb(244, 246, 250);
            Controls.Add(FieldLabel("صورة الموظف:", labelX, 530));
            Controls.Add(_photoBox);

            Button btnPickPhoto = new Button();
            btnPickPhoto.Text = "اختيار صورة…";
            btnPickPhoto.Location = new Point(290, 540);
            btnPickPhoto.Size = new Size(120, 30);
            btnPickPhoto.Click += OnPickPhotoClick;
            Controls.Add(btnPickPhoto);

            Button btnClearPhoto = new Button();
            btnClearPhoto.Text = "إزالة الصورة";
            btnClearPhoto.Location = new Point(290, 575);
            btnClearPhoto.Size = new Size(120, 30);
            btnClearPhoto.Click +=delegate { SetPhoto(null); };
            Controls.Add(btnClearPhoto);

            // persistent bottom bar: the save/cancel buttons can never scroll
            // away, working at every window size (mirrored RTL: same order as
            // the other dialogs — cancel logically left, save logically right).
            Panel buttonBar = new Panel();
            buttonBar.Dock = DockStyle.Bottom;
            buttonBar.Height = 48;
            buttonBar.BackColor = Theme.ToolbarColor;

            Button btnSave = new Button();
            btnSave.Text = "حفظ";
            btnSave.Size = new Size(120, 32);
            btnSave.Location = new Point(20, 8);
            btnSave.Click += OnSaveClick;
            buttonBar.Controls.Add(btnSave);

            Button btnCancel = new Button();
            btnCancel.Text = "إلغاء";
            btnCancel.Size = new Size(120, 32);
            btnCancel.Location = new Point(620, 8);
            btnCancel.Click += delegate { DialogResult = DialogResult.Cancel; };
            buttonBar.Controls.Add(btnCancel);

            // pin the cancel button to the (logical) right side at every width —
            // the bar is mirrored RTL, so this keeps cancel visually at the end.
            EventHandler repositionButtons = delegate
            {
                btnCancel.Left = buttonBar.ClientSize.Width - 140;
                btnSave.Left = 20;
            };
            buttonBar.Resize += repositionButtons;

            Controls.Add(buttonBar);
            repositionButtons(null, EventArgs.Empty);
            AcceptButton = btnSave;
            CancelButton = btnCancel;
            btnSave.TabIndex = 99;
            btnCancel.TabIndex = 100;
        }

        // ---------------- lookup loading and combo binding ----------------

        private void LoadLookups()
        {
            List<Company> companies = OrganizationBLL.Companies(false);
            List<Branch> branches = OrganizationBLL.Branches(false);
            List<Department> departments = OrganizationBLL.Departments(false);
            List<Section> sections = OrganizationBLL.Sections(null, false);
            List<JobTitle> jobTitles = OrganizationBLL.JobTitles(false);
            List<ActualJob> actualJobs = OrganizationBLL.ActualJobs(null, false);
            List<EmployeeStatus> statuses = OrganizationBLL.EmployeeStatuses();
            List<Qualification> qualifications = OrganizationBLL.Qualifications();

            _companyNames = BuildNames(companies,
                delegate(Company c) { return c.CompanyId; },
                delegate(Company c) { return Display(c.IsActive, c.CompanyNameAr); });
            _branchNames = BuildNames(branches,
                delegate(Branch b) { return b.BranchId; },
                delegate(Branch b) { return Display(b.IsActive, b.BranchName); });
            _branchCompanies = BuildParents(branches,
                delegate(Branch b) { return b.BranchId; },
                delegate(Branch b) { return b.CompanyId; });
            _departmentNames = BuildNames(departments,
                delegate(Department d) { return d.DepartmentId; },
                delegate(Department d) { return Display(d.IsActive, d.DepartmentName); });
            _sectionNames = BuildNames(sections,
                delegate(Section s) { return s.SectionId; },
                delegate(Section s) { return Display(s.IsActive, s.SectionName); });
            _sectionDepartments = BuildParents(sections,
                delegate(Section s) { return s.SectionId; },
                delegate(Section s) { return s.DepartmentId; });
            _jobTitleNames = BuildNames(jobTitles,
                delegate(JobTitle j) { return j.JobTitleId; },
                delegate(JobTitle j) { return Display(j.IsActive, j.TitleName); });
            _actualJobNames = BuildNames(actualJobs,
                delegate(ActualJob a) { return a.ActualJobId; },
                delegate(ActualJob a) { return Display(a.IsActive, a.JobName); });
            _actualJobDepartments = BuildParents(actualJobs,
                delegate(ActualJob a) { return a.ActualJobId; },
                delegate(ActualJob a) { return a.DepartmentId; });
            _statusNames = BuildNames(statuses,
                delegate(EmployeeStatus s) { return s.EmployeeStatusId; },
                delegate(EmployeeStatus s) { return s.StatusName; });
            _qualificationNames = BuildNames(qualifications,
                delegate(Qualification q) { return q.QualificationId; },
                delegate(Qualification q) { return q.QualName; });

            BindCombo(_cboCompany, _companyNames, true);
            BindCombo(_cboDepartment, _departmentNames, true);
            BindCombo(_cboJobTitle, _jobTitleNames, true);
            BindCombo(_cboStatus, _statusNames, true);
            BindCombo(_cboQualification, OptionalDictionary(_qualificationNames), false);

            FillBranches();
            FillSections();
            FillActualJobs();
        }

        /// <summary>Marks inactive rows with a (معطل) suffix so old references stay visible.</summary>
        private static string Display(bool? isActive, string name)
        {
            if (isActive == false)
            {
                return name + " (معطل)";
            }
            return name;
        }

        private static Dictionary<int, string> BuildNames<T>(List<T> source, Converter<T, int> getId, Converter<T, string> getName) where T : class
        {
            Dictionary<int, string> names = new Dictionary<int, string>();
            foreach (T item in source)
            {
                names[getId(item)] = getName(item);
            }
            return names;
        }

        private static Dictionary<int, int> BuildParents<T>(List<T> source, Converter<T, int> getId, Converter<T, int?> getParentId) where T : class
        {
            Dictionary<int, int> parents = new Dictionary<int, int>();
            foreach (T item in source)
            {
                int? parentId = getParentId(item);
                if (parentId != null)
                {
                    parents[getId(item)] = parentId.Value;
                }
            }
            return parents;
        }

        /// <summary>Wraps values into an optional dictionary with the (بدون) sentinel at Id 0.</summary>
        private static Dictionary<int, string> OptionalDictionary(Dictionary<int, string> source)
        {
            Dictionary<int, string> options = new Dictionary<int, string>();
            options[0] = OptionNone;
            foreach (KeyValuePair<int, string> pair in source)
            {
                options.Add(pair.Key, pair.Value);
            }
            return options;
        }

        /// <summary>
        /// Binds (Id, Name) pairs so the selected Id always travels with the item
        /// through ValueMember = "Key". No index arithmetic, no name matching.
        /// When requireSelection is true the combo starts unselected (-1) so the
        /// user must pick a value explicitly; the sentinel is then absent by design.
        /// </summary>
        private static void BindCombo(ComboBox combo, Dictionary<int, string> options, bool requireSelection)
        {
            if (options == null || options.Count == 0)
            {
                combo.DataSource = null;
                combo.SelectedIndex = -1;
                return;
            }
            if (!requireSelection && !StartsWithSentinel(options))
            {
                options = OptionalDictionary(options);
            }
            Theme.BindDictionary(combo, options);
            if (requireSelection)
            {
                combo.SelectedIndex = -1;
            }
        }

        /// <summary>Single-argument form: binds the combo as an optional (sentinel-first) list.</summary>
        private static void BindCombo(ComboBox combo, Dictionary<int, string> options)
        {
            BindCombo(combo, options, false);
        }

        /// <summary>Checks whether the first entry is the Id-0 sentinel.</summary>
        private static bool StartsWithSentinel(Dictionary<int, string> options)
        {
            foreach (KeyValuePair<int, string> pair in options)
            {
                return pair.Key == 0;
            }
            return false;
        }

        /// <summary>Reads the real Id of the selected item; null for the sentinel or empty selection.</summary>
        private static int? SelectedId(ComboBox combo)
        {
            if (combo.SelectedIndex < 0)
            {
                return null;
            }
            object value = combo.SelectedValue;
            if (value == null)
            {
                return null;
            }
            int id = Convert.ToInt32(value);
            if (id == 0)
            {
                return null;
            }
            return id;
        }

        /// <summary>Reads a mandatory Id; throws an Arabic BusinessException when nothing is selected.</summary>
        private static int RequiredId(ComboBox combo, string caption)
        {
            int? id = SelectedId(combo);
            if (id == null)
            {
                throw new BusinessException("الرجاء اختيار: " + caption);
            }
            return id.Value;
        }

        /// <summary>Selects the row whose Id matches; Id 0 selects the sentinel, null clears the selection.</summary>
        private static void SetComboValue(ComboBox combo, int? id)
        {
            combo.SelectedValue = id == null ? 0 : id.Value;
        }

        // ---------------- cascading fills ----------------

        private void HandleCompanyChanged(object sender, EventArgs args)
        {
            FillBranches();
        }

        private void HandleDepartmentChanged(object sender, EventArgs args)
        {
            FillSections();
            FillActualJobs();
        }

        /// <summary>Fills the branch options limited to the selected company.</summary>
        private void FillBranches()
        {
            if (_branchNames == null)
            {
                return;
            }
            int? companyId = SelectedId(_cboCompany);
            Dictionary<int, string> options = new Dictionary<int, string>();
            options[0] = OptionNone;
            if (companyId != null)
            {
                foreach (KeyValuePair<int, string> pair in _branchNames)
                {
                    int parent;
                    if (_branchCompanies.TryGetValue(pair.Key, out parent) && parent == companyId.Value)
                    {
                        options.Add(pair.Key, pair.Value);
                    }
                }
            }
            BindCombo(_cboBranch, options);
        }

        /// <summary>Fills the section options limited to the selected department.</summary>
        private void FillSections()
        {
            if (_sectionNames == null)
            {
                return;
            }
            int? departmentId = SelectedId(_cboDepartment);
            Dictionary<int, string> options = new Dictionary<int, string>();
            options[0] = OptionNone;
            if (departmentId != null)
            {
                foreach (KeyValuePair<int, string> pair in _sectionNames)
                {
                    int parent;
                    if (_sectionDepartments.TryGetValue(pair.Key, out parent) && parent == departmentId.Value)
                    {
                        options.Add(pair.Key, pair.Value);
                    }
                }
            }
            BindCombo(_cboSection, options);
        }

        /// <summary>Fills the actual job options limited to the selected department.</summary>
        private void FillActualJobs()
        {
            if (_actualJobNames == null)
            {
                return;
            }
            int? departmentId = SelectedId(_cboDepartment);
            Dictionary<int, string> options = new Dictionary<int, string>();
            options[0] = OptionNone;
            if (departmentId != null)
            {
                foreach (KeyValuePair<int, string> pair in _actualJobNames)
                {
                    int parent;
                    if (_actualJobDepartments.TryGetValue(pair.Key, out parent) && parent == departmentId.Value)
                    {
                        options.Add(pair.Key, pair.Value);
                    }
                }
            }
            BindCombo(_cboActualJob, options);
        }

        // ---------------- data loading ----------------

        private void LoadEmployee(int employeeId)
        {
            Employee employee = EmployeeBLL.GetById(employeeId);
            if (employee == null)
            {
                Theme.ShowError("الموظف غير موجود.");
                Close();
                return;
            }
            _txtCode.Text = employee.EmployeeCode ?? string.Empty;
            _txtCode.Enabled = false;
            _txtNationalId.Text = employee.NationalId ?? string.Empty;
            _txtNameAr.Text = employee.FullNameAr ?? string.Empty;
            _txtNameEn.Text = employee.FullNameEn ?? string.Empty;
            for (int index = 0; index < GenderLabels.Length; index++)
            {
                if (string.Equals(employee.Gender, GenderLabels[index], StringComparison.Ordinal))
                {
                    _cboGender.SelectedIndex = index;
                    break;
                }
            }
            if (employee.BirthDate != null)
            {
                _dtpBirthDate.Checked = true;
                _dtpBirthDate.Value = employee.BirthDate.Value;
            }
            if (employee.HireDate != null)
            {
                _dtpHireDate.Value = employee.HireDate.Value;
            }
            SetComboValue(_cboCompany, employee.CompanyId);
            SetComboValue(_cboBranch, employee.BranchId);
            SetComboValue(_cboDepartment, employee.DepartmentId);
            SetComboValue(_cboSection, employee.SectionId);
            SetComboValue(_cboJobTitle, employee.JobTitleId);
            SetComboValue(_cboActualJob, employee.ActualJobId);
            SetComboValue(_cboStatus, employee.EmployeeStatusId);
            SetComboValue(_cboQualification, employee.QualificationId);
            if (!string.IsNullOrEmpty(employee.EmploymentType))
            {
                for (int index = 0; index < EmploymentTypeValues.Length; index++)
                {
                    if (string.Equals(employee.EmploymentType, EmploymentTypeValues[index], StringComparison.Ordinal))
                    {
                        _cboEmploymentType.SelectedIndex = index;
                        break;
                    }
                }
            }
            _txtPhone.Text = employee.Phone ?? string.Empty;
            _txtEmail.Text = employee.Email ?? string.Empty;
            _txtAddress.Text = employee.Address ?? string.Empty;
            _txtEnroll.Text = employee.DeviceEnrollNumber ?? string.Empty;
            _txtShiftGroup.Text = employee.ShiftGroupCode ?? string.Empty;
            if (employee.ContractHours != null)
            {
                _numContractHours.Value = Convert.ToDecimal(employee.ContractHours.Value);
            }
            if (employee.Salary != null)
            {
                _numSalary.Value = employee.Salary.Value;
            }
            _txtNotes.Text = employee.Notes ?? string.Empty;
            if (employee.FirstWorkDate != null)
            {
                _dtpFirstWork.Checked = true;
                _dtpFirstWork.Value = employee.FirstWorkDate.Value;
            }
            if (employee.NominalHireDate != null)
            {
                _dtpNominalHire.Checked = true;
                _dtpNominalHire.Value = employee.NominalHireDate.Value;
            }
            if (employee.JobKeepDate != null)
            {
                _dtpJobKeep.Checked = true;
                _dtpJobKeep.Value = employee.JobKeepDate.Value;
            }
            SetPhoto(employee.PhotoPath); // show the stored photo when editing an existing employee
        }

        // ---------------- save ----------------

        private void OnSaveClick(object sender, EventArgs args)
        {
            try
            {
                Employee employee;
                if (_employeeId != null)
                {
                    employee = EmployeeBLL.GetById(_employeeId.Value);
                }
                else
                {
                    employee = new Employee();
                    employee.EmployeeCode = _txtCode.Text;
                }
                if (employee == null)
                {
                    Theme.ShowError("الموظف غير موجود أو مؤرشف، لا يمكن تعديله.");
                    return;
                }
                employee.NationalId = EmptyToNull(_txtNationalId.Text);
                employee.FullNameAr = EmptyToNull(_txtNameAr.Text);
                employee.FullNameEn = EmptyToNull(_txtNameEn.Text);
                employee.Gender = Convert.ToString(_cboGender.SelectedItem);
                employee.BirthDate = _dtpBirthDate.Checked ? (DateTime?)_dtpBirthDate.Value.Date : null;
                employee.HireDate = _dtpHireDate.Value.Date;
                employee.CompanyId = RequiredId(_cboCompany, "الشركة");
                employee.BranchId = SelectedId(_cboBranch);
                employee.DepartmentId = RequiredId(_cboDepartment, "القسم");
                employee.SectionId = SelectedId(_cboSection);
                employee.JobTitleId = RequiredId(_cboJobTitle, "المسمى الوظيفي");
                employee.ActualJobId = SelectedId(_cboActualJob);
                employee.EmployeeStatusId = RequiredId(_cboStatus, "حالة الموظف");
                employee.QualificationId = SelectedId(_cboQualification);
                employee.EmploymentType = EmploymentTypeToken();
                employee.Phone = EmptyToNull(_txtPhone.Text);
                employee.Email = EmptyToNull(_txtEmail.Text);
                employee.Address = EmptyToNull(_txtAddress.Text);
                employee.DeviceEnrollNumber = EmptyToNull(_txtEnroll.Text);
                employee.ShiftGroupCode = string.IsNullOrWhiteSpace(_txtShiftGroup.Text) ? "G1" : _txtShiftGroup.Text.Trim();
                employee.ContractHours = (float)_numContractHours.Value;
                employee.Salary = _numSalary.Value == 0 ? (decimal?)null : _numSalary.Value;
                employee.Notes = EmptyToNull(_txtNotes.Text);
                employee.PhotoPath = _photoPath;
                employee.FirstWorkDate = _dtpFirstWork.Checked ? (DateTime?)_dtpFirstWork.Value.Date : null;
                employee.NominalHireDate = _dtpNominalHire.Checked ? (DateTime?)_dtpNominalHire.Value.Date : null;
                employee.JobKeepDate = _dtpJobKeep.Checked ? (DateTime?)_dtpJobKeep.Value.Date : null;

                if (_employeeId == null)
                {
                    EmployeeBLL.Add(employee);
                    Theme.ShowInfo("تم إضافة الموظف بنجاح.");
                }
                else
                {
                    EmployeeBLL.Update(employee);
                    Theme.ShowInfo("تم حفظ التعديلات بنجاح.");
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
                Logger.LogError(unexpected, "FrmEmployeeEdit.OnSaveClick");
                Theme.ShowError("حدث خطأ أثناء الحفظ. تم حفظ التفاصيل في السجلات.");
            }
        }

        // ---------------- employee photo ----------------

        private void OnPickPhotoClick(object sender, EventArgs args)
        {
            try
            {
                using (OpenFileDialog dialog = new OpenFileDialog())
                {
                    dialog.Title = "اختيار صورة الموظف";
                    dialog.Filter = "صور|*.jpg;*.jpeg;*.png;*.bmp";
                    if (dialog.ShowDialog(this) != DialogResult.OK)
                    {
                        return;
                    }
                    string folder = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Photos");
                    if (!System.IO.Directory.Exists(folder))
                    {
                        System.IO.Directory.CreateDirectory(folder);
                    }
                    string safeName = (_employeeId != null ? _employeeId.Value.ToString() : "new") + "_" +
                        DateTime.Now.ToString("yyyyMMddHHmmss") + System.IO.Path.GetExtension(dialog.FileName);
                    System.IO.File.Copy(dialog.FileName, System.IO.Path.Combine(folder, safeName), true);
                    SetPhoto(System.IO.Path.Combine("Photos", safeName));
                }
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmEmployeeEdit.OnPickPhotoClick");
                Theme.ShowError("تعذر نسخ الصورة. تأكد من أن الملف صورة صالحة.");
            }
        }

        private void SetPhoto(string storedPath)
        {
            _photoPath = storedPath;
            if (_photoBox.Image != null)
            {
                Image old = _photoBox.Image;
                _photoBox.Image = null;
                old.Dispose();
            }
            if (string.IsNullOrEmpty(storedPath))
            {
                return;
            }
            try
            {
                string resolved = System.IO.Path.IsPathRooted(storedPath) ? storedPath :
                    System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, storedPath);
                if (System.IO.File.Exists(resolved))
                {
                    // GDI+ keeps using the stream for the image lifetime: read the bytes
                    // and hand the image a private MemoryStream that is never disposed.
                    byte[] imageBytes = System.IO.File.ReadAllBytes(resolved);
                    System.IO.MemoryStream memory = new System.IO.MemoryStream(imageBytes);
                    _photoBox.Image = Image.FromStream(memory);
                }
            }
            catch (Exception unexpected)
            {
                Logger.LogError(unexpected, "FrmEmployeeEdit.SetPhoto");
            }
        }

        /// <summary>Maps the selected Arabic label of employment type to its stored token.</summary>
        private string EmploymentTypeToken()
        {
            int index = _cboEmploymentType.SelectedIndex;
            if (index < 0)
            {
                return null;
            }
            return EmploymentTypeValues[index];
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
