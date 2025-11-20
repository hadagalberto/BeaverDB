namespace BeaverDB.API.Models.DTOs;

public class CreateServerDto
{
    public required string Name { get; set; }
    public required string Type { get; set; } // MySQL, PostgreSQL, SQLServer, Redis, MongoDB
    public required string Host { get; set; }
    public int Port { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public bool IsManagedByDocker { get; set; }
}

public class UpdateServerDto
{
    public string? Name { get; set; }
    public string? Host { get; set; }
    public int? Port { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
}

public class ServerResponseDto
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Type { get; set; }
    public required string Host { get; set; }
    public int Port { get; set; }
    public string? Username { get; set; }
    public bool IsManagedByDocker { get; set; }
    public string? DockerContainerId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastConnectionTest { get; set; }
    public bool? LastConnectionSuccess { get; set; }
    public string? Status { get; set; } // running, stopped, unknown
}
