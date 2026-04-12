#nullable enable
using GodotGenerator.Application.Orchestration;
using GodotMcp.Plugin;
using GodotMcp.Plugin.Extensions;
using GodotGenerator.Infrastructure.Ai.Options;
using GodotGenerator.Infrastructure.Ai.Services;
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
    IOptions<OrchestrationOptions> orchestrationOptions,
    Services.IProviderSecretResolver providerSecretResolver,
    ILoggerFactory loggerFactory,
    ILogger<GodotKernelFactory> logger) : IKernelFactory
{
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private readonly Dictionary<string, Kernel> _kernelsByCacheKey = new(StringComparer.Ordinal);
    private bool _pluginInitialized;
    private bool _pluginInitializationSkipped;

    /// <inheritdoc />
    public async Task<Kernel> GetOrCreateKernelAsync(
        string? provider = null,
        string? preferredModelId = null,
        string? modalityKeyForToolFiltering = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveProvider = ResolveProvider(provider);
        var modelId = ResolveModelId(preferredModelId);
        var applyFiltering = orchestrationOptions.Value.EnableModalityToolFiltering
            && ModalityMcpToolPolicy.ShouldApplyFiltering(modalityKeyForToolFiltering);
        var filterSegment = applyFiltering
            ? EffectiveSelectionPolicy.NormalizeModality(modalityKeyForToolFiltering!.Trim())
            : "full";
        var cacheKey = BuildCacheKey(effectiveProvider, modelId, filterSegment);
        if (_kernelsByCacheKey.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        await _initLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_kernelsByCacheKey.TryGetValue(cacheKey, out cached))
            {
                return cached;
            }

            var apiKey = await ResolveApiKeyAsync(effectiveProvider, cancellationToken).ConfigureAwait(false);
            EnsureApiKeyConfigured(apiKey, effectiveProvider);
            await EnsurePluginInitializedAsync(cancellationToken).ConfigureAwait(false);

            var kernel = BuildKernel(apiKey!, modelId, modalityKeyForToolFiltering, applyFiltering);
            _kernelsByCacheKey[cacheKey] = kernel;
            if (_pluginInitializationSkipped)
            {
                logger.LogInformation(
                    "Kernel ready without Godot MCP tools for provider {Provider}, model {ModelId}.",
                    effectiveProvider,
                    modelId);
            }
            else
            {
                logger.LogInformation(
                    "Kernel ready with Godot MCP tools for provider {Provider}, model {ModelId}.",
                    effectiveProvider,
                    modelId);
            }
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
    /// <param name="toolFilterSegment">Normalized modality segment or <c>full</c> when no tool filtering applies.</param>
    /// <returns>The effective cache key to use for the kernel.</returns>
    private static string BuildCacheKey(string provider, string modelId, string toolFilterSegment) =>
        $"{provider}::{modelId}::{toolFilterSegment}";

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
            if (IsKnownToolSchemaShapeMismatch(ex))
            {
                _pluginInitializationSkipped = true;
                _pluginInitialized = true;
                logger.LogWarning(
                    ex,
                    "Detected a known Godot MCP tool-schema mismatch (string vs array). " +
                    "Continuing without Godot MCP plugin tools for this process.");
                return;
            }

            logger.LogError(ex, "Failed during Godot MCP plugin initialization phase.");
            throw new InvalidOperationException("Failed to initialize Godot MCP plugin.", ex);
        }
    }

    /// <summary>
    /// Builds a semantic kernel for a specific model and registers Godot tools.
    /// </summary>
    /// <param name="apiKey">Provider API key.</param>
    /// <param name="modelId">Resolved model id.</param>
    /// <param name="modalityKeyForToolFiltering">Modality key when filtering is enabled.</param>
    /// <param name="applyToolFiltering">Whether to narrow Godot MCP functions for the modality.</param>
    /// <returns>Ready-to-use kernel instance.</returns>
    private Kernel BuildKernel(string apiKey, string modelId, string? modalityKeyForToolFiltering, bool applyToolFiltering)
    {
        try
        {
            var kernelBuilder = Kernel.CreateBuilder();
            kernelBuilder.Services.AddSingleton(loggerFactory);
            kernelBuilder.AddOpenAIChatCompletion(modelId, apiKey);

            var kernel = kernelBuilder.Build();
            if (_pluginInitializationSkipped)
            {
                logger.LogWarning(
                    "Godot MCP plugin tools are unavailable due to schema mismatch; kernel created without Godot tools for model {ModelId}.",
                    modelId);
            }
            else
            {
                kernel.RegisterGodotTools(rootServices);
                logger.LogInformation("Godot tools registered for model {ModelId}.", modelId);
                if (applyToolFiltering && !string.IsNullOrWhiteSpace(modalityKeyForToolFiltering))
                {
                    ModalityGodotToolFilter.Apply(kernel, modalityKeyForToolFiltering, logger);
                }
            }
            return kernel;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed during kernel build or Godot tool registration for model {ModelId}.", modelId);
            throw new InvalidOperationException("Failed to build Semantic Kernel with Godot tools.", ex);
        }
    }

    private static bool IsKnownToolSchemaShapeMismatch(Exception ex)
    {
        var message = ex.Message;
        var stack = ex.StackTrace;
        return message.Contains("requires an element of type 'String'", StringComparison.OrdinalIgnoreCase)
            && message.Contains("target element has type 'Array'", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(stack)
            && stack.Contains("GodotMcpToolDefinitionMapper.ParseInputSchema", StringComparison.Ordinal);
    }
}
