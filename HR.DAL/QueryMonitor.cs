using System;
using System.Collections.Generic;

namespace HR.DAL
{
    /// <summary>
    /// In-memory slow-query monitor (performance rule): Db.* helpers measure
    /// elapsed milliseconds per executed statement and Record here when above
    /// ThresholdMs. The threshold is pushed from AppSettings
    /// (Performance.SlowQueryMs) by SettingsBLL.Refresh - never hardcoded
    /// values become policy (rule 36). Intentionally bounded and read-only:
    /// Jet 4.0 exposes no query statistics, so this is the practical
    /// alternative; nothing is written to the database.
    /// </summary>
    public static class QueryMonitor
    {
        /// <summary>Slow when elapsed >= this (configurable via Performance.SlowQueryMs).</summary>
        public static int ThresholdMs = 500;

        private const int Capacity = 50;
        private static readonly object Sync = new object();
        private static readonly List<SlowQuery> Recent = new List<SlowQuery>();

        public class SlowQuery
        {
            public string Sql { get; set; }
            public long ElapsedMs { get; set; }
            public DateTime At { get; set; }
        }

        /// <summary>Records a completed statement (never throws - monitoring must not break data).</summary>
        public static void Record(string sql, long elapsedMs)
        {
            if (elapsedMs < ThresholdMs)
            {
                return;
            }
            try
            {
                lock (Sync)
                {
                    SlowQuery entry = new SlowQuery();
                    entry.Sql = sql;
                    entry.ElapsedMs = elapsedMs;
                    entry.At = DateTime.Now;
                    Recent.Insert(0, entry);
                    if (Recent.Count > Capacity)
                    {
                        Recent.RemoveRange(Capacity, Recent.Count - Capacity);
                    }
                }
            }
            catch (Exception)
            {
                // monitoring failure must never affect the data operation
            }
        }

        /// <summary>Newest slow statements first (bounded snapshot).</summary>
        public static List<SlowQuery> Snapshot()
        {
            lock (Sync)
            {
                return new List<SlowQuery>(Recent);
            }
        }

        /// <summary>Clears the in-memory log (e.g. after reviewing on the monitoring screen).</summary>
        public static void Clear()
        {
            lock (Sync)
            {
                Recent.Clear();
            }
        }
    }
}
