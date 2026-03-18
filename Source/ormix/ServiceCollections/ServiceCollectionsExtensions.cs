using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ormix.DataSources;
using Ormix.Session;
using Ormix.Transactions.Distributed;
using Ormix.Transactions.UnitOfWork;
using System.Runtime.CompilerServices;

namespace Ormix.ServiceCollections
{
    public static class ServiceCollectionsExtensions
    {
        public static IServiceCollection AddLogServices(this IServiceCollection services)
        {
            return services.AddLogging();
        }

        public static IServiceCollection AddInformixServices(
              this IServiceCollection services
            , IConnectionStringConfiguration connectionStringConfiguration)
        {
            RuntimeHelpers.RunClassConstructor(typeof(ConnectionContext).TypeHandle);

            services.AddSingleton<IConnectionStringConfiguration>(connectionStringConfiguration);

            services.AddScoped<ConnectionContext, ConnectionContext>();
            services.AddScoped<ConnectionContextXA, ConnectionContextXA>();

            services.AddScoped<ISessionContext, SessionContext>();
            services.AddScoped<IUnitOfWorkManager, UnitOfWorkManagerDefault>();
            services.AddScoped<UnitOfWorkEvent, UnitOfWorkEventDefault>();
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddScoped<IDistributedTransaction, DistributedTransaction>();
            services.AddScoped<DistributedTransactionManager, DistributedTransactionManager>();

            return services;
        }
    }
}
