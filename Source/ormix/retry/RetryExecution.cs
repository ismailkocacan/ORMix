using Ormix.Exceptions;

namespace Ormix.retry
{
    public enum RetryType
    {
        Connection,
        Query
    }

    public class RetryExecution
    {
        private const int WaitMilliSecond = 20_000; // 20 sn

        private int retryCount;
        public int RetryCount => retryCount;

        private readonly RetryType retryType;

        public RetryExecution(RetryType retryType, int retryCount = 3)
        {
            if (retryCount < 1)
                throw new ArgumentException($"{nameof(retryCount)} must be min 1");

            this.retryType = retryType;
            this.retryCount = retryCount;
        }


        private bool TryHandleError(Exception exception)
        {
            Func<Exception, bool> retryDispatch = this.retryType switch
            {
                RetryType.Connection => e => e.IsTransientNetworkError(),
                RetryType.Query => e => e.IsTransientError() || e.IsTransientInternalError(),
                _ => _ => false
            };

            if (!retryDispatch(exception))
                return false;

            Thread.Sleep(WaitMilliSecond);
            return true;
        }

        public void Handle(Action action)
        {
            for (int i = 1; i <= this.retryCount; i++)
            {
                try
                {
                    action();
                    break;
                }
                catch (Exception exception)
                {
                    if (!TryHandleError(exception))
                        throw;
                }
            }
        }

        public T Handle<T>(Func<T> function)
        {
            for (int i = 1; i <= this.retryCount; i++)
            {
                try
                {
                    return function();
                }
                catch (Exception exception)
                {
                    if (!TryHandleError(exception))
                        throw;
                }
            }
            return function(); 
        }
    }
}
