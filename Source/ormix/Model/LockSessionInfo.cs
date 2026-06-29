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

        public string connection_duration { get; set; } = null!;
        public string hostname { get; set; } = null!;
        public string username { get; set; } = null!;
        public string dbsname { get; set; } = null!;
        public string tabname { get; set; } = null!;
        public string type { get; set; } = null!;
        public string lock_duration { get; set; } = null!;
        public string feprogram { get; set; } = null!;

        public string onstat_session_sql { get; set; } = null!;
        public string onstat_monitor_session { get; set; } = null!;
    }
}
