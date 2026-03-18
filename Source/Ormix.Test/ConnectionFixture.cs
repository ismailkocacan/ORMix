using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ormix.ServiceCollections;
using Ormix.Session;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit.Extensions.AssemblyFixture;


[assembly: TestFramework(AssemblyFixtureFramework.TypeName, AssemblyFixtureFramework.AssemblyName)]
namespace Ormix.Test
{
    public class ConnectionFixture : IDisposable
    {
        private ConnectionScope connectionScope;

        public ConnectionFixture()
        {
            connectionScope = new ConnectionScope();
        }

        public ConnectionScope ConnectionScope => connectionScope;

        public void Dispose()
        {
            connectionScope?.Dispose();
        }
    }


    public static class ConfigurationHelper
    {
        public static IConfiguration LoadFromFile(string appSettingsPath)
        {
            if (!File.Exists(appSettingsPath))
                throw new FileNotFoundException($"{appSettingsPath} bulunamadı");

            var basePath = Path.GetDirectoryName(Path.GetFullPath(appSettingsPath))!;
            var fileName = Path.GetFileName(appSettingsPath);

            var builder = new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile(fileName, optional: false, reloadOnChange: false);
            return builder.Build();
        }
    }

    public class ConnectionScope : IDisposable
    {
        private readonly IServiceCollection services = new ServiceCollection();
        private readonly IServiceProvider serviceProvider;
        private readonly IServiceProvider scopeServiceProvider;
        private readonly IServiceScopeFactory serviceScopeFactory;
        private readonly IServiceScope serviceConnectionScope;

        public ConnectionScope()
        {
            this.services.AddLogServices();
            this.services.AddInformixServices(new ConnectionStringConfiguration());

            this.serviceProvider = services.BuildServiceProvider();
            this.serviceScopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

            this.serviceConnectionScope = serviceScopeFactory.CreateScope();
            this.scopeServiceProvider = serviceConnectionScope.ServiceProvider;
            var sessionContext = this.scopeServiceProvider.GetRequiredService<ISessionContext>();
            sessionContext.Client = "ormix_unittest";
        }


        public IServiceProvider ServiceProvider => scopeServiceProvider;

        public void Dispose()
        {
            this.serviceConnectionScope?.Dispose();
        }
    }
}
