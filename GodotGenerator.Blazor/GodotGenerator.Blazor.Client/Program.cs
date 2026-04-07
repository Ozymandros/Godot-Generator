using GodotGenerator.Blazor.Client.Services;
using GodotGenerator.Blazor.Client.Services.Transport;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.FluentUI.AspNetCore.Components;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddFluentUIComponents();
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Existing HTTP client — kept for dual-stack compatibility during migration.
builder.Services.AddScoped<GodotGeneratorHttpClient>();

// Transport abstraction: both implementations are registered; the facade selects at runtime.
builder.Services.AddScoped<HttpBffTransport>();
builder.Services.AddScoped<ElectronIpcTransport>();
builder.Services.AddScoped<GodotGeneratorClientFacade>();

builder.Services.AddScoped<ProjectStateService>();
builder.Services.AddSingleton<LogBufferService>();
builder.Services.AddSingleton<StatusBannerService>();

await builder.Build().RunAsync();
