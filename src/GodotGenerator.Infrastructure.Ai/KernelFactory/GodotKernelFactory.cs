#nullable enable
using GodotMcp.Plugin;
using GodotMcp.Plugin.Extensions;
using GodotGenerator.Infrastructure.Ai.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;

namespace GodotGenerator.Infrastructure.Ai.KernelFactory;

/// <summary>
/// Creates a <see cref="Kernel"/> with OpenAI chat completion and tools from the Godot MCP Semantic Kernel plugin (GodotMcp.SemanticKernel.Plugin).
/// </summary>
public sealed class GodotKernelFactory(
    IServiceProvider rootServices,
    IOptions<LlmOptions> llmOptions,
    ILoggerFactory loggerFactory,
    ILogger<GodotKernelFactory> logger) : IKernelFactory
{
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private readonly Dictionary<string, Kernel> _kernelsByModel = new(StringComparer.Ordinal);
    private bool _pluginInitialized;

    /// <inheritdoc />
    public async Task<Kernel> GetOrCreateKernelAsync(string? preferredModelId = null, CancellationToken cancellationToken = default)
    {
        var modelId = ResolveModelId(preferredModelId);
        if (_kernelsByModel.TryGetValue(modelId, out var cached))
        {
            return cached;
        }

        await _initLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_kernelsByModel.TryGetValue(modelId, out cached))
            {
                return cached;
            }

            var llm = llmOptions.Value;
            EnsureApiKeyConfigured(llm.ApiKey);
            await EnsurePluginInitializedAsync(cancellationToken).ConfigureAwait(false);

            var kernel = BuildKernel(llm.ApiKey, modelId);
            _kernelsByModel[modelId] = kernel;
            logger.LogInformation("Kernel ready with Godot MCP tools registered for model {ModelId}.", modelId);
            return kernel;
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <summary>
    /// Resolves the model id for kernel creation, applying optional request-level overrides.
    /// </summary>
    /// <param name="preferredModelId">Optional preferred model id from the caller.</param>
    /// <returns>The effective model id to use for the kernel.</returns>
    private string ResolveModelId(string? preferredModelId)
    {
        var configured = llmOptions.Value.ChatModelId;
        if (string.IsNullOrWhiteSpace(preferredModelId))
        {
            return configured;
        }

        logger.LogDebug("Using preferred model id override: {ModelId}", preferredModelId);
        return preferredModelId.Trim();
    }

    /// <summary>
    /// Ensures an API key exists before attempting provider initialization.
    /// </summary>
    /// <param name="apiKey">Configured API key value.</param>
    private static void EnsureApiKeyConfigured(string apiKey)
    {
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            return;
        }

        throw new InvalidOperationException(
            "Llm:ApiKey is not configured. Set user secrets or environment for development.");
    }

    /// <summary>
    /// Initializes the Godot MCP plugin once for the process lifetime.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task EnsurePluginInitializedAsync(CancellationToken cancellationToken)
    {
        if (_pluginInitialized)
        {
            return;
        }

        try
        {
            var godotPlugin = rootServices.GetRequiredService<GodotPlugin>();
            logger.LogInformation("Initializing Godot MCP plugin for Semantic Kernel...");
            await godotPlugin.InitializeAsync(cancellationToken).ConfigureAwait(false);
            _pluginInitialized = true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed during Godot MCP plugin initialization phase.");
            throw new InvalidOperationException("Failed to initialize Godot MCP plugin.", ex);
        }
    }

    /// <summary>
    /// Builds a semantic kernel for a specific model and registers Godot tools.
    /// </summary>
    /// <param name="apiKey">Provider API key.</param>
    /// <param name="modelId">Resolved model id.</param>
    /// <returns>Ready-to-use kernel instance.</returns>
    private Kernel BuildKernel(string apiKey, string modelId)
    {
        try
        {
            var kernelBuilder = Kernel.CreateBuilder();
            kernelBuilder.Services.AddSingleton(loggerFactory);
            kernelBuilder.AddOpenAIChatCompletion(modelId, apiKey);

            var kernel = kernelBuilder.Build();
            kernel.RegisterGodotTools(rootServices);
            logger.LogInformation("Godot tools registered for model {ModelId}.", modelId);
            return kernel;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed during kernel build or Godot tool registration for model {ModelId}.", modelId);
            throw new InvalidOperationException("Failed to build Semantic Kernel with Godot tools.", ex);
        }
    }
}
