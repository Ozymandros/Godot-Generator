#nullable enable
using Godot_Generator_Blazor.Models;
using Xunit;

namespace Godot_Generator_Blazor.Tests;

/// <summary>
/// Unit tests for frontend model primitives.
/// </summary>
public sealed class ModelsTests
{
    /// <summary>
    /// Verifies panel model stores its modality and starts in clean state.
    /// </summary>
    [Fact]
    public void GenerationPanelModel_initializes_with_modality()
    {
        var model = new GenerationPanelModel(GenerationModality.Code);
        Assert.Equal(GenerationModality.Code, model.Modality);
        Assert.Equal(string.Empty, model.Prompt);
        Assert.False(model.IsBusy);
    }

    /// <summary>
    /// Verifies language catalog exposes canonical preference key and values.
    /// </summary>
    [Fact]
    public void LanguageCatalog_exposes_defaults()
    {
        Assert.Equal("preferred_language", LanguageCatalog.PreferredLanguagePreferenceKey);
        Assert.Contains("csharp", LanguageCatalog.Supported);
    }
}
