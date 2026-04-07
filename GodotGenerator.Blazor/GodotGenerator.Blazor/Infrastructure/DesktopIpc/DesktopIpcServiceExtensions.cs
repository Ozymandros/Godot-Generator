using GodotGenerator.Blazor.Infrastructure.DesktopIpc.Handlers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace GodotGenerator.Blazor.Infrastructure.DesktopIpc;

/// <summary>
/// DI registration extensions for the desktop IPC infrastructure.
/// Call <see cref="AddDesktopIpc"/> from <c>Program.cs</c> when running in desktop mode.
/// The pipe host and all command handlers are registered as singletons so the dispatcher
/// index is built once and shared across all concurrent pipe connections.
/// </summary>
public static class DesktopIpcServiceExtensions
{
    /// <summary>
    /// Registers the named-pipe host, command dispatcher, and all command handlers.
    /// Safe to call unconditionally; the host will silently no-op on non-Windows platforms
    /// where named pipes behave differently.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The same <paramref name="services"/> for chaining.</returns>
    public static IServiceCollection AddDesktopIpc(this IServiceCollection services)
    {
        // Register all command handlers as ICommandHandler.
        // Each handler advertises its command names via ICommandHandler.CommandNames.
        services.AddSingleton<ICommandHandler, ConfigCommandHandler>();
        services.AddSingleton<ICommandHandler, PreferenceCommandHandler>();
        services.AddSingleton<ICommandHandler, KeysCommandHandler>();
        services.AddSingleton<ICommandHandler, GenerateCommandHandler>();

        // The dispatcher is built from the ICommandHandler registrations above.
        services.AddSingleton<CommandDispatcher>();

        // The pipe host is a background service that drives the accept/dispatch loop.
        services.AddSingleton<NamedPipeCommandHost>();
        services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<NamedPipeCommandHost>());

        return services;
    }
}
