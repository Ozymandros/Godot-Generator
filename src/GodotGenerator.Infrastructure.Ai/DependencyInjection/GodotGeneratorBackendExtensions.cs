using GodotGenerator.Infrastructure.Persistence.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GodotGenerator.Infrastructure.Ai.DependencyInjection;

/// <summary>
/// Registers persistence, AI infrastructure, and application use cases in one call (persistence first).
/// </summary>
public static class GodotGeneratorBackendExtensions
{
    /// <summary>
    /// Adds JSON persistence, Godot MCP + Semantic Kernel orchestration, and application use cases.
    /// </summary>
    public static IServiceCollection AddGodotGeneratorBackend(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddGodotGeneratorPersistence(configuration);
        services.AddGodotGeneratorInfrastructure(configuration);
        return services;
    }
}
