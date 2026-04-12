#nullable enable
using System.Collections.Generic;
using GodotGenerator.Application.Abstractions;
using GodotGenerator.Infrastructure.Ai.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GodotGenerator.Infrastructure.Ai.Tests;

public sealed class PreferenceProviderSecretResolverTests
{
    [Fact]
    public async Task ResolveApiKeyAsync_returns_trimmed_key_when_present()
    {
        var repo = new InMemoryPreferenceRepository();
        await repo.SetAsync("api.keys.openai", "  sk-123  ");
        var sut = new PreferenceProviderSecretResolver(repo, NullLogger<PreferenceProviderSecretResolver>.Instance);

        var key = await sut.ResolveApiKeyAsync("openai");

        Assert.Equal("sk-123", key);
    }

    [Fact]
    public async Task ResolveApiKeyAsync_returns_null_when_missing()
    {
        var repo = new InMemoryPreferenceRepository();
        var sut = new PreferenceProviderSecretResolver(repo, NullLogger<PreferenceProviderSecretResolver>.Instance);

        var key = await sut.ResolveApiKeyAsync("missing");

        Assert.Null(key);
    }

    private sealed class InMemoryPreferenceRepository : IPreferenceRepository
    {
        private readonly Dictionary<string, string?> _values = new(StringComparer.Ordinal);

        public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
        {
            _values.TryGetValue(key, out var value);
            return Task.FromResult(value);
        }

        public Task SetAsync(string key, string? value, CancellationToken cancellationToken = default)
        {
            if (value is null)
            {
                _values.Remove(key);
            }
            else
            {
                _values[key] = value;
            }

            return Task.CompletedTask;
        }
    }
}

