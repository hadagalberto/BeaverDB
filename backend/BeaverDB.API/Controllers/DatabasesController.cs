using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BeaverDB.API.Data;
using BeaverDB.API.Services;
using BeaverDB.API.Services.DatabaseProviders;

namespace BeaverDB.API.Controllers;

[Authorize]
[ApiController]
[Route("api/servers/{serverId}/[controller]")]
public class DatabasesController : ControllerBase
{
    private readonly BeaverDbContext _context;
    private readonly IEncryptionService _encryptionService;
    private readonly IDatabaseProviderFactory _providerFactory;
    private readonly ILogger<DatabasesController> _logger;

    public DatabasesController(
        BeaverDbContext context,
        IEncryptionService encryptionService,
        IDatabaseProviderFactory providerFactory,
        ILogger<DatabasesController> logger)
    {
        _context = context;
        _encryptionService = encryptionService;
        _providerFactory = providerFactory;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult> ListDatabases(int serverId)
    {
        var server = await _context.DatabaseServers.FindAsync(serverId);
        if (server == null)
        {
            return NotFound(new { message = "Server not found" });
        }

        try
        {
            var password = !string.IsNullOrEmpty(server.PasswordEncrypted)
                ? _encryptionService.Decrypt(server.PasswordEncrypted)
                : "";

            var provider = _providerFactory.CreateProvider(server, password);
            var databases = await provider.ListDatabasesAsync();

            return Ok(databases);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to list databases for server {serverId}");
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<ActionResult> CreateDatabase(int serverId, [FromBody] CreateDatabaseDto dto)
    {
        var server = await _context.DatabaseServers.FindAsync(serverId);
        if (server == null)
        {
            return NotFound(new { message = "Server not found" });
        }

        try
        {
            var password = !string.IsNullOrEmpty(server.PasswordEncrypted)
                ? _encryptionService.Decrypt(server.PasswordEncrypted)
                : "";

            var provider = _providerFactory.CreateProvider(server, password);
            await provider.CreateDatabaseAsync(dto.Name, dto.Charset, dto.Collation);

            return Ok(new { message = $"Database '{dto.Name}' created successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to create database on server {serverId}");
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{databaseName}")]
    public async Task<ActionResult> DeleteDatabase(int serverId, string databaseName)
    {
        var server = await _context.DatabaseServers.FindAsync(serverId);
        if (server == null)
        {
            return NotFound(new { message = "Server not found" });
        }

        try
        {
            var password = !string.IsNullOrEmpty(server.PasswordEncrypted)
                ? _encryptionService.Decrypt(server.PasswordEncrypted)
                : "";

            var provider = _providerFactory.CreateProvider(server, password);
            await provider.DropDatabaseAsync(databaseName);

            return Ok(new { message = $"Database '{databaseName}' deleted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to delete database on server {serverId}");
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{databaseName}/tables")]
    public async Task<ActionResult> ListTables(int serverId, string databaseName)
    {
        var server = await _context.DatabaseServers.FindAsync(serverId);
        if (server == null)
        {
            return NotFound(new { message = "Server not found" });
        }

        try
        {
            var password = !string.IsNullOrEmpty(server.PasswordEncrypted)
                ? _encryptionService.Decrypt(server.PasswordEncrypted)
                : "";

            var provider = _providerFactory.CreateProvider(server, password);
            var tables = await provider.ListTablesAsync(databaseName);

            return Ok(tables);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to list tables for database {databaseName}");
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{databaseName}/tables/{tableName}/schema")]
    public async Task<ActionResult> GetTableSchema(int serverId, string databaseName, string tableName)
    {
        var server = await _context.DatabaseServers.FindAsync(serverId);
        if (server == null)
        {
            return NotFound(new { message = "Server not found" });
        }

        try
        {
            var password = !string.IsNullOrEmpty(server.PasswordEncrypted)
                ? _encryptionService.Decrypt(server.PasswordEncrypted)
                : "";

            var provider = _providerFactory.CreateProvider(server, password);
            var schema = await provider.GetTableSchemaAsync(databaseName, tableName);

            return Ok(schema);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to get schema for table {tableName}");
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{databaseName}/query")]
    public async Task<ActionResult> ExecuteQuery(int serverId, string databaseName, [FromBody] QueryDto dto)
    {
        var server = await _context.DatabaseServers.FindAsync(serverId);
        if (server == null)
        {
            return NotFound(new { message = "Server not found" });
        }

        try
        {
            var password = !string.IsNullOrEmpty(server.PasswordEncrypted)
                ? _encryptionService.Decrypt(server.PasswordEncrypted)
                : "";

            var provider = _providerFactory.CreateProvider(server, password);
            var result = await provider.ExecuteQueryAsync(databaseName, dto.Query);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to execute query on database {databaseName}");
            return BadRequest(new { message = ex.Message });
        }
    }
}

public class CreateDatabaseDto
{
    public required string Name { get; set; }
    public string? Charset { get; set; }
    public string? Collation { get; set; }
}

public class QueryDto
{
    public required string Query { get; set; }
}
