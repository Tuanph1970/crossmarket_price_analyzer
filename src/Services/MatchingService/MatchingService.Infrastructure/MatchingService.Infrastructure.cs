using Microsoft.Extensions.DependencyInjection;

namespace MatchingService.Infrastructure;

/// <summary>
/// DI extension to register MatchingService infrastructure into the service container.
/// Call from Startup/Program.cs of MatchingService.Api.
/// </summary>
public static class MatchingServiceInfrastructureExtensions
{
    /// <summary>
    /// Registers all MatchingService infrastructure services.
    /// MatchingDbContext must already be registered (via AddDbContext in the API project).
    /// </summary>
    public static IServiceCollection AddMatchingServiceInfrastructure(this IServiceCollection services)
    {
        // Infrastructure services are registered in MatchingService.Api/Program.cs
        // because MatchingService.Application already has the repository and
        // FuzzyMatchingService registrations there.
        // This extension is a placeholder for future infrastructure services.
        return services;
    }
}
