using GodotGenerator.Mcp.Api.Services;
using GodotGenerator.Wizard.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace GodotGenerator.Mcp.Api.DependencyInjection;

/// <summary>
/// Registers API-side adapters used by the dedicated wizard MCP/plugin path.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds wizard gateway adapters that bridge plugin tool calls into the existing API facade.
    /// </summary>
    public static IServiceCollection AddGodotGeneratorMcpApi(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<IWizardGenerationGateway, ApiWizardGenerationGateway>();
        return services;
    }
}
