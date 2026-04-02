using Godot_Generator_Blazor;
using Godot_Generator_Blazor.Services;
using Godot_Generator_Blazor.State;
using GodotGenerator.Api.DependencyInjection;
using GodotGenerator.Application.Abstractions;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<IPreferenceRepository, BrowserPreferenceRepository>();
builder.Services.AddScoped<IAiOrchestrationService, BrowserAiOrchestrationService>();
builder.Services.AddGodotGeneratorApi();
builder.Services.AddScoped<IApiClient, GeneratorApiClient>();
builder.Services.AddScoped<GlobalConfigStateService>();
builder.Services.AddScoped<GenerationPanelStateService>();

await builder.Build().RunAsync();
