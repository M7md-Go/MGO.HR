using System.Collections.Generic;
using HR.DAL;
using HR.Entities;

namespace HR.BLL
{
    /// <summary>Read access to the historical trail of each employee (rule 8 — employee history).</summary>
    public static class EmployeeHistoryBLL
    {
        /// <summary>History rows of one employee, newest first.</summary>
        public static List<EmployeeHistory> GetForEmployee(int employeeId, int maxRows)
        {
            return EmployeeHistoryDAL.GetForEmployee(employeeId, maxRows);
        }
    }
}
