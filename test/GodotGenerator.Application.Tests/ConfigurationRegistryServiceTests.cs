#nullable enable
using GodotGenerator.Application;
using GodotGenerator.Application.Configuration;
using GodotGenerator.Application.Services;
using Xunit;

namespace GodotGenerator.Application.Tests;

public sealed class ConfigurationRegistryServiceTests
{
    [Fact]
    public void Serialize_roundtrips_provider_registry()
    {
        var doc = new ProviderRegistryDocument
        {
            Version = 1,
            Providers =
            [
                new ProviderRegistryEntry
                {
                    Id = "openai",
                    KeyStoreHandle = "openai",
                    Modalities = ["llm"],
                },
            ],
        };

        var json = ConfigurationRegistryService.Serialize(doc);
        var back = ConfigurationRegistryService.ParseProviderRegistry(json);
        Assert.Single(back.Providers);
        Assert.Equal("openai", back.Providers[0].Id);
        Assert.Null(ConfigurationRegistryService.ValidateProviderRegistry(back));
    }

    [Fact]
    public void ValidateProviderRegistry_rejects_duplicate_ids()
    {
        var doc = new ProviderRegistryDocument
        {
            Providers =
            [
                new ProviderRegistryEntry { Id = "x", KeyStoreHandle = "x" },
                new ProviderRegistryEntry { Id = "x", KeyStoreHandle = "y" },
            ],
        };

        Assert.NotNull(ConfigurationRegistryService.ValidateProviderRegistry(doc));
    }

    [Fact]
    public void MergeLegacyPrompts_fills_empty_slots()
    {
        var doc = new SystemPromptsDocument();
        var prefs = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            [PreferenceKeys.PromptsTextLegacy] = "legacy-text",
        };

        ConfigurationRegistryService.MergeLegacyPrompts(prefs, doc);
        Assert.Equal("legacy-text", doc.Prompts["text"]);
    }

    [Fact]
    public void MergeLegacyPrompts_maps_godot_lighting_legacy_key()
    {
        var doc = new SystemPromptsDocument();
        var prefs = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            [PreferenceKeys.PromptsGodotLighting] = "legacy-lighting-overlay",
        };

        ConfigurationRegistryService.MergeLegacyPrompts(prefs, doc);
        Assert.Equal("legacy-lighting-overlay", doc.Prompts["godot-lighting"]);
    }
}
