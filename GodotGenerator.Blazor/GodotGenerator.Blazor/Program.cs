using GodotGenerator.Api.Abstractions;
using GodotGenerator.Api.Services;
using GodotGenerator.Blazor.Client.Services;
using GodotGenerator.Blazor.Components;
using GodotGenerator.Blazor.Infrastructure.DesktopIpc;
using GodotGenerator.Infrastructure.Ai.DependencyInjection;
using GodotGenerator.Infrastructure.Persistence.DependencyInjection;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.FluentUI.AspNetCore.Components;

var builder = WebApplication.CreateBuilder(args);

var isDesktopIpc = string.Equals(
    builder.Configuration["GODOT_DESKTOP_IPC"] ?? Environment.GetEnvironmentVariable("GODOT_DESKTOP_IPC"),
    "1",
    StringComparison.Ordinal);

if (builder.Environment.IsDevelopment() || isDesktopIpc)
{
    builder.WebHost.UseStaticWebAssets();
}

builder.Services.AddFluentUIComponents();
builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();
builder.Services.AddGodotGeneratorPersistence(builder.Configuration);
builder.Services.AddGodotGeneratorInfrastructure(builder.Configuration);
builder.Services.AddScoped<IGodotGeneratorApiService, GodotGeneratorApiService>();
builder.Services.AddScoped<ProjectStateService>();
builder.Services.AddSingleton<LogBufferService>();
builder.Services.AddSingleton<StatusBannerService>();
builder.Services.AddHttpClient<GodotGeneratorHttpClient>((sp, client) =>
{
    var accessor = sp.GetRequiredService<IHttpContextAccessor>();
    var ctx = accessor.HttpContext
        ?? throw new InvalidOperationException("HttpContext is required to build the BFF client base address during server render.");
    client.BaseAddress = new Uri($"{ctx.Request.Scheme}://{ctx.Request.Host}{ctx.Request.PathBase}/");
});

// Register the named-pipe IPC host when running in desktop mode.
// The GODOT_DESKTOP_IPC environment variable is set by the Electron backend lifecycle manager.
if (isDesktopIpc)
{
    builder.Services.AddDesktopIpc();
}

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

app.MapControllers();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(GodotGenerator.Blazor.Client._Imports).Assembly);

app.Run();
