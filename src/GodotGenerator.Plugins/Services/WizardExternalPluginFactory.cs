#nullable enable

using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace GodotGenerator.Plugins.Services;

/// <summary>
/// Creates optional external MCP plugins for the Wizard kernel from environment variables.
/// </summary>
/// <remarks>
/// <para>
/// Each method reads an API key from a well-known environment variable and returns a
/// ready-to-register <see cref="KernelPlugin"/>, or <see langword="null"/> when the key
/// is absent. A <see langword="null"/> return is treated as "plugin disabled" — the Wizard
/// kernel starts normally and the LLM simply won't have access to that capability.
/// </para>
/// <para>
/// <strong>Required NuGet packages</strong> (add to <c>GodotGenerator.Plugins.csproj</c>
/// once the packages are available and the implementation stubs below are replaced):
/// <list type="bullet">
///   <item><term>ElevenLabs.McpClient</term><description>https://www.nuget.org/packages/ElevenLabs.McpClient</description></item>
///   <item><term>ImageGenMcp.SemanticKernel.Plugin</term><description>https://www.nuget.org/packages/ImageGenMcp.SemanticKernel.Plugin</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>Implementation note</strong>: the method bodies below are stubs that log a
/// warning and return <see langword="null"/>. Replace each stub body with the real
/// package initialization once the packages are installed and their API surfaces are known.
/// The env-var reading, graceful-degradation pattern, and logging are already in place.
/// </para>
/// </remarks>
internal static class WizardExternalPluginFactory
{
    /// <summary>Name of the environment variable holding the ElevenLabs API key.</summary>
    internal const string ElevenLabsApiKeyEnvVar = "ELEVENLABS_API_KEY";

    /// <summary>Name of the environment variable holding the image-generation API key.</summary>
    internal const string ImageGenApiKeyEnvVar = "IMAGE_GEN_API_KEY";

    /// <summary>
    /// Attempts to create the ElevenLabs audio/TTS plugin from the
    /// <c>ELEVENLABS_API_KEY</c> environment variable.
    /// </summary>
    /// <param name="logger">Logger for warnings when the plugin is skipped or fails.</param>
    /// <param name="cancellationToken">Cancellation token passed to the plugin initializer.</param>
    /// <returns>
    /// An initialized <see cref="KernelPlugin"/> ready to register, or <see langword="null"/>
    /// when the key is missing or the plugin cannot be created.
    /// </returns>
    /// <remarks>
    /// <strong>Stub implementation</strong>. Replace the body with actual
    /// <c>ElevenLabs.McpClient</c> initialization, for example:
    /// <code>
    /// using ElevenLabs.Mcp;
    ///
    /// var client = new ElevenLabsMcpClient(apiKey);
    /// await client.InitializeAsync(cancellationToken);
    /// return client.AsKernelPlugin("ElevenLabsAudio");
    /// </code>
    /// </remarks>
    internal static Task<KernelPlugin?> TryCreateElevenLabsPluginAsync(
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var apiKey = Environment.GetEnvironmentVariable(ElevenLabsApiKeyEnvVar);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            logger.LogWarning(
                "ElevenLabs audio plugin disabled: environment variable '{EnvVar}' is not set.",
                ElevenLabsApiKeyEnvVar);
            return Task.FromResult<KernelPlugin?>(null);
        }

        // TODO: Replace with ElevenLabs.McpClient initialization once the package is installed.
        // See XML doc above for the expected implementation pattern.
        logger.LogWarning(
            "ElevenLabs audio plugin: API key is configured but ElevenLabs.McpClient is not yet " +
            "wired up. Install the NuGet package and replace this stub in {FactoryType}.",
            nameof(WizardExternalPluginFactory));
        return Task.FromResult<KernelPlugin?>(null);
    }

    /// <summary>
    /// Attempts to create the image-generation plugin from the
    /// <c>IMAGE_GEN_API_KEY</c> environment variable.
    /// </summary>
    /// <param name="logger">Logger for warnings when the plugin is skipped or fails.</param>
    /// <param name="cancellationToken">Cancellation token passed to the plugin initializer.</param>
    /// <returns>
    /// An initialized <see cref="KernelPlugin"/> ready to register, or <see langword="null"/>
    /// when the key is missing or the plugin cannot be created.
    /// </returns>
    /// <remarks>
    /// <strong>Stub implementation</strong>. Replace the body with actual
    /// <c>ImageGenMcp.SemanticKernel.Plugin</c> initialization, for example:
    /// <code>
    /// using ImageGenMcp.Plugin;
    /// using ImageGenMcp.Plugin.Extensions;
    ///
    /// var helperKernel = Kernel.CreateBuilder().Build();
    /// helperKernel.RegisterImageGenTools(apiKey);
    /// return helperKernel.Plugins["ImageGen"];
    /// </code>
    /// or, if the package provides a direct factory:
    /// <code>
    /// return ImageGenMcpPlugin.CreateAsync(apiKey, cancellationToken);
    /// </code>
    /// </remarks>
    internal static Task<KernelPlugin?> TryCreateImageGenPluginAsync(
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var apiKey = Environment.GetEnvironmentVariable(ImageGenApiKeyEnvVar);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            logger.LogWarning(
                "Image generation plugin disabled: environment variable '{EnvVar}' is not set.",
                ImageGenApiKeyEnvVar);
            return Task.FromResult<KernelPlugin?>(null);
        }

        // TODO: Replace with ImageGenMcp.SemanticKernel.Plugin initialization once the package is installed.
        // See XML doc above for the expected implementation patterns.
        logger.LogWarning(
            "Image generation plugin: API key is configured but ImageGenMcp.SemanticKernel.Plugin is not yet " +
            "wired up. Install the NuGet package and replace this stub in {FactoryType}.",
            nameof(WizardExternalPluginFactory));
        return Task.FromResult<KernelPlugin?>(null);
    }
}
