#nullable enable
using GodotGenerator.Application.Orchestration;
using Xunit;

namespace GodotGenerator.Application.Tests;

/// <summary>
/// Unit tests for <see cref="ModalityTurnComposer"/>.
/// </summary>
public sealed class ModalityTurnComposerTests
{
    /// <summary>
    /// Verifies preferred language is appended to the system prompt when present in options.
    /// </summary>
    [Fact]
    public void Compose_includes_language_instruction_when_option_set()
    {
        var sut = new ModalityTurnComposer();
        var options = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [ModalityTurnComposer.PreferredLanguageOptionKey] = "gdscript",
        };

        var turn = sut.Compose("code", "make a node", null, null, null, options);

        Assert.Contains("gdscript", turn.SystemPrompt, StringComparison.Ordinal);
        Assert.Contains("Godot", turn.SystemPrompt, StringComparison.Ordinal);
        Assert.Equal("code", turn.Modality);
    }

    /// <summary>
    /// Verifies project name is prefixed on the user prompt.
    /// </summary>
    /// <summary>
    /// Verifies preferred script language hint is merged into the system prompt.
    /// </summary>
    [Fact]
    public void Compose_includes_script_language_when_option_set()
    {
        var sut = new ModalityTurnComposer();
        var options = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [ModalityTurnComposer.PreferredScriptLanguageOptionKey] = "GDScript",
        };

        var turn = sut.Compose("code", "add a player", null, null, null, options);

        Assert.Contains("GDScript", turn.SystemPrompt, StringComparison.Ordinal);
        Assert.Contains("prefer", turn.SystemPrompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Compose_prefixes_prompt_with_project_name()
    {
        var sut = new ModalityTurnComposer();
        var turn = sut.Compose("text", "hello", null, "MyGame", null, null);

        Assert.Contains("MyGame", turn.Prompt, StringComparison.Ordinal);
        Assert.StartsWith("[Project:", turn.Prompt, StringComparison.Ordinal);
    }
}
