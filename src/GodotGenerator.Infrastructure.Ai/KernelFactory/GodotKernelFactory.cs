#nullable enable
using GodotGenerator.Application.Orchestration;
using GodotGenerator.Application;
using GodotGenerator.Application.Abstractions;
using GodotGenerator.Application.Configuration;
using GodotGenerator.Application.Services;
using GodotMcp.Plugin;
using GodotMcp.Plugin.Extensions;
using GodotGenerator.Infrastructure.Ai.Options;
using System.Text.Json;
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
    IPreferenceRepository preferences,
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

            var endpoint = ResolveProviderEndpoint(effectiveProvider);
            var kernel = BuildKernel(
                effectiveProvider,
                apiKey!,
                modelId,
                endpoint,
                modalityKeyForToolFiltering,
                applyFiltering);
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

    private Uri? ResolveProviderEndpoint(string provider)
    {
        var providerConfigEndpoint = ResolveProviderConfigEndpoint(provider);
        if (providerConfigEndpoint is not null)
        {
            return providerConfigEndpoint;
        }

        var json = preferences
            .GetAsync(PreferenceKeys.ProvidersRegistryV1, CancellationToken.None)
            .ConfigureAwait(false)
            .GetAwaiter()
            .GetResult();

        var doc = ConfigurationRegistryService.ParseProviderRegistry(json);
        foreach (var entry in doc.Providers)
        {
            if (!string.Equals(entry.Id, provider, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!entry.OpenAiCompatibility)
            {
                throw new InvalidOperationException(
                    $"Provider '{provider}' is not marked OpenAI-compatible.");
            }

            var effectiveEndpoint = string.IsNullOrWhiteSpace(entry.Endpoint)
                ? GetDefaultEndpoint(provider)
                : entry.Endpoint.Trim();
            if (string.IsNullOrWhiteSpace(effectiveEndpoint))
            {
                return null;
            }

            if (Uri.TryCreate(effectiveEndpoint, UriKind.Absolute, out var endpoint))
            {
                return endpoint;
            }

            throw new InvalidOperationException(
                $"Provider '{provider}' endpoint '{effectiveEndpoint}' is not a valid absolute URI.");
        }

        var fallbackEndpoint = GetDefaultEndpoint(provider);
        if (string.IsNullOrWhiteSpace(fallbackEndpoint))
        {
            return null;
        }

        if (Uri.TryCreate(fallbackEndpoint, UriKind.Absolute, out var fallbackUri))
        {
            return fallbackUri;
        }

        throw new InvalidOperationException(
            $"Provider '{provider}' default endpoint '{fallbackEndpoint}' is not a valid absolute URI.");
    }

    private Uri? ResolveProviderConfigEndpoint(string provider)
    {
        var key = $"providers/{provider}";
        var json = preferences
            .GetAsync(key, CancellationToken.None)
            .ConfigureAwait(false)
            .GetAwaiter()
            .GetResult();
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("endpoint", out var endpointElement))
            {
                return null;
            }

            var endpoint = endpointElement.GetString();
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                return null;
            }

            if (Uri.TryCreate(endpoint.Trim(), UriKind.Absolute, out var uri))
            {
                return uri;
            }

            throw new InvalidOperationException(
                $"Provider '{provider}' endpoint '{endpoint}' from key '{key}' is not a valid absolute URI.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Provider settings for '{provider}' at key '{key}' are not valid JSON.", ex);
        }
    }

    private static string? GetDefaultEndpoint(string provider)
    {
        return provider.ToLowerInvariant() switch
        {
            "anthropic" => "https://api.anthropic.com/v1",
            "google" => "https://generativelanguage.googleapis.com",
            "vertex_ai" => "https://aiplatform.googleapis.com",
            "deepseek" => "https://api.deepseek.com/v1",
            "openrouter" => "https://openrouter.ai/api/v1",
            "huggingface" => "https://api-inference.huggingface.co",
            "ollama" => "http://localhost:11434/v1",
            "groq" => "https://api.groq.com/openai/v1",
            "qwen" => "https://dashscope.aliyuncs.com/compatible-mode/v1",
            "stability" => "https://api.stability.ai",
            "flux" => "https://api.bfl.ai/v1",
            "elevenlabs" => "https://api.elevenlabs.io",
            "playht" => "https://api.play.ht/api/v2",
            _ => null,
        };
    }

    /// <summary>
    /// Builds a semantic kernel for a specific model and registers Godot tools.
    /// </summary>
    /// <param name="provider">Resolved provider id.</param>
    /// <param name="apiKey">Provider API key.</param>
    /// <param name="modelId">Resolved model id.</param>
    /// <param name="endpoint">Optional OpenAI-compatible endpoint override.</param>
    /// <param name="modalityKeyForToolFiltering">Modality key when filtering is enabled.</param>
    /// <param name="applyToolFiltering">Whether to narrow Godot MCP functions for the modality.</param>
    /// <returns>Ready-to-use kernel instance.</returns>
    private Kernel BuildKernel(
        string provider,
        string apiKey,
        string modelId,
        Uri? endpoint,
        string? modalityKeyForToolFiltering,
        bool applyToolFiltering)
    {
        try
        {
            var kernelBuilder = Kernel.CreateBuilder();
            kernelBuilder.Services.AddSingleton(loggerFactory);
            if (endpoint is null)
            {
                kernelBuilder.AddOpenAIChatCompletion(modelId, apiKey);
            }
            else
            {
                kernelBuilder.AddOpenAIChatCompletion(modelId, endpoint, apiKey);
            }

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
            logger.LogError(
                ex,
                "Failed during kernel build or Godot tool registration for provider {Provider}, model {ModelId}.",
                provider,
                modelId);
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
