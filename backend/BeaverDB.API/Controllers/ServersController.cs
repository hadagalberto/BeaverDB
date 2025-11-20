using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BeaverDB.API.Data;
using BeaverDB.API.Models;
using BeaverDB.API.Models.DTOs;
using BeaverDB.API.Services;
using BeaverDB.API.Services.DatabaseProviders;

namespace BeaverDB.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ServersController : ControllerBase
{
    private readonly BeaverDbContext _context;
    private readonly IEncryptionService _encryptionService;
    private readonly IDockerService _dockerService;
    private readonly IDatabaseProviderFactory _providerFactory;
    private readonly ILogger<ServersController> _logger;

    public ServersController(
        BeaverDbContext context,
        IEncryptionService encryptionService,
        IDockerService dockerService,
        IDatabaseProviderFactory providerFactory,
        ILogger<ServersController> logger)
    {
        _context = context;
        _encryptionService = encryptionService;
        _dockerService = dockerService;
        _providerFactory = providerFactory;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<List<ServerResponseDto>>> GetServers()
    {
        var servers = await _context.DatabaseServers.ToListAsync();
        
        var response = new List<ServerResponseDto>();
        foreach (var server in servers)
        {
            string? status = null;
            if (server.IsManagedByDocker && !string.IsNullOrEmpty(server.DockerContainerId))
            {
                status = await _dockerService.GetContainerStatusAsync(server.DockerContainerId);
            }

            response.Add(new ServerResponseDto
            {
                Id = server.Id,
                Name = server.Name,
                Type = server.Type.ToString(),
                Host = server.Host,
                Port = server.Port,
                Username = server.Username,
                IsManagedByDocker = server.IsManagedByDocker,
                DockerContainerId = server.DockerContainerId,
                CreatedAt = server.CreatedAt,
                LastConnectionTest = server.LastConnectionTest,
                LastConnectionSuccess = server.LastConnectionSuccess,
                Status = status
            });
        }

        return Ok(response);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ServerResponseDto>> GetServer(int id)
    {
        var server = await _context.DatabaseServers.FindAsync(id);
        if (server == null)
        {
            return NotFound();
        }

        string? status = null;
        if (server.IsManagedByDocker && !string.IsNullOrEmpty(server.DockerContainerId))
        {
            status = await _dockerService.GetContainerStatusAsync(server.DockerContainerId);
        }

        return Ok(new ServerResponseDto
        {
            Id = server.Id,
            Name = server.Name,
            Type = server.Type.ToString(),
            Host = server.Host,
            Port = server.Port,
            Username = server.Username,
            IsManagedByDocker = server.IsManagedByDocker,
            DockerContainerId = server.DockerContainerId,
            CreatedAt = server.CreatedAt,
            LastConnectionTest = server.LastConnectionTest,
            LastConnectionSuccess = server.LastConnectionSuccess,
            Status = status
        });
    }

    [HttpPost]
    public async Task<ActionResult<ServerResponseDto>> CreateServer([FromBody] CreateServerDto dto)
    {
        if (!Enum.TryParse<ServerType>(dto.Type, out var serverType))
        {
            return BadRequest(new { message = "Invalid server type" });
        }

        var server = new DatabaseServer
        {
            Name = dto.Name,
            Type = serverType,
            Host = dto.Host,
            Port = dto.Port,
            Username = dto.Username,
            PasswordEncrypted = !string.IsNullOrEmpty(dto.Password) 
                ? _encryptionService.Encrypt(dto.Password) 
                : null,
            IsManagedByDocker = dto.IsManagedByDocker
        };

        // If managed by Docker, create and start container
        if (dto.IsManagedByDocker && !string.IsNullOrEmpty(dto.Password))
        {
            try
            {
                var containerId = await _dockerService.CreateAndStartContainerAsync(server, dto.Password);
                server.DockerContainerId = containerId;
                
                // Wait a bit for container to start
                await Task.Delay(3000);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create Docker container");
                return BadRequest(new { message = "Failed to create Docker container: " + ex.Message });
            }
        }

        _context.DatabaseServers.Add(server);
        await _context.SaveChangesAsync();

        // Test connection
        await TestConnectionInternal(server, dto.Password ?? "");

        return CreatedAtAction(nameof(GetServer), new { id = server.Id }, new ServerResponseDto
        {
            Id = server.Id,
            Name = server.Name,
            Type = server.Type.ToString(),
            Host = server.Host,
            Port = server.Port,
            Username = server.Username,
            IsManagedByDocker = server.IsManagedByDocker,
            DockerContainerId = server.DockerContainerId,
            CreatedAt = server.CreatedAt,
            LastConnectionTest = server.LastConnectionTest,
            LastConnectionSuccess = server.LastConnectionSuccess
        });
    }

    [HttpPut("{id}")]
    public async Task<ActionResult> UpdateServer(int id, [FromBody] UpdateServerDto dto)
    {
        var server = await _context.DatabaseServers.FindAsync(id);
        if (server == null)
        {
            return NotFound();
        }

        if (dto.Name != null) server.Name = dto.Name;
        if (dto.Host != null) server.Host = dto.Host;
        if (dto.Port.HasValue) server.Port = dto.Port.Value;
        if (dto.Username != null) server.Username = dto.Username;
        if (dto.Password != null) server.PasswordEncrypted = _encryptionService.Encrypt(dto.Password);

        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteServer(int id)
    {
        var server = await _context.DatabaseServers.FindAsync(id);
        if (server == null)
        {
            return NotFound();
        }

        // If Docker managed, remove container
        if (server.IsManagedByDocker && !string.IsNullOrEmpty(server.DockerContainerId))
        {
            try
            {
                await _dockerService.RemoveContainerAsync(server.DockerContainerId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Failed to remove Docker container {server.DockerContainerId}");
            }
        }

        _context.DatabaseServers.Remove(server);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpPost("{id}/test-connection")]
    public async Task<ActionResult> TestConnection(int id)
    {
        var server = await _context.DatabaseServers.FindAsync(id);
        if (server == null)
        {
            return NotFound();
        }

        var password = !string.IsNullOrEmpty(server.PasswordEncrypted)
            ? _encryptionService.Decrypt(server.PasswordEncrypted)
            : "";

        var success = await TestConnectionInternal(server, password);

        return Ok(new { success, lastTest = server.LastConnectionTest });
    }

    [HttpPost("{id}/start")]
    public async Task<ActionResult> StartContainer(int id)
    {
        var server = await _context.DatabaseServers.FindAsync(id);
        if (server == null || !server.IsManagedByDocker || string.IsNullOrEmpty(server.DockerContainerId))
        {
            return BadRequest(new { message = "Server is not Docker managed" });
        }

        try
        {
            await _dockerService.StartContainerAsync(server.DockerContainerId);
            return Ok(new { message = "Container started" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id}/stop")]
    public async Task<ActionResult> StopContainer(int id)
    {
        var server = await _context.DatabaseServers.FindAsync(id);
        if (server == null || !server.IsManagedByDocker || string.IsNullOrEmpty(server.DockerContainerId))
        {
            return BadRequest(new { message = "Server is not Docker managed" });
        }

        try
        {
            await _dockerService.StopContainerAsync(server.DockerContainerId);
            return Ok(new { message = "Container stopped" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{id}/status")]
    public async Task<ActionResult> GetContainerStatus(int id)
    {
        var server = await _context.DatabaseServers.FindAsync(id);
        if (server == null || !server.IsManagedByDocker || string.IsNullOrEmpty(server.DockerContainerId))
        {
            return BadRequest(new { message = "Server is not Docker managed" });
        }

        var status = await _dockerService.GetContainerStatusAsync(server.DockerContainerId);
        return Ok(new { status });
    }

    private async Task<bool> TestConnectionInternal(DatabaseServer server, string password)
    {
        try
        {
            var provider = _providerFactory.CreateProvider(server, password);
            var success = await provider.TestConnectionAsync();
            
            server.LastConnectionTest = DateTime.UtcNow;
            server.LastConnectionSuccess = success;
            await _context.SaveChangesAsync();

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Connection test failed for server {server.Name}");
            server.LastConnectionTest = DateTime.UtcNow;
            server.LastConnectionSuccess = false;
            await _context.SaveChangesAsync();
            return false;
        }
    }
}
