#nullable enable

using System.Text.Json;
using GodotGenerator.Application;
using GodotGenerator.Application.Abstractions;
using GodotGenerator.Application.Services;
using GodotGenerator.Infrastructure.Ai.Options;
using Microsoft.Extensions.Options;

namespace GodotGenerator.Infrastructure.Ai.Services;

/// <summary>
/// Centralized resolver for provider model, API key, and endpoint settings.
/// Reused by both core kernel factory and wizard orchestration.
/// </summary>
public sealed class ProviderConnectionResolver(
    IPreferenceRepository preferences,
    IProviderSecretResolver providerSecretResolver,
    IOptions<LlmOptions> llmOptions) : IProviderConnectionResolver
{
    /// <inheritdoc />
    public async Task<ProviderConnectionSettings> ResolveAsync(
        string? provider,
        string? preferredModelId,
        CancellationToken cancellationToken = default)
    {
        var preferredProvider = await preferences
            .GetAsync(PreferenceKeys.PreferredLlmProvider, cancellationToken)
            .ConfigureAwait(false);
        var preferredModel = await preferences
            .GetAsync(PreferenceKeys.PreferredLlmModel, cancellationToken)
            .ConfigureAwait(false);

        var effectiveProvider = !string.IsNullOrWhiteSpace(provider)
            ? provider.Trim().ToLowerInvariant()
            : !string.IsNullOrWhiteSpace(preferredProvider)
                ? preferredProvider.Trim().ToLowerInvariant()
                : string.Empty;
        if (string.IsNullOrWhiteSpace(effectiveProvider))
        {
            throw new InvalidOperationException(
                "No default LLM provider is configured. Set one in Settings > Configuration.");
        }
        var effectiveModelId = !string.IsNullOrWhiteSpace(preferredModelId)
            ? preferredModelId.Trim()
            : !string.IsNullOrWhiteSpace(preferredModel)
                ? preferredModel.Trim()
                : llmOptions.Value.ChatModelId;

        var apiKey = await ResolveApiKeyAsync(effectiveProvider, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                $"API key for provider '{effectiveProvider}' is not configured. Set it in Settings > Secrets.");
        }

        var endpoint = ResolveProviderEndpoint(effectiveProvider);
        return new ProviderConnectionSettings(effectiveProvider, effectiveModelId, apiKey, endpoint);
    }

    /// <summary>
    /// Resolves an API key for the selected provider, including legacy OpenAI fallback.
    /// </summary>
    /// <param name="provider">Normalized provider id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Trimmed API key when available; otherwise null.</returns>
    private async Task<string?> ResolveApiKeyAsync(string provider, CancellationToken cancellationToken)
    {
        var key = await providerSecretResolver.ResolveApiKeyAsync(provider, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(key))
        {
            return key.Trim();
        }

        if (string.Equals(provider, "openai", StringComparison.OrdinalIgnoreCase))
        {
            var fallback = llmOptions.Value.ApiKey;
            return string.IsNullOrWhiteSpace(fallback) ? null : fallback.Trim();
        }

        return null;
    }

    /// <summary>
    /// Resolves the effective endpoint for a provider using override, registry, and defaults.
    /// </summary>
    /// <param name="provider">Normalized provider id.</param>
    /// <returns>Resolved endpoint URI or null when endpoint is not required.</returns>
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

            return ParseEndpoint(provider, effectiveEndpoint);
        }

        return ParseEndpoint(provider, GetDefaultEndpoint(provider));
    }

    /// <summary>
    /// Resolves the provider-specific endpoint override from <c>providers/{provider}</c> settings.
    /// </summary>
    /// <param name="provider">Normalized provider id.</param>
    /// <returns>Configured endpoint URI or null when no override exists.</returns>
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

            return ParseEndpoint(provider, endpoint.Trim());
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Provider settings for '{provider}' at key '{key}' are not valid JSON.",
                ex);
        }
    }

    /// <summary>
    /// Parses and validates an endpoint URI candidate.
    /// </summary>
    /// <param name="provider">Provider id used for diagnostics.</param>
    /// <param name="endpoint">Endpoint string to parse.</param>
    /// <returns>Absolute URI when valid; otherwise null for empty values.</returns>
    private static Uri? ParseEndpoint(string provider, string? endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return null;
        }

        if (Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
        {
            return uri;
        }

        throw new InvalidOperationException(
            $"Provider '{provider}' endpoint '{endpoint}' is not a valid absolute URI.");
    }

    /// <summary>
    /// Returns built-in OpenAI-compatible endpoint defaults for known providers.
    /// </summary>
    /// <param name="provider">Normalized provider id.</param>
    /// <returns>Default endpoint URL string when known; otherwise null.</returns>
    private static string? GetDefaultEndpoint(string provider) =>
        provider.ToLowerInvariant() switch
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
