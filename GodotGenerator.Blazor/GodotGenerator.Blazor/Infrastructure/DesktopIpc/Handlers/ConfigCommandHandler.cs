using System.Text.Json;
using GodotGenerator.Api.Abstractions;
using GodotGenerator.Desktop.Contracts.Commands;
using GodotGenerator.Desktop.Contracts.Envelope;
using GodotGenerator.Desktop.Contracts.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GodotGenerator.Blazor.Infrastructure.DesktopIpc.Handlers;

/// <summary>
/// Handles the <see cref="ConfigCommandNames.GetAll"/> IPC command by delegating to
/// <see cref="IGodotGeneratorApiService.GetAllConfigAsync"/> and packaging the result
/// into a <see cref="ResponseEnvelope"/>.
/// </summary>
internal sealed class ConfigCommandHandler : ICommandHandler
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ConfigCommandHandler> _logger;

    /// <summary>
    /// Initialises the handler with the API service and logger.
    /// </summary>
    public ConfigCommandHandler(IServiceScopeFactory scopeFactory, ILogger<ConfigCommandHandler> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> CommandNames { get; } = [ConfigCommandNames.GetAll];

    /// <inheritdoc/>
    public async Task<ResponseEnvelope> HandleAsync(CommandEnvelope envelope, CancellationToken ct)
    {
        _logger.LogDebug("Handling IPC command '{Command}' (correlation: {Id}).",
            envelope.Command, envelope.CorrelationId);

        using var scope = _scopeFactory.CreateScope();
        var apiService = scope.ServiceProvider.GetRequiredService<IGodotGeneratorApiService>();
        var result = await apiService.GetAllConfigAsync(ct).ConfigureAwait(false);

        if (!result.Success)
        {
            return new ResponseEnvelope(
                envelope.CorrelationId, false, null,
                DesktopErrorCode.HandlerFaulted, result.Error);
        }

        var payloadJson = JsonSerializer.Serialize(result.Data, ContractJsonOptions.Default);
        return new ResponseEnvelope(envelope.CorrelationId, true, payloadJson, null, null);
    }
}
