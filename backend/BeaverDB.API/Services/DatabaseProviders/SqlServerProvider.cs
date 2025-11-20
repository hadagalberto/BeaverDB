using Microsoft.Data.SqlClient;
using System.Data;

namespace BeaverDB.API.Services.DatabaseProviders;

public class SqlServerProvider : IDatabaseProvider
{
    private readonly string _connectionString;
    private readonly string _host;
    private readonly int _port;
    private readonly string _username;
    private readonly string _password;

    public SqlServerProvider(string host, int port, string username, string password)
    {
        _host = host;
        _port = port;
        _username = username;
        _password = password;
        
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = $"{host},{port}",
            UserID = username,
            Password = password,
            TrustServerCertificate = true, // For development/docker environments
            InitialCatalog = "master" // Default to master to list databases
        };
        _connectionString = builder.ConnectionString;
    }

    private string GetConnectionString(string? databaseName = null)
    {
        var builder = new SqlConnectionStringBuilder(_connectionString);
        if (!string.IsNullOrEmpty(databaseName))
        {
            builder.InitialCatalog = databaseName;
        }
        return builder.ConnectionString;
    }

    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            using var connection = new SqlConnection(_connectionString);
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
        
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var command = new SqlCommand(@"
            SELECT 
                name,
                collation_name,
                (SELECT SUM(size) * 8 * 1024 FROM sys.master_files WHERE database_id = d.database_id) as size
            FROM sys.databases d
            WHERE name NOT IN ('master', 'tempdb', 'model', 'msdb')", 
            connection);

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            databases.Add(new DatabaseInfo
            {
                Name = reader.GetString(0),
                Collation = reader.IsDBNull(1) ? null : reader.GetString(1),
                Size = reader.IsDBNull(2) ? null : reader.GetInt64(2),
                Charset = null // SQL Server handles charset via collation mostly
            });
        }

        return databases;
    }

    public async Task CreateDatabaseAsync(string databaseName, string? charset = null, string? collation = null)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var collationClause = !string.IsNullOrEmpty(collation) ? $" COLLATE {collation}" : "";
        
        // Sanitize database name to prevent injection (basic check)
        if (databaseName.Any(c => !char.IsLetterOrDigit(c) && c != '_' && c != '-'))
            throw new ArgumentException("Invalid database name");

        var command = new SqlCommand($"CREATE DATABASE [{databaseName}]{collationClause}", connection);
        await command.ExecuteNonQueryAsync();
    }

    public async Task DropDatabaseAsync(string databaseName)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        // Kill active connections first
        var killCommand = new SqlCommand($@"
            DECLARE @kill varchar(8000) = '';  
            SELECT @kill = @kill + 'kill ' + CONVERT(varchar(5), session_id) + ';'  
            FROM sys.dm_exec_sessions
            WHERE database_id  = db_id('{databaseName}')

            EXEC(@kill);", connection);
        
        await killCommand.ExecuteNonQueryAsync();

        var command = new SqlCommand($"DROP DATABASE [{databaseName}]", connection);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<List<TableInfo>> ListTablesAsync(string databaseName)
    {
        var tables = new List<TableInfo>();
        
        using var connection = new SqlConnection(GetConnectionString(databaseName));
        await connection.OpenAsync();

        var command = new SqlCommand(@"
            SELECT 
                TABLE_SCHEMA,
                TABLE_NAME
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_TYPE = 'BASE TABLE'", 
            connection);

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tables.Add(new TableInfo
            {
                Schema = reader.GetString(0),
                Name = reader.GetString(1)
            });
        }

        return tables;
    }

    public async Task<List<ColumnInfo>> GetTableSchemaAsync(string databaseName, string tableName)
    {
        var columns = new List<ColumnInfo>();
        
        using var connection = new SqlConnection(GetConnectionString(databaseName));
        await connection.OpenAsync();

        var command = new SqlCommand(@"
            SELECT 
                c.COLUMN_NAME,
                c.DATA_TYPE,
                c.IS_NULLABLE,
                c.COLUMN_DEFAULT,
                CASE WHEN k.COLUMN_NAME IS NOT NULL THEN 1 ELSE 0 END as IsPrimaryKey
            FROM INFORMATION_SCHEMA.COLUMNS c
            LEFT JOIN (
                SELECT ku.COLUMN_NAME
                FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE ku ON tc.CONSTRAINT_NAME = ku.CONSTRAINT_NAME
                WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY' AND tc.TABLE_NAME = @tableName
            ) k ON c.COLUMN_NAME = k.COLUMN_NAME
            WHERE c.TABLE_NAME = @tableName
            ORDER BY c.ORDINAL_POSITION", 
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
                DefaultValue = reader.IsDBNull(3) ? null : reader.GetString(3),
                IsPrimaryKey = reader.GetInt32(4) == 1
            });
        }

        return columns;
    }

    public async Task<QueryResult> ExecuteQueryAsync(string databaseName, string query)
    {
        var result = new QueryResult();
        
        using var connection = new SqlConnection(GetConnectionString(databaseName));
        await connection.OpenAsync();

        var command = new SqlCommand(query, connection);

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
