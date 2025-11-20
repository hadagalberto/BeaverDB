using BeaverDB.API.Models;

namespace BeaverDB.API.Services.DatabaseProviders;

public interface IDatabaseProviderFactory
{
    IDatabaseProvider CreateProvider(DatabaseServer server, string password);
}

public class DatabaseProviderFactory : IDatabaseProviderFactory
{
    public IDatabaseProvider CreateProvider(DatabaseServer server, string password)
    {
        return server.Type switch
        {
            ServerType.MySQL => new MySqlProvider(server.Host, server.Port, server.Username ?? "root", password),
            ServerType.PostgreSQL => new PostgreSqlProvider(server.Host, server.Port, server.Username ?? "postgres", password),
            // Add other providers as needed
            _ => throw new NotSupportedException($"Database type {server.Type} is not yet supported")
        };
    }
}
