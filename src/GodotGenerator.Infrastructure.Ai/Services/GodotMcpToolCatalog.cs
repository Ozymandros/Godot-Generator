#nullable enable
using GodotGenerator.Application.Abstractions;
using GodotGenerator.Infrastructure.Ai.KernelFactory;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace GodotGenerator.Infrastructure.Ai.Services;

/// <summary>
/// Enumerates SK-registered functions after the Godot MCP plugin is attached to the kernel.
/// </summary>
public sealed class GodotMcpToolCatalog(
    IKernelFactory kernelFactory,
    ILogger<GodotMcpToolCatalog> logger) : IGodotMcpToolCatalog
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> ListRegisteredToolNamesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var kernel = await kernelFactory
                .GetOrCreateKernelAsync(null, cancellationToken)
                .ConfigureAwait(false);
            return CollectFunctionNames(kernel);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to enumerate Godot MCP kernel functions.");
            return Array.Empty<string>();
        }
    }

    private static IReadOnlyList<string> CollectFunctionNames(Kernel kernel)
    {
        var names = new List<string>();
        foreach (var plugin in kernel.Plugins)
        {
            foreach (var function in plugin)
            {
                names.Add($"{plugin.Name}::{function.Name}");
            }
        }

        return names;
    }
}
