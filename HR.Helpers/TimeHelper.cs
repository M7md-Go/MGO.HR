using System;

namespace HR.Helpers
{
    /// <summary>
    /// Time primitives shared by the schedule validator and the attendance engine.
    /// Times of day are stored as "HH:mm" text in the database and as minutes
    /// from midnight (0..1439) during calculations.
    /// </summary>
    public static class TimeHelper
    {
        /// <summary>Parses "HH:mm" or "H:mm" text into minutes from midnight; returns null when invalid.</summary>
        public static int? ParseMinutes(string hhmm)
        {
            if (string.IsNullOrWhiteSpace(hhmm))
            {
                return null;
            }
            string text = hhmm.Trim();
            string[] parts = text.Split(':');
            if (parts.Length != 2)
            {
                return null;
            }
            int hours;
            int minutes;
            bool hoursOk = int.TryParse(parts[0], out hours);
            bool minutesOk = int.TryParse(parts[1], out minutes);
            if (!hoursOk || !minutesOk || hours < 0 || hours > 23 || minutes < 0 || minutes > 59)
            {
                return null;
            }
            return hours * 60 + minutes;
        }

        /// <summary>Formats minutes from midnight as "HH:mm"; returns null for null input.</summary>
        public static string MinutesToText(int? minutes)
        {
            if (minutes == null)
            {
                return null;
            }
            int value = minutes.Value % 1440;
            return string.Format("{0:00}:{1:00}", value / 60, value % 60);
        }

        /// <summary>Combines a date with minutes-from-midnight, moving forward across days when needed.</summary>
        public static DateTime Combine(DateTime date, int minutes)
        {
            int dayOffset = minutes / 1440;
            int minutesOfDay = minutes % 1440;
            return date.Date.AddDays(dayOffset).AddMinutes(minutesOfDay);
        }

        /// <summary>Duration in minutes between two times of day, handling cross-day shifts (BR-05).</summary>
        public static int DurationMinutes(int startMinutes, int endMinutes)
        {
            int duration = endMinutes - startMinutes;
            if (duration < 0)
            {
                duration += 1440;
            }
            return duration;
        }

        /// <summary>True when the given time-of-day string represents a cross-day shift end (BR-05).</summary>
        public static bool IsCrossDay(string startTime, string endTime)
        {
            int? start = ParseMinutes(startTime);
            int? end = ParseMinutes(endTime);
            if (start == null || end == null)
            {
                return false;
            }
            return end.Value < start.Value;
        }
    }
}
