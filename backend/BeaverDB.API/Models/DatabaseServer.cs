namespace BeaverDB.API.Models;

public enum ServerType
{
    MySQL,
    PostgreSQL,
    SQLServer,
    Redis,
    MongoDB
}

public class DatabaseServer
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public ServerType Type { get; set; }
    public required string Host { get; set; }
    public int Port { get; set; }
    public string? Username { get; set; }
    public string? PasswordEncrypted { get; set; }
    public bool IsManagedByDocker { get; set; }
    public string? DockerContainerId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastConnectionTest { get; set; }
    public bool? LastConnectionSuccess { get; set; }
}
