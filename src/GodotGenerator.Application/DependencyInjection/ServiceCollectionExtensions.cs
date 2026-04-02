using GodotGenerator.Application.UseCases;
using Microsoft.Extensions.DependencyInjection;

namespace GodotGenerator.Application.DependencyInjection;

/// <summary>
/// Registers application-layer services (use cases). Ports must be registered by Infrastructure.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Godot Generator application use cases.
    /// </summary>
    public static IServiceCollection AddGodotGeneratorApplication(this IServiceCollection services)
    {
        services.AddScoped<RunAgentTurnUseCase>();
        services.AddScoped<GetPreferenceUseCase>();
        services.AddScoped<SetPreferenceUseCase>();
        services.AddScoped<GetApiKeysUseCase>();
        services.AddScoped<SaveApiKeysUseCase>();
        services.AddScoped<GetAllConfigUseCase>();
        return services;
    }
}
