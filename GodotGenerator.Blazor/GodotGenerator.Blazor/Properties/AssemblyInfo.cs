using System.Runtime.CompilerServices;

// Expose internal DesktopIpc types (handlers, dispatcher, pipe host) to the
// dedicated IPC unit-test project so tests can verify them directly.
[assembly: InternalsVisibleTo("GodotGenerator.Desktop.Ipc.Tests")]

// Required so Moq can create dynamic proxy implementations of internal interfaces.
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]
