using System.Net.Http;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Common.IntegrationTests;

/// <summary>
/// Shared base fixture that owns MySQL, RabbitMQ, and Redis Testcontainers.
/// Each derived fixture adds service-specific WebApplicationFactory overrides.
/// </summary>
public abstract class IntegrationTestFixture : IAsyncDisposable
{
    // ── Docker network so containers can reference each other by name ──────────
    private readonly INetwork _network;

    // ── Container instances ────────────────────────────────────────────────────
    private MySqlTestcontainer? _mysqlContainer;
    private RabbitMqTestcontainer? _rabbitMqContainer;
    private RedisTestcontainer? _redisContainer;

    private bool _started;

    /// <summary>MySQL connection string pointing at the live testcontainer.</summary>
    public string MySqlConnectionString => _mysqlContainer?.ConnectionString
        ?? throw new InvalidOperationException("Containers not started — call StartContainersAsync first.");

    /// <summary>RabbitMQ connection string for the testcontainer.</summary>
    public string RabbitMqConnectionString => _rabbitMqContainer?.ConnectionString
        ?? throw new InvalidOperationException("Containers not started — call StartContainersAsync first.");

    /// <summary>Redis connection string for the testcontainer.</summary>
    public string RedisConnectionString => _redisContainer?.ConnectionString
        ?? throw new InvalidOperationException("Containers not started — call StartContainersAsync first.");

    protected IntegrationTestFixture()
    {
        _network = new NetworkBuilder()
            .WithName($"cma-integration-test-{Guid.NewGuid():N}")
            .Build();
    }

    /// <summary>
    /// Starts MySQL, RabbitMQ, and Redis containers in parallel.
    /// Derived fixtures must call this before constructing their WebApplicationFactory.
    /// </summary>
    public async Task StartContainersAsync(CancellationToken ct = default)
    {
        if (_started) return;
        _started = true;

        _mysqlContainer = new ContainerBuilder<MySqlTestcontainer>()
            .WithNetwork(_network)
            .WithDatabase(new MySqlTestcontainerConfiguration
            {
                Database = "cma_test",
                Username = "root",
                Password = "testcontainers"
            })
            .WithResourceMapping(
                new byte[] { },
                Path.Combine(Path.GetTempPath(), $"mysql-init-{Guid.NewGuid():N}.sql"))
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(3306))
            .WithStartupCallback(async (_, cancellationToken) =>
            {
                // Grant root access from any host so the app service can connect
                using var client = new MySqlConnector.MySqlConnection(_mysqlContainer!.ConnectionString);
                await client.ConnectAsync(cancellationToken);
                await using var cmd = client.CreateCommand();
                cmd.CommandText = "CREATE DATABASE IF NOT EXISTS cma_test;";
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            })
            .Build();

        _rabbitMqContainer = new ContainerBuilder<RabbitMqTestcontainer>()
            .WithNetwork(_network)
            .WithRabbitMQ(new RabbitMqTestcontainerConfiguration
            {
                DefaultUserCredentials = new RabbitMqTestcontainerDefaultCredentials
                {
                    Username = "guest",
                    Password = "guest"
                }
            })
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5672))
            .Build();

        _redisContainer = new ContainerBuilder<RedisTestcontainer>()
            .WithNetwork(_network)
            .WithRedisConfiguration(new RedisTestcontainerConfiguration
            {
                Database = new RedisTestcontainerDatabaseConfiguration()
            })
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(6379))
            .Build();

        await Task.WhenAll(
            _mysqlContainer.StartAsync(ct),
            _rabbitMqContainer.StartAsync(ct),
            _redisContainer.StartAsync(ct));
    }

    /// <summary>
    /// Returns the connection string override dictionary used to configure
    /// <see cref="WebApplicationFactory{TEntryPoint}"/> with testcontainer endpoints.
    /// </summary>
    protected Dictionary<string, string?> BuildAppSettingsOverride()
    {
        // Replace {DbName} in the connection string template with the real database name.
        // The infrastructure layer uses "Database={DbName}" in the template; Testcontainers
        // mounts the actual container DB name into the same placeholder.
        var mysqlCs = MySqlConnectionString;
        var redisCs = RedisConnectionString;

        return new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = mysqlCs,
            ["ConnectionStrings:RedisConnection"] = redisCs,
            ["ConnectionStrings:RabbitMqConnection"] = RabbitMqConnectionString,
        };
    }

    /// <summary>
    /// Override in derived fixtures to add service-specific service registrations
    /// on top of the WebApplicationFactory defaults.
    /// </summary>
    protected virtual void ConfigureTestServices(IServiceCollection services) { }

    /// <summary>
    /// Override in derived fixtures to tweak the <see cref="IConfiguration"/>
    /// before the app is built.
    /// </summary>
    protected virtual void ConfigureAppConfiguration(IConfigurationBuilder config)
    {
        foreach (var (key, value) in BuildAppSettingsOverride())
            config.AddInMemoryCollection(new[] { new KeyValuePair<string, string?>(key, value) });
    }

    /// <summary>
    /// Override in derived fixtures to add environment variables pointing at
    /// the testcontainer endpoints (used by RabbitMqEventPublisher etc.).
    /// </summary>
    protected virtual void ConfigureTestEnvironment(IDictionary<string, string> env) { }

    /// <summary>Ensures all containers are stopped and disposed.</summary>
    public async ValueTask DisposeAsync()
    {
        if (_redisContainer != null)
            await _redisContainer.DisposeAsync();
        if (_rabbitMqContainer != null)
            await _rabbitMqContainer.DisposeAsync();
        if (_mysqlContainer != null)
            await _mysqlContainer.DisposeAsync();
        if (_network != null)
            await _network.DisposeAsync();
    }
}
