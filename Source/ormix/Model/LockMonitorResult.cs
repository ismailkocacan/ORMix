using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ormix.Model
{
    public class LockMonitorResult
    {
        public int WaitSessionId { get; set; }
        public string SessionMonitorDatabaseServer { get; set; }
        public string WaitSessionSql { get; set; }
        public List<LockSessionInfo> LockSessionInfos { get; set; }
    }
}
