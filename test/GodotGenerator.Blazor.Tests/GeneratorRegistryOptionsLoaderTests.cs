#nullable enable
using System.Text.Json;
using GodotGenerator.Blazor.Client.Models;
using GodotGenerator.Blazor.Client.Services;
using Xunit;

namespace GodotGenerator.Blazor.Tests;

public sealed class GeneratorRegistryOptionsLoaderTests
{
    private static Dictionary<string, object?> SampleEnvelope()
    {
        var providers = JsonSerializer.SerializeToElement(new object[]
        {
            new { id = "openai", modalities = new[] { "llm", "text" } },
        });
        var models = JsonSerializer.SerializeToElement(new Dictionary<string, object[]>
        {
            ["openai"] =
            [
                new { providerId = "openai", engineValue = "gpt-4", modality = "llm" },
            ],
        });
        return new Dictionary<string, object?>
        {
            ["providers"] = providers,
            ["models"] = models,
            ["preferences"] = JsonSerializer.SerializeToElement(new Dictionary<string, string>
            {
                ["preferred_llm_provider"] = "openai",
            }),
        };
    }

    [Fact]
    public void Fill_filters_providers_and_models_by_modality_tags()
    {
        var data = SampleEnvelope();
        var providers = new List<string>();
        var map = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        GeneratorRegistryOptionsLoader.Fill(data, providers, map, GenerationModality.Text);

        Assert.Contains("openai", providers);
        Assert.True(map.TryGetValue("openai", out var models));
        Assert.Contains("gpt-4", models!);
    }

    [Fact]
    public void TryGetCanonicalProviderId_matches_case_insensitively()
    {
        var data = SampleEnvelope();
        var ok = GeneratorRegistryOptionsLoader.TryGetCanonicalProviderId(data, "OPENAI", out var id);
        Assert.True(ok);
        Assert.Equal("openai", id);
    }

    [Fact]
    public void ParsePreferences_reads_string_values()
    {
        var data = SampleEnvelope();
        var prefs = GeneratorRegistryOptionsLoader.ParsePreferences(data);
        Assert.Equal("openai", prefs["preferred_llm_provider"]);
    }

    [Fact]
    public void EnsureSavedModelEngineListedForProvider_adds_missing_filtered_model()
    {
        var data = SampleEnvelope();
        var map = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["openai"] = new List<string> { "gpt-3" },
        };

        GeneratorRegistryOptionsLoader.EnsureSavedModelEngineListedForProvider(data, "openai", "gpt-4", map);

        Assert.Contains("gpt-4", map["openai"]);
    }
}
