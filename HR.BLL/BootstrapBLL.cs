using HR.DAL;

namespace HR.BLL
{
    /// <summary>Composition-root bootstrap: creates the MDB and applies the schema once.</summary>
    public static class BootstrapBLL
    {
        /// <summary>Creates the database file on first run; safe to call at every start.</summary>
        public static void EnsureDatabase()
        {
            DbInitializer.EnsureInitialized();
        }
    }
}
