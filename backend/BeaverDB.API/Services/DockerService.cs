using Docker.DotNet;
using Docker.DotNet.Models;
using BeaverDB.API.Models;

namespace BeaverDB.API.Services;

public interface IDockerService
{
    Task<string> CreateAndStartContainerAsync(DatabaseServer server, string password);
    Task StopContainerAsync(string containerId);
    Task StartContainerAsync(string containerId);
    Task<string> GetContainerStatusAsync(string containerId);
    Task RemoveContainerAsync(string containerId);
}

public class DockerService : IDockerService
{
    private readonly DockerClient _dockerClient;
    private readonly ILogger<DockerService> _logger;

    public DockerService(ILogger<DockerService> logger)
    {
        _logger = logger;
        
        try
        {
            // For Windows, use named pipe. For Linux, use unix socket
            var dockerUri = Environment.OSVersion.Platform == PlatformID.Win32NT
                ? "npipe://./pipe/docker_engine"
                : "unix:///var/run/docker.sock";
                
            _dockerClient = new DockerClientConfiguration(new Uri(dockerUri)).CreateClient();
            _logger.LogInformation("Docker client initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to initialize Docker client. Docker-managed servers will not be available.");
            throw;
        }
    }

    public async Task<string> CreateAndStartContainerAsync(DatabaseServer server, string password)
    {
        var (image, envVars, exposedPort) = GetDockerConfig(server.Type, password);

        try
        {
            _logger.LogInformation($"Pulling Docker image: {image}:latest");
            
            // Pull image if not exists
            await _dockerClient.Images.CreateImageAsync(
                new ImagesCreateParameters 
                { 
                    FromImage = image,
                    Tag = "latest" 
                },
                null,
                new Progress<JSONMessage>(message =>
                {
                    if (!string.IsNullOrEmpty(message.Status))
                    {
                        _logger.LogDebug($"Docker pull: {message.Status}");
                    }
                }));

            _logger.LogInformation($"Image {image}:latest pulled successfully");

            // Create container
            var containerName = $"beaverdb-{server.Type.ToString().ToLower()}-{server.Id}";
            
            var createResponse = await _dockerClient.Containers.CreateContainerAsync(new CreateContainerParameters
            {
                Image = $"{image}:latest",
                Name = containerName,
                Env = envVars,
                HostConfig = new HostConfig
                {
                    PortBindings = new Dictionary<string, IList<PortBinding>>
                    {
                        {
                            $"{exposedPort}/tcp",
                            new List<PortBinding> { new PortBinding { HostPort = server.Port.ToString() } }
                        }
                    },
                    RestartPolicy = new RestartPolicy { Name = RestartPolicyKind.UnlessStopped }
                },
                ExposedPorts = new Dictionary<string, EmptyStruct>
                {
                    { $"{exposedPort}/tcp", new EmptyStruct() }
                }
            });

            // Start container
            await _dockerClient.Containers.StartContainerAsync(createResponse.ID, new ContainerStartParameters());

            _logger.LogInformation($"Container {containerName} created and started with ID: {createResponse.ID}");
            
            return createResponse.ID;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error creating/starting container for server {server.Name}");
            throw;
        }
    }

    public async Task StopContainerAsync(string containerId)
    {
        await _dockerClient.Containers.StopContainerAsync(containerId, new ContainerStopParameters());
    }

    public async Task StartContainerAsync(string containerId)
    {
        await _dockerClient.Containers.StartContainerAsync(containerId, new ContainerStartParameters());
    }

    public async Task<string> GetContainerStatusAsync(string containerId)
    {
        try
        {
            var container = await _dockerClient.Containers.InspectContainerAsync(containerId);
            return container.State.Status;
        }
        catch
        {
            return "unknown";
        }
    }

    public async Task RemoveContainerAsync(string containerId)
    {
        await _dockerClient.Containers.RemoveContainerAsync(containerId, new ContainerRemoveParameters { Force = true });
    }

    private (string image, List<string> envVars, int port) GetDockerConfig(ServerType type, string password)
    {
        return type switch
        {
            ServerType.MySQL => (
                "mysql:8",
                new List<string>
                {
                    "MYSQL_ROOT_PASSWORD=" + password,
                    "MYSQL_DATABASE=defaultdb"
                },
                3306
            ),
            ServerType.PostgreSQL => (
                "postgres:16",
                new List<string>
                {
                    "POSTGRES_PASSWORD=" + password,
                    "POSTGRES_DB=defaultdb"
                },
                5432
            ),
            ServerType.SQLServer => (
                "mcr.microsoft.com/mssql/server:2022-latest",
                new List<string>
                {
                    "ACCEPT_EULA=Y",
                    "SA_PASSWORD=" + password,
                    "MSSQL_PID=Express"
                },
                1433
            ),
            ServerType.MongoDB => (
                "mongo:latest",
                new List<string>
                {
                    "MONGO_INITDB_ROOT_USERNAME=admin",
                    "MONGO_INITDB_ROOT_PASSWORD=" + password
                },
                27017
            ),
            ServerType.Redis => (
                "redis:latest",
                new List<string>
                {
                    $"--requirepass {password}"
                },
                6379
            ),
            _ => throw new ArgumentException($"Unsupported server type: {type}")
        };
    }
}
