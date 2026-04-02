using GodotGenerator.Api.Abstractions;
using GodotGenerator.Api.Services;
using GodotGenerator.Application.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace GodotGenerator.Api.DependencyInjection;

/// <summary>
/// Registers the transport-agnostic public API service layer.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds API facade services and required application services.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <returns>Updated service collection.</returns>
    public static IServiceCollection AddGodotGeneratorApi(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddGodotGeneratorApplication();
        services.AddScoped<IGodotGeneratorApiService, GodotGeneratorApiService>();
        return services;
    }
}
