#nullable enable
using GodotGenerator.Application.Orchestration;
using GodotGenerator.Application.Abstractions;
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
    IOptions<OrchestrationOptions> orchestrationOptions,
    IProviderConnectionResolver providerConnectionResolver,
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
        string projectRoot = "",
        CancellationToken cancellationToken = default)
    {
        var connection = await providerConnectionResolver
            .ResolveAsync(provider, preferredModelId, cancellationToken)
            .ConfigureAwait(false);
        var applyFiltering = orchestrationOptions.Value.EnableModalityToolFiltering
            && ModalityMcpToolPolicy.ShouldApplyFiltering(modalityKeyForToolFiltering);
        var filterSegment = applyFiltering
            ? EffectiveSelectionPolicy.NormalizeModality(modalityKeyForToolFiltering!.Trim())
            : "full";
        var cacheKey = BuildCacheKey(connection.Provider, connection.ModelId, filterSegment, projectRoot);

        await _initLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsurePluginInitializedAsync(cancellationToken).ConfigureAwait(false);

            if (_kernelsByCacheKey.TryGetValue(cacheKey, out var cached))
            {
                return cached;
            }

            var kernel = BuildKernel(
                connection.Provider,
                connection.ApiKey,
                connection.ModelId,
                connection.Endpoint,
                modalityKeyForToolFiltering,
                applyFiltering);
            _kernelsByCacheKey[cacheKey] = kernel;
            if (_pluginInitializationSkipped)
            {
                logger.LogInformation(
                    "Kernel ready without Godot MCP tools for provider {Provider}, model {ModelId}.",
                    connection.Provider,
                    connection.ModelId);
            }
            else
            {
                logger.LogInformation(
                    "Kernel ready with Godot MCP tools for provider {Provider}, model {ModelId}.",
                    connection.Provider,
                    connection.ModelId);
            }
            return kernel;
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <summary>
    /// Builds a cache key for the kernel creation, applying optional request-level overrides.
    /// </summary>
    /// <param name="provider">Provider identifier used for the cache key.</param>
    /// <param name="modelId">Model id used for the cache key.</param>
    /// <param name="toolFilterSegment">Normalized modality segment or <c>full</c> when no tool filtering applies.</param>
    /// <returns>The effective cache key to use for the kernel.</returns>
    private static string BuildCacheKey(string provider, string modelId, string toolFilterSegment, string? projectRoot) =>
        $"{provider}::{modelId}::{toolFilterSegment}::{NormalizeProjectRootForCache(projectRoot)}";

    private static string NormalizeProjectRootForCache(string? projectRoot) =>
        string.IsNullOrWhiteSpace(projectRoot) ? string.Empty : projectRoot.Trim().ToLowerInvariant();

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
                try
                {
                    kernel.RegisterGodotTools(rootServices);
                    logger.LogInformation("Godot tools registered for model {ModelId}.", modelId);
                }
                catch (ArgumentException ex) when (IsInvalidMcpFunctionName(ex))
                {
                    // MCP 1.5+ exposes dotted tool names (e.g. camera.create); SK requires [A-Za-z0-9_]. Recovery path is expected.
                    logger.LogDebug(ex, "RegisterGodotTools skipped for model {ModelId} (invalid SK function name from MCP).", modelId);
                    logger.LogInformation(
                        "Using typed Godot MCP skills for model {ModelId} (dynamic MCP tools use dotted names incompatible with SK identifiers).",
                        modelId);

                    kernel.AddGodotMcpSkills(rootServices);
                }

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

    /// <summary>
    /// Detects the known Godot MCP schema mismatch condition that can be safely downgraded.
    /// </summary>
    /// <param name="ex">Initialization exception to inspect.</param>
    /// <returns>
    /// True when the exception matches the known mapper mismatch signature; otherwise false.
    /// </returns>
    private static bool IsKnownToolSchemaShapeMismatch(Exception ex)
    {
        var message = ex.Message;
        var stack = ex.StackTrace;
        return message.Contains("requires an element of type 'String'", StringComparison.OrdinalIgnoreCase)
            && message.Contains("target element has type 'Array'", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(stack)
            && stack.Contains("GodotMcpToolDefinitionMapper.ParseInputSchema", StringComparison.Ordinal);
    }

    private static bool IsInvalidMcpFunctionName(ArgumentException ex) =>
        ex.Message.Contains("A function name can contain only ASCII letters, digits, and underscores", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("is not a valid name", StringComparison.OrdinalIgnoreCase);
}
