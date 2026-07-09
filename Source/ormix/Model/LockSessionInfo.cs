using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ormix.Model
{
    public class LockSessionInfo
    {
        public int txid { get; set; }
        public int owner { get; set; }
        public int waiter { get; set; }
        public int pid { get; set; }

        public string connection_duration { get; set; }
        public string hostname { get; set; }
        public string username { get; set; }
        public string dbsname { get; set; }
        public string tabname { get; set; }
        public string type { get; set; }
        public string lock_duration { get; set; }
        public string feprogram { get; set; }

        public string onstat_session_sql { get; set; }
        public string onstat_monitor_session { get; set; }
    }
}
