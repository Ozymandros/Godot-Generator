namespace GodotGenerator.Blazor.Client.Models;

/// <summary>
/// Keys for <see cref="GodotGenerator.Api.Dtos.GenerateRequest.Options"/>; aligned with
/// <c>GodotGenerator.Application.Orchestration.ModalityTurnComposer</c> so the WASM client stays in sync with the API.
/// </summary>
public static class GenerationOptionKeys
{
    /// <summary>User-facing language hint merged into the system prompt.</summary>
    public const string PreferredLanguage = "preferred_language";

    /// <summary>Code generation preference: GDScript or C# (merged into system prompt).</summary>
    public const string PreferredScriptLanguage = "preferred_script_language";

    /// <summary>Sampling temperature for the LLM (when supported by execution settings).</summary>
    public const string Temperature = "temperature";

    /// <summary>Optional Godot project root; when set, the orchestrator validates <c>project.godot</c> before the LLM runs.</summary>
    public const string GodotProjectPath = "godot_project_path";
}
