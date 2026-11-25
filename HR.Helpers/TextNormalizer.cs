using System;

namespace HR.Helpers
{
    /// <summary>
    /// Input normalization helpers. Windows Arabic keyboards produce
    /// Eastern-Arabic digits (٠-٩ U+0660..U+0669 and ۰-۹ U+06F0..U+06F9),
    /// which no numeric parser in this application accepts; every free-text
    /// time entry flows through NormalizeDigits before validation so that
    /// both digit systems behave identically (root-cause fix for rejected
    /// mission/shift/adjustment time entries).
    /// </summary>
    public static class TextNormalizer
    {
        /// <summary>Converts Eastern-Arabic digit characters into ASCII digits.</summary>
        public static string NormalizeDigits(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }
            bool changed = false;
            char[] chars = null;
            for (int i = 0; i < text.Length; i++)
            {
                char original = text[i];
                int value = -1;
                if (original >= '\u0660' && original <= '\u0669')
                {
                    value = original - '\u0660';
                }
                else if (original >= '\u06F0' && original <= '\u06F9')
                {
                    value = original - '\u06F0';
                }
                if (value >= 0)
                {
                    if (!changed)
                    {
                        chars = text.ToCharArray();
                        changed = true;
                    }
                    chars[i] = (char)('0' + value);
                }
            }
            return changed ? new string(chars) : text;
        }

        /// <summary>Trims and normalizes digits — the standard entry-point normalization.</summary>
        public static string NormalizeTimeText(string text)
        {
            return (NormalizeDigits(text) ?? string.Empty).Trim();
        }
    }
}
