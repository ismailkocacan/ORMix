using java.sql;
using Ormix.Session;
using Ormix.retry;


namespace Ormix.DataSources
{
    public interface IConnectionStringConfiguration
    {
        string GetConnectionString(IServiceProvider? serviceProvider = null);
    }

    public class ConnectionContext : IDisposable
    {
        private readonly Connection dbConnection;

        static ConnectionContext()
        {
            DriverManager.registerDriver(new com.informix.jdbc.IfxDriver());
            AppDomain.CurrentDomain.ProcessExit += CurrentDomainProcessExit;
        }

        private static void CurrentDomainProcessExit(object? sender, EventArgs e)
        {
            UnRegisterDrivers();
        }

        private static void UnRegisterDrivers()
        {
            var drivers = DriverManager.getDrivers();
            if (drivers == null)
                return;

            while (drivers.hasMoreElements())
            {
                var element = drivers.nextElement();
                if (element == null)
                    continue;
                if (element is Driver driver)
                    DriverManager.deregisterDriver(driver);
            }
        }

        public ConnectionContext(
              ISessionContext? sessionContext
            , IServiceProvider? serviceProvider
            , IConnectionStringConfiguration connectionStringConfiguration)
        {
            var connectionString = connectionStringConfiguration.GetConnectionString(serviceProvider);
            dbConnection = new RetryExecution(RetryType.Connection).Handle(() =>
            {
                return DriverManager.getConnection(connectionString);
            });

            SessionId = GetSessionId();

            if (sessionContext != null)
            {
                sessionContext.SessionId = SessionId;
                sessionContext.ThreadId = Thread.CurrentThread.ManagedThreadId;
            }
        }


        private void SetSessionAppName(ISessionContext? sessionContext)
        {
            if (sessionContext == null)
                return;

            if (string.IsNullOrWhiteSpace(sessionContext.Client))
                return;

            string currentThreadName = java.lang.Thread.currentThread().getName();
            string newName = $"{currentThreadName} : client:{sessionContext.Client}";
            java.lang.Thread.currentThread().setName(newName);
        }

        public int GetSessionId()
        {
            using var statement = dbConnection
                 .prepareStatement(@"select DBINFO('sessionid') as sessionid
                                        from systables limit 1");

            using var resultset = statement.executeQuery();
            if (resultset.next())
                return resultset.getInt("sessionid");
            return 0;
        }

        public int TransactionId()
        {
            try
            {
                using var statement = dbConnection
                     .prepareStatement(@"select mi_get_transaction_id() as transactionid
                                        from systables limit 1");
                using var resultset = statement.executeQuery();
                if (resultset.next())
                    return resultset.getInt("transactionid");
            }
            catch (Exception)
            {
                //mi_get_transaction_id maybe not loaded
            }
            return 0;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            // Cleanup resources
            dbConnection?.close();
            dbConnection?.Dispose();
        }

        ~ConnectionContext()
        {
            Dispose(false);
        }

        public Connection Connection
        {
            get
            {
                return dbConnection;
            }
        }

        public int SessionId { get; set; }
    }
}
