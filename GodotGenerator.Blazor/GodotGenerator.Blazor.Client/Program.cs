using GodotGenerator.Blazor.Client.Services;
using GodotGenerator.Blazor.Client.Services.Transport;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.FluentUI.AspNetCore.Components;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddFluentUIComponents();

// IPC-only transport: all backend calls go via Electron named-pipe bridge.
builder.Services.AddScoped<IGodotGeneratorClientTransport, ElectronIpcTransport>();
builder.Services.AddScoped<GodotGeneratorClientFacade>();

builder.Services.AddScoped<ProjectStateService>();
builder.Services.AddScoped<ElectronBridgeService>();
// AppLogService must be registered before LogBufferService (the shim depends on it).
builder.Services.AddSingleton<AppLogService>();
builder.Services.AddSingleton<LogBufferService>();
builder.Services.AddSingleton<StatusBannerService>();

await builder.Build().RunAsync();
