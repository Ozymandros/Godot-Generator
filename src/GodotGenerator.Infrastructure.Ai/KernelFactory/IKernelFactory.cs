#nullable enable
using Microsoft.SemanticKernel;

namespace GodotGenerator.Infrastructure.Ai.KernelFactory;

/// <summary>
/// Builds and caches a <see cref="Kernel"/> with chat completion and Godot MCP tools.
/// </summary>
public interface IKernelFactory
{
    /// <summary>
    /// Returns a shared kernel for a model, initializing the Godot MCP plugin on first use.
    /// </summary>
    /// <param name="provider">Optional provider/service key used for credential resolution.</param>
    /// <param name="preferredModelId">Optional model id override for this kernel.</param>
    /// <param name="modalityKeyForToolFiltering">
    /// Optional generation modality (e.g. <c>godot-lighting</c>). When null, the full Godot MCP tool surface stays registered (e.g. tool catalog discovery).
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Kernel> GetOrCreateKernelAsync(
        string? provider = null,
        string? preferredModelId = null,
        string? modalityKeyForToolFiltering = null,
        CancellationToken cancellationToken = default);
}
