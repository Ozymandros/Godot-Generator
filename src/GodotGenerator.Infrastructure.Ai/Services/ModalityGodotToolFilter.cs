#nullable enable
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace GodotGenerator.Infrastructure.Ai.Services;

/// <summary>
/// Narrows Godot MCP <see cref="KernelPlugin"/> registrations to modality-relevant SK functions.
/// </summary>
public static class ModalityGodotToolFilter
{
    /// <summary>
    /// Rebuilds plugins so only functions allowed by <see cref="ModalityMcpToolPolicy"/> remain.
    /// </summary>
    public static void Apply(Kernel kernel, string modalityKey, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(kernel);
        if (string.IsNullOrWhiteSpace(modalityKey))
        {
            return;
        }

        if (!ModalityMcpToolPolicy.ShouldApplyFiltering(modalityKey))
        {
            return;
        }

        var snapshot = kernel.Plugins.ToArray();
        foreach (var plugin in snapshot)
        {
            var kept = new List<KernelFunction>();
            foreach (var fn in plugin)
            {
                var name = fn.Metadata.Name;
                if (ModalityMcpToolPolicy.IsFunctionAllowed(modalityKey, plugin.Name, name))
                {
                    kept.Add(fn);
                }
            }

            var beforeCount = plugin.Count();
            if (kept.Count == beforeCount)
            {
                continue;
            }

            if (kept.Count == 0)
            {
                logger.LogWarning(
                    "Modality tool filter removed all functions from plugin {Plugin} for modality {Modality}; keeping plugin unchanged.",
                    plugin.Name,
                    modalityKey);
                continue;
            }

            kernel.Plugins.Remove(plugin);
            var rebuilt = KernelPluginFactory.CreateFromFunctions(plugin.Name, kept);
            kernel.Plugins.Add(rebuilt);
            logger.LogDebug(
                "Modality {Modality}: filtered plugin {Plugin} from {Before} to {After} functions.",
                modalityKey,
                plugin.Name,
                beforeCount,
                kept.Count);
        }
    }
}
