using System;
using System.Collections.Generic;
using System.Data.OleDb;
using HR.Entities;

namespace HR.DAL
{
    /// <summary>
    /// PublicHolidays table access: the official holiday calendar the attendance
    /// engine consults (holiday days never count as absence). Date must stay
    /// unique (UQ in the schema); recurring rows repeat every year.
    /// </summary>
    public static class PublicHolidayDAL
    {
        public static List<PublicHoliday> List(DateTime? fromDate, DateTime? toDate)
        {
            string sql = "SELECT HolidayId, HolidayDate, HolidayName, IsRecurring, ShiftId, AppliesTo " +
                "FROM PublicHolidays WHERE 1 = 1";
            List<OleDbParameter> parameters = new List<OleDbParameter>();
            if (fromDate != null)
            {
                sql += " AND (IsRecurring = False AND HolidayDate >= ?)";
                parameters.Add(Db.PDate(fromDate.Value.Date));
            }
            if (toDate != null)
            {
                sql += " AND (IsRecurring = False AND HolidayDate <= ?)";
                parameters.Add(Db.PDate(toDate.Value.Date));
            }
            sql += " ORDER BY IsRecurring DESC, HolidayDate";
            return Db.ReadList(sql, MapHoliday, parameters.ToArray());
        }

        public static PublicHoliday GetById(int holidayId)
        {
            List<PublicHoliday> rows = Db.ReadList(
                "SELECT HolidayId, HolidayDate, HolidayName, IsRecurring, ShiftId, AppliesTo " +
                "FROM PublicHolidays WHERE HolidayId = ?",
                MapHoliday, Db.P(holidayId));
            return rows.Count > 0 ? rows[0] : null;
        }

        /// <summary>True when another row already occupies the same date (recurring rows exempt).</summary>
        public static bool DateExists(DateTime date, int? excludeHolidayId)
        {
            string sql = "SELECT COUNT(*) FROM PublicHolidays WHERE IsRecurring = False AND HolidayDate = ?";
            OleDbParameter[] parameters = excludeHolidayId == null
                ? new OleDbParameter[] { Db.PDate(date.Date) }
                : new OleDbParameter[] { Db.PDate(date.Date), Db.P(excludeHolidayId.Value) };
            if (excludeHolidayId != null)
            {
                sql += " AND HolidayId <> ?";
            }
            return Db.Count(sql, parameters) > 0;
        }

        public static int Insert(PublicHoliday holiday)
        {
            string sql = "INSERT INTO PublicHolidays (HolidayDate, HolidayName, IsRecurring, ShiftId, AppliesTo) " +
                "VALUES (?, ?, ?, ?, ?)";
            return Db.ExecuteInTransaction(delegate(OleDbConnection connection, OleDbTransaction transaction)
            {
                using (OleDbCommand command = new OleDbCommand(sql, connection, transaction))
                {
                    command.Parameters.Add(Db.PDateRequired(holiday.HolidayDate));
                    command.Parameters.Add(Db.P(holiday.HolidayName));
                    command.Parameters.Add(Db.PBool(holiday.IsRecurring));
                    command.Parameters.Add(Db.PNullableId(holiday.ShiftId));
                    command.Parameters.Add(Db.PNullableId(holiday.AppliesTo));
                    command.ExecuteNonQuery();
                }
                using (OleDbCommand identity = new OleDbCommand("SELECT @@IDENTITY", connection, transaction))
                {
                    return Convert.ToInt32(identity.ExecuteScalar());
                }
            });
        }

        public static void Update(PublicHoliday holiday)
        {
            Db.ExecuteNonQuery(
                "UPDATE PublicHolidays SET HolidayDate = ?, HolidayName = ?, IsRecurring = ?, ShiftId = ?, AppliesTo = ? " +
                "WHERE HolidayId = ?",
                Db.PDateRequired(holiday.HolidayDate), Db.P(holiday.HolidayName), Db.PBool(holiday.IsRecurring),
                Db.PNullableId(holiday.ShiftId), Db.PNullableId(holiday.AppliesTo), Db.P(holiday.HolidayId));
        }

        public static void Delete(int holidayId)
        {
            Db.ExecuteNonQuery("DELETE FROM PublicHolidays WHERE HolidayId = ?", Db.P(holidayId));
        }

        private static PublicHoliday MapHoliday(OleDbDataReader r)
        {
            PublicHoliday row = new PublicHoliday();
            row.HolidayId = r.GetInt32(0);
            row.HolidayDate = r.IsDBNull(1) ? (DateTime?)null : r.GetDateTime(1);
            row.HolidayName = r.IsDBNull(2) ? null : r.GetString(2);
            row.IsRecurring = r.IsDBNull(3) ? (bool?)null : Convert.ToInt32(r.GetValue(3)) != 0;
            row.ShiftId = r.IsDBNull(4) ? (int?)null : Convert.ToInt32(r.GetValue(4));
            row.AppliesTo = r.IsDBNull(5) ? (int?)null : Convert.ToInt32(r.GetValue(5));
            return row;
        }
    }
}
