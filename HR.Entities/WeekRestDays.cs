using System;
using System.Collections.Generic;
using System.Globalization;

namespace HR.Entities
{
    /// <summary>
    /// Weekly rest days (BR-03) from the Sch.RestDays setting — replaces the
    /// fixed-Friday rule so every company can configure its own weekly rest
    /// days (rule 36: configurable, never hardcoded). Accepts Arabic day
    /// names, English day names (3+ letters) and numbers 0-6 (Sunday = 0),
    /// comma separated, e.g. "Friday,Saturday" or "الجمعة,السبت" or "5,6".
    /// </summary>
    public static class WeekRestDays
    {
        /// <summary>System Settings key holding the weekly rest days list.</summary>
        public const string SettingKey = "Sch.RestDays";

        /// <summary>Fallback (Friday only) when the setting is missing or invalid.</summary>
        public const string DefaultDays = "Friday";

        /// <summary>Parses the setting value; never returns an empty list.</summary>
        public static List<DayOfWeek> Parse(string raw)
        {
            List<DayOfWeek> result = new List<DayOfWeek>();
            string source = string.IsNullOrWhiteSpace(raw) ? DefaultDays : raw;
            string[] tokens = source.Split(',', '\u060C', ';', '|');
            foreach (string token in tokens)
            {
                DayOfWeek? parsed = ParseToken(token);
                if (parsed != null && !result.Contains(parsed.Value))
                {
                    result.Add(parsed.Value);
                }
            }
            if (result.Count == 0)
            {
                result.Add(DayOfWeek.Friday);
            }
            return result;
        }

        /// <summary>Operational week starts the day after the latest configured rest
        /// day (Friday only → Saturday; Friday + Saturday → Sunday; Saturday → Sunday).</summary>
        public static DayOfWeek WeekStartAfterRest(List<DayOfWeek> restDays)
        {
            if (restDays == null || restDays.Count == 0)
            {
                return DayOfWeek.Saturday;
            }
            int latest = (int)restDays[0];
            foreach (DayOfWeek day in restDays)
            {
                if ((int)day > latest)
                {
                    latest = (int)day;
                }
            }
            return (DayOfWeek)((latest + 1) % 7);
        }

        /// <summary>Arabic list description used in messages ("الخميس، الجمعة").</summary>
        public static string Describe(List<DayOfWeek> restDays)
        {
            string[] arabic = new string[] { "الأحد", "الاثنين", "الثلاثاء", "الأربعاء", "الخميس", "الجمعة", "السبت" };
            List<string> names = new List<string>();
            foreach (DayOfWeek day in restDays)
            {
                names.Add(arabic[(int)day]);
            }
            return string.Join("، ", names.ToArray());
        }

        private static DayOfWeek? ParseToken(string token)
        {
            if (token == null)
            {
                return null;
            }
            string text = token.Trim();
            if (text.Length == 0)
            {
                return null;
            }
            int number;
            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out number) &&
                number >= 0 && number <= 6)
            {
                return (DayOfWeek)number;
            }
            string[] english = new string[] { "sunday", "monday", "tuesday", "wednesday", "thursday", "friday", "saturday" };
            for (int index = 0; index < english.Length; index++)
            {
                if (english[index].Equals(text, StringComparison.OrdinalIgnoreCase) ||
                    (text.Length >= 3 && english[index].StartsWith(text, StringComparison.OrdinalIgnoreCase)))
                {
                    return (DayOfWeek)index;
                }
            }
            string[] arabic = new string[]
            {
                "الأحد", "الاحد", "الاثنين", "الإثنين", "الثلاثاء", "الثلاثاء",
                "الأربعاء", "الاربعاء", "الخميس", "الخمي", "الجمعة", "الجمعه", "السبت"
            };
            int[] arabicDays = new int[] { 0, 0, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 6 };
            for (int index = 0; index < arabic.Length; index++)
            {
                if (string.Equals(arabic[index], text, StringComparison.Ordinal))
                {
                    return (DayOfWeek)arabicDays[index];
                }
            }
            return null;
        }
    }
}
