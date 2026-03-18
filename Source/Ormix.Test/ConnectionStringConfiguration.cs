using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ormix.DataSources;

namespace Ormix.Test
{
    public class ConnectionStringConfiguration : IConnectionStringConfiguration
    {
        public string GetConnectionString(IServiceProvider? serviceProvider = null)
        {
            return @"jdbc:informix-sqli://<host>:<port>/<database>:INFORMIXSERVER=<server_name>;
                     IFX_TRIMTRAILINGSPACES=1;
                     IFX_AUTOFREE=1;
                     INFORMIXCONRETRY=3;
                     INFORMIXCONTIME=60";
        }
    }
}

