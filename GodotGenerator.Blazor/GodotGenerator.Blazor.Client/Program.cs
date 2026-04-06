using GodotGenerator.Blazor.Client.Services;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.FluentUI.AspNetCore.Components;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddFluentUIComponents();
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<GodotGeneratorHttpClient>();
builder.Services.AddScoped<ProjectStateService>();
builder.Services.AddSingleton<LogBufferService>();
builder.Services.AddSingleton<StatusBannerService>();

await builder.Build().RunAsync();
