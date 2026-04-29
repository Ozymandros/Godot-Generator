using GodotGenerator.Application.Abstractions;
using GodotGenerator.Plugins.Plugins;
using GodotGenerator.Plugins.Services;
using Microsoft.Extensions.DependencyInjection;

namespace GodotGenerator.Plugins.DependencyInjection;

/// <summary>
/// Registers dedicated wizard plugin orchestration services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds wizard plugin runtime services used by <see cref="IWizardOrchestrationService"/>.
    /// </summary>
    public static IServiceCollection AddGodotGeneratorPlugins(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<WizardMcpPlugin>();
        services.AddSingleton<IWizardOrchestrationService, WizardOrchestrationService>();
        return services;
    }
}
