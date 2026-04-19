using System.Text.Json;
using System.IO;
using GodotGenerator.Api.Abstractions;
using GodotGenerator.Api.Dtos;
using GodotGenerator.Application.Dtos;
using GodotGenerator.Desktop.Contracts.Commands;
using GodotGenerator.Desktop.Contracts.Envelope;
using GodotGenerator.Application.Serialization;
using GodotGenerator.Desktop.Contracts.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GodotGenerator.Blazor.Infrastructure.DesktopIpc.Handlers;

/// <summary>
/// Handles all <c>Generate.*</c> IPC commands by mapping the versioned command name to the
/// appropriate <see cref="IGodotGeneratorApiService"/> method and returning the result.
/// </summary>
/// <remarks>
/// Active Godot game folder and name come from the client (project header / options), not from host configuration files.
/// </remarks>
internal sealed class GenerateCommandHandler : ICommandHandler
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<GenerateCommandHandler> _logger;
    private readonly IHostEnvironment _hostEnvironment;

    /// <summary>
    /// Initialises the handler with the API service and logger.
    /// </summary>
    public GenerateCommandHandler(
        IServiceScopeFactory scopeFactory,
        ILogger<GenerateCommandHandler> logger,
        IHostEnvironment hostEnvironment)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _hostEnvironment = hostEnvironment;
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> CommandNames => GenerateCommandNames.All;

    /// <inheritdoc/>
    public async Task<ResponseEnvelope> HandleAsync(CommandEnvelope envelope, CancellationToken ct)
    {
        _logger.LogDebug("Handling IPC command '{Command}' (correlation: {Id}).",
            envelope.Command, envelope.CorrelationId);

        if (string.IsNullOrWhiteSpace(envelope.PayloadJson))
        {
            return Failure(envelope.CorrelationId, DesktopErrorCode.ValidationFailed,
                $"{envelope.Command}: a JSON payload with 'prompt' is required.");
        }

        var cmdRequest = JsonSerializer.Deserialize<GenerateCommandRequest>(
            envelope.PayloadJson, ContractJsonOptions.Default);

        if (cmdRequest is null || string.IsNullOrWhiteSpace(cmdRequest.Prompt))
        {
            return Failure(envelope.CorrelationId, DesktopErrorCode.ValidationFailed,
                $"{envelope.Command}: 'prompt' is required.");
        }

        // Map IReadOnlyDictionary → Dictionary for GenerateRequest compat
        var options = cmdRequest.Options is not null
            ? new Dictionary<string, object?>(cmdRequest.Options, StringComparer.OrdinalIgnoreCase)
            : null;

        var resolvedProjectPathRoot = ResolveProjectPathRoot(options);
        var resolvedProjectName = ResolveProjectName(cmdRequest.ProjectName, options);
        var resolvedProjectPath = ComposeMcpProjectPath(resolvedProjectPathRoot, resolvedProjectName);
        if (options is not null
            && !options.ContainsKey("godot_project_path")
            && !string.IsNullOrWhiteSpace(resolvedProjectPath))
        {
            options["godot_project_path"] = resolvedProjectPath;
        }

        var apiRequest = new GenerateRequest(
            cmdRequest.Prompt,
            cmdRequest.Provider,
            options,
            cmdRequest.ApiKey,
            cmdRequest.SystemPrompt,
            resolvedProjectName,
            cmdRequest.PreferredModelId);

        using var scope = _scopeFactory.CreateScope();
        var apiService = scope.ServiceProvider.GetRequiredService<IGodotGeneratorApiService>();

        var result = await DispatchToModalityAsync(apiService, envelope.Command, apiRequest, ct)
            .ConfigureAwait(false);

        if (!result.Success)
        {
            return Failure(envelope.CorrelationId, DesktopErrorCode.HandlerFaulted, result.Error);
        }

        var response = new GenerateCommandResponse(
            result.Data ?? new Dictionary<string, object?>());

        return new ResponseEnvelope(
            envelope.CorrelationId, true,
            JsonSerializer.Serialize(response, ContractJsonOptions.Default),
            null, null);
    }

    // ── Modality dispatch ─────────────────────────────────────────────────────

    /// <summary>
    /// Maps a versioned generate command name to the corresponding
    /// <see cref="IGodotGeneratorApiService"/> method.
    /// </summary>
    private static Task<Api.Dtos.ApiResponse<Dictionary<string, object?>>> DispatchToModalityAsync(
        IGodotGeneratorApiService apiService,
        string command,
        GenerateRequest request,
        CancellationToken ct) =>
        command switch
        {
            GenerateCommandNames.Text => apiService.GenerateTextAsync(request, ct),
            GenerateCommandNames.Code => apiService.GenerateCodeAsync(request, ct),
            GenerateCommandNames.Image => apiService.GenerateImageAsync(request, ct),
            GenerateCommandNames.Audio => apiService.GenerateAudioAsync(request, ct),
            GenerateCommandNames.Video => apiService.GenerateVideoAsync(request, ct),
            GenerateCommandNames.Sprites => apiService.GenerateSpritesAsync(request, ct),
            GenerateCommandNames.GodotUi => apiService.GenerateGodotUiAsync(request, ct),
            GenerateCommandNames.GodotPhysics => apiService.GenerateGodotPhysicsAsync(request, ct),
            GenerateCommandNames.GodotProject => apiService.GenerateGodotProjectAsync(request, ct),
            GenerateCommandNames.Scenes => apiService.CreateSceneAsync(request, ct),
            GenerateCommandNames.Animations => apiService.GenerateAnimationsAsync(request, ct),
            GenerateCommandNames.GodotLighting => apiService.GenerateGodotLightingAsync(request, ct),
            GenerateCommandNames.GodotCamera => apiService.GenerateGodotCameraAsync(request, ct),
            GenerateCommandNames.GodotShaders => apiService.GenerateGodotShadersAsync(request, ct),
            GenerateCommandNames.GodotSignals => apiService.GenerateGodotSignalsAsync(request, ct),
            GenerateCommandNames.GodotNodes => apiService.GenerateGodotNodesAsync(request, ct),
            GenerateCommandNames.Wizard     => apiService.RunWizardAsync(
                new WizardRequest(
                    Prompt: request.Prompt,
                    ProjectName: request.ProjectName,
                    GodotProjectPath: GetOptionString(request.Options, "godot_project_path"),
                    GodotTargetFileName: GetOptionString(request.Options, "godot_file_name"),
                    Provider: request.Provider,
                    PreferredModelId: request.PreferredModelId,
                    SystemPromptOverride: request.SystemPrompt),
                ct),
            _ => throw new InvalidOperationException($"No modality mapping for command '{command}'.")
        };

    /// <summary>Extracts a string value from an options dictionary; returns null if absent or not a string.</summary>
    private static string? GetOptionString(
        IReadOnlyDictionary<string, object?>? options,
        string key)
    {
        if (options is null || !options.TryGetValue(key, out var raw) || raw is null)
        {
            return null;
        }

        return JsonOptionValue.AsTrimmedString(raw);
    }

    /// <summary>Project root from the client request only (<c>godot_project_path</c>); optional relative paths resolve against content root.</summary>
    private string? ResolveProjectPathRoot(IReadOnlyDictionary<string, object?>? options)
    {
        var fromOptions = GetOptionString(options, "godot_project_path");
        if (string.IsNullOrWhiteSpace(fromOptions))
        {
            return null;
        }

        return NormalizeProjectPathRoot(fromOptions.Trim());
    }

    /// <summary>Makes a relative path from the client absolute against the host content root.</summary>
    private string? NormalizeProjectPathRoot(string path)
    {
        if (Path.IsPathRooted(path))
        {
            return path;
        }

        try
        {
            return Path.GetFullPath(Path.Combine(_hostEnvironment.ContentRootPath, path));
        }
        catch
        {
            return path;
        }
    }

    private static string? ComposeMcpProjectPath(string? projectPathRoot, string? projectName)
    {
        if (string.IsNullOrWhiteSpace(projectPathRoot))
        {
            return null;
        }

        var root = projectPathRoot.Trim();
        if (string.IsNullOrWhiteSpace(projectName))
        {
            return root;
        }

        var name = projectName.Trim();
        var rootLeaf = Path.GetFileName(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.Equals(rootLeaf, name, StringComparison.OrdinalIgnoreCase))
        {
            return root;
        }

        return Path.Combine(root, name);
    }

    private string? ResolveProjectName(
        string? explicitProjectName,
        IReadOnlyDictionary<string, object?>? options)
    {
        if (!string.IsNullOrWhiteSpace(explicitProjectName))
        {
            return explicitProjectName.Trim();
        }

        var fromOptions = GetOptionString(options, "project_name");
        return string.IsNullOrWhiteSpace(fromOptions) ? null : fromOptions.Trim();
    }

    private static ResponseEnvelope Failure(string correlationId, string errorCode, string? message) =>
        new(correlationId, false, null, errorCode, message);
}
