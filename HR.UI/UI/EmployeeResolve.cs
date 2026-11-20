using System;
using System.Collections.Generic;
using HR.BLL;
using HR.Entities;

namespace HR.UI.UI
{
    /// <summary>
    /// Single shared resolver from free text (employee code or name) to an
    /// exact employee. Root-cause fix for "first match wins": the code is
    /// matched exactly first; name matches are accepted only when unique
    /// (otherwise the caller shows the ambiguity error).
    /// </summary>
    internal static class EmployeeResolve
    {
        /// <summary>Resolves text to an employee; on failure returns null with a reason in resolveError.</summary>
        internal static Employee FromText(string text, out string resolveError)
        {
            resolveError = null;
            string candidate = (text ?? string.Empty).Trim();
            if (candidate.Length == 0)
            {
                return null;

            }
            if (IsDigitsOnly(candidate))
            {
                Employee byCode = EmployeeBLL.GetByCode(candidate, true);
                if (byCode != null)
                {
                    return byCode;
                }
            }

            EmployeeSearchFilter filter = new EmployeeSearchFilter();
            filter.Text = candidate;
            filter.IncludeArchived = true;
            filter.PageSize = 8;
            List<Employee> matches = EmployeeBLL.Search(filter);

            foreach (Employee match in matches)
            {
                if (string.Equals(match.EmployeeCode, candidate, StringComparison.OrdinalIgnoreCase))
                {
                    return match;
                }
            }
            if (matches.Count == 1)
            {
                return matches[0];
            }
            if (matches.Count == 0)
            {
                return null;
            }
            resolveError = "المطابقة لـ «" + candidate + "» غير محددة (" + matches.Count +
                " موظفين) — أدخل رقم الموظف كاملًا.";
            return null;
        }

        private static bool IsDigitsOnly(string value)
        {
            foreach (char digit in value)
            {
                if (!char.IsDigit(digit))
                {
                    return false;
                }
            }
            return value.Length > 0;
        }
    }
}
