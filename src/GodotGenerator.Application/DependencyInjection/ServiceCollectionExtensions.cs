using GodotGenerator.Application.Abstractions;
using GodotGenerator.Application.Orchestration;
using GodotGenerator.Application.UseCases;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
        services.TryAddSingleton<ILlmDiscoveryInfoProvider, DefaultLlmDiscoveryInfoProvider>();
        services.TryAddSingleton<IGodotMcpToolCatalog, NullGodotMcpToolCatalog>();
        services.TryAddSingleton<IGodotProjectPathValidator, PassThroughGodotProjectPathValidator>();
        services.AddSingleton<IModalityTurnComposer, ModalityTurnComposer>();
        services.AddScoped<RunAgentTurnUseCase>();
        services.AddScoped<GetPreferenceUseCase>();
        services.AddScoped<SetPreferenceUseCase>();
        services.AddScoped<GetApiKeysUseCase>();
        services.AddScoped<SaveApiKeysUseCase>();
        services.AddScoped<GetAllConfigUseCase>();
        services.AddScoped<EnhancePromptUseCase>();
        services.AddScoped<RunWizardUseCase>();
        return services;
    }
}
