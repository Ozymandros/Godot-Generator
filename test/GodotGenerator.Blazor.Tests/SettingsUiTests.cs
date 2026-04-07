using System.Net;
using System.Text;
using System.Text.Json;
using Bunit;
using Bunit.TestDoubles;
using GodotGenerator.Api.Dtos;
using GodotGenerator.Application;
using GodotGenerator.Application.Configuration;
using GodotGenerator.Blazor.Client.Components.Generic;
using GodotGenerator.Blazor.Client.Models;
using GodotGenerator.Blazor.Client.Pages;
using GodotGenerator.Blazor.Client.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components;
using Xunit;

namespace GodotGenerator.Blazor.Tests;

/// <summary>
/// UI tests for settings hub tabs, Advanced Options chrome, and Providers registry panel.
/// </summary>
public sealed class SettingsUiTests
{
    [Fact]
    public void AdvancedOptionsSection_renders_collapsed_details_without_open_attribute()
    {
        using var ctx = CreateFluentContext();
        var options = new GenerationAdvancedOptions();

        var cut = ctx.Render<AdvancedOptionsSection>(p => p
            .Add(x => x.Options, options));

        var details = cut.Find("details.advanced-options-section");
        Assert.False(details.HasAttribute("open"));
    }

    [Fact]
    public void SettingsHub_tab_strip_renders_icons_beside_labels()
    {
        using var ctx = CreateFluentContext();
        ((IServiceCollection)ctx.Services).AddSingleton<NavigationManager>(_ => new BunitNavigationManager(ctx));
        ((IServiceCollection)ctx.Services).AddSingleton(_ => CreateHttpClientForConfig());

        var cut = ctx.Render<SettingsHub>();

        cut.WaitForAssertion(() =>
        {
            var icons = cut.FindAll(".settings-tab__icon");
            Assert.True(icons.Count >= 5, $"expected at least 5 tab icons, got {icons.Count}");
        });
    }

    [Fact]
    public async Task SettingsProvidersPanel_commit_posts_providers_registry_preference()
    {
        using var ctx = CreateFluentContext();
        ctx.JSInterop.Mode = Bunit.JSRuntimeMode.Loose;
        ctx.JSInterop.Setup<bool>("confirm").SetResult(true);

        string? postedKey = null;
        string? postedValue = null;
        ((IServiceCollection)ctx.Services).AddSingleton(_ => CreateHttpClientForPreferences((key, value) =>
        {
            postedKey = key;
            postedValue = value;
        }));

        var entry = new ProviderRegistryEntry
        {
            Id = "acme",
            KeyStoreHandle = "acme",
            Modalities = ["llm"],
            AuthenticationRequired = true,
            Streaming = true,
            FunctionCalling = true,
            GenericToolUse = true,
        };

        var cut = ctx.Render<SettingsProvidersPanel>(p => p
            .Add(x => x.ConfigEpoch, 1)
            .Add(x => x.SourceProviders, new[] { entry })
            .Add(x => x.OnRegistrySaved, EventCallback.Factory.Create(this, OnRegistrySavedNoOp)));

        var commit = cut.FindAll("fluent-button").FirstOrDefault(b =>
            b.TextContent.Contains("Commit", StringComparison.Ordinal));
        Assert.NotNull(commit);
        await cut.InvokeAsync(async () => await commit!.ClickAsync(new MouseEventArgs()));

        Assert.Equal(PreferenceKeys.ProvidersRegistryV1, postedKey);
        Assert.False(string.IsNullOrWhiteSpace(postedValue));
        using var doc = JsonDocument.Parse(postedValue!);
        Assert.Equal(1, doc.RootElement.GetProperty("version").GetInt32());
        Assert.Equal("acme", doc.RootElement.GetProperty("providers")[0].GetProperty("id").GetString());
    }

    [Fact]
    public async Task SettingsModelsPanel_commit_posts_models_registry_preference()
    {
        using var ctx = CreateFluentContext();
        ctx.JSInterop.Mode = Bunit.JSRuntimeMode.Loose;
        ctx.JSInterop.Setup<bool>("confirm").SetResult(true);

        string? postedKey = null;
        string? postedValue = null;
        ((IServiceCollection)ctx.Services).AddSingleton(_ => CreateHttpClientForPreferences((key, value) =>
        {
            postedKey = key;
            postedValue = value;
        }));

        var entry = new ModelRegistryEntry
        {
            ProviderId = "google",
            FriendlyName = "Gemini 1.5 Flash",
            EngineValue = "gemini-1.5-flash-001",
            Modality = "llm",
        };

        var cut = ctx.Render<SettingsModelsPanel>(p => p
            .Add(x => x.ConfigEpoch, 1)
            .Add(x => x.SourceModels, new[] { entry })
            .Add(x => x.ProviderIds, new[] { "google" })
            .Add(x => x.OnRegistrySaved, EventCallback.Factory.Create(this, OnRegistrySavedNoOp)));

        var commit = cut.FindAll("fluent-button").FirstOrDefault(b =>
            b.TextContent.Contains("Commit", StringComparison.Ordinal) &&
            b.TextContent.Contains("Changes", StringComparison.Ordinal));
        Assert.NotNull(commit);
        await cut.InvokeAsync(async () => await commit!.ClickAsync(new MouseEventArgs()));

        Assert.Equal(PreferenceKeys.ModelsRegistryV1, postedKey);
        Assert.False(string.IsNullOrWhiteSpace(postedValue));
        using var doc = JsonDocument.Parse(postedValue!);
        Assert.Equal(1, doc.RootElement.GetProperty("version").GetInt32());
        Assert.Equal("google", doc.RootElement.GetProperty("models")[0].GetProperty("providerId").GetString());
        Assert.Equal("gemini-1.5-flash-001", doc.RootElement.GetProperty("models")[0].GetProperty("engineValue").GetString());
    }

    [Fact]
    public async Task SettingsPromptsPanel_commit_posts_prompts_system_preference()
    {
        using var ctx = CreateFluentContext();
        string? postedKey = null;
        string? postedValue = null;
        ((IServiceCollection)ctx.Services).AddSingleton(_ => CreateHttpClientForPreferences((key, value) =>
        {
            postedKey = key;
            postedValue = value;
        }));

        var src = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["code"] = "You are a test code assistant.",
        };

        var cut = ctx.Render<SettingsPromptsPanel>(p => p
            .Add(x => x.ConfigEpoch, 1)
            .Add(x => x.SourcePrompts, src)
            .Add(x => x.OnPromptsSaved, EventCallback.Factory.Create(this, OnRegistrySavedNoOp)));

        var save = cut.FindAll("fluent-button").FirstOrDefault(b =>
            b.TextContent.Contains("Save prompts", StringComparison.Ordinal));
        Assert.NotNull(save);
        await cut.InvokeAsync(async () => await save!.ClickAsync(new MouseEventArgs()));

        Assert.Equal(PreferenceKeys.PromptsSystemV1, postedKey);
        Assert.False(string.IsNullOrWhiteSpace(postedValue));
        using var doc = JsonDocument.Parse(postedValue!);
        Assert.Equal(1, doc.RootElement.GetProperty("version").GetInt32());
        Assert.Equal("You are a test code assistant.", doc.RootElement.GetProperty("prompts").GetProperty("code").GetString());
    }

    [Fact(Skip = "Fluent web-component interaction is not stable in bUnit for this scenario.")]
    public async Task SettingsSecretsPanel_save_replacements_posts_api_keys_payload()
    {
        using var ctx = CreateFluentContext();

        string? postedKeysJson = null;
        ((IServiceCollection)ctx.Services).AddSingleton(_ => CreateHttpClientForApiKeys(body => postedKeysJson = body));

        var cut = ctx.Render<SettingsSecretsPanel>(p => p
            .Add(x => x.ConfigEpoch, 1)
            .Add(x => x.SourceKeyNames, new[] { "openai" })
            .Add(x => x.OnSecretsSaved, EventCallback.Factory.Create(this, OnRegistrySavedNoOp)));

        var replaceField = cut.FindAll("fluent-text-field").FirstOrDefault();
        Assert.NotNull(replaceField);
        await cut.InvokeAsync(async () => await replaceField!.InputAsync(new ChangeEventArgs { Value = "new-secret-value" }));

        var save = cut.FindAll("fluent-button").FirstOrDefault(b =>
            b.TextContent.Contains("Save replacements", StringComparison.Ordinal));
        Assert.NotNull(save);
        await cut.InvokeAsync(async () => await save!.ClickAsync(new MouseEventArgs()));

        cut.WaitForAssertion(() => Assert.False(string.IsNullOrEmpty(postedKeysJson)));
        using var doc = JsonDocument.Parse(postedKeysJson!);
        Assert.Equal("new-secret-value", doc.RootElement.GetProperty("keys").GetProperty("openai").GetString());
    }

    private Task OnRegistrySavedNoOp() => Task.CompletedTask;

    private static BunitContext CreateFluentContext()
    {
        var ctx = new BunitContext();
        ctx.JSInterop.Mode = Bunit.JSRuntimeMode.Loose;
        ((IServiceCollection)ctx.Services).AddFluentUIComponents();
        return ctx;
    }

    private static GodotGeneratorHttpClient CreateHttpClientForConfig()
    {
        var handler = new StubHandler(_ =>
        {
            var payload = ApiResponse<Dictionary<string, object?>>.Ok(new Dictionary<string, object?>
            {
                ["preferences"] = new Dictionary<string, object?>(),
                ["providers"] = JsonSerializer.Deserialize<JsonElement>("[]"),
                ["models"] = JsonSerializer.Deserialize<JsonElement>("{}"),
            });
            var json = JsonSerializer.Serialize(
                payload,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
        });

        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        return new GodotGeneratorHttpClient(http);
    }

    private static GodotGeneratorHttpClient CreateHttpClientForPreferences(Action<string, string?> capture)
    {
        var handler = new StubHandler(req =>
        {
            if (req.Method == HttpMethod.Post && req.RequestUri?.PathAndQuery == "/api/preference")
            {
                var body = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                using var doc = JsonDocument.Parse(body);
                capture(
                    doc.RootElement.GetProperty("key").GetString()!,
                    doc.RootElement.TryGetProperty("value", out var v) && v.ValueKind != JsonValueKind.Null
                        ? v.GetString()
                        : null);
                var ok = ApiResponse<Dictionary<string, string?>>.Ok(new Dictionary<string, string?>());
                var json = JsonSerializer.Serialize(
                    ok,
                    new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json"),
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        return new GodotGeneratorHttpClient(http);
    }

    private static GodotGeneratorHttpClient CreateHttpClientForApiKeys(Action<string> captureBody)
    {
        var handler = new StubHandler(req =>
        {
            var path = req.RequestUri?.PathAndQuery ?? string.Empty;
            if (req.Method == HttpMethod.Post && (path == "/api/keys" || path == "api/keys"))
            {
                var body = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                captureBody(body);
                var ok = ApiResponse<Dictionary<string, object?>>.Ok(new Dictionary<string, object?>
                {
                    ["saved"] = Array.Empty<string>(),
                });
                var json = JsonSerializer.Serialize(
                    ok,
                    new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json"),
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        return new GodotGeneratorHttpClient(http);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(_respond(request));
    }
}
