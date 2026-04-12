using Common.IntegrationTests;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ScoringService.Application.Persistence;

namespace ScoringService.IntegrationTests;

/// <summary>
/// WebApplicationFactory for ScoringService integration tests.
/// </summary>
public sealed class ScoringServiceTestFixture : IntegrationTestFixture
{
    public WebApplicationFactory<Program>? _webAppFactory;

    public WebApplicationFactory<Program> WebAppFactory
    {
        get
        {
            if (_webAppFactory != null) return _webAppFactory;

            using var sem = new SemaphoreSlim(1);
            sem.Wait();
            try { StartContainersAsync().GetAwaiter().GetResult(); }
            finally { sem.Release(); }

            _webAppFactory = new ScoringServiceWebApplicationFactory(this);
            return _webAppFactory;
        }
    }

    private sealed class ScoringServiceWebApplicationFactory
        : WebApplicationFactory<Program>
    {
        private readonly ScoringServiceTestFixture _fixture;

        public ScoringServiceWebApplicationFactory(ScoringServiceTestFixture fixture)
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
                var existingOptions = services
                    .SingleOrDefault(s => s.ServiceType ==
                        typeof(DbContextOptions<ScoringDbContext>));
                if (existingOptions != null) services.Remove(existingOptions);

                var existingCtx = services
                    .SingleOrDefault(s => s.ServiceType == typeof(ScoringDbContext));
                if (existingCtx != null) services.Remove(existingCtx);

                services.AddDbContext<ScoringDbContext>(opts =>
                    opts.UseMySql(
                        _fixture.MySqlConnectionString,
                        ServerVersion.AutoDetect(_fixture.MySqlConnectionString),
                        mySql => mySql.EnableRetryOnFailure(maxRetryCount: 3)));

                _fixture.ConfigureTestServices(services);
            });
        }
    }

    public override async ValueTask DisposeAsync()
    {
        _webAppFactory?.Dispose();
        await base.DisposeAsync();
    }
}
