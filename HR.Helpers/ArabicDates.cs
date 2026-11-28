using System;

namespace HR.Helpers
{
    /// <summary>
    /// Single shared source for Arabic month and weekday names (Phase 3, R-03).
    /// Replaces the three duplicated copies (FrmMonthlySchedule, FrmEmployeeSchedule,
    /// ReportEngine) so a wording fix happens in exactly one place.
    /// </summary>
    public static class ArabicDates
    {
        private static readonly string[] MonthNames = new string[]
        {
            "يناير", "فبراير", "مارس", "أبريل", "مايو", "يونيو",
            "يوليو", "أغسطس", "سبتمبر", "أكتوبر", "نوفمبر", "ديسمبر"
        };

        private static readonly string[] DayNames = new string[]
        {
            "الأحد", "الاثنين", "الثلاثاء", "الأربعاء", "الخميس", "الجمعة", "السبت"
        };

        /// <summary>Arabic month name for month 1..12.</summary>
        public static string MonthName(int month)
        {
            if (month < 1 || month > 12)
            {
                throw new ArgumentOutOfRangeException("month", "الشهر يجب أن يكون بين 1 و 12.");
            }
            return MonthNames[month - 1];
        }

        /// <summary>Arabic weekday name.</summary>
        public static string DayName(DayOfWeek day)
        {
            return DayNames[(int)day];
        }
    }
}
