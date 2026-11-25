using System;
using System.Collections.Generic;
using HR.DAL;
using HR.Entities;
using HR.Helpers;

namespace HR.BLL
{
    /// <summary>
    /// Mission management (rule 16): creating/updating a mission needs any
    /// authenticated user; approving or rejecting needs "Missions.Approve".
    /// Approved missions (IsExemptAtt = true) automatically exempt their days
    /// from the attendance engine (day precedence: Mission > Leave > ...).
    /// </summary>
    public static class MissionBLL
    {
        public const string StatusPending = "Pending";
        public const string StatusApproved = "Approved";
        public const string StatusRejected = "Rejected";

        public static List<MissionView> Search(string status, int? employeeId,
            DateTime? fromDate, DateTime? toDate)
        {
            return MissionDAL.Search(RequireSearchStatus(status), employeeId, fromDate, toDate, 1000);
        }

        private static string RequireSearchStatus(string status)
        {
            if (status == StatusPending || status == StatusApproved ||
                status == StatusRejected || string.IsNullOrEmpty(status))
            {
                return string.IsNullOrEmpty(status) ? null : status;
            }
            throw new BusinessException("حالة المأمورية غير معروفة: " + status);
        }

        public static MissionView GetById(int missionId)
        {
            return MissionDAL.GetById(missionId);
        }

        public static int Create(Mission mission)
        {
            ValidateContent(mission);
            int missionId = MissionDAL.Insert(mission, CurrentUserId());
            AuditBLL.Log("Mission.Create", "Missions", missionId.ToString(), null,
                "Employee=" + mission.EmployeeId +
                ", From=" + mission.StartDate.Value.ToString("yyyy-MM-dd") +
                " " + (mission.StartTime ?? "") +
                ", To=" + mission.EndDate.Value.ToString("yyyy-MM-dd") +
                " " + (mission.EndTime ?? "") +
                ", Exempt=" + (mission.IsExemptAtt == true ? "1" : "0"));
            return missionId;
        }

        public static void Update(int missionId, Mission mission)
        {
            ValidateContent(mission);
            MissionView stored = MissionDAL.GetById(missionId);
            if (stored == null)
            {
                throw new BusinessException("المأمورية غير موجودة.");
            }
            if (stored.Status == StatusApproved)
            {
                throw new BusinessException("لا يمكن تعديل مأمورية معتمدة.");
            }
            if (stored.Status == StatusRejected)
            {
                throw new BusinessException("لا يمكن تعديل مأمورية مرفوضة — أنشئ مأمورية جديدة.");
            }
            MissionDAL.Update(missionId, mission);
            AuditBLL.Log("Mission.Update", "Missions", missionId.ToString(),
                stored.StartDate.Value.ToString("yyyy-MM-dd") + " → " + stored.EndDate.Value.ToString("yyyy-MM-dd"),
                mission.StartDate.Value.ToString("yyyy-MM-dd") + " → " + mission.EndDate.Value.ToString("yyyy-MM-dd"));
        }

        public static void Approve(int missionId)
        {
            AppContext.RequirePermission("Missions.Approve");
            MissionView stored = MissionDAL.GetById(missionId);
            if (stored == null)
            {
                throw new BusinessException("المأمورية غير موجودة.");
            }
            if (stored.Status != StatusPending)
            {
                throw new BusinessException("الاعتماد متاح للمأموريات المعلّقة فقط — الحالة الحالية: " + stored.Status + ".");
            }
            MissionDAL.SetStatus(missionId, StatusApproved, CurrentUserId());
            AuditBLL.Log("Mission.Approve", "Missions", missionId.ToString(), StatusPending, StatusApproved);
        }

        public static void Reject(int missionId)
        {
            AppContext.RequirePermission("Missions.Approve");
            MissionView stored = MissionDAL.GetById(missionId);
            if (stored == null)
            {
                throw new BusinessException("المأمورية غير موجودة.");
            }
            if (stored.Status != StatusPending)
            {
                throw new BusinessException("الرفض متاح للمأموريات المعلّقة فقط — الحالة الحالية: " + stored.Status + ".");
            }
            MissionDAL.SetStatus(missionId, StatusRejected, CurrentUserId());
            AuditBLL.Log("Mission.Reject", "Missions", missionId.ToString(), StatusPending, StatusRejected);
        }

        private static void ValidateContent(Mission mission)
        {
            if (mission == null || mission.EmployeeId == null || mission.EmployeeId.Value <= 0)
            {
                throw new BusinessException("اختر موظفًا أولاً (الرقم غير صالح).");
            }
            if (mission.StartDate == null || mission.EndDate == null)
            {
                throw new BusinessException("حدد تاريخ البداية والنهاية.");
            }
            if (mission.EndDate.Value.Date < mission.StartDate.Value.Date)
            {
                throw new BusinessException("تاريخ النهاية قبل تاريخ البداية.");
            }
            // store normalized (Western digits) times so the attendance engine
            // and every later parser see the same canonical form.
            mission.StartTime = TextNormalizer.NormalizeTimeText(mission.StartTime);
            mission.EndTime = TextNormalizer.NormalizeTimeText(mission.EndTime);
            ValidateTime(mission.StartTime, "وقت بداية المأمورية");
            ValidateTime(mission.EndTime, "وقت نهاية المأمورية");
            if (!string.IsNullOrEmpty(mission.Location) && mission.Location.Length > 150)
            {
                throw new BusinessException("مكان المأمورية يتجاوز 150 حرفًا.");
            }
        }

        private static void ValidateTime(string rawTime, string fieldName)
        {
            string time = TextNormalizer.NormalizeDigits(rawTime);
            if (string.IsNullOrEmpty(time))
            {
                return;
            }
            TimeSpan parsed;
            if (!TimeSpan.TryParse(time, out parsed) || parsed < TimeSpan.Zero || parsed >= TimeSpan.FromHours(24))
            {
                throw new BusinessException(fieldName + " يجب أن يكون بصيغة HH:mm صحيحة (مثال: 08:30).");
            }
        }

        private static int CurrentUserId()
        {
            return AppContext.CurrentUser == null ? 0 : AppContext.CurrentUser.UserId;
        }
    }
}
