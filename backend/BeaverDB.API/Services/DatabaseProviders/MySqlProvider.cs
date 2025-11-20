using MySqlConnector;

namespace BeaverDB.API.Services.DatabaseProviders;

public class MySqlProvider : IDatabaseProvider
{
    private readonly string _connectionString;

    public MySqlProvider(string host, int port, string username, string password)
    {
        _connectionString = $"Server={host};Port={port};User ID={username};Password={password};";
    }

    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            using var connection = new MySqlConnection(_connectionString);
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
        
        using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        var command = new MySqlCommand(@"
            SELECT 
                SCHEMA_NAME as Name,
                DEFAULT_CHARACTER_SET_NAME as Charset,
                DEFAULT_COLLATION_NAME as Collation
            FROM information_schema.SCHEMATA
            WHERE SCHEMA_NAME NOT IN ('information_schema', 'mysql', 'performance_schema', 'sys')", 
            connection);

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            databases.Add(new DatabaseInfo
            {
                Name = reader.GetString("Name"),
                Charset = reader.IsDBNull(reader.GetOrdinal("Charset")) ? null : reader.GetString("Charset"),
                Collation = reader.IsDBNull(reader.GetOrdinal("Collation")) ? null : reader.GetString("Collation")
            });
        }

        return databases;
    }

    public async Task CreateDatabaseAsync(string databaseName, string? charset = null, string? collation = null)
    {
        using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        var charsetClause = !string.IsNullOrEmpty(charset) ? $" CHARACTER SET {charset}" : "";
        var collationClause = !string.IsNullOrEmpty(collation) ? $" COLLATE {collation}" : "";

        var command = new MySqlCommand(
            $"CREATE DATABASE `{databaseName}`{charsetClause}{collationClause}", 
            connection);
        
        await command.ExecuteNonQueryAsync();
    }

    public async Task DropDatabaseAsync(string databaseName)
    {
        using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        var command = new MySqlCommand($"DROP DATABASE `{databaseName}`", connection);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<List<TableInfo>> ListTablesAsync(string databaseName)
    {
        var tables = new List<TableInfo>();
        
        using var connection = new MySqlConnection(_connectionString + $"Database={databaseName};");
        await connection.OpenAsync();

        var command = new MySqlCommand(@"
            SELECT 
                TABLE_NAME as Name,
                ENGINE as Engine,
                TABLE_ROWS as RowCount
            FROM information_schema.TABLES
            WHERE TABLE_SCHEMA = @dbName AND TABLE_TYPE = 'BASE TABLE'", 
            connection);
        
        command.Parameters.AddWithValue("@dbName", databaseName);

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tables.Add(new TableInfo
            {
                Name = reader.GetString("Name"),
                Engine = reader.IsDBNull(reader.GetOrdinal("Engine")) ? null : reader.GetString("Engine"),
                RowCount = reader.IsDBNull(reader.GetOrdinal("RowCount")) ? null : reader.GetInt64("RowCount")
            });
        }

        return tables;
    }

    public async Task<List<ColumnInfo>> GetTableSchemaAsync(string databaseName, string tableName)
    {
        var columns = new List<ColumnInfo>();
        
        using var connection = new MySqlConnection(_connectionString + $"Database={databaseName};");
        await connection.OpenAsync();

        var command = new MySqlCommand(@"
            SELECT 
                COLUMN_NAME as Name,
                COLUMN_TYPE as DataType,
                IS_NULLABLE as IsNullable,
                COLUMN_KEY as ColumnKey,
                COLUMN_DEFAULT as DefaultValue
            FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA = @dbName AND TABLE_NAME = @tableName
            ORDER BY ORDINAL_POSITION", 
            connection);
        
        command.Parameters.AddWithValue("@dbName", databaseName);
        command.Parameters.AddWithValue("@tableName", tableName);

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(new ColumnInfo
            {
                Name = reader.GetString("Name"),
                DataType = reader.GetString("DataType"),
                IsNullable = reader.GetString("IsNullable") == "YES",
                IsPrimaryKey = reader.GetString("ColumnKey") == "PRI",
                DefaultValue = reader.IsDBNull(reader.GetOrdinal("DefaultValue")) ? null : reader.GetString("DefaultValue")
            });
        }

        return columns;
    }

    public async Task<QueryResult> ExecuteQueryAsync(string databaseName, string query)
    {
        var result = new QueryResult();
        
        using var connection = new MySqlConnection(_connectionString + $"Database={databaseName};");
        await connection.OpenAsync();

        var command = new MySqlCommand(query, connection);

        // Check if it's a SELECT query
        if (query.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
        {
            using var reader = await command.ExecuteReaderAsync();
            
            // Get column names
            for (int i = 0; i < reader.FieldCount; i++)
            {
                result.Columns.Add(reader.GetName(i));
            }

            // Get rows
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
