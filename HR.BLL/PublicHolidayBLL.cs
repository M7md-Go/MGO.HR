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
