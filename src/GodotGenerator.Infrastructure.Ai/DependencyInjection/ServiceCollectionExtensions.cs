using GodotGenerator.Application.Abstractions;
using GodotGenerator.Application.DependencyInjection;
using GodotGenerator.Infrastructure.Ai.KernelFactory;
using GodotGenerator.Infrastructure.Ai.Options;
using GodotGenerator.Infrastructure.Ai.Services;
using GodotMcp.Plugin.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GodotGenerator.Infrastructure.Ai.DependencyInjection;

/// <summary>
/// Registers Semantic Kernel, Godot MCP plugin, and AI orchestration.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Godot Generator AI infrastructure: <c>GodotMcp.SemanticKernel.Plugin</c>, kernel factory, and <see cref="IAiOrchestrationService"/>.
    /// Call <see cref="GodotGenerator.Application.DependencyInjection.ServiceCollectionExtensions.AddGodotGeneratorApplication"/> after persistence if use cases are required.
    /// </summary>
    public static IServiceCollection AddGodotGeneratorInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(services);

        // Validate shape/annotations at bind time but do not force validation at startup.
        // The API key is a user secret required only when actually creating kernels / calling LLMs.
        // Kernel creation (GodotKernelFactory) will enforce that an API key exists when needed.
        services
            .AddOptions<LlmOptions>()
            .Bind(configuration.GetSection(LlmOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.ChatModelId),
                $"{LlmOptions.SectionName}:ChatModelId is required.");
        services
            .AddOptions<OrchestrationOptions>()
            .Bind(configuration.GetSection(OrchestrationOptions.SectionName))
            .ValidateDataAnnotations();
        services.AddGodotMcp(configuration);

        services.AddSingleton<IKernelFactory, GodotKernelFactory>();
        services.AddSingleton<IProviderSecretResolver, PreferenceProviderSecretResolver>();
        services.AddSingleton<IProviderCapabilityRouter, ProviderCapabilityRouter>();
        services.AddSingleton<GodotGenerator.Application.Abstractions.IGodotProjectPathValidator, GodotProjectPathValidator>();
        services.AddSingleton<IAiOrchestrationService, AiOrchestrationService>();
        services.AddSingleton<GodotGenerator.Application.Abstractions.ILlmDiscoveryInfoProvider, LlmDiscoveryInfoProvider>();
        services.AddSingleton<GodotGenerator.Application.Abstractions.IGodotMcpToolCatalog, GodotMcpToolCatalog>();
        services.AddGodotGeneratorApplication();
        return services;
    }
}
