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
    Services.IProviderSecretResolver providerSecretResolver,
    ILoggerFactory loggerFactory,
    ILogger<GodotKernelFactory> logger) : IKernelFactory
{
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private readonly Dictionary<string, Kernel> _kernelsByProviderAndModel = new(StringComparer.Ordinal);
    private bool _pluginInitialized;

    /// <inheritdoc />
    public async Task<Kernel> GetOrCreateKernelAsync(
        string? provider = null,
        string? preferredModelId = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveProvider = ResolveProvider(provider);
        var modelId = ResolveModelId(preferredModelId);
        var cacheKey = BuildCacheKey(effectiveProvider, modelId);
        if (_kernelsByProviderAndModel.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        await _initLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_kernelsByProviderAndModel.TryGetValue(cacheKey, out cached))
            {
                return cached;
            }

            var apiKey = await ResolveApiKeyAsync(effectiveProvider, cancellationToken).ConfigureAwait(false);
            EnsureApiKeyConfigured(apiKey, effectiveProvider);
            await EnsurePluginInitializedAsync(cancellationToken).ConfigureAwait(false);

            var kernel = BuildKernel(apiKey!, modelId);
            _kernelsByProviderAndModel[cacheKey] = kernel;
            logger.LogInformation(
                "Kernel ready with Godot MCP tools for provider {Provider}, model {ModelId}.",
                effectiveProvider,
                modelId);
            return kernel;
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <summary>
    /// Resolves the provider for the kernel creation, applying optional request-level overrides.
    /// </summary>
    /// <param name="provider">Optional provider from the caller.</param>
    /// <returns>The effective provider to use for the kernel.</returns>
    private string ResolveProvider(string? provider) =>
        string.IsNullOrWhiteSpace(provider) ? "openai" : provider.Trim().ToLowerInvariant();

    /// <summary>
    /// Builds a cache key for the kernel creation, applying optional request-level overrides.
    /// </summary>
    /// <param name="provider">Provider identifier used for the cache key.</param>
    /// <param name="modelId">Model id used for the cache key.</param>
    /// <returns>The effective cache key to use for the kernel.</returns>
    private static string BuildCacheKey(string provider, string modelId) => $"{provider}::{modelId}";

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
    /// <param name="provider">Provider identifier used for the error message.</param>
    private static void EnsureApiKeyConfigured(string? apiKey, string provider)
    {
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            return;
        }

        throw new InvalidOperationException(
            $"API key for provider '{provider}' is not configured. Set it in Settings > Secrets.");
    }

    private async Task<string?> ResolveApiKeyAsync(string provider, CancellationToken cancellationToken)
    {
        var key = await providerSecretResolver.ResolveApiKeyAsync(provider, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(key))
        {
            return key;
        }

        // Backward-compatible fallback for existing local config.
        if (string.Equals(provider, "openai", StringComparison.OrdinalIgnoreCase))
        {
            var configured = llmOptions.Value.ApiKey;
            return string.IsNullOrWhiteSpace(configured) ? null : configured.Trim();
        }

        return null;
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
