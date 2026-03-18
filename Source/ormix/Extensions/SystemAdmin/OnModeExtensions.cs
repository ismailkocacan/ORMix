using java.sql;


namespace Ormix.Extensions.SystemAdmin
{
    public static class OnModeExtensions
    {
        /// <summary>
        /// https://help.hcl-software.com/hclinformix/1410/adr/ids_sapi_065.html
        /// </summary>
        /// <param name="dbConnection"></param>
        /// <param name="sessionId"></param>
        /// <returns></returns>
        public static string OnModeZ(this Connection dbConnection, int sessionId)
        {
            return dbConnection
                .QuerySingleString($@"execute function sysadmin:task('onmode', 'z', '{sessionId}')")
                .Data[0];
        }


        /// <summary>
        /// Turn on Dynamic Explain to get plan for a session
        /// onmode -Y: Dynamically change SET EXPLAIN
        /// https://www.ibm.com/docs/en/informix-servers/12.10.0?topic=utility-onmode-y-dynamically-change-set-explain
        /// </summary>
        /// <param name="dbConnection"></param>
        /// <param name="sessionId"></param>
        /// <returns></returns>
        public static string OnModeY(this Connection dbConnection, int sessionId, TOnModeY onModeY)
        {
            return dbConnection
                .QuerySingleString($@"execute function sysadmin:task('onmode', 'Y', '{sessionId}','{(byte)onModeY})'")
                .Data[0];
        }
    }

    public enum TOnModeY : byte
    {
        /// <summary>
        /// Turns SET EXPLAIN off for session_id
        /// </summary>
        Off = 0,

        PlanAndStatisticsOn = 1,

        /// <summary>
        /// Turns SET EXPLAIN on for session_id
        /// </summary>
        OnlyPlanOn = 2
    }
}
