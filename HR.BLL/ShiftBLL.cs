using System;
using System.Collections.Generic;
using HR.DAL;
using HR.Entities;
using HR.Helpers;

namespace HR.BLL
{
    /// <summary>
    /// Shift definitions business logic (Phase 1 rule 6). A shift is defined by its
    /// actual data, never by its name alone: Start/End, CrossDay, NormalHours vs
    /// DurationHrs, category; grace rules come from AppSettings at calculation time.
    /// </summary>
    public static class ShiftBLL
    {
        public static List<Shift> GetAll(bool activeOnly)
        {
            return ShiftDAL.GetAll(activeOnly);
        }

        public static Shift GetById(int shiftId)
        {
            return ShiftDAL.GetById(shiftId);
        }

        public static Shift GetByCode(string shiftCode)
        {
            return ShiftDAL.GetByCode(shiftCode);
        }

        /// <summary>Creates a shift (permission "Org.Manage").</summary>
        public static int Create(Shift shift)
        {
            AppContext.RequirePermission("Org.Manage");
            Validate(shift, null);
            if (ShiftDAL.CodeExists(shift.ShiftCode, null))
            {
                throw new BusinessException("كود الوردية '" + shift.ShiftCode + "' مستخدم بالفعل.");
            }
            int newId = ShiftDAL.Insert(shift);
            AuditBLL.Log("Shifts.Add", "Shifts", newId.ToString(), null, shift);
            return newId;
        }

        /// <summary>Updates a shift (permission "Org.Manage").</summary>
        public static void Update(Shift shift)
        {
            AppContext.RequirePermission("Org.Manage");
            Validate(shift, shift.ShiftId);
            Shift oldShift = ShiftDAL.GetById(shift.ShiftId);
            if (oldShift == null)
            {
                throw new BusinessException("الوردية غير موجودة.");
            }
            if (ShiftDAL.CodeExists(shift.ShiftCode, shift.ShiftId))
            {
                throw new BusinessException("كود الوردية '" + shift.ShiftCode + "' مستخدم بالفعل لوردية أخرى.");
            }
            ShiftDAL.Update(shift);
            AuditBLL.Log("Shifts.Update", "Shifts", shift.ShiftId.ToString(), oldShift, shift);
        }

        /// <summary>Deletes a shift that has no schedule/attendance usage.</summary>
        public static void Delete(int shiftId)
        {
            AppContext.RequirePermission("Org.Manage");
            Shift shift = ShiftDAL.GetById(shiftId);
            if (shift == null)
            {
                throw new BusinessException("الوردية غير موجودة.");
            }
            long usage = ShiftDAL.CountUsage(shiftId);
            if (usage > 0)
            {
                throw new BusinessException("لا يمكن حذف الوردية لاستخدامها في الجداول أو الحضور (" + usage + " ارتباط). قم بتعطيلها بدلاً من حذفها.");
            }
            ShiftDAL.Delete(shiftId);
            AuditBLL.Log("Shifts.Delete", "Shifts", shiftId.ToString(), shift, null);
        }

        /// <summary>
        /// Validation per rule 6 and BR-06: no shift without times except the rest-day
        /// template (FR); end before start only when CrossDay; duration max 24h;
        /// NormalHours cannot exceed the shift duration.
        /// </summary>
        public static void Validate(Shift shift, int? excludeShiftId)
        {
            if (shift == null)
            {
                throw new BusinessException("لا توجد وردية لحفظها.");
            }
            if (string.IsNullOrWhiteSpace(shift.ShiftCode))
            {
                throw new BusinessException("كود الوردية إلزامي.");
            }
            if (string.IsNullOrWhiteSpace(shift.ShiftName))
            {
                throw new BusinessException("اسم الوردية إلزامي.");
            }
            bool isRestDay = string.Equals(shift.ShiftCode, "FR", StringComparison.OrdinalIgnoreCase);

            if (isRestDay)
            {
                if ((shift.DurationHrs != null && shift.DurationHrs > 0) ||
                    (shift.NormalHours != null && shift.NormalHours > 0))
                {
                    throw new BusinessException("وردية الراحة (FR) يجب أن تكون مدتها صفر.");
                }
                return;
            }

            if (string.IsNullOrWhiteSpace(shift.StartTime) || string.IsNullOrWhiteSpace(shift.EndTime))
            {
                throw new BusinessException("الوردية '" + shift.ShiftCode + "' بدون وقت — وقت البداية والنهاية إلزاميان.");
            }
            int? start = TimeHelper.ParseMinutes(TextNormalizer.NormalizeDigits(shift.StartTime));
            int? end = TimeHelper.ParseMinutes(TextNormalizer.NormalizeDigits(shift.EndTime));
            if (start == null)
            {
                throw new BusinessException("وقت بداية الوردية '" + shift.ShiftCode + "' غير صحيح؛ استخدم الصيغة HH:mm.");
            }
            if (end == null)
            {
                throw new BusinessException("وقت نهاية الوردية '" + shift.ShiftCode + "' غير صحيح؛ استخدم الصيغة HH:mm.");
            }
            bool expectedCrossDay = end.Value < start.Value;
            if (shift.CrossDay == null)
            {
                shift.CrossDay = expectedCrossDay;
            }
            if (expectedCrossDay != shift.CrossDay)
            {
                throw new BusinessException("وردية '" + shift.ShiftCode + "': النهاية قبل البداية مسموح فقط للورديات الممتدة ليوم التالي (CrossDay).");
            }
            int durationMinutes = TimeHelper.DurationMinutes(start.Value, end.Value);
            if (durationMinutes > 24 * 60)
            {
                throw new BusinessException("وردية '" + shift.ShiftCode + "' تتجاوز الحد الأقصى 24 ساعة متواصلة (BR-06).");
            }
            float durationHours = durationMinutes / 60f;
            if (shift.DurationHrs == null || shift.DurationHrs <= 0)
            {
                shift.DurationHrs = durationHours;
            }
            else if (Math.Abs(shift.DurationHrs.Value - durationHours) > 0.02f)
            {
                throw new BusinessException("مدة الوردية المعلنة (" + shift.DurationHrs + " س) لا تطابق (البداية/النهاية = " + durationHours.ToString("0.##") + " س). يجب توحيدها.");
            }
            if (shift.NormalHours == null || shift.NormalHours < 0)
            {
                shift.NormalHours = durationHours;
            }
            if (shift.NormalHours > durationHours + 0.02f)
            {
                throw new BusinessException("الساعات الطبيعية (" + shift.NormalHours + ") لا يمكن أن تتجاوز مدة الوردية (" + durationHours.ToString("0.##") + ").");
            }
            if (shift.BreakMinutes == null || shift.BreakMinutes < 0)
            {
                shift.BreakMinutes = 0;
            }
            if (durationMinutes > 0 && shift.BreakMinutes >= durationMinutes)
            {
                throw new BusinessException("وقت الراحة داخل الوردية 'لا يمكن' أن يلتهم كامل مدتها.");
            }
            if (string.IsNullOrWhiteSpace(shift.CategoryCode))
            {
                throw new BusinessException("صنف الوردية (CategoryCode) إلزامي لمنع خلط الأصناف (BR-09).");
            }
        }
    }
}
