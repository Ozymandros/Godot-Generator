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

    /// <summary>
    /// Every new Godot-specific modality must have a distinct, non-empty system instruction.
    /// </summary>
    [Theory]
    [InlineData("godot-lighting", "lighting")]
    [InlineData("godot-camera", "camera")]
    [InlineData("godot-shaders", "shader")]
    [InlineData("godot-signals", "signal")]
    [InlineData("godot-nodes", "node")]
    [InlineData("wizard", "orchestrat")]
    public void GetSystemInstruction_returns_non_empty_for_new_modalities(string key, string keyword)
    {
        var sut = new ModalityTurnComposer();
        var instruction = sut.GetSystemInstruction(key);

        Assert.False(string.IsNullOrWhiteSpace(instruction));
        Assert.Contains(keyword, instruction, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// New modalities produce distinct system instructions — none fall through to the default.
    /// </summary>
    [Fact]
    public void GetSystemInstruction_all_new_modalities_are_distinct_from_default()
    {
        var sut = new ModalityTurnComposer();
        var defaultInstruction = sut.GetSystemInstruction("__unknown__");
        string[] newModalities = ["godot-lighting", "godot-camera", "godot-shaders", "godot-signals", "godot-nodes", "wizard"];

        foreach (var key in newModalities)
        {
            var instruction = sut.GetSystemInstruction(key);
            Assert.NotEqual(defaultInstruction, instruction);
        }
    }

    /// <summary>
    /// GetSystemInstruction is exposed through the IModalityTurnComposer interface.
    /// </summary>
    [Fact]
    public void IModalityTurnComposer_exposes_GetSystemInstruction()
    {
        IModalityTurnComposer sut = new ModalityTurnComposer();
        var instruction = sut.GetSystemInstruction("godot-physics");
        Assert.Contains("physics", instruction, StringComparison.OrdinalIgnoreCase);
    }
}
