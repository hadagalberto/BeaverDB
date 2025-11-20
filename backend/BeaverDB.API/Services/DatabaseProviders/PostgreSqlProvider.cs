using Npgsql;

namespace BeaverDB.API.Services.DatabaseProviders;

public class PostgreSqlProvider : IDatabaseProvider
{
    private readonly string _connectionString;
    private readonly string _host;
    private readonly int _port;
    private readonly string _username;
    private readonly string _password;

    public PostgreSqlProvider(string host, int port, string username, string password)
    {
        _host = host;
        _port = port;
        _username = username;
        _password = password;
        _connectionString = $"Host={host};Port={port};Username={username};Password={password};Database=postgres;";
    }

    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<DatabaseInfo>> ListDatabasesAsync()
    {
        var databases = new List<DatabaseInfo>();
        
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        var command = new NpgsqlCommand(@"
            SELECT 
                datname as name,
                pg_encoding_to_char(encoding) as charset,
                datcollate as collation,
                pg_database_size(datname) as size
            FROM pg_database
            WHERE datistemplate = false 
            AND datname NOT IN ('postgres')", 
            connection);

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            databases.Add(new DatabaseInfo
            {
                Name = reader.GetString(0),
                Charset = reader.IsDBNull(1) ? null : reader.GetString(1),
                Collation = reader.IsDBNull(2) ? null : reader.GetString(2),
                Size = reader.IsDBNull(3) ? null : reader.GetInt64(3)
            });
        }

        return databases;
    }

    public async Task CreateDatabaseAsync(string databaseName, string? charset = null, string? collation = null)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        var encodingClause = !string.IsNullOrEmpty(charset) ? $" ENCODING '{charset}'" : "";
        var collationClause = !string.IsNullOrEmpty(collation) ? $" LC_COLLATE '{collation}'" : "";

        var command = new NpgsqlCommand(
            $"CREATE DATABASE \"{databaseName}\"{encodingClause}{collationClause}", 
            connection);
        
        await command.ExecuteNonQueryAsync();
    }

    public async Task DropDatabaseAsync(string databaseName)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        // Terminate existing connections
        var terminateCommand = new NpgsqlCommand($@"
            SELECT pg_terminate_backend(pg_stat_activity.pid)
            FROM pg_stat_activity
            WHERE pg_stat_activity.datname = '{databaseName}'
            AND pid <> pg_backend_pid()", connection);
        
        await terminateCommand.ExecuteNonQueryAsync();

        var command = new NpgsqlCommand($"DROP DATABASE \"{databaseName}\"", connection);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<List<TableInfo>> ListTablesAsync(string databaseName)
    {
        var tables = new List<TableInfo>();
        
        var connString = $"Host={_host};Port={_port};Username={_username};Password={_password};Database={databaseName};";
        using var connection = new NpgsqlConnection(connString);
        await connection.OpenAsync();

        var command = new NpgsqlCommand(@"
            SELECT 
                schemaname as schema,
                tablename as name
            FROM pg_tables
            WHERE schemaname NOT IN ('pg_catalog', 'information_schema')", 
            connection);

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tables.Add(new TableInfo
            {
                Name = reader.GetString(1),
                Schema = reader.GetString(0)
            });
        }

        return tables;
    }

    public async Task<List<ColumnInfo>> GetTableSchemaAsync(string databaseName, string tableName)
    {
        var columns = new List<ColumnInfo>();
        
        var connString = $"Host={_host};Port={_port};Username={_username};Password={_password};Database={databaseName};";
        using var connection = new NpgsqlConnection(connString);
        await connection.OpenAsync();

        var command = new NpgsqlCommand(@"
            SELECT 
                c.column_name as name,
                c.data_type as datatype,
                c.is_nullable as isnullable,
                c.column_default as defaultvalue,
                CASE WHEN pk.column_name IS NOT NULL THEN true ELSE false END as isprimarykey
            FROM information_schema.columns c
            LEFT JOIN (
                SELECT ku.column_name
                FROM information_schema.table_constraints tc
                JOIN information_schema.key_column_usage ku
                    ON tc.constraint_name = ku.constraint_name
                WHERE tc.constraint_type = 'PRIMARY KEY'
                    AND tc.table_name = @tableName
            ) pk ON c.column_name = pk.column_name
            WHERE c.table_name = @tableName
            ORDER BY c.ordinal_position", 
            connection);
        
        command.Parameters.AddWithValue("@tableName", tableName);

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(new ColumnInfo
            {
                Name = reader.GetString(0),
                DataType = reader.GetString(1),
                IsNullable = reader.GetString(2) == "YES",
                IsPrimaryKey = reader.GetBoolean(4),
                DefaultValue = reader.IsDBNull(3) ? null : reader.GetString(3)
            });
        }

        return columns;
    }

    public async Task<QueryResult> ExecuteQueryAsync(string databaseName, string query)
    {
        var result = new QueryResult();
        
        var connString = $"Host={_host};Port={_port};Username={_username};Password={_password};Database={databaseName};";
        using var connection = new NpgsqlConnection(connString);
        await connection.OpenAsync();

        var command = new NpgsqlCommand(query, connection);

        if (query.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
        {
            using var reader = await command.ExecuteReaderAsync();
            
            for (int i = 0; i < reader.FieldCount; i++)
            {
                result.Columns.Add(reader.GetName(i));
            }

            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object?>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                }
                result.Rows.Add(row);
            }
        }
        else
        {
            result.RowsAffected = await command.ExecuteNonQueryAsync();
        }

        return result;
    }
}
