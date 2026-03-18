using java.sql;
using Ormix.Extensions.SystemAdmin.Parsers;


namespace Ormix.Extensions.SystemAdmin
{
    public static class OnStatExtensions
    {

        /// <summary>
        /// The command onstat -g env sid displays the environment for the session
        /// https://www.oninit.com/onstat/pda.php?id=onstat-genv
        /// </summary>
        /// <param name="dbConnection"></param>
        /// <param name="executedSql"></param>
        /// <param name="sessionId"></param>
        /// <returns></returns>
        public static string GetSessionEnvironmentVariables(this Connection dbConnection, out string executedSql, int? sessionId = null)
        {
            executedSql = sessionId == null ?
                $"execute function sysadmin:task('onstat','-g env all');" :
                $"execute function sysadmin:task('onstat','-g env {sessionId?.ToString()}');";            
            return dbConnection.QuerySingleString(executedSql).Data[0];
        }


        /// <summary>
        /// Monitor the database server
        /// https://www.ibm.com/docs/en/informix-servers/12.10.0?topic=saaf-onstat-argument-monitor-database-server-sql-administration-api
        /// </summary>
        /// <param name="dbConnection"></param>
        /// <param name="sessionId"></param>
        /// <returns></returns>
        public static string MonitorDatabaseServer(this Connection dbConnection, out string executedSql, int? sessionId = null)
        {
            executedSql = $"execute function sysadmin:task('onstat','-g ses {sessionId?.ToString()}');";
            return dbConnection.QuerySingleString(executedSql).Data[0];
        }


        /// <summary>
        /// Use the onstat -g sql command to display SQL-related information about a session.
        /// https://www.ibm.com/docs/en/informix-servers/12.10.0?topic=ogmo-onstat-g-sql-command-print-sql-related-session-information
        /// </summary>
        /// <param name="dbConnection"></param>
        /// <param name="sessionId"></param>
        /// <returns></returns>
        public static string SqlRelatedSessionInformation(this Connection dbConnection, out string executedSql, int? sessionId = null)
        {
            executedSql = $"execute function sysadmin:task('onstat','-g sql {sessionId?.ToString()}');";
            return dbConnection.QuerySingleString(executedSql).Data[0];
        }


        /// <summary>
        /// Use the onstat -k command to print information about active locks, including the address of the lock in the lock table.
        /// https://www.ibm.com/docs/en/informix-servers/12.10.0?topic=utility-onstat-k-command-print-active-lock-information
        /// </summary>
        /// <param name="dbConnection"></param>
        /// <param name="sessionId"></param>
        /// <returns></returns>
        public static string ActiveLockInformation(this Connection dbConnection, out string executedSql)
        {
            executedSql = $"execute function sysadmin:task('onstat','-k');";
            return dbConnection.QuerySingleString(executedSql).Data[0];
        }



        /// <summary>
        /// onstat -g pqs command: Print operators for all SQL queries
        /// 
        /// Use the onstat –g pqs command to display information about the operators used in all of the SQL queries that are currently running.
        /// You can use this command to troubleshoot an application, to find which operators are running for the query and for how long,
        /// and how many rows each operator returns.While the EXPLAIN file contains information that will give you a general sense of the query plan, 
        /// the onstat –g pqs command displays the runtime operator information for the query and the query plan.
        /// 
        /// https://www.ibm.com/docs/en/informix-servers/12.10.0?topic=ogmo-onstat-g-pqs-command-print-operators-all-sql-queries
        /// </summary>
        /// <param name="dbConnection"></param>
        /// <param name="sessionId"></param>
        /// <returns></returns>
        public static string PrintOperatorsForAllSQLQueries(this Connection dbConnection, out string executedSql, int? sessionId = null)
        {
            executedSql = $"execute function sysadmin:task('onstat','-g pqs {sessionId?.ToString()}');";
            return dbConnection.QuerySingleString(executedSql).Data[0];
        }



        /// <summary>
        /// onstat -g stm command: Print SQL statement memory usage
        /// Use the onstat -g stm command to display the memory that each prepared SQL statement uses.
        /// By default, only the DBSA can view onstat -g stm syssqltrace information.However, when the UNSECURE_ONSTAT configuration parameter is set to 1, all users can view this information.
        /// Read syntax diagram
        /// 
        /// https://www.ibm.com/docs/en/informix-servers/12.10.0?topic=ogmo-onstat-g-stm-command-print-sql-statement-memory-usage
        /// </summary>
        /// <param name="dbConnection"></param>
        /// <param name="sessionId"></param>
        /// <returns></returns>
        public static string SQLStatementMemoryUsage(this Connection dbConnection, out string executedSql, int? sessionId = null)
        {
            executedSql = $"execute function sysadmin:task('onstat','-g stm {sessionId?.ToString()}');";
            return dbConnection.QuerySingleString(executedSql).Data[0];
        }



        public static async Task<OnStatLockSnapshot> GetLockSnapshotAsync(this Connection dbConnection, CancellationToken cancellationToken = default)
            => await Task.Run(() => dbConnection.GetLockSnapshot(), cancellationToken);

        public static OnStatLockSnapshot GetLockSnapshot(this Connection dbConnection)
        {
            return dbConnection
                    .QuerySingle<OnStatLockSnapshot>(@"select 
									 sysadmin:task('onstat', '-u') as user_activity_profile
									,sysadmin:task('onstat', '-k') as active_lock_information
									from sysmaster:sysdual")!;
        }


        public static async Task<OnStatLockSnapshotData> GetLockSnapshotDataAsync(this Connection dbConnection, CancellationToken cancellationToken = default)
            => await Task.Run(() => dbConnection.GetLockSnapshotData(), cancellationToken);

        public static OnStatLockSnapshotData GetLockSnapshotData(this Connection dbConnection)
        {
            var parserParserUserActivityProfile = new ParserUserActivityProfile();
            var parserActiveLocks = new ParserActiveLockInformation();

            var lockSnapshot = dbConnection.GetLockSnapshot();
            var userActivityProfile = parserParserUserActivityProfile.Parse(lockSnapshot.user_activity_profile);
            var activeLocks = parserActiveLocks.Parse(lockSnapshot.active_lock_information);

            return new OnStatLockSnapshotData()
            {
                UserThreads = userActivityProfile,
                ActiveLocks = activeLocks,
            };
        }
    }


    public class OnStatLockSnapshot
    {
        public string user_activity_profile { get; set; } = string.Empty;
        public string active_lock_information { get; set; } = string.Empty;
    }

    public class OnStatLockSnapshotData
    {
        public List<UserThread> UserThreads { get; set; }
        public List<ActiveLock> ActiveLocks { get; set; }
    }
}
