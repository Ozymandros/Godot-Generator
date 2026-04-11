#nullable enable
using System.Text.Json;
using System.Text.Json.Serialization;
using GodotGenerator.Application.Configuration;
using GodotGenerator.Domain;
using static GodotGenerator.Application.PreferenceKeys;

namespace GodotGenerator.Application.Services;

/// <summary>Loads, merges, serializes, and validates versioned configuration registry JSON.</summary>
public static class ConfigurationRegistryService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    /// <summary>Parses provider registry JSON and falls back to built-in defaults when invalid or empty.</summary>
    public static ProviderRegistryDocument ParseProviderRegistry(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return GetDefaultProviderRegistry();
        }

        try
        {
            var doc = JsonSerializer.Deserialize<ProviderRegistryDocument>(json, JsonOptions);
            if (doc is null || doc.Providers.Count == 0)
            {
                return GetDefaultProviderRegistry();
            }

            return doc;
        }
        catch (JsonException)
        {
            return GetDefaultProviderRegistry();
        }
    }

    /// <summary>Parses model registry JSON and falls back to built-in defaults when invalid or empty.</summary>
    public static ModelRegistryDocument ParseModelRegistry(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return GetDefaultModelRegistry();
        }

        try
        {
            var doc = JsonSerializer.Deserialize<ModelRegistryDocument>(json, JsonOptions);
            return doc is null || doc.Models.Count == 0 ? GetDefaultModelRegistry() : doc;
        }
        catch (JsonException)
        {
            return GetDefaultModelRegistry();
        }
    }

    private static ProviderRegistryDocument GetDefaultProviderRegistry()
    {
        return new ProviderRegistryDocument
        {
            Version = 1,
            Providers =
            [
                new() { Id = Constants.ProviderOpenAi, KeyStoreHandle = Constants.ProviderOpenAi, OpenAiCompatibility = true },
                new() { Id = Constants.ProviderAnthropic, KeyStoreHandle = Constants.ProviderAnthropic },
                new() { Id = Constants.ProviderGoogle, KeyStoreHandle = Constants.ProviderGoogle },
                new() { Id = Constants.ProviderDeepSeek, KeyStoreHandle = Constants.ProviderDeepSeek, OpenAiCompatibility = true },
                new()
                {
                    Id = Constants.ProviderOpenRouter,
                    KeyStoreHandle = Constants.ProviderOpenRouter,
                    Endpoint = "https://openrouter.ai/api/v1",
                    OpenAiCompatibility = true,
                },
                new() { Id = Constants.ProviderHuggingFace, KeyStoreHandle = Constants.ProviderHuggingFace },
                new() { Id = Constants.ProviderOllama, KeyStoreHandle = "ollama", Endpoint = "http://localhost:11434/v1", OpenAiCompatibility = true },
                new() { Id = Constants.ProviderGroq, KeyStoreHandle = Constants.ProviderGroq, OpenAiCompatibility = true },
                new() { Id = Constants.ProviderStability, KeyStoreHandle = Constants.ProviderStability },
                new() { Id = Constants.ProviderFlux, KeyStoreHandle = "flux" },
                new() { Id = Constants.ProviderElevenLabs, KeyStoreHandle = Constants.ProviderElevenLabs },
            ]
        };
    }

    private static ModelRegistryDocument GetDefaultModelRegistry()
    {
        return new ModelRegistryDocument
        {
            Version = 1,
            Models =
            [
                new() { ProviderId = Constants.ProviderOpenAi, FriendlyName = "GPT-4o", EngineValue = Constants.ModelGpt4O, Modality = "llm" },
                new() { ProviderId = Constants.ProviderOpenAi, FriendlyName = "GPT-4o mini", EngineValue = "gpt-4o-mini", Modality = "llm" },
                new() { ProviderId = Constants.ProviderAnthropic, FriendlyName = "Claude 3.5 Sonnet", EngineValue = Constants.ModelClaude35Sonnet, Modality = "llm" },
                new() { ProviderId = Constants.ProviderGoogle, FriendlyName = "Gemini 1.5 Pro", EngineValue = Constants.ModelGemini15Pro, Modality = "llm" },
                new() { ProviderId = Constants.ProviderGoogle, FriendlyName = "Gemini 1.5 Flash", EngineValue = Constants.ModelGemini15Flash, Modality = "llm" },
                new() { ProviderId = Constants.ProviderGoogle, FriendlyName = "Google TTS", EngineValue = "google-tts", Modality = "audio" },
                new() { ProviderId = Constants.ProviderGoogle, FriendlyName = "Imagen 3.0 Generate 002", EngineValue = "imagen-3.0-generate-002", Modality = "image" },
                new() { ProviderId = Constants.ProviderGroq, FriendlyName = "Llama 3 70B", EngineValue = Constants.ModelLlama370B, Modality = "llm" },
                new() { ProviderId = Constants.ProviderDeepSeek, FriendlyName = "DeepSeek Coder", EngineValue = Constants.ModelDeepSeekCoder, Modality = "llm" },
                new() { ProviderId = Constants.ProviderDeepSeek, FriendlyName = "DeepSeek Chat", EngineValue = Constants.ModelDeepSeekChat, Modality = "llm" },
                new() { ProviderId = Constants.ProviderStability, FriendlyName = "SDXL 1.0", EngineValue = "stable-diffusion-xl-1024-v1-0", Modality = "image" },
                new() { ProviderId = Constants.ProviderStability, FriendlyName = "SDXL Sprites", EngineValue = "stable-diffusion-xl-1024-v1-0", Modality = "sprites" },
                new() { ProviderId = Constants.ProviderStability, FriendlyName = "SD 1.5", EngineValue = "stable-diffusion-v1-5", Modality = "image" },
                new() { ProviderId = Constants.ProviderElevenLabs, FriendlyName = "Eleven English v1", EngineValue = "eleven_monolingual_v1", Modality = "audio" },
                new() { ProviderId = Constants.ProviderElevenLabs, FriendlyName = "Eleven Multilingual v2", EngineValue = "eleven_multilingual_v2", Modality = "audio" },
            ]
        };
    }

    /// <summary>Parses the system prompts document and guarantees required modality defaults are present.</summary>
    public static SystemPromptsDocument ParseSystemPrompts(string? json)
    {
        SystemPromptsDocument doc;
        if (string.IsNullOrWhiteSpace(json))
        {
            doc = new SystemPromptsDocument();
        }
        else
        {
            try
            {
                doc = JsonSerializer.Deserialize<SystemPromptsDocument>(json, JsonOptions) ?? new SystemPromptsDocument();
            }
            catch (JsonException)
            {
                doc = new SystemPromptsDocument();
            }
        }

        EnsurePromptDefaults(doc);
        return doc;
    }

    /// <summary>Fills missing or empty modality entries from <see cref="SystemPromptDefaults"/> (first-run and partial documents).</summary>
    private static void EnsurePromptDefaults(SystemPromptsDocument doc)
    {
        foreach (var (key, value) in SystemPromptDefaults.ByModality)
        {
            if (!doc.Prompts.TryGetValue(key, out var existing) || string.IsNullOrWhiteSpace(existing))
            {
                doc.Prompts[key] = value;
            }
        }
    }

    /// <summary>Merges legacy flat prompt keys into the versioned document for display.</summary>
    public static SystemPromptsDocument MergeLegacyPrompts(
        IReadOnlyDictionary<string, string?> preferences,
        SystemPromptsDocument document)
    {
        void SetIfEmpty(string modalityKey, string? legacyValue)
        {
            if (string.IsNullOrWhiteSpace(legacyValue))
            {
                return;
            }

            if (!document.Prompts.ContainsKey(modalityKey) || string.IsNullOrWhiteSpace(document.Prompts[modalityKey]))
            {
                document.Prompts[modalityKey] = legacyValue.Trim();
            }
        }

        SetIfEmpty("text", preferences.GetValueOrDefault(PromptsTextLegacy));
        SetIfEmpty("code", preferences.GetValueOrDefault(PromptsCodeLegacy));
        SetIfEmpty("godot-ui", preferences.GetValueOrDefault(PromptsGodotUiLegacy));
        SetIfEmpty("godot-physics", preferences.GetValueOrDefault(PromptsGodotPhysicsLegacy));
        SetIfEmpty("audio", preferences.GetValueOrDefault("prompts.audio.legacy"));
        SetIfEmpty("sprites", preferences.GetValueOrDefault("prompts.sprites.legacy"));
        SetIfEmpty("scenes", preferences.GetValueOrDefault("prompts.scenes.legacy")); // Placeholder for future mapping
        SetIfEmpty("godot-project", preferences.GetValueOrDefault("prompts.project.legacy"));
        SetIfEmpty("godot-lighting", preferences.GetValueOrDefault(PromptsGodotLighting));
        SetIfEmpty("godot-camera", preferences.GetValueOrDefault(PromptsGodotCamera));
        SetIfEmpty("godot-shaders", preferences.GetValueOrDefault(PromptsGodotShaders));
        SetIfEmpty("godot-signals", preferences.GetValueOrDefault(PromptsGodotSignals));
        SetIfEmpty("godot-nodes", preferences.GetValueOrDefault(PromptsGodotNodes));

        return document;
    }

    /// <summary>Groups model entries by provider id using case-insensitive keys.</summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<ModelRegistryEntry>> GroupModelsByProvider(
        IReadOnlyList<ModelRegistryEntry> models)
    {
        var map = new Dictionary<string, List<ModelRegistryEntry>>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in models)
        {
            var pid = m.ProviderId.Trim();
            if (string.IsNullOrEmpty(pid))
            {
                continue;
            }

            if (!map.TryGetValue(pid, out var list))
            {
                list = [];
                map[pid] = list;
            }

            list.Add(m);
        }

        return map.ToDictionary(
            x => x.Key,
            x => (IReadOnlyList<ModelRegistryEntry>)x.Value.AsReadOnly(),
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Serializes provider registry data into compact JSON.</summary>
    public static string Serialize(ProviderRegistryDocument document) =>
        JsonSerializer.Serialize(document, JsonOptions);

    /// <summary>Serializes model registry data into compact JSON.</summary>
    public static string Serialize(ModelRegistryDocument document) =>
        JsonSerializer.Serialize(document, JsonOptions);

    /// <summary>Serializes system prompts data into compact JSON.</summary>
    public static string Serialize(SystemPromptsDocument document) =>
        JsonSerializer.Serialize(document, JsonOptions);

    /// <summary>Returns null if valid; otherwise an error message.</summary>
    public static string? ValidateProviderRegistry(ProviderRegistryDocument doc)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in doc.Providers)
        {
            var id = p.Id.Trim();
            if (string.IsNullOrEmpty(id))
            {
                return "Each provider must have a non-empty id.";
            }

            if (!seen.Add(id))
            {
                return $"Duplicate provider id: {id}";
            }
        }

        return null;
    }

    /// <summary>Returns null if valid; otherwise an error message.</summary>
    public static string? ValidateModelRegistry(ModelRegistryDocument doc)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in doc.Models)
        {
            var pid = m.ProviderId.Trim();
            var engine = m.EngineValue.Trim();
            if (string.IsNullOrEmpty(pid) || string.IsNullOrEmpty(engine))
            {
                return "Each model must have providerId and engineValue.";
            }

            var key = pid + "\u001f" + engine;
            if (!seen.Add(key))
            {
                return $"Duplicate model for provider {pid}: {engine}";
            }
        }

        return null;
    }
}
