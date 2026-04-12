#nullable enable
using GodotGenerator.Application;
using GodotGenerator.Application.Orchestration;
using Xunit;

namespace GodotGenerator.Application.Tests;

public sealed class EffectiveSelectionPolicyTests
{
    [Theory]
    [InlineData("text", "preferred_llm_provider", "preferred_llm_model")]
    [InlineData("code", "preferred_llm_provider", "preferred_llm_model")]
    [InlineData("image", "preferred_image_provider", "preferred_image_model")]
    [InlineData("sprites", "preferred_image_provider", "preferred_image_model")]
    [InlineData("audio", "preferred_audio_provider", "preferred_audio_model")]
    [InlineData("video", "preferred_video_provider", "preferred_video_model")]
    [InlineData("godot-ui", "preferred_llm_provider", "preferred_llm_model")]
    public void GetPreferenceKeys_maps_modality_to_expected_keys(string modality, string expectedProviderKey, string expectedModelKey)
    {
        var (p, m) = EffectiveSelectionPolicy.GetPreferenceKeys(modality);
        Assert.Equal(expectedProviderKey, p);
        Assert.Equal(expectedModelKey, m);
    }

    [Theory]
    [InlineData("TEXT", "text")]
    [InlineData("godotui", "godot-ui")]
    [InlineData("godotphysics", "godot-physics")]
    public void NormalizeModality_normalizes_casing_and_aliases(string input, string expected)
    {
        Assert.Equal(expected, EffectiveSelectionPolicy.NormalizeModality(input));
    }

    [Fact]
    public void Resolve_prefers_request_then_preferences_then_host_defaults()
    {
        var prefs = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            [PreferenceKeys.PreferredLlmProvider] = "from-pref",
            [PreferenceKeys.PreferredLlmModel] = "from-model",
        };

        var a = EffectiveSelectionPolicy.Resolve(
            "text",
            requestProvider: "req-p",
            requestModelId: "req-m",
            panelLanguageOverride: null,
            globalPreferredLanguage: null,
            preferences: prefs,
            hostDefaultProvider: "host-p",
            hostDefaultModelId: "host-m");

        Assert.Equal("req-p", a.Provider);
        Assert.Equal("req-m", a.ModelId);

        var b = EffectiveSelectionPolicy.Resolve(
            "text",
            requestProvider: null,
            requestModelId: null,
            panelLanguageOverride: null,
            globalPreferredLanguage: "glang",
            preferences: prefs,
            hostDefaultProvider: "host-p",
            hostDefaultModelId: "host-m");

        Assert.Equal("from-pref", b.Provider);
        Assert.Equal("from-model", b.ModelId);

        var c = EffectiveSelectionPolicy.Resolve(
            "text",
            requestProvider: null,
            requestModelId: null,
            panelLanguageOverride: "panel",
            globalPreferredLanguage: "glang",
            preferences: new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase),
            hostDefaultProvider: "host-p",
            hostDefaultModelId: "host-m");

        Assert.Equal("host-p", c.Provider);
        Assert.Equal("host-m", c.ModelId);
        Assert.Equal("panel", c.PreferredLanguage);
    }
}
