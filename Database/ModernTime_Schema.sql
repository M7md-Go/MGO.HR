CREATE TABLE Companies (
CompanyId     AUTOINCREMENT PRIMARY KEY,
CompanyCode   VARCHAR(20) NOT NULL UNIQUE,
CompanyNameAr VARCHAR(150) NOT NULL,
CompanyNameEn VARCHAR(150),
TaxNumber     VARCHAR(30),
Phone         VARCHAR(20),
Email         VARCHAR(100),
Address       LONGCHAR,
IsActive      YESNO
);

CREATE TABLE Branches (
BranchId     AUTOINCREMENT PRIMARY KEY,
BranchCode   VARCHAR(20) NOT NULL UNIQUE,
BranchName   VARCHAR(100) NOT NULL,
CompanyId    LONG NOT NULL,
Address      LONGCHAR,
Phone        VARCHAR(20),
IsActive     YESNO
);

CREATE TABLE Departments (
DepartmentId   AUTOINCREMENT PRIMARY KEY,
DepartmentCode VARCHAR(20) NOT NULL UNIQUE,
DepartmentName VARCHAR(100) NOT NULL,
BranchId       LONG,
ManagerEmpId   LONG,
IsActive       YESNO
);

CREATE TABLE Sections (
SectionId     AUTOINCREMENT PRIMARY KEY,
SectionCode   VARCHAR(20) NOT NULL UNIQUE,
SectionName   VARCHAR(100) NOT NULL,
DepartmentId  LONG NOT NULL,
ManagerEmpId  LONG,
IsActive      YESNO
);

CREATE TABLE JobTitles (
JobTitleId   AUTOINCREMENT PRIMARY KEY,
TitleCode    VARCHAR(20) NOT NULL UNIQUE,
TitleName    VARCHAR(100) NOT NULL,
IsActive     YESNO
);

CREATE TABLE ActualJobs (
ActualJobId  AUTOINCREMENT PRIMARY KEY,
JobCode      VARCHAR(20) NOT NULL UNIQUE,
JobName      VARCHAR(100) NOT NULL,
DepartmentId LONG,
IsActive     YESNO
);

CREATE TABLE Qualifications (
QualificationId AUTOINCREMENT PRIMARY KEY,
QualCode        VARCHAR(20) NOT NULL UNIQUE,
QualName        VARCHAR(100) NOT NULL
);

CREATE TABLE EmployeeStatus (
EmployeeStatusId AUTOINCREMENT PRIMARY KEY,
StatusCode       VARCHAR(20) NOT NULL UNIQUE,
StatusName       VARCHAR(50) NOT NULL
);

CREATE TABLE Employees (
EmployeeId         AUTOINCREMENT PRIMARY KEY,
EmployeeCode       VARCHAR(20) NOT NULL UNIQUE,
NationalId         VARCHAR(20),
FullNameAr         VARCHAR(150) NOT NULL,
FullNameEn         VARCHAR(150),
Gender             VARCHAR(10),
BirthDate          DATETIME,
HireDate           DATETIME NOT NULL,
EndDate            DATETIME,
CompanyId          LONG NOT NULL,
BranchId           LONG,
DepartmentId       LONG NOT NULL,
SectionId          LONG,
JobTitleId         LONG NOT NULL,
ActualJobId        LONG,
EmployeeStatusId   LONG NOT NULL,
QualificationId    LONG,
EmploymentType     VARCHAR(20),
Phone              VARCHAR(20),
Email              VARCHAR(100),
Address            LONGCHAR,
PhotoPath          VARCHAR(255),
DeviceEnrollNumber VARCHAR(20),
ShiftGroupCode     VARCHAR(10),
ContractHours      SINGLE,
Salary             CURRENCY,
Status             VARCHAR(15),
IsDeleted          YESNO,
Notes              LONGCHAR,
CreatedAt          DATETIME,
CreatedBy          LONG,
UpdatedAt          DATETIME,
UpdatedBy          LONG
);

CREATE TABLE Shifts (
ShiftId      AUTOINCREMENT PRIMARY KEY,
ShiftCode    VARCHAR(10) NOT NULL UNIQUE,
ShiftName    VARCHAR(60) NOT NULL,
StartTime    VARCHAR(5),
EndTime      VARCHAR(5),
CrossDay     YESNO,
DurationHrs  SINGLE,
NormalHours  SINGLE,
BreakMinutes LONG,
CategoryCode VARCHAR(10),
RequireCheck YESNO,
IsActive     YESNO
);

CREATE TABLE MonthlySchedules (
ScheduleId AUTOINCREMENT PRIMARY KEY,
YearVal    LONG NOT NULL,
MonthVal   LONG NOT NULL,
Status     VARCHAR(12),
CreatedBy  LONG,
CreatedAt  DATETIME,
ApprovedBy LONG,
ApprovedAt DATETIME,
LockedAt   DATETIME,
CONSTRAINT UQ_Sched UNIQUE (YearVal, MonthVal)
);

CREATE TABLE ScheduleDetails (
DetailId   AUTOINCREMENT PRIMARY KEY,
ScheduleId LONG NOT NULL,
EmployeeId LONG NOT NULL,
DayNumber  LONG NOT NULL,
ShiftId    LONG NOT NULL,
CONSTRAINT UQ_SchedDet UNIQUE (ScheduleId, EmployeeId, DayNumber)
);

CREATE TABLE Devices (
DeviceId      AUTOINCREMENT PRIMARY KEY,
DeviceName    VARCHAR(60) NOT NULL,
IpAddress     VARCHAR(15) NOT NULL,
Port          LONG,
SerialNumber  VARCHAR(40),
LastSync      DATETIME,
LastStatus    VARCHAR(15),
LastPingDelay LONG,
IsActive      YESNO,
Location      VARCHAR(100)
);

CREATE TABLE FingerprintLogs (
LogId      AUTOINCREMENT PRIMARY KEY,
DeviceId   LONG,
EmployeeId LONG,
EmpDevCode VARCHAR(20),
LogTime    DATETIME NOT NULL,
InOutFlag  LONG,
SourceFile VARCHAR(200),
CreatedAt  DATETIME,
CONSTRAINT UQ_Log UNIQUE (EmployeeId, LogTime, InOutFlag)
);

CREATE TABLE Attendance (
AttendanceId  AUTOINCREMENT PRIMARY KEY,
EmployeeId    LONG NOT NULL,
AttDate       DATETIME NOT NULL,
ShiftId       LONG,
StartTime     DATETIME,
EndTime       DATETIME,
Status        VARCHAR(15),
IsManuallyChg YESNO,
MinutesWorked LONG,
OvertimeMin   LONG,
LateMinutes   LONG,
EarlyLeaveMinutes LONG,
ApprovedBy    LONG,
ApprovedAt    DATETIME,
CreatedAt     DATETIME,
CONSTRAINT UQ_Att UNIQUE (EmployeeId, AttDate)
);

CREATE TABLE AttendanceExceptions (
ExceptionId AUTOINCREMENT PRIMARY KEY,
EmployeeId  LONG NOT NULL,
AttDate     DATETIME NOT NULL,
ExcType     VARCHAR(20) NOT NULL,
ExcValue    LONG,
Status      VARCHAR(12),
AppliedTo   LONG,
CreatedAt   DATETIME,
CreatedBy   LONG,
UpdatedAt   DATETIME,
UpdatedBy   LONG
);

CREATE TABLE AttendanceAdjustments (
AdjustmentId   AUTOINCREMENT PRIMARY KEY,
EmployeeId     LONG NOT NULL,
AttDate        DATETIME NOT NULL,
AdjustmentType VARCHAR(15) NOT NULL,
NewInTime      VARCHAR(5),
NewOutTime     VARCHAR(5),
NewStatus      VARCHAR(15),
Reason         LONGCHAR,
Status         VARCHAR(12),
CreatedBy      LONG,
CreatedAt      DATETIME,
ApprovedBy     LONG,
ApprovedAt     DATETIME
);

CREATE TABLE Overtime (
OvertimeId       AUTOINCREMENT PRIMARY KEY,
EmployeeId       LONG NOT NULL,
OtDate           DATETIME NOT NULL,
PlannedMinutes   LONG,
ActualMinutes    LONG,
EffectiveMinutes LONG,
Rate             DOUBLE,
Source           VARCHAR(10),
Status           VARCHAR(12),
RequestedBy      LONG,
ApprovedBy       LONG,
ApprovedAt       DATETIME,
Notes            LONGCHAR,
CONSTRAINT UQ_OvT UNIQUE (EmployeeId, OtDate)
);

CREATE TABLE LeaveTypes (
LeaveTypeId   AUTOINCREMENT PRIMARY KEY,
LeaveTypeCode VARCHAR(10) NOT NULL UNIQUE,
TypeName      VARCHAR(50) NOT NULL,
AnnualDays    LONG,
IsPaid        YESNO,
RequiresDoc   YESNO
);

CREATE TABLE LeaveRequests (
LeaveId     AUTOINCREMENT PRIMARY KEY,
EmployeeId  LONG NOT NULL,
LeaveTypeId LONG NOT NULL,
StartDate   DATETIME NOT NULL,
EndDate     DATETIME NOT NULL,
DaysCount   LONG NOT NULL,
Reason      LONGCHAR,
Status      VARCHAR(12),
ApprovedBy  LONG,
CreatedAt   DATETIME,
CreatedBy   LONG
);

CREATE TABLE LeaveBalances (
BalanceId   AUTOINCREMENT PRIMARY KEY,
EmployeeId  LONG NOT NULL,
YearVal     LONG NOT NULL,
LeaveTypeId LONG NOT NULL,
QuotaDays   LONG NOT NULL,
UsedDays    LONG,
Remaining   LONG,
CONSTRAINT UQ_LevBal UNIQUE (EmployeeId, YearVal, LeaveTypeId)
);

CREATE TABLE Missions (
MissionId   AUTOINCREMENT PRIMARY KEY,
EmployeeId  LONG NOT NULL,
StartDate   DATETIME NOT NULL,
StartTime   VARCHAR(5),
EndDate     DATETIME NOT NULL,
EndTime     VARCHAR(5),
Location    VARCHAR(150),
Purpose     LONGCHAR,
IsExemptAtt YESNO,
Status      VARCHAR(12),
ApprovedBy  LONG,
CreatedAt   DATETIME
);

CREATE TABLE Penalties (
PenaltyId   AUTOINCREMENT PRIMARY KEY,
EmployeeId  LONG NOT NULL,
AttDate     DATETIME,
PenaltyDate DATETIME,
TypeCode    VARCHAR(50) NOT NULL,
Amount      CURRENCY,
Reason      LONGCHAR,
IssuedBy    LONG,
CreatedAt   DATETIME
);

CREATE TABLE PublicHolidays (
HolidayId   AUTOINCREMENT PRIMARY KEY,
HolidayDate DATETIME NOT NULL UNIQUE,
HolidayName VARCHAR(100) NOT NULL,
IsRecurring YESNO,
ShiftId     LONG,
AppliesTo   LONG
);

CREATE TABLE MonthlyShiftLocks (
LockId           AUTOINCREMENT PRIMARY KEY,
YearVal          LONG NOT NULL,
MonthVal         LONG NOT NULL,
IsLocked         YESNO,
LockedBy         LONG,
LockedAt         DATETIME,
UnlockedBy       LONG,
UnlockedAt       DATETIME,
UnlockReason     LONGCHAR,
CONSTRAINT UQ_Lock UNIQUE (YearVal, MonthVal)
);

CREATE TABLE Roles (
RoleId    AUTOINCREMENT PRIMARY KEY,
RoleName  VARCHAR(50) NOT NULL UNIQUE,
IsAdmin   YESNO
);

CREATE TABLE Permissions (
PermissionId  AUTOINCREMENT PRIMARY KEY,
PermissionKey VARCHAR(50) NOT NULL UNIQUE,
DescriptionAr VARCHAR(150)
);

CREATE TABLE RolePermissions (
RolePermissionId AUTOINCREMENT PRIMARY KEY,
RoleId           LONG NOT NULL,
PermissionId     LONG NOT NULL,
CONSTRAINT UQ_RolePerm UNIQUE (RoleId, PermissionId)
);

CREATE TABLE Users (
UserId       AUTOINCREMENT PRIMARY KEY,
Username     VARCHAR(50) NOT NULL UNIQUE,
PwdHash      VARCHAR(120) NOT NULL,
Salt         VARCHAR(64) NOT NULL,
FullName     VARCHAR(100),
RoleId       LONG NOT NULL,
DeptId       LONG,
IsDisabled   YESNO,
IsLocked     YESNO,
FailedLogins LONG,
LastLogin    DATETIME,
CreatedAt    DATETIME,
CreatedBy    LONG
);

CREATE TABLE UserPermissions (
UserPermissionId AUTOINCREMENT PRIMARY KEY,
UserId           LONG NOT NULL,
PermissionId     LONG NOT NULL,
IsGranted        YESNO,
CONSTRAINT UQ_UserPerm UNIQUE (UserId, PermissionId)
);

CREATE TABLE LoginHistory (
LoginId      AUTOINCREMENT PRIMARY KEY,
UserId       LONG,
AttemptTime  DATETIME NOT NULL,
Success      YESNO,
MachineName  VARCHAR(50),
IpAddress    VARCHAR(15)
);

CREATE TABLE EmployeeHistory (
HistoryId  AUTOINCREMENT PRIMARY KEY,
EmployeeId LONG NOT NULL,
ChangeDate DATETIME NOT NULL,
ChangeType VARCHAR(20) NOT NULL,
FieldName  VARCHAR(50),
OldValue   LONGCHAR,
NewValue   LONGCHAR,
ChangedBy  LONG
);

CREATE TABLE AuditLogs (
AuditId     AUTOINCREMENT PRIMARY KEY,
UserId      LONG,
ActionName  VARCHAR(50) NOT NULL,
ActionTime  DATETIME NOT NULL,
TableName   VARCHAR(50),
RecordId    VARCHAR(30),
OldValue    LONGCHAR,
NewValue    LONGCHAR,
MachineName VARCHAR(50),
IpAddress   VARCHAR(15)
);

CREATE TABLE AppSettings (
SettingKey   VARCHAR(50) NOT NULL,
SettingValue VARCHAR(200) NOT NULL,
SettingDesc  VARCHAR(200),
UpdatedAt    DATETIME,
PRIMARY KEY (SettingKey)
);

CREATE TABLE BackupLogs (
BackupId    AUTOINCREMENT PRIMARY KEY,
BackupType  VARCHAR(20) NOT NULL,
FilePath    VARCHAR(255) NOT NULL,
FileSize    LONG,
CreatedAt   DATETIME,
CreatedBy   LONG,
Status      VARCHAR(12),
RestorePath VARCHAR(255)
);

CREATE TABLE ArchiveLogs (
ArchiveId   AUTOINCREMENT PRIMARY KEY,
TableName   VARCHAR(40) NOT NULL,
FromDate    DATETIME NOT NULL,
ToDate      DATETIME NOT NULL,
RowsCount   LONG NOT NULL,
ArchiveFile VARCHAR(255) NOT NULL,
ArchivedBy  LONG,
CreatedAt   DATETIME
);

ALTER TABLE Branches  ADD CONSTRAINT FK_Branch_Company  FOREIGN KEY (CompanyId)   REFERENCES Companies(CompanyId);

ALTER TABLE Sections  ADD CONSTRAINT FK_Section_Dept    FOREIGN KEY (DepartmentId) REFERENCES Departments(DepartmentId);

ALTER TABLE ActualJobs ADD CONSTRAINT FK_ActJob_Dept    FOREIGN KEY (DepartmentId) REFERENCES Departments(DepartmentId);

ALTER TABLE Employees ADD CONSTRAINT FK_Emp_Company  FOREIGN KEY (CompanyId)        REFERENCES Companies(CompanyId);

ALTER TABLE Employees ADD CONSTRAINT FK_Emp_Branch   FOREIGN KEY (BranchId)         REFERENCES Branches(BranchId);

ALTER TABLE Employees ADD CONSTRAINT FK_Emp_Dept     FOREIGN KEY (DepartmentId)     REFERENCES Departments(DepartmentId);

ALTER TABLE Employees ADD CONSTRAINT FK_Emp_Section  FOREIGN KEY (SectionId)        REFERENCES Sections(SectionId);

ALTER TABLE Employees ADD CONSTRAINT FK_Emp_JobTitle FOREIGN KEY (JobTitleId)       REFERENCES JobTitles(JobTitleId);

ALTER TABLE Employees ADD CONSTRAINT FK_Emp_ActJob   FOREIGN KEY (ActualJobId)      REFERENCES ActualJobs(ActualJobId);

ALTER TABLE Employees ADD CONSTRAINT FK_Emp_Status   FOREIGN KEY (EmployeeStatusId) REFERENCES EmployeeStatus(EmployeeStatusId);

ALTER TABLE Employees ADD CONSTRAINT FK_Emp_Qual     FOREIGN KEY (QualificationId)  REFERENCES Qualifications(QualificationId);

ALTER TABLE ScheduleDetails ADD CONSTRAINT FK_Det_Sched FOREIGN KEY (ScheduleId) REFERENCES MonthlySchedules(ScheduleId);

ALTER TABLE ScheduleDetails ADD CONSTRAINT FK_Det_Emp   FOREIGN KEY (EmployeeId) REFERENCES Employees(EmployeeId);

ALTER TABLE ScheduleDetails ADD CONSTRAINT FK_Det_Shift FOREIGN KEY (ShiftId)    REFERENCES Shifts(ShiftId);

ALTER TABLE FingerprintLogs ADD CONSTRAINT FK_Log_Emp   FOREIGN KEY (EmployeeId) REFERENCES Employees(EmployeeId);

ALTER TABLE FingerprintLogs ADD CONSTRAINT FK_Log_Dev  FOREIGN KEY (DeviceId)   REFERENCES Devices(DeviceId);

ALTER TABLE Attendance      ADD CONSTRAINT FK_Att_Emp   FOREIGN KEY (EmployeeId) REFERENCES Employees(EmployeeId);

ALTER TABLE AttendanceExceptions ADD CONSTRAINT FK_Exc_Emp FOREIGN KEY (EmployeeId) REFERENCES Employees(EmployeeId);

ALTER TABLE AttendanceAdjustments ADD CONSTRAINT FK_Adj_Emp FOREIGN KEY (EmployeeId) REFERENCES Employees(EmployeeId);

ALTER TABLE Overtime ADD CONSTRAINT FK_OT_Emp  FOREIGN KEY (EmployeeId) REFERENCES Employees(EmployeeId);

ALTER TABLE LeaveRequests ADD CONSTRAINT FK_Lev_Emp  FOREIGN KEY (EmployeeId)  REFERENCES Employees(EmployeeId);

ALTER TABLE LeaveRequests ADD CONSTRAINT FK_Lev_Type FOREIGN KEY (LeaveTypeId) REFERENCES LeaveTypes(LeaveTypeId);

ALTER TABLE LeaveBalances ADD CONSTRAINT FK_Bal_Emp  FOREIGN KEY (EmployeeId)  REFERENCES Employees(EmployeeId);

ALTER TABLE LeaveBalances ADD CONSTRAINT FK_Bal_Type FOREIGN KEY (LeaveTypeId) REFERENCES LeaveTypes(LeaveTypeId);

ALTER TABLE Missions   ADD CONSTRAINT FK_Mis_Emp FOREIGN KEY (EmployeeId) REFERENCES Employees(EmployeeId);

ALTER TABLE Penalties  ADD CONSTRAINT FK_Pen_Emp FOREIGN KEY (EmployeeId) REFERENCES Employees(EmployeeId);

ALTER TABLE RolePermissions ADD CONSTRAINT FK_RPerm_Role FOREIGN KEY (RoleId)       REFERENCES Roles(RoleId);

ALTER TABLE RolePermissions ADD CONSTRAINT FK_RPerm_Perm FOREIGN KEY (PermissionId) REFERENCES Permissions(PermissionId);

ALTER TABLE UserPermissions ADD CONSTRAINT FK_UPerm_User FOREIGN KEY (UserId)       REFERENCES Users(UserId);

ALTER TABLE UserPermissions ADD CONSTRAINT FK_UPerm_Perm FOREIGN KEY (PermissionId) REFERENCES Permissions(PermissionId);

ALTER TABLE EmployeeHistory ADD CONSTRAINT FK_Hist_Emp FOREIGN KEY (EmployeeId) REFERENCES Employees(EmployeeId);

CREATE INDEX IX_LogTime     ON FingerprintLogs (LogTime);

CREATE INDEX IX_LogEmpDate  ON FingerprintLogs (EmployeeId, LogTime);

CREATE UNIQUE INDEX IX_Emp_Enroll ON Employees (DeviceEnrollNumber);

CREATE INDEX IX_Emp_Dept    ON Employees (DepartmentId);

CREATE INDEX IX_Emp_Name    ON Employees (FullNameAr);

CREATE INDEX IX_Emp_Status  ON Employees (Status, IsDeleted);

CREATE INDEX IX_Att_Date    ON Attendance (AttDate);

CREATE INDEX IX_Exc_EmpDate ON AttendanceExceptions (EmployeeId, AttDate);

CREATE INDEX IX_Adj_Pending ON AttendanceAdjustments (Status);

CREATE INDEX IX_OT_Date     ON Overtime (OtDate);

CREATE INDEX IX_PenaltyEmp  ON Penalties (EmployeeId, PenaltyDate);

CREATE INDEX IX_Audit_Time  ON AuditLogs (ActionTime);

CREATE INDEX IX_Audit_Table ON AuditLogs (TableName, RecordId);

CREATE INDEX IX_Audit_User  ON AuditLogs (UserId, ActionTime);

CREATE INDEX IX_Login_User  ON LoginHistory (UserId, AttemptTime);

CREATE INDEX IX_Hist_Emp    ON EmployeeHistory (EmployeeId, ChangeDate);

INSERT INTO Shifts (ShiftCode, ShiftName, StartTime, EndTime, CrossDay, DurationHrs, NormalHours, BreakMinutes, CategoryCode, RequireCheck, IsActive)
VALUES ('M', 'وردية صباحية 6 ساعات', '07:00', '13:00', 0, 6.0, 6.0, 0, 'Office', 1, 1);
INSERT INTO Shifts (ShiftCode, ShiftName, StartTime, EndTime, CrossDay, DurationHrs, NormalHours, BreakMinutes, CategoryCode, RequireCheck, IsActive)
VALUES ('M1', 'وردية صباحية 6 ساعات', '08:00', '14:00', 0, 6.0, 6.0, 0, 'Office', 1, 1);
INSERT INTO Shifts (ShiftCode, ShiftName, StartTime, EndTime, CrossDay, DurationHrs, NormalHours, BreakMinutes, CategoryCode, RequireCheck, IsActive)
VALUES ('M2', 'وردية صباحية 7 ساعات', '08:00', '15:00', 0, 7.0, 7.0, 0, 'Office', 1, 1);
INSERT INTO Shifts (ShiftCode, ShiftName, StartTime, EndTime, CrossDay, DurationHrs, NormalHours, BreakMinutes, CategoryCode, RequireCheck, IsActive)
VALUES ('X', 'وردية 9 ساعات', '08:00', '17:00', 0, 9.0, 8.0, 60, 'Office', 1, 1);
INSERT INTO Shifts (ShiftCode, ShiftName, StartTime, EndTime, CrossDay, DurationHrs, NormalHours, BreakMinutes, CategoryCode, RequireCheck, IsActive)
VALUES ('L', 'وردية 12 ساعة', '08:00', '20:00', 0, 12.0, 8.0, 60, 'Field', 1, 1);
INSERT INTO Shifts (ShiftCode, ShiftName, StartTime, EndTime, CrossDay, DurationHrs, NormalHours, BreakMinutes, CategoryCode, RequireCheck, IsActive)
VALUES ('N', 'وردية ليلية', '22:00', '06:00', 1, 8.0, 8.0, 0, 'Night', 1, 1);
INSERT INTO Shifts (ShiftCode, ShiftName, StartTime, EndTime, CrossDay, DurationHrs, NormalHours, BreakMinutes, CategoryCode, RequireCheck, IsActive)
VALUES ('FR', 'راحة أسبوعية', NULL, NULL, 0, 0.0, 0.0, 0, 'Rest', 0, 1);
INSERT INTO LeaveTypes (LeaveTypeCode, TypeName, AnnualDays, IsPaid, RequiresDoc)
VALUES ('Annual', 'سنوية', 21, 1, 0);
INSERT INTO LeaveTypes (LeaveTypeCode, TypeName, AnnualDays, IsPaid, RequiresDoc)
VALUES ('Sick', 'مرضية', 30, 1, 1);
INSERT INTO LeaveTypes (LeaveTypeCode, TypeName, AnnualDays, IsPaid, RequiresDoc)
VALUES ('Casual', 'عارضة', 7, 1, 0);
INSERT INTO LeaveTypes (LeaveTypeCode, TypeName, AnnualDays, IsPaid, RequiresDoc)
VALUES ('Maternity', 'أمومة', 90, 1, 1);
INSERT INTO LeaveTypes (LeaveTypeCode, TypeName, AnnualDays, IsPaid, RequiresDoc)
VALUES ('Hajj', 'حج', 30, 1, 1);
INSERT INTO LeaveTypes (LeaveTypeCode, TypeName, AnnualDays, IsPaid, RequiresDoc)
VALUES ('Study', 'دراسية', 15, 0, 1);
INSERT INTO Roles (RoleName, IsAdmin)
VALUES ('Admin', 1);
INSERT INTO Roles (RoleName, IsAdmin)
VALUES ('Supervisor', 0);
INSERT INTO Roles (RoleName, IsAdmin)
VALUES ('User', 0);
INSERT INTO Permissions (PermissionKey, DescriptionAr)
VALUES ('Org.Manage', 'إدارة الهيكل التنظيمي');
INSERT INTO Permissions (PermissionKey, DescriptionAr)
VALUES ('Employees.Add', 'إضافة موظف');
INSERT INTO Permissions (PermissionKey, DescriptionAr)
VALUES ('Employees.Edit', 'تعديل موظف');
INSERT INTO Permissions (PermissionKey, DescriptionAr)
VALUES ('Employees.Delete', 'حذف موظف');
INSERT INTO Permissions (PermissionKey, DescriptionAr)
VALUES ('Employees.Archive', 'أرشفة موظف');
INSERT INTO Permissions (PermissionKey, DescriptionAr)
VALUES ('Schedule.Edit', 'تعديل الجدول الشهري');
INSERT INTO Permissions (PermissionKey, DescriptionAr)
VALUES ('Schedule.Approve', 'اعتماد الجدول الشهري');
INSERT INTO Permissions (PermissionKey, DescriptionAr)
VALUES ('Schedule.Unlock', 'فتح شهر مقفل');
INSERT INTO Permissions (PermissionKey, DescriptionAr)
VALUES ('Attendance.Edit', 'تعديل الحضور');
INSERT INTO Permissions (PermissionKey, DescriptionAr)
VALUES ('Attendance.ApproveAdjust', 'اعتماد تصحيحات الحضور');
INSERT INTO Permissions (PermissionKey, DescriptionAr)
VALUES ('Leaves.Approve', 'اعتماد الإجازات');
INSERT INTO Permissions (PermissionKey, DescriptionAr)
VALUES ('Missions.Approve', 'اعتماد المأموريات');
INSERT INTO Permissions (PermissionKey, DescriptionAr)
VALUES ('Overtime.Approve', 'اعتماد الساعات الإضافية');
INSERT INTO Permissions (PermissionKey, DescriptionAr)
VALUES ('Penalties.Add', 'تسجيل الجزاءات');
INSERT INTO Permissions (PermissionKey, DescriptionAr)
VALUES ('Devices.Manage', 'إدارة أجهزة البصمة');
INSERT INTO Permissions (PermissionKey, DescriptionAr)
VALUES ('Reports.View', 'عرض التقارير');
INSERT INTO Permissions (PermissionKey, DescriptionAr)
VALUES ('Users.Manage', 'إدارة المستخدمين');
INSERT INTO Permissions (PermissionKey, DescriptionAr)
VALUES ('Settings.Manage', 'إدارة الإعدادات');
INSERT INTO Permissions (PermissionKey, DescriptionAr)
VALUES ('Backup.Execute', 'النسخ الاحتياطي والاستعادة');
INSERT INTO Permissions (PermissionKey, DescriptionAr)
VALUES ('Archive.Execute', 'تنفيذ الأرشفة');
INSERT INTO Permissions (PermissionKey, DescriptionAr)
VALUES ('Audit.View', 'عرض سجل العمليات');
INSERT INTO EmployeeStatus (StatusCode, StatusName)
VALUES ('Permanent', 'دائم');
INSERT INTO EmployeeStatus (StatusCode, StatusName)
VALUES ('Contract', 'متعاقد');
INSERT INTO EmployeeStatus (StatusCode, StatusName)
VALUES ('PartTime', 'دوام جزئي');
INSERT INTO EmployeeStatus (StatusCode, StatusName)
VALUES ('Intern', 'متدرب');
INSERT INTO AppSettings (SettingKey, SettingValue, SettingDesc, UpdatedAt)
VALUES ('Org.CompanyNameAr', 'Modern Time', 'اسم المؤسسة', Now());
INSERT INTO AppSettings (SettingKey, SettingValue, SettingDesc, UpdatedAt)
VALUES ('Att.GraceMinutesLate', '30', 'فتح التأخير المسموح (دقيقة)', Now());
INSERT INTO AppSettings (SettingKey, SettingValue, SettingDesc, UpdatedAt)
VALUES ('Att.GraceMinutesEarlyLeave', '15', 'فتح الانصراف المبكر المسموح (دقيقة)', Now());
INSERT INTO AppSettings (SettingKey, SettingValue, SettingDesc, UpdatedAt)
VALUES ('Att.LateCountMode', 'FromShiftStart', 'احتساب التأخير: FromShiftStart أو AfterGrace', Now());
INSERT INTO AppSettings (SettingKey, SettingValue, SettingDesc, UpdatedAt)
VALUES ('Att.MaxContinuousWorkHours', '24', 'أقصى ساعات عمل متواصلة', Now());
INSERT INTO AppSettings (SettingKey, SettingValue, SettingDesc, UpdatedAt)
VALUES ('Att.OvertimeRate', '1.5', 'معامل الساعات الإضافية الافتراضي', Now());
INSERT INTO AppSettings (SettingKey, SettingValue, SettingDesc, UpdatedAt)
VALUES ('Att.HolidayOTRate', '2.0', 'معامل إضافي العطلة الرسمية', Now());
INSERT INTO AppSettings (SettingKey, SettingValue, SettingDesc, UpdatedAt)
VALUES ('Att.DuplicatePunchWindowMin','1', 'فترة إهمال البصمة المكررة (دقيقة)', Now());
INSERT INTO AppSettings (SettingKey, SettingValue, SettingDesc, UpdatedAt)
VALUES ('Sch.MinWeeklyHours', '36', 'الحد الأدنى الأسبوعي', Now());
INSERT INTO AppSettings (SettingKey, SettingValue, SettingDesc, UpdatedAt)
VALUES ('Sch.MaxWeeklyHours', '42', 'الحد الأقصى الأسبوعي', Now());
INSERT INTO AppSettings (SettingKey, SettingValue, SettingDesc, UpdatedAt)
VALUES ('Sch.MaxContinuousHours', '24', 'أقصى ساعات متواصلة في الجدول', Now());
INSERT INTO AppSettings (SettingKey, SettingValue, SettingDesc, UpdatedAt)
VALUES ('Sch.OTMinThresholdMinutes', '30', 'أدنى مدة تحسب ساعات إضافية (دقيقة)', Now());
INSERT INTO AppSettings (SettingKey, SettingValue, SettingDesc, UpdatedAt)
VALUES ('Sch.OTRequiresApprove', 'YES', 'الساعات الإضافية تتطلب اعتماد', Now());
INSERT INTO AppSettings (SettingKey, SettingValue, SettingDesc, UpdatedAt)
VALUES ('Sch.DisableMixingCategories','YES', 'منع خلط أصناف الورديات في جدول الموظف', Now());
INSERT INTO AppSettings (SettingKey, SettingValue, SettingDesc, UpdatedAt)
VALUES ('Sec.MaxFailedLogins', '5', 'أقصى محاولات دخول فاشلة', Now());
INSERT INTO AppSettings (SettingKey, SettingValue, SettingDesc, UpdatedAt)
VALUES ('Sec.LockoutMinutes', '15', 'مدة قفل الحساب (دقيقة)', Now());
INSERT INTO AppSettings (SettingKey, SettingValue, SettingDesc, UpdatedAt)
VALUES ('Backup.AutoEnabled', 'YES', 'تفعيل النسخ الاحتياطي التلقائي', Now());
INSERT INTO AppSettings (SettingKey, SettingValue, SettingDesc, UpdatedAt)
VALUES ('Backup.AutoTime', '23:30', 'وقت النسخ التلقائي', Now());
INSERT INTO AppSettings (SettingKey, SettingValue, SettingDesc, UpdatedAt)
VALUES ('Backup.RetentionDays', '30', 'الاحتفاظ بالنسخ (يوم)', Now());
INSERT INTO AppSettings (SettingKey, SettingValue, SettingDesc, UpdatedAt)
VALUES ('Arch.ActivePeriodDays', '1095', 'مدة الاحتفاظ بالبيانات النشطة (يوم)', Now());
INSERT INTO AppSettings (SettingKey, SettingValue, SettingDesc, UpdatedAt)
VALUES ('Integration.ZKTeco.TimeoutSec', '15', 'مهلة اتصال أجهزة البصمة (ثانية)', Now());
INSERT INTO AppSettings (SettingKey, SettingValue, SettingDesc, UpdatedAt)
VALUES ('Fp.MachineNumber', '1', 'رقم جهاز MachineNumber الافتراضي للاتصال', Now());