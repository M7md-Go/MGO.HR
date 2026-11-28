using System;
using System.Collections.Generic;
using HR.DAL;
using HR.Entities;
using HR.Helpers;

namespace HR.BLL
{
    /// <summary>
    /// Administration of the official holiday calendar ([PublicHolidays]). The
    /// attendance engine treats these dates as non-working days, so writes are
    /// gated by the seeded permission "Attendance.Edit". Recurring holidays
    /// (national days) repeat every year; fixed ones occupy one exact date
    /// which must stay unique.
    /// </summary>
    public static class PublicHolidayBLL
    {
        public static List<PublicHoliday> List(DateTime? fromDate, DateTime? toDate)
        {
            return PublicHolidayDAL.List(fromDate, toDate);
        }

        /// <summary>
        /// Phase 3 (R-02): shared day-number set of the official holidays of
        /// one month (explicit dates inside the month + recurring definitions
        /// whose month/day fall inside it). Replaces the two duplicated tint
        /// loops in FrmMonthlySchedule and FrmEmployeeSchedule — a change to
        /// the recurring-holiday rule now happens in exactly one place.
        /// </summary>
        public static HashSet<int> HolidayDays(int year, int month)
        {
            HashSet<int> days = new HashSet<int>();
            DateTime first = new DateTime(year, month, 1);
            DateTime last = new DateTime(year, month, DateTime.DaysInMonth(year, month));
            foreach (PublicHoliday holiday in List(first, last))
            {
                if (holiday.HolidayDate == null)
                {
                    continue;
                }
                DateTime date = holiday.HolidayDate.Value;
                if (date.Year == year && date.Month == month)
                {
                    days.Add(date.Day);
                }
                else if (holiday.IsRecurring == true && date.Month == month &&
                    date.Day <= DateTime.DaysInMonth(year, month))
                {
                    days.Add(date.Day);
                }
            }
            return days;
        }

        public static PublicHoliday GetById(int holidayId)
        {
            return PublicHolidayDAL.GetById(holidayId);
        }

        public static int Create(DateTime date, string name, bool isRecurring)
        {
            AppContext.RequireAnyPermission(new string[] { "Attendance.Holidays", "Attendance.Edit" });
            Validate(date, name);
            if (!isRecurring && PublicHolidayDAL.DateExists(date, null))
            {
                throw new BusinessException("يوجد عطلة أخرى مسجّلة بنفس التاريخ: " + date.ToString("yyyy-MM-dd"));
            }
            PublicHoliday holiday = new PublicHoliday();
            holiday.HolidayDate = date.Date;
            holiday.HolidayName = name.Trim();
            holiday.IsRecurring = isRecurring;
            int newId = PublicHolidayDAL.Insert(holiday);
            AuditBLL.Log("Att.AddHoliday", "PublicHolidays", newId.ToString(), null,
                date.ToString("yyyy-MM-dd") + " - " + name.Trim());
            return newId;
        }

        public static void Update(int holidayId, DateTime date, string name, bool isRecurring)
        {
            AppContext.RequireAnyPermission(new string[] { "Attendance.Holidays", "Attendance.Edit" });
            Validate(date, name);
            PublicHoliday stored = PublicHolidayDAL.GetById(holidayId);
            if (stored == null)
            {
                throw new BusinessException("العطلة المطلوب تعديلها غير موجودة.");
            }
            if (!isRecurring && PublicHolidayDAL.DateExists(date, holidayId))
            {
                throw new BusinessException("يوجد عطلة أخرى مسجّلة بنفس التاريخ: " + date.ToString("yyyy-MM-dd"));
            }
            stored.HolidayDate = date.Date;
            stored.HolidayName = name.Trim();
            stored.IsRecurring = isRecurring;
            PublicHolidayDAL.Update(stored);
            AuditBLL.Log("Att.UpdateHoliday", "PublicHolidays", holidayId.ToString(),
                stored.HolidayDate.HasValue ? stored.HolidayDate.Value.ToString("yyyy-MM-dd") : "",
                date.ToString("yyyy-MM-dd") + " - " + name.Trim());
        }

        public static void Delete(int holidayId)
        {
            AppContext.RequireAnyPermission(new string[] { "Attendance.Holidays", "Attendance.Edit" });
            PublicHoliday stored = PublicHolidayDAL.GetById(holidayId);
            if (stored == null)
            {
                throw new BusinessException("العطلة المطلوب حذفها غير موجودة.");
            }
            PublicHolidayDAL.Delete(holidayId);
            AuditBLL.Log("Att.DeleteHoliday", "PublicHolidays", holidayId.ToString(),
                stored.HolidayName, null);
        }

        private static void Validate(DateTime date, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new BusinessException("أدخل اسم العطلة.");
            }
            if (name.Trim().Length > 100)
            {
                throw new BusinessException("اسم العطلة لا يتجاوز 100 حرف.");
            }
            if (date.Date > DateTime.Now.AddYears(5))
            {
                throw new BusinessException("تاريخ العطلة بعيد جدًا في المستقبل.");
            }
        }
    }
}
