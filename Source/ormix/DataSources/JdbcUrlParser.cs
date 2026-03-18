using java.util;

namespace Ormix.DataSources
{
    internal static class JdbcUrlParser
    {
        public static Properties Parse(string jdbcUrl)
        {
            var properties = new Properties();
            string searchString = "jdbc:informix-sqli://";

            int index = jdbcUrl.IndexOf(searchString);
            int endIndex = jdbcUrl.IndexOf(';');
            var jdbc = jdbcUrl.Substring(index, endIndex + 1);


            var jdbcUrlx = jdbc.Replace(searchString, "");
            string[] hostServer = jdbcUrlx.Split("/");
            var hostPortData = hostServer[0];         
            var hostPort = hostPortData.Split(":");
            var host = hostPort[0];
            var port = hostPort[1];

            var serverData = hostServer[1];   
            var server = serverData.Split(":");
            var databaseName = server[0];
            var serverStr = server[1].Split("=")[1]; 
            serverStr = serverStr.Replace(";", "");


            properties.setProperty("IFXHOST", host);
            properties.setProperty("INFORMIXSERVER", serverStr);
            properties.setProperty("DATABASE", databaseName);
            properties.setProperty("PORTNO", port);


            var newJdbcUrl = jdbcUrl.Replace(jdbc, "");
            string[] urlParts = newJdbcUrl.Split(';');
            foreach (var part in urlParts)
            {
                string[] keyValue = part.Split('=');
                if (keyValue.Length == 2)
                    properties.setProperty(keyValue[0], keyValue[1]);
            }
            return properties;
        }
    }
}
