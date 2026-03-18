using java.sql;
using javax.sql;
using com.informix.jdbcx;
using javax.transaction.xa;
using Microsoft.Extensions.DependencyInjection;
using Ormix.Transactions.Distributed;

namespace Ormix.DataSources
{
    public class ConnectionContextXA : IDisposable
    {
        private readonly XAConnection xaConnection;
        private readonly Connection dbConnection;

        private Xid currentXId;
        private XAResource currentXAResource;
        private DistributedTransactionManager distributedTransactionManager;

        static ConnectionContextXA()
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

        public void Enlist(IServiceProvider serviceProvider)
        {
            this.distributedTransactionManager = serviceProvider
                .GetRequiredService<DistributedTransactionManager>();
        }

        public void Start()
        {
            this.currentXId = GetUniqueXid();
            this.currentXAResource = xaConnection.getXAResource();
            this.currentXAResource.start(currentXId, XAResource.TMNOFLAGS);
            var keyValuePair = new KeyValuePair<Xid, XAResource>(currentXId, currentXAResource);
            this.distributedTransactionManager.Add(keyValuePair);
        }

        public void End(bool isSuccess = true)
        {
            currentXAResource.end(xid: currentXId,
                flags: isSuccess ? XAResource.TMSUCCESS : XAResource.TMFAIL);
        }

        private Xid GetUniqueXid()
        {
            return OpenTransactionBranchIdentifier.GetUniqueXid(Environment.TickCount);
        }

        public ConnectionContextXA(
              IServiceProvider? serviceProvider
            , IConnectionStringConfiguration connectionStringConfiguration)
        {
            var connectionString = connectionStringConfiguration.GetConnectionString(serviceProvider);

            var xadataSource = GetXADataSources(connectionString);

            this.xaConnection = xadataSource.getXAConnection(uid:
                (string)xadataSource.getProp("user"),
                (string)xadataSource.getProp("password"));

            this.dbConnection = xaConnection.getConnection();
            this.SessionId = GetSessionId();
        }


        private IfxXADataSource GetXADataSources(string jdbcConectionString)
        {
            var xaDataSource = new IfxXADataSource();
            var properties = JdbcUrlParser.Parse(jdbcConectionString);
            var propertyNames = properties.propertyNames();
            while (propertyNames.hasMoreElements())
            {
                var key = (string)propertyNames.nextElement();
                var value = properties.getProperty(key);
                xaDataSource.addProp(key, value);
            }
            return xaDataSource;
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
            xaConnection?.close();

            dbConnection?.close();
            dbConnection?.Dispose();
        }

        ~ConnectionContextXA()
        {
            Dispose(false);
        }

        public Connection Connection => dbConnection;

        public int SessionId { get; set; }
    }
}
