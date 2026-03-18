/*
 https://www.ibm.com/docs/en/informix-servers/14.10.0?topic=down-enable-sql-tracing
 https://www.ibm.com/docs/en/informix-servers/14.10.0?topic=down-disable-sql-tracing-globally-session
 */

using java.sql;
using Ormix.Model;
using SOrmix.Model;


namespace Ormix.Extensions.SystemAdmin
{
    public static class SQLApiExtensions
    {
        public static async Task<string> GetIfxVersionAsync(this Connection dbConnection, CancellationToken cancellationToken = default)
            => await Task.Run(() => dbConnection.GetIfxVersion(), cancellationToken);
        public static string GetIfxVersion(this Connection dbConnection)
            => dbConnection.QuerySingleString($"select dbinfo('version', 'full') as version_info from systables where tabid = 1;").Data[0];

        public static string SetSqlUserTracingWithSessionId(this Connection dbConnection, int sessionId, bool enabled)
        {
            string onOff = enabled ? "on" : "off";
            return dbConnection.QuerySingleString(
                $"select sysadmin:task('set sql user tracing {onOff}', {sessionId}) from sysmaster:sysdual").Data[0];
        }

        public static string SetSqlTracing(this Connection dbConnection, bool enabled)
        {
            string onOff = enabled ? "on" : "off";
            return dbConnection.QuerySingleString(
                $"select sysadmin:task('set sql tracing {onOff}') from sysmaster:sysdual").Data[0];
        }


        public static List<string> SetSqlTraceClear(this Connection dbConnection)
        {
            return dbConnection.QuerySingleString(
                $@"select sysadmin:task(""set sql user tracing clear"")       from sysmaster:sysdual 
        union all select sysadmin:task('set sql tracing user clear')       from sysmaster:sysdual
        union all select sysadmin:task('set sql tracing database clear')   from sysmaster:sysdual
        union all select sysadmin:task('set sql tracing session', 'clear') from sysmaster:sysdual").Data;
        }


        public static dynamic? GetLockTabBySessionId(this Connection dbConnection, int sessionId)
        {
            var dynamicResult = dbConnection
                .QueryDynamic(@$"select * from sysmaster:syslcktab  
					where owner in (select address from sysmaster:systxptab 
												where owner in (select address from sysmaster:sysrstcb 
												where sid = {sessionId}))");
            return dynamicResult.Data.Any() ? dynamicResult.Data[0] : null;
        }


        public static List<string> GetSqlTraceInfos(this Connection dbConnection)
        {
            return dbConnection.QuerySingleString(
                $@"       select sysadmin:task('set sql tracing info')             from sysmaster:sysdual
                union all select sysadmin:task('set sql tracing database list')    from sysmaster:sysdual
                union all select sysadmin:task('set sql tracing user list')        from sysmaster:sysdual
                union all select sysadmin:task('set sql tracing session list')     from sysmaster:sysdual").Data;
        }

        public static List<SqlSysSession> GetSessions(this Connection dbConnection)
        {
            return dbConnection
                .Query<SqlSysSession>("select * from sysmaster:syssessions").Data;
        }

        public static List<CommandHistory> GetCommandHistory(this Connection dbConnection)
        {
            return dbConnection
                .Query<CommandHistory>(@"select * from sysadmin:command_history 
                            order by cmd_number desc").Data;
        }

        public static List<SqlSysTrace> GetTraces(this Connection dbConnection)
        {
            return dbConnection
                .Query<SqlSysTrace>($@"select * from sysmaster:syssqltrace limit 30").Data;
        }

        public static List<SysSqExplain> GetSqExplains(this Connection dbConnection, int sessionId)
        {
            return dbConnection
                .Query<SysSqExplain>($@"select * from sysmaster:syssqexplain 
                    where sqx_sessionid = :sqx_sessionid", new
                {
                    sqx_sessionid = sessionId
                }).Data;
        }

        public static List<SysPtProf> GetSysPtProf(this Connection dbConnection)
        {
            return dbConnection
                .Query<SysPtProf>($@"select * from sysmaster:sysptprof
                       where  lockreqs > 0
                          and lockwts > 0
                    and dbsname not in ('sysmaster','sysadmin','sysuser','sysutils')   
                    order by lockwts desc").Data;
        }

        public static List<SysSessionProfile> GetSysSessionProfiles(this Connection dbConnection)
        {
            return dbConnection
                .Query<SysSessionProfile>($@"select  trim(ses.username) as username 
                                   ,prof.sid as sessionid
                                   ,prof.lockreqs as lockreqs       -- Number of locks requested
                                   ,prof.locksheld as locksheld     -- Number of locks currently held 
                                   ,prof.lockwts as lockwts         -- Number of times waited for a lock
                                   ,prof.deadlks as deadlks         -- Number of deadlocks detected
                                   ,prof.lktouts as lktouts         -- Number of deadlock time-outs
                            from sysmaster:syssesprof as prof
                            inner join sysmaster:syssessions as ses on ses.sid = prof.sid
                            where prof.lockwts > 0
                              and prof.locksheld > 0
                            order by prof.locksheld desc,
                                     prof.lockwts desc ").Data;
        }


        public static List<dynamic> GetMostExpensiveSqlRunning(this Connection dbConnection)
        {
            return dbConnection
                .QueryDynamic($@"select 
                                      trim(sqx_sqlstatement)  as sqlstatement
                                     ,sum(sqx_estcost) as sum_estcost
                                     ,count(*) as count_executions
                                from sysmaster:syssqexplain
                                group by 1
                                order by 2 desc; ").Data;
        }


        public static void CreateLockTypeLookupTempTable(this Connection dbConnection)
        {
            dbConnection
                .Execute($@"drop table if exists lock_type_lookup;   
                            select * from (
                                        select 'B'  as lock_type, 'Byte lock' as lock_description from sysmaster:sysdual
                             union all select 'IS'  as lock_type, 'Intent shared lock'  as lock_description from sysmaster:sysdual
                             union all select 'S'   as lock_type, 'Shared lock' as lock_description from sysmaster:sysdual  
                             union all select 'XS'  as lock_type, 'Shared key value held by a repeatable reader'  as lock_description from sysmaster:sysdual
                             union all select 'U'   as lock_type, 'Update lock'  as lock_description from sysmaster:sysdual
                             union all select 'IX'  as lock_type, 'Intent exclusive lock'  as lock_description from sysmaster:sysdual
                             union all select 'SIX' as lock_type, 'Shared intent exclusive lock'  as lock_description from sysmaster:sysdual
                             union all select 'X'   as lock_type, 'Exclusive lock'  as lock_description from sysmaster:sysdual
                             union all select 'XR'  as lock_type, 'Exclusive key value held by a repeatable reader'  as lock_description from sysmaster:sysdual
                            ) into temp lock_type_lookup with no log;
                        ");
        }

        public static void DropLockTypeLookupTempTable(this Connection dbConnection)
        {
            dbConnection.Execute($@"drop table if exists lock_type_lookup;");
        }

        public static List<LockSessionInfo> LockMonitorData(this Connection dbConnection, string? lockedTableName, int? excludeSessionId)
        {
            /* Gelen lock hatasında tablo adi geciyor ve basarili bir sekilde parse edilmis ve lockedTableName parametresine dolu gelmisse, 
               iste o vakit tablo adini filtreliyoruz. Bu hakikat, zaten goren gozler icin, asagidaki SQL sorgusunda gorulmektetir. Oyle degil mi... 
            */
            string wlock = string.IsNullOrWhiteSpace(lockedTableName) ? $" and tabname.tabname = '{lockedTableName}'" : string.Empty;
            
            // Bu sorguyu calistiran, connectiona ait session bilgilerine ait kilit bilgileri gelmesin.
            string wExcludeSessionId = excludeSessionId.HasValue ? $" and session.sid <> {excludeSessionId}" : string.Empty; 

            return dbConnection
                .Query<LockSessionInfo>(
                @$"select  
	                  txptab.txid
	                , session.sid as owner
	                , wait_thread.sid as waiter
	                , session.pid
	                , (current - dbinfo('utc_to_datetime', session.connected)) :: interval hour(4) to second as connection_duration 
	                , trim(session.hostname) as hostname
	                , trim(session.username) as username
	                , trim(tabname.dbsname) as dbsname
	                , trim(tabname.tabname) as tabname
	                , decode(lcktab.rowidn,0,'T','R') || flags_txt.txt[1,3] as type
	                , (current - dbinfo('utc_to_datetime', lcktab.grtime)) :: interval hour(4) to second as lock_duration
	                , session.feprogram
	                , sysadmin:task('onstat','-g sql '||session.sid) as onstat_session_sql
	                , sysadmin:task('onstat','-g ses '||session.sid) as onstat_monitor_session
                from    

	                sysmaster:syslcktab        as lcktab,
	                sysmaster:systabnames      as tabname,
	                sysmaster:systxptab        as txptab,
	                sysmaster:sysrstcb         as lock_thread,
	                sysmaster:flags_text       as flags_txt,
	                sysmaster:syssessions      as session,
	                outer sysmaster:sysrstcb   as wait_thread
	
                where   tabname.partnum     = lcktab.partnum
	                and txptab.address      = lcktab.owner
	                and lock_thread.address = txptab.owner
	                and lcktab.wtlist      	= wait_thread.address   -- lock waiters 
	                and flags_txt.flags    	= lcktab.type
	                and session.sid         = lock_thread.sid
	                and flags_txt.tabname   = 'syslcktab'
	                and tabname.dbsname    != 'sysmaster'   		-- real databases only
	                and tabname.tabname    not like '% %'  		    -- real tables only
	                and flags_txt.txt      not like '%I%'  		    -- ignore ""intended"" locks 
	                and tabname.tabname <> 'command_history'
	                {wlock}
	                {wExcludeSessionId}
                limit 10;").Data;
        }

        public static async Task<AggregateResult<SqlSysLock, SqlSysLockAggregate>> GetLocksAsync(this Connection dbConnection, LockFilter? lockFilter, CancellationToken cancellationToken = default)
            => await Task.Run(() => dbConnection.GetLocks(lockFilter), cancellationToken);

        public static AggregateResult<SqlSysLock, SqlSysLockAggregate> GetLocks(this Connection dbConnection, LockFilter? lockFilter)
        {
            var wLimit = (lockFilter?.Limit ?? 0) > 0 ? $"limit {lockFilter?.Limit}" : string.Empty;

            var wLockedUser = string.Join(Environment.NewLine, lockFilter?.LockedUserUids
                    .Where(x => x > 0)
                    .Select(x => $"and sl.uid = {x} ")
                    .ToArray()!);

            var wWaiterUser = string.Join(Environment.NewLine, lockFilter?.WaiterUserUids
                    .Where(x => x > 0)
                    .Select(x => $"and sw.uid = {x} ")
                    .ToArray()!);

            var wDatabases = string.Join(Environment.NewLine, lockFilter?.DatabasePartNums
                    .Where(x => x > 0)
                    .Select(x => $"and b.partnum = {x} ")
                    .ToArray()!);

            var wTables = string.Join(Environment.NewLine, lockFilter?.TablePartNums
                    .Where(x => x > 0)
                    .Select(x => $"and b.partnum = {x} ")
                    .ToArray()!);

            return dbConnection.Query<SqlSysLock, SqlSysLockAggregate>($@"select 
                     trim(sl.username) as locked_user
                    ,sl.sid as locked_session

                    ,sw.username as waiter_user
                    ,sw.sid as waiter_session

                    ,f2.txt as lock_owner_wait_reason 
                    ,f3.txt as lock_waiter_wait_reason  

                    ,c.txid as transactionid
                    ,c.flags as flags                     -- INTEGER,         transaction flags             
                    ,c.dlklist                            -- INTEGER,         used for deadlk detection     
                    ,c.deadflag                           -- SMALLINT,        flag for deadlock detection   

                    ,c.lkwaitcnt                          -- SMALLINT,        # of threads waiting on locks 
                    ,c.lklist                             -- INTEGER,         list of locks held            
                    ,c.wtlist                             -- INTEGER,         users waiting for this tx     

                    ,c.nlocks                             -- INTEGER,         number of locks held          
                    ,c.lkwait                             -- SMALLINT,        lock wait timeout             
                    ,c.longtx                             -- SMALLINT,        this is long transaction                   
                    , trim(b.dbsname) as database_name
                    , trim(b.tabname) as table_name
                    , rowidr as lock_rowid
                    , keynum
                    , e.txt[1,4] as lock_type
                    , ltl.lock_description as lock_type_desc
                    , d.sid as owner
                    , f.sid as waiter
                    , a.grtime                             -- INTEGER          time lock was granted         
                    --, DBINFO('utc_to_datetime', a.grtime) as grtime_utc

                    ,trim(cast(l_scb.cbl_stmt as varchar(200))) as locked_sql_statement
                    ,trim(cast(w_scb.cbl_stmt as varchar(200))) as waiter_sql_statement
                    from sysmaster:syslcktab a                                      --   Locks 
                    left join sysmaster:systabnames b on a.partnum = b.partnum     --   join for partnums to table names 
                    left join sysmaster:systxptab c on a.owner = c.address         --   Transactions 
                    left join sysmaster:sysrstcb d on c.owner = d.address          --   rsam thread control blocks 
                    left join sysmaster:sysrstcb f on a.wtlist = f.address         --   rsam thread control blocks 
                    left join sysmaster:flags_text e on e.tabname = 'syslcktab' and e.flags = a.type 

                    inner join sysmaster:syssessions sl on sl.sid = d.sid
                    left join sysmaster:syssessions sw on sw.sid = f.sid
                    left join lock_type_lookup ltl on ltl.lock_type = e.txt[1,4]
                    left join sysmaster:sysconblock l_scb on l_scb.cbl_sessionid = d.sid
                    left join sysmaster:sysconblock w_scb on w_scb.cbl_sessionid = f.sid
                    left join sysmaster:systwaits w on d.tid = w.tid 


                    left join sysmaster:systwaits w2 on d.tid = w2.tid
                    left join sysmaster:flags_text f2 on w2.wreason = f2.flags and f2.tabname = 'systwaits'

                    left join sysmaster:systwaits w3 on f.tid = w3.tid
                    left join sysmaster:flags_text f3 on w3.wreason = f3.flags and f3.tabname = 'systwaits'


                    where b.partnum <> 1048578
                    {wLockedUser}
                    {wWaiterUser}
                    {wDatabases}
                    {wTables}                       
                    order by sl.sid desc 
                    {wLimit}
            ",
            aggregate: (sysLock, sqlSysLockAggregate) =>
            {
                if (sysLock.waiter_session > 0)
                    sqlSysLockAggregate.waiter_session_count++;
            });
        }


        public static List<SysDatabase> GetSysDatabases(this Connection dbConnection)
        {
            return dbConnection
                .Query<SysDatabase>($@"select 
                           partnum
                          ,trim(name) as name
                        from sysmaster:sysdatabases 
                        where partnum not in (1048855, 1049190, 1048951, 1048694)").Data;
        }

        public static List<SysTable> GetSysTables(this Connection dbConnection)
        {
            return dbConnection
                .Query<SysTable>($@"select * from (
                        select 
                          partnum
                        , trim(tabname) as name
                        from systables
                        where partnum > 0
                         and tabtype = 'T'
                         and tabname not like 'sys%'
                 union all
                        select 
                          partnum
                        , trim(tabname) as name
                        from systables
                        where partnum > 0
                         and tabtype = 'T'
                         and tabname not like 'sys%'
                ) where nvl(name,'') <> '' 
                order by partnum asc").Data;
        }

        public static async Task<List<SysProcedure>> GetSysProceduresAsync(this Connection dbConnection, string searchData, CancellationToken cancellationToken = default, params int[] excludeProcIds)
            => await Task.Run(() => dbConnection.GetSysProcedures(searchData, excludeProcIds), cancellationToken);
        public static async Task<List<SysFunction>> GetSysFunctionsAsync(this Connection dbConnection, string searchData, CancellationToken cancellationToken = default, params int[] excludeProcIds)
            => await Task.Run(() => dbConnection.GetSysFunctions(searchData, excludeProcIds), cancellationToken);

        public static async Task<List<SysTrigger>> SearchDataInTriggerBodyAsync(this Connection dbConnection, string searchData, CancellationToken cancellationToken = default)
            => await Task.Run(() => dbConnection.SearchDataInTriggerBody(searchData), cancellationToken);

        public static async Task<List<SysView>> SearchDataInViewsBodyAsync(this Connection dbConnection, string searchData, int? excludeTabId = null, CancellationToken cancellationToken = default)
             => await Task.Run(() => dbConnection.SearchDataInViewsBody(searchData, excludeTabId), cancellationToken);



        public static async Task<string> GetProcFuncDdlByNameAsync(this Connection dbConnection, string name, bool isProc = true, CancellationToken cancellationToken = default)
            => await Task.Run(() => dbConnection.GetProcFuncDdlByName(name, isProc), cancellationToken);

        public static async Task<string> GetTriggerDdlByNameAsync(this Connection dbConnection, string name, CancellationToken cancellationToken = default)
            => await Task.Run(() => dbConnection.GetTriggerDdlByName(name), cancellationToken);

        public static async Task<string> GetViewDdlByNameAsync(this Connection dbConnection, string name, CancellationToken cancellationToken = default)
            => await Task.Run(() => dbConnection.GetViewDdlByName(name), cancellationToken);


        public static List<SysProcedure> GetSysProcedures(this Connection dbConnection, string searchData, params int[] excludeProcIds)
        {
            var wExcludeProcIds = string.Join(Environment.NewLine, excludeProcIds
                        .Where(x => x > 0)
                        .Select(x => $" and p.procid <> {x} ")
                        .ToArray()!);

            return dbConnection
                .Query<SysProcedure>($@"select distinct
                        p.procid
                       ,p.procname as name
                    from
                        sysprocedures as p,
                        sysprocbody as b
                    where   p.procid = b.procid
                        and b.datakey = 'T'  
                        and p.isproc = 't' 
                        and (b.data like '%{searchData}%' 
                             or b.data like '%{searchData.ToUpperInvariant()}%'
                             or b.data like '%{searchData.ToLowerInvariant()}%')
                        {wExcludeProcIds}
                    order by p.procname asc").Data;
        }

        public static List<SysFunction> GetSysFunctions(this Connection dbConnection, string searchData, params int[] excludeProcIds)
        {
            var wExcludeProcIds = string.Join(Environment.NewLine, excludeProcIds
                        .Where(x => x > 0)
                        .Select(x => $" and p.procid <> {x} ")
                        .ToArray()!);

            return dbConnection
                .Query<SysFunction>($@"select distinct
                        p.procid
                       ,p.procname as name
                    from
                        sysprocedures as p,
                        sysprocbody as b
                    where   p.procid = b.procid
                        and b.datakey = 'T'  
                        and p.isproc = 'f' 
                        and (b.data like '%{searchData}%'
                             or b.data like '%{searchData.ToUpperInvariant()}%'
                             or b.data like '%{searchData.ToLowerInvariant()}%')
                        {wExcludeProcIds}
                    order by p.procname asc").Data;
        }


        public static string GetProcFuncDdlByName(this Connection dbConnection, string name, bool isProc = true)
        {
            var wIsProc = isProc ? " and p.isproc = 't' " : " and p.isproc = 'f' ";
            var data = dbConnection
                .QuerySingleString($@"select 
                      b.data
                    from sysprocedures p, sysprocbody b
                    where p.procid = b.procid  
                      and b.datakey = 'T'
                      and p.procname = :procname 
                      {wIsProc}
                    order by b.seqno asc", new
                {
                    procname = name
                }).Data;

            return string.Join(string.Empty, data.ToArray()!);
        }


        public static string GetTriggerDdlByName(this Connection dbConnection, string triggerName)
        {
            var data = dbConnection
                .QuerySingleString($@"select 
                         b.data
                        from systriggers t, systrigbody b
                        where t.trigid = b.trigid
                          and t.trigname = :trigname
                          and b.datakey in ('D', 'A')
                        order by b.datakey desc, b.seqno asc", new
                {
                    trigname = triggerName
                }).Data;

            return string.Join(string.Empty, data.ToArray()!);
        }


        public static string GetViewDdlByName(this Connection dbConnection, string viewName)
        {
            var data = dbConnection
                .QuerySingleString($@"select  
                       v.viewtext
                    from sysviews v
                    inner join systables t on v.tabid = t.tabid
                    where t.tabname = :tabname
                    order by v.seqno asc
                    ", new
                {
                    tabname = viewName
                }).Data;

            return string.Join(string.Empty, data.ToArray()!);
        }


        public static List<SysTrigger> GetSysTriggersOfTable(this Connection dbConnection, string tableName)
        {
            return dbConnection
                .Query<SysTrigger>($@"select
                    t.tabname as table_name,
                    tr.trigname as trigger_name,
    
                    case tr.event
                        when 'I' then 'INSERT'
                        when 'U' then 'UPDATE'
                        when 'D' then 'DELETE'
                        when 'S' then 'SELECT'
                        when 'i' then 'INSTEAD OF INSERT'
                        when 'u' then 'INSTEAD OF UPDATE'
                        when 'd' then 'INSTEAD OF DELETE'
                    end as trigger_event
                from
                    systables as t,
                    systriggers as tr,
                    systrigbody as tb
                where   tr.tabid = t.tabid
                    and t.tabname like  '%{tableName}%'
                    and tb.trigid = tr.trigid
                    and tb.datakey in ('A', 'D');").Data;
        }

        public static List<SysTrigger> SearchDataInTriggerBody(this Connection dbConnection, string searchData)
        {
            return dbConnection
                .Query<SysTrigger>($@"select
                    t.tabname as table_name,
                    tr.trigname as trigger_name,
    
                    case tr.event
                        when 'I' then 'INSERT'
                        when 'U' then 'UPDATE'
                        when 'D' then 'DELETE'
                        when 'S' then 'SELECT'
                        when 'i' then 'INSTEAD OF INSERT'
                        when 'u' then 'INSTEAD OF UPDATE'
                        when 'd' then 'INSTEAD OF DELETE'
                    end as trigger_event
                from
                    systables as t,
                    systriggers as tr,
                    systrigbody as tb
                where tr.tabid = t.tabid
                    and tb.trigid = tr.trigid
                    and tb.datakey in ('A', 'D')
                    and (tb.data like '%{searchData}%' 
                        or tb.data like '%{searchData.ToUpperInvariant()}%' 
                        or tb.data like '%{searchData.ToLowerInvariant()}%')").Data;
        }



        public static List<SysView> SearchDataInViewsBody(this Connection dbConnection, string searchData, int? excludeTabId = null)
        {
            var excludeViewSelf = excludeTabId.HasValue ? $" and v.tabid <> {excludeTabId}" : string.Empty;
            return dbConnection
                .Query<SysView>($@"select  
                        trim(t.tabname) as viewname
                       ,v.tabid
                from sysviews v
                inner join systables t on v.tabid = t.tabid
                where  (v.viewtext like '%{searchData}%' 
                        or v.viewtext like '%{searchData.ToUpperInvariant()}%' 
                        or v.viewtext like '%{searchData.ToLowerInvariant()}%')
                {excludeViewSelf}
                order by t.tabname asc").Data;
        }



        public static dynamic? GetRowBy(this Connection dbConnection, string databaseName, string tableName, int rowId)
        {
            var result = dbConnection
                .QueryDynamic($@"select rowid,* from {databaseName}:{tableName} where rowid = :rowid", new
                {
                    rowid = rowId
                });
            return result.Data != null && result.Data.Any() ? result.Data[0] : null;
        }

        public static bool TryGetRowBy(this Connection dbConnection, string databaseName, string tableName, int rowId, out dynamic? tableRow)
        {
            tableRow = null;
            try
            {
                tableRow = dbConnection.GetRowBy(databaseName, tableName, rowId);
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

    }


    public class LockFilter
    {
        public const char Seperator = ',';
        public int Limit { get; set; }
        public int[] LockedUserUids { get; set; }
        public int[] WaiterUserUids { get; set; }
        public int[] DatabasePartNums { get; set; }
        public int[] TablePartNums { get; set; }
    }


    public class SqlSysLockAggregate
    {
        public int waiter_session_count { get; set; }
    }

    public class SysDatabase
    {
        public int partnum { get; set; }
        public string name { get; set; }
    }

    public class SysTable
    {
        public int partnum { get; set; }
        public string name { get; set; }
    }

    public class SysUser
    {
        public int uid { get; set; }
        public string username { get; set; }
    }

    public enum ObjecType : short
    {
        Procedure,
        Function,
        Trigger,
        View,
    }

    public class SysObject
    {
        private ObjecType objectType;
        public ObjecType ObjectType => objectType;

        public SysObject(ObjecType objectType)
          => this.objectType = objectType;
    }

    public class SysProcedure : SysObject
    {
        public SysProcedure() : base(ObjecType.Procedure) { }

        public int procid { get; set; }
        public string name { get; set; }
    }

    public class SysFunction : SysObject
    {
        public int procid { get; set; }
        public string name { get; set; }
        public SysFunction() : base(ObjecType.Function) { }
    }

    public class SysTrigger : SysObject
    {
        public string table_name { get; set; }
        public string trigger_name { get; set; }
        public string trigger_event { get; set; }
        public SysTrigger() : base(ObjecType.Trigger) { }
    }


    public class SysView : SysObject
    {
        public string viewname { get; set; }
        public int tabid { get; set; }
        public SysView() : base(ObjecType.View) { }
    }

    /// <summary>
    /// The command_history table contains the list and results of all the SQL administration API functions that were run in the previous 30 days.
    ///The command_history table shows each SQL administration API function that was run and displays information about the user who ran the function, the time the function was run, the primary arguments of the function, and the message returned when the database server completed running the function.
    /// https://www.ibm.com/docs/en/informix-servers/12.10.0?topic=database-command-history-table
    /// </summary>
    public class CommandHistory
    {
        /// <summary>
        /// The unique ID for each row.
        /// </summary>
        public int cmd_number { get; set; }

        /// <summary>
        /// The time that the function started.
        /// </summary>
        public DateTime cmd_exec_time { get; set; }

        /// <summary>
        /// The user who ran the function.
        /// </summary>
        public string cmd_user { get; set; }

        /// <summary>
        /// The name of the host computer from which the function was run.
        /// </summary>
        public string cmd_hostname { get; set; }

        /// <summary>
        /// The primary argument of the function that was run.
        /// </summary>
        public string cmd_executed { get; set; }

        /// <summary>
        /// Return code.
        /// </summary>
        public int cmd_ret_status { get; set; }

        /// <summary>
        /// Return message.
        /// </summary>
        public string cmd_ret_msg { get; set; }
    }


    public class SqlSysLock
    {
        public string locked_user { get; set; }
        public int locked_session { get; set; }

        public string waiter_user { get; set; }
        public int waiter_session { get; set; }

        public int transactionid { get; set; }
        public string lock_type { get; set; }
        public string lock_type_desc { get; set; }


        public int dlklist { get; set; }
        public int deadflag { get; set; }
        public int lkwaitcnt { get; set; }
        public int lklist { get; set; }
        public int wtlist { get; set; }

        public int nlocks { get; set; }
        public int lkwait { get; set; }
        public int longtx { get; set; }
        public string database_name { get; set; }
        public string table_name { get; set; }

        public int lock_rowid { get; set; }

        public int grtime { get; set; }
        public DateTimeOffset grtime_unix_epoch => DateTimeOffset.FromUnixTimeSeconds(grtime);
        public DateTime grtime_local => grtime_unix_epoch.LocalDateTime;

        public string locked_sql_statement { get; set; }
        public string waiter_sql_statement { get; set; }

        public string lock_owner_wait_reason { get; set; }
        public string lock_waiter_wait_reason { get; set; }
    }


    /// <summary>
    /// https://www.ibm.com/docs/en/informix-servers/12.10.0?topic=tables-syssessions
    /// </summary>
    public class SqlSysSession
    {
        /// <summary>
        /// Session ID
        /// </summary>
        public int sid { get; set; }

        /// <summary>
        /// User ID
        /// </summary>
        public string username { get; set; }

        /// <summary>
        /// User ID number
        /// </summary>
        public short uid { get; set; }

        /// <summary>
        /// Process ID of the client
        /// </summary>
        public int pid { get; set; }

        /// <summary>
        /// Hostname of client
        /// </summary>
        public string hostname { get; set; }

        /// <summary>
        /// Name of the user's stderr file
        /// </summary>
        public string tty { get; set; }

        /// <summary>
        /// Time that user connected to the database server
        /// Unix Timestamp (Epoch Time) formatı
        /// </summary>
        public int connected { get; set; }
        public DateTimeOffset connected_unix_epoch => DateTimeOffset.FromUnixTimeSeconds(connected);
        public DateTimeOffset connected_local_time => connected_unix_epoch.LocalDateTime;
        public DateTimeOffset connected_local_utc => connected_unix_epoch.UtcDateTime;

        public DateTime connected_time => connected_unix_epoch.LocalDateTime;
        public TimeSpan connected_elapsed => DateTime.Now - connected_unix_epoch.LocalDateTime;


        /// <summary>
        /// Absolute path of the executable program or application
        /// </summary>
        public string feprogram { get; set; }

        /// <summary>
        /// Session pool address
        /// </summary>
        public int pooladdr { get; set; }

        /// <summary>
        /// 1 if the primary thread for the session is waiting for a latch
        /// </summary>
        public int is_wlatch { get; set; }

        /// <summary>
        /// 1 if the primary thread for the session is waiting for a lock
        /// </summary>
        public int is_wlock { get; set; }

        /// <summary>
        /// 1 if the primary thread for the session is waiting for a buffer
        /// </summary>
        public int is_wbuff { get; set; }

        /// <summary>
        /// 1 if the primary thread for the session is waiting for a checkpoint
        /// </summary>
        public int is_wckpt { get; set; }

        /// <summary>
        /// 1 if the primary thread for the session is waiting for a log buffer
        /// </summary>
        public int is_wlogbuf { get; set; }

        /// <summary>
        /// 1 if the primary thread for the session is waiting for a transaction
        /// </summary>
        public int is_wtrans { get; set; }

        /// <summary>
        /// 1 if the session is a special monitoring process
        /// </summary>
        public int is_monitor { get; set; }

        /// <summary>
        /// 1 if the primary thread for the session is in a critical section
        /// </summary>
        public int is_incrit { get; set; }

        /// <summary>
        /// Flags
        /// </summary>
        public int state { get; set; }
    }


    /// <summary>
    /// https://www.ibm.com/docs/en/informix-servers/12.10.0?topic=tables-syssqltrace
    /// </summary>
    public class SqlSysTrace
    {
        /// <summary>
        /// Unique SQL execution ID
        /// </summary>
        public long sql_id { get; set; }

        /// <summary>
        /// Address of the statement in the code block
        /// </summary>
        public long sql_address { get; set; }

        /// <summary>
        /// Database session ID of the user running the SQL statement
        /// </summary>
        public int sql_sid { get; set; }

        /// <summary>
        /// User ID of the statement running the SQL
        /// </summary>
        public int sql_uid { get; set; }

        /// <summary>
        /// Statement type
        /// </summary>
        public int sql_stmttype { get; set; }

        /// <summary>
        /// Statement type displayed as a word
        /// </summary>
        public string sql_stmtname { get; set; }

        /// <summary>
        /// Time this statement completed (UNIX)
        /// </summary>
        public int sql_finishtime { get; set; }

        /// <summary>
        /// Time this transaction started
        /// </summary>
        public int sql_begintxtime { get; set; }


        /// <summary>
        /// Statement execution time
        /// </summary>
        public float sql_runtime { get; set; }
        public TimeSpan sql_runtime_t => TimeSpan.FromDays(sql_runtime);




        /// <summary>
        /// Number of disk reads for this SQL statement
        /// </summary>
        public int sql_pgreads { get; set; }

        /// <summary>
        /// Number of buffer reads for this SQL statement
        /// </summary>
        public int sql_bfreads { get; set; }

        /// <summary>
        /// Percentage of time the page was read from the buffer pool
        /// </summary>
        public float sql_rdcache { get; set; }
        public TimeSpan sql_rdcache_t => TimeSpan.FromDays(sql_rdcache);


        /// <summary>
        /// Number of index page buffer reads
        /// </summary>
        public int sql_bfidxreads { get; set; }

        /// <summary>
        /// Number of pages written to disk
        /// </summary>
        public int sql_pgwrites { get; set; }

        /// <summary>
        /// Number of pages modified and returned to the buffer pool
        /// </summary>
        public int sql_bfwrites { get; set; }

        /// <summary>
        /// Percentage of time a page was written to the buffer pool but not to disk
        /// </summary>
        public float sql_wrcache { get; set; }
        public TimeSpan sql_wrcache_t => TimeSpan.FromDays(sql_wrcache);



        /// <summary>
        /// Total number of locks required by this SQL statement
        /// </summary>
        public int sql_lockreq { get; set; }

        /// <summary>
        /// Number of times the SQL statement waited on locks
        /// </summary>
        public int sql_lockwaits { get; set; }

        /// <summary>
        /// Time the system waited for locks during SQL statement
        /// </summary>
        public float sql_lockwttime { get; set; }
        public TimeSpan sql_lockwttime_t => TimeSpan.FromDays(sql_lockwttime);





        /// <summary>
        /// Amount of space the SQL statement used in the logical log
        /// </summary>
        public int sql_logspace { get; set; }

        /// <summary>
        /// Number of sorts that ran for the statement
        /// </summary>
        public int sql_sorttotal { get; set; }

        /// <summary>
        /// Number of sorts that ran on disk
        /// </summary>
        public int sql_sortdisk { get; set; }

        /// <summary>
        /// Number of sorts that ran in memory
        /// </summary>
        public int sql_sortmem { get; set; }





        /// <summary>
        /// Number of times the SQL statement ran
        /// </summary>
        public int sql_executions { get; set; }

        /// <summary>
        /// Total amount of time spent running the statement
        /// </summary>
        public float sql_totaltime { get; set; }
        public TimeSpan sql_totaltime_t => TimeSpan.FromDays(sql_totaltime);


        /// <summary>
        /// Average amount of time spent running the statement
        /// </summary>
        public float sql_avgtime { get; set; }
        public TimeSpan sql_avgtime_t => TimeSpan.FromDays(sql_avgtime);


        /// <summary>
        /// Maximum amount of time spent executing the SQL statement
        /// </summary>
        public float sql_maxtime { get; set; }
        public TimeSpan sql_maxtime_t => TimeSpan.FromDays(sql_maxtime);




        /// <summary>
        /// Number of times an I/O operation had to wait
        /// </summary>
        public int sql_numiowaits { get; set; }

        /// <summary>
        /// Average amount of time that the SQL statement had to wait
        /// </summary>
        public float sql_avgiowaits { get; set; }
        public TimeSpan sql_avgiowaits_t => TimeSpan.FromDays(sql_avgiowaits);



        /// <summary>
        /// Total amount of time that the SQL statement had to wait for I/O. This excludes any asynchronous I/O.
        /// </summary>
        public float sql_totaliowaits { get; set; }
        public TimeSpan sql_totaliowaits_t => TimeSpan.FromDays(sql_totaliowaits);


        /// <summary>
        /// Average number of rows (per second) produced
        /// </summary>
        public float sql_rowspersec { get; set; }

        /// <summary>
        /// Cost associated with the SQL statement
        /// </summary>
        public int sql_estcost { get; set; }

        /// <summary>
        /// Estimated number of rows returned for the SQL statement as predicted by the optimizer
        /// </summary>
        public int sql_estrows { get; set; }

        /// <summary>
        /// Number of rows returned for the SQL statement
        /// </summary>
        public int sql_actualrows { get; set; }

        /// <summary>
        /// SQL error number
        /// </summary>
        public int sql_sqlerror { get; set; }

        /// <summary>
        /// RSAM/ISAM error number
        /// </summary>
        public int sql_isamerror { get; set; }

        /// <summary>
        /// Isolation level of the SQL statement.
        /// </summary>
        public int sql_isollevel { get; set; }

        /// <summary>
        /// Number of bytes needed to execute the SQL statement
        /// </summary>
        public int sql_sqlmemory { get; set; }

        /// <summary>
        /// Number of iterators used by the statement
        /// </summary>
        public int sql_numiterators { get; set; }

        /// <summary>
        /// Database name
        /// </summary>
        public string sql_database { get; set; }

        /// <summary>
        /// Number of tables used in executing the SQL statement
        /// </summary>
        public int sql_numtables { get; set; }

        /// <summary>
        /// List of table names directly referenced in the SQL statement. If the SQL statement fires triggers that execute statements against other tables, the other tables are not listed.
        /// </summary>
        public string sql_tablelist { get; set; }

        /// <summary>
        /// SQL statement that ran
        /// </summary>
        public string sql_statement { get; set; }
    }




    /// <summary>
    /// https://www.ibm.com/docs/en/informix-servers/12.10.0?topic=tables-syssqexplain-table
    /// </summary>
    public class SysSqExplain
    {
        /// <summary>
        /// The session ID associated with the SQL statement.
        /// </summary>
        public int sqx_sessionid { get; set; }

        /// <summary>
        /// The position of the query in the array of session IDs.
        /// </summary>
        public int sqx_sdbno { get; set; }

        /// <summary>
        /// Whether the query is the current SQL statement. 
        /// Y = Yes, N = No
        /// </summary>
        public char sqx_iscurrent { get; set; }

        /// <summary>
        /// The total number of executions of the query.
        /// </summary>
        public int sqx_executions { get; set; }

        /// <summary>
        /// The cumulative time to run the query (in seconds).
        /// </summary>
        public float sqx_cumtime { get; set; }

        /// <summary>
        /// The number of buffer reads performed while running the query.
        /// </summary>
        public int sqx_bufreads { get; set; }

        /// <summary>
        /// The number of page reads performed while running the query.
        /// </summary>
        public int sqx_pagereads { get; set; }

        /// <summary>
        /// The number of buffer writes performed while running the query.
        /// </summary>
        public int sqx_bufwrites { get; set; }

        /// <summary>
        /// The number of page writes performed while running the query.
        /// </summary>
        public int sqx_pagewrites { get; set; }

        /// <summary>
        /// The total number of sorts performed while running the query.
        /// </summary>
        public int sqx_totsorts { get; set; }

        /// <summary>
        /// The number of disk sorts performed while running the query.
        /// </summary>
        public int sqx_dsksorts { get; set; }

        /// <summary>
        /// The maximum disk space (in KB) required by a sort operation.
        /// Important: If this value is large, consider tuning sort memory parameters.
        /// </summary>
        public int sqx_sortspmax { get; set; }

        /// <summary>
        /// The position in the conblock list.
        /// </summary>
        public short sqx_conbno { get; set; }

        /// <summary>
        /// Whether the query is in the main block for the statement. 
        /// Y = Yes, N = No
        /// </summary>
        public char sqx_ismain { get; set; }

        /// <summary>
        /// The type of SQL statement, such as SELECT, UPDATE, DELETE.
        /// </summary>
        public string sqx_selflag { get; set; }

        /// <summary>
        /// The estimated cost of the query.
        /// Important: This value is provided by the optimizer and does not represent actual resource usage.
        /// </summary>
        public int sqx_estcost { get; set; }

        /// <summary>
        /// The estimated number of rows returned by the query.
        /// Important: Large differences between estimated and actual rows may indicate inaccurate statistics.
        /// </summary>
        public int sqx_estrows { get; set; }

        /// <summary>
        /// The number of sequential scans used by the query.
        /// </summary>
        public short sqx_seqscan { get; set; }

        /// <summary>
        /// The number of sort scans used by the query.
        /// </summary>
        public short sqx_srtscan { get; set; }

        /// <summary>
        /// The number of auto-index scans used by the query.
        /// </summary>
        public short sqx_autoindex { get; set; }

        /// <summary>
        /// The number of index path accesses used by the query.
        /// </summary>
        public short sqx_index { get; set; }

        /// <summary>
        /// The number of remote SQL accesses performed by the query.
        /// </summary>
        public short sqx_remsql { get; set; }

        /// <summary>
        /// The number of sort-merge joins used by the query.
        /// </summary>
        public short sqx_mrgjoin { get; set; }

        /// <summary>
        /// The number of dynamic hash joins used by the query.
        /// </summary>
        public short sqx_dynhashjoin { get; set; }

        /// <summary>
        /// The number of key-only scans used by the query.
        /// </summary>
        public short sqx_keyonly { get; set; }

        /// <summary>
        /// The number of temporary files created for query processing.
        /// Important: Excessive temporary file usage may indicate inefficient queries or insufficient memory settings.
        /// </summary>
        public short sqx_tempfile { get; set; }

        /// <summary>
        /// The number of temporary tables for views created by the query.
        /// </summary>
        public short sqx_tempview { get; set; }

        /// <summary>
        /// The number of secondary threads used by the query.
        /// </summary>
        public short sqx_secthreads { get; set; }

        /// <summary>
        /// The text of the SQL query that was run.
        /// Important: For security reasons, only authorized users may view full query text.
        /// </summary>
        public string sqx_sqlstatement { get; set; }
    }


    /// <summary>
    /// https://www.ibm.com/docs/en/informix-servers/12.10.0?topic=tables-sysptprof-table
    /// </summary>
    public class SysPtProf
    {
        /// <summary>
        /// Database name (CHAR(128))
        /// </summary>
        public string dbsname { get; set; }

        /// <summary>
        /// Table name (CHAR(128))
        /// </summary>
        public string tabname { get; set; }

        /// <summary>
        /// Partition (tblspace) number (INTEGER)
        /// </summary>
        public int partnum { get; set; }

        /// <summary>
        /// Number of lock requests (INTEGER)
        /// </summary>
        public int lockreqs { get; set; }

        /// <summary>
        /// Number of lock waits (INTEGER)
        /// </summary>
        public int lockwts { get; set; }

        /// <summary>
        /// Number of deadlocks (INTEGER)
        /// </summary>
        public int deadlks { get; set; }

        /// <summary>
        /// Number of lock timeouts (INTEGER)
        /// </summary>
        public int lktouts { get; set; }

        /// <summary>
        /// Number of isreads (INTEGER)
        /// </summary>
        public int isreads { get; set; }

        /// <summary>
        /// Number of iswrites (INTEGER)
        /// </summary>
        public int iswrites { get; set; }

        /// <summary>
        /// Number of isrewrites (INTEGER)
        /// </summary>
        public int isrewrites { get; set; }

        /// <summary>
        /// Number of isdeletes (INTEGER)
        /// </summary>
        public int isdeletes { get; set; }

        /// <summary>
        /// Number of buffer reads (INTEGER)
        /// </summary>
        public int bufreads { get; set; }

        /// <summary>
        /// Number of buffer writes (INTEGER)
        /// </summary>
        public int bufwrites { get; set; }

        /// <summary>
        /// Number of sequential scans (INTEGER)
        /// </summary>
        public int seqscans { get; set; }

        /// <summary>
        /// Number of page reads (INTEGER)
        /// </summary>
        public int pagreads { get; set; }

        /// <summary>
        /// Number of page writes (INTEGER)
        /// </summary>
        public int pagwrites { get; set; }
    }



    /// <summary>
    /// https://www.ibm.com/docs/en/informix-servers/12.10.0?topic=tables-syssesprof
    /// </summary>
    public class SysSessionProfile
    {
        public string username { get; set; }
        public int sessionid { get; set; }

        /// <summary>
        /// Number of locks requested
        /// </summary>
        public int lockreqs { get; set; }


        /// <summary>
        /// Number of locks currently held 
        /// </summary>
        public int locksheld { get; set; }


        /// <summary>
        /// Number of times waited for a lock
        /// </summary>
        public int lockwts { get; set; }


        /// <summary>
        /// Number of deadlocks detected
        /// </summary>
        public int deadlks { get; set; }


        /// <summary>
        /// Number of deadlock time-outs
        /// </summary>
        public int lktouts { get; set; }
    }
}
