namespace BeaverDB.API.Services.DatabaseProviders;

public class DatabaseInfo
{
    public required string Name { get; set; }
    public string? Charset { get; set; }
    public string? Collation { get; set; }
    public long? Size { get; set; }
}

public class TableInfo
{
    public required string Name { get; set; }
    public string? Schema { get; set; }
    public long? RowCount { get; set; }
    public string? Engine { get; set; }
}

public class ColumnInfo
{
    public required string Name { get; set; }
    public required string DataType { get; set; }
    public bool IsNullable { get; set; }
    public bool IsPrimaryKey { get; set; }
    public string? DefaultValue { get; set; }
}

public class QueryResult
{
    public List<Dictionary<string, object?>> Rows { get; set; } = new();
    public int RowsAffected { get; set; }
    public List<string> Columns { get; set; } = new();
}

public interface IDatabaseProvider
{
    Task<bool> TestConnectionAsync();
    Task<List<DatabaseInfo>> ListDatabasesAsync();
    Task CreateDatabaseAsync(string databaseName, string? charset = null, string? collation = null);
    Task DropDatabaseAsync(string databaseName);
    Task<List<TableInfo>> ListTablesAsync(string databaseName);
    Task<List<ColumnInfo>> GetTableSchemaAsync(string databaseName, string tableName);
    Task<QueryResult> ExecuteQueryAsync(string databaseName, string query);
}
