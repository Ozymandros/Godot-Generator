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
    /// <param name="preferredModelId">Optional model id override for this kernel.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Kernel> GetOrCreateKernelAsync(string? preferredModelId = null, CancellationToken cancellationToken = default);
}
