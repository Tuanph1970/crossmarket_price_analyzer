using Common.IntegrationTests;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProductService.Infrastructure.Persistence;

namespace ProductService.IntegrationTests;

/// <summary>
/// WebApplicationFactory for ProductService integration tests.
/// Starts MySQL, RabbitMQ and Redis testcontainers, then builds the
/// ProductService.Api application with connection strings pointing at the
/// live containers.
/// </summary>
public sealed class ProductServiceTestFixture : IntegrationTestFixture
{
    public WebApplicationFactory<Program>? _webAppFactory;

    /// <summary>
    /// Lazily creates and returns the <see cref="WebApplicationFactory"/>.
    /// Tests MUST access this property (rather than the fixture directly) so that
    /// containers are started before the app is built.
    /// </summary>
    public WebApplicationFactory<Program> WebAppFactory
    {
        get
        {
            if (_webAppFactory != null) return _webAppFactory;

            // 1. Start all containers
            using var syncScope = new SemaphoreSlim(1);
            syncScope.Wait(); // serialise fixture initialisation
            try
            {
                StartContainersAsync().GetAwaiter().GetResult();
            }
            finally
            {
                syncScope.Release();
            }

            // 2. Build the factory with testcontainer connection strings
            _webAppFactory = new ProductServiceWebApplicationFactory(this);
            return _webAppFactory;
        }
    }

    private sealed class ProductServiceWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly ProductServiceTestFixture _fixture;

        public ProductServiceWebApplicationFactory(ProductServiceTestFixture fixture)
        {
            _fixture = fixture;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");

            builder.ConfigureAppConfiguration((_, config) =>
                _fixture.ConfigureAppConfiguration(config));

            builder.ConfigureServices(services =>
            {
                // Remove any previously registered ProductDbContext registrations
                // so we can replace them with container-backed ones.
                var existingDb = services.SingleOrDefault(s =>
                    s.ServiceType == typeof(DbContextOptions<ProductDbContext>));
                if (existingDb != null)
                    services.Remove(existingDb);

                var existingDbContext = services.SingleOrDefault(s =>
                    s.ServiceType == typeof(ProductDbContext));
                if (existingDbContext != null)
                    services.Remove(existingDbContext);

                services.AddDbContext<ProductDbContext>(opts =>
                    opts.UseMySql(
                        _fixture.MySqlConnectionString,
                        ServerVersion.AutoDetect(_fixture.MySqlConnectionString),
                        mySqlOpts =>
                        {
                            mySqlOpts.EnableRetryOnFailure(maxRetryCount: 3);
                        }));

                _fixture.ConfigureTestServices(services);
            });

            builder.UseSetting("ASPNETCORE_ENVIRONMENT", "Development");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _webAppFactory?.Dispose();
            base.Dispose(disposing);
        }
    }

    public override async ValueTask DisposeAsync()
    {
        _webAppFactory?.Dispose();
        await base.DisposeAsync();
    }
}
