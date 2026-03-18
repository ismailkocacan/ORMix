using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ormix.Exceptions
{
    [Serializable]
    public class LockException<T> : Exception
    {
        public T? LockMonitor { get; set; }
        public LockException(string message)
            : base(message)
        {
        }

        public LockException(string message, Exception inner, T? lockMonitor = default)

            : base(message, inner)
        {
            this.LockMonitor = lockMonitor;
        }

        public static bool TryGetLockMonitor(Exception exception, out T? lockMonitor)
        {
            lockMonitor = default;
            if (exception == null)
                return false;

            if (exception is LockException<T> lockException &&
                lockException.LockMonitor != null)
            {
                lockMonitor = lockException.LockMonitor;
                return true;
            }
            return false;
        }
    }
}
