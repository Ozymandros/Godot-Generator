#nullable enable
using System.Text.Json;
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
    /// Verifies project name is copied into options so downstream orchestration can inject Godot tool parameters.
    /// </summary>
    [Fact]
    public void Compose_merges_project_name_into_options()
    {
        var sut = new ModalityTurnComposer();
        var turn = sut.Compose("code", "ping", null, "MyGame", null, null);

        Assert.NotNull(turn.Options);
        Assert.True(turn.Options!.TryGetValue(ModalityTurnComposer.ProjectNameOptionKey, out var v));
        Assert.Equal("MyGame", v?.ToString());
    }

    /// <summary>
    /// Verifies Godot project path and name appear in the system prompt as tool hints.
    /// </summary>
    [Fact]
    public void Compose_includes_godot_tool_hints_when_path_and_name_in_options()
    {
        var sut = new ModalityTurnComposer();
        var options = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [ModalityTurnComposer.GodotProjectPathOptionKey] = @"C:\demo\game",
            [ModalityTurnComposer.ProjectNameOptionKey] = "Demo",
        };

        var turn = sut.Compose("godot-nodes", "add node", null, null, null, options);

        Assert.Contains(@"C:\demo\game", turn.SystemPrompt, StringComparison.Ordinal);
        Assert.Contains("Demo", turn.SystemPrompt, StringComparison.Ordinal);
        Assert.Contains("Godot project root path", turn.SystemPrompt, StringComparison.Ordinal);
    }

    [Fact]
    public void Compose_includes_godot_file_name_hint_when_option_set()
    {
        var sut = new ModalityTurnComposer();
        var options = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [ModalityTurnComposer.GodotProjectPathOptionKey] = @"C:\demo\game",
            [ModalityTurnComposer.GodotTargetFileNameOptionKey] = "scenes/Main.tscn",
        };

        var turn = sut.Compose("godot-nodes", "add node", null, null, null, options);

        Assert.Contains("scenes/Main.tscn", turn.SystemPrompt, StringComparison.Ordinal);
        Assert.Contains("fileName", turn.SystemPrompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Compose_includes_godot_tool_hints_when_options_are_json_elements()
    {
        var sut = new ModalityTurnComposer();
        var pathElement = JsonDocument.Parse("\"C:\\\\demo\\\\ipc\"").RootElement;
        var nameElement = JsonDocument.Parse("\"IpcGame\"").RootElement;
        var options = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [ModalityTurnComposer.GodotProjectPathOptionKey] = pathElement,
            [ModalityTurnComposer.ProjectNameOptionKey] = nameElement,
        };

        var turn = sut.Compose("godot-nodes", "add node", null, null, null, options);

        Assert.Contains(@"C:\demo\ipc", turn.SystemPrompt, StringComparison.Ordinal);
        Assert.Contains("IpcGame", turn.SystemPrompt, StringComparison.Ordinal);
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
