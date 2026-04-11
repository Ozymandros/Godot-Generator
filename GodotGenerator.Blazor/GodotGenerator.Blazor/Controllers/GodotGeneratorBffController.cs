using GodotGenerator.Api.Abstractions;
using GodotGenerator.Api.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace GodotGenerator.Blazor.Controllers;

/// <summary>
/// HTTP BFF for the Blazor WebAssembly client; delegates to <see cref="IGodotGeneratorApiService"/>.
/// </summary>
[ApiController]
[IgnoreAntiforgeryToken]
[Route("api")]
public sealed class GodotGeneratorBffController(IGodotGeneratorApiService api) : ControllerBase
{
    [HttpPost("generate/text")]
    public Task<IActionResult> GenerateText([FromBody] GenerateRequest request, CancellationToken cancellationToken) =>
        Map(api.GenerateTextAsync(request, cancellationToken));

    [HttpPost("generate/code")]
    public Task<IActionResult> GenerateCode([FromBody] GenerateRequest request, CancellationToken cancellationToken) =>
        Map(api.GenerateCodeAsync(request, cancellationToken));

    [HttpPost("generate/image")]
    public Task<IActionResult> GenerateImage([FromBody] GenerateRequest request, CancellationToken cancellationToken) =>
        Map(api.GenerateImageAsync(request, cancellationToken));

    [HttpPost("generate/audio")]
    public Task<IActionResult> GenerateAudio([FromBody] GenerateRequest request, CancellationToken cancellationToken) =>
        Map(api.GenerateAudioAsync(request, cancellationToken));

    [HttpPost("generate/video")]
    public Task<IActionResult> GenerateVideo([FromBody] GenerateRequest request, CancellationToken cancellationToken) =>
        Map(api.GenerateVideoAsync(request, cancellationToken));

    [HttpPost("generate/sprites")]
    public Task<IActionResult> GenerateSprites([FromBody] GenerateRequest request, CancellationToken cancellationToken) =>
        Map(api.GenerateSpritesAsync(request, cancellationToken));

    [HttpPost("generate/godot-ui")]
    public Task<IActionResult> GenerateGodotUi([FromBody] GenerateRequest request, CancellationToken cancellationToken) =>
        Map(api.GenerateGodotUiAsync(request, cancellationToken));

    [HttpPost("generate/godot-physics")]
    public Task<IActionResult> GenerateGodotPhysics([FromBody] GenerateRequest request, CancellationToken cancellationToken) =>
        Map(api.GenerateGodotPhysicsAsync(request, cancellationToken));

    [HttpPost("generate/godot-project")]
    public Task<IActionResult> GenerateGodotProject([FromBody] GenerateRequest request, CancellationToken cancellationToken) =>
        Map(api.GenerateGodotProjectAsync(request, cancellationToken));

    [HttpPost("generate/scenes")]
    public Task<IActionResult> CreateScene([FromBody] GenerateRequest request, CancellationToken cancellationToken) =>
        Map(api.CreateSceneAsync(request, cancellationToken));

    [HttpPost("generate/animations")]
    public Task<IActionResult> GenerateAnimations([FromBody] GenerateRequest request, CancellationToken cancellationToken) =>
        Map(api.GenerateAnimationsAsync(request, cancellationToken));

    [HttpPost("generate/godot-lighting")]
    public Task<IActionResult> GenerateGodotLighting([FromBody] GenerateRequest request, CancellationToken cancellationToken) =>
        Map(api.GenerateGodotLightingAsync(request, cancellationToken));

    [HttpPost("generate/godot-camera")]
    public Task<IActionResult> GenerateGodotCamera([FromBody] GenerateRequest request, CancellationToken cancellationToken) =>
        Map(api.GenerateGodotCameraAsync(request, cancellationToken));

    [HttpPost("generate/godot-shaders")]
    public Task<IActionResult> GenerateGodotShaders([FromBody] GenerateRequest request, CancellationToken cancellationToken) =>
        Map(api.GenerateGodotShadersAsync(request, cancellationToken));

    [HttpPost("generate/godot-signals")]
    public Task<IActionResult> GenerateGodotSignals([FromBody] GenerateRequest request, CancellationToken cancellationToken) =>
        Map(api.GenerateGodotSignalsAsync(request, cancellationToken));

    [HttpPost("generate/godot-nodes")]
    public Task<IActionResult> GenerateGodotNodes([FromBody] GenerateRequest request, CancellationToken cancellationToken) =>
        Map(api.GenerateGodotNodesAsync(request, cancellationToken));

    [HttpGet("preference/{key}")]
    public async Task<IActionResult> GetPreference(string key, CancellationToken cancellationToken)
    {
        var result = await api.GetPreferenceAsync(key, cancellationToken).ConfigureAwait(false);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("preference")]
    public async Task<IActionResult> SetPreference([FromBody] SetPreferenceRequest request, CancellationToken cancellationToken)
    {
        var result = await api.SetPreferenceAsync(request, cancellationToken).ConfigureAwait(false);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("keys")]
    public async Task<IActionResult> GetApiKeys(CancellationToken cancellationToken)
    {
        var result = await api.GetApiKeysAsync(cancellationToken).ConfigureAwait(false);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("keys")]
    public async Task<IActionResult> SaveApiKeys([FromBody] ApiKeysRequest request, CancellationToken cancellationToken)
    {
        var result = await api.SaveApiKeysAsync(request, cancellationToken).ConfigureAwait(false);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("config")]
    public async Task<IActionResult> GetAllConfig(CancellationToken cancellationToken)
    {
        var result = await api.GetAllConfigAsync(cancellationToken).ConfigureAwait(false);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    private async Task<IActionResult> Map(Task<ApiResponse<Dictionary<string, object?>>> task)
    {
        var result = await task.ConfigureAwait(false);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
