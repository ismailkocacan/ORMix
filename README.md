# ORMix
High-Performance JDBC-Based Informix .NET ORM

A high-performance .NET ORM designed for Informix with JDBC-inspired features and robust transaction and retry mechanisms.

## Features

- **Connection Mapping**  
  Mapping extension methods specifically designed for the `java.sql.Connection` class.

- **Named Parameters in SQL Queries**  
  Pass parameters to SQL queries (e.g., `:id`) using dynamic anonymous objects.

- **Transaction Management**  
  Various classes for easy and flexible transaction handling.

- **Retry Mechanisms**  
  Automatically retries temporary connection (network) failures and temporary lock errors.

- **Administrative and Monitoring Support**  
  Ready-to-use methods for various administrative commands and system monitoring tables.


## Nuget
dotnet add package ORMix --version 1.0.0

```csharp
using Ormix.ServiceCollections;
var builder = WebApplication.CreateBuilder(args);
.
.
builder.Services.AddInformixServices(new ConnectionStringConfiguration(builder.Configuration));

var app = builder.Build();
.
.
app.Run();
```


ConnectionStringConfiguration class
```csharp
public class ConnectionStringConfiguration : IConnectionStringConfiguration
{
	private readonly IConfiguration configuration;
	public ConnectionStringConfiguration(IConfiguration configuration)
	{
		this.configuration = configuration;
	}
  
	public string GetConnectionString(IServiceProvider? serviceProvider = null)
	{
		// Provide a JDBC URL...
		return configuration.GetConnectionString("Informix")!;
	}
}
```

Simply inject ConnectionContext class and use.
```csharp
 public class RepositoryPerson(Ormix.DataSources.ConnectionContext connectionContext) 
 {	 
	public Person? GetBy(Guid uuid)
	{
		return connectionContext.Connection
			.QuerySingle<Person>(
			   @"select id
					  , firstname
					  , lastname
					  , created 
				from person where id = :id",
			   new
			   {
				   id = uuid
			   });
	}	 
 }
 ```
