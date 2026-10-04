using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using TheKameleon.Superpowers.Skills.Install;

namespace TheKameleon.Superpowers.Skills.Models;

public sealed class ModelPreferencesStore(ProfilePaths paths)
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    public string PreferencesFile => Path.Combine(paths.StateDirectory, "model-preferences.json");

    public ModelPreferences Load()
    {
        if (!File.Exists(this.PreferencesFile))
        {
            return ModelPreferences.Empty;
        }

        try
        {
            var text = File.ReadAllText(this.PreferencesFile);
            var version = JsonNode.Parse(text)?["schemaVersion"]?.GetValue<int>();
            if (version == 1)
            {
                return MigrateVersion1(text);
            }

            var preferences = JsonSerializer.Deserialize<ModelPreferences>(text, Options);
            return preferences is null || preferences.SchemaVersion != ModelPreferences.CurrentSchemaVersion
                ? ModelPreferences.Empty
                : Normalize(preferences);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or FormatException)
        {
            return ModelPreferences.Empty;
        }
    }

    private static ModelPreferences MigrateVersion1(string text)
    {
        var legacy = JsonSerializer.Deserialize<LegacyPreferences>(text, Options);
        if (legacy is null)
        {
            return ModelPreferences.Empty;
        }

        return new ModelPreferences
        {
            Plan = legacy.Plan,
            Preferences = (legacy.Preferences ?? Array.Empty<LegacyPreference>())
                .Where(p => !string.IsNullOrWhiteSpace(p.Model))
                .Select(p => new ModelPreference(p.Function, new[] { p.Model!.Trim() }))
                .ToArray(),
        };
    }

    private static ModelPreferences Normalize(ModelPreferences preferences) => preferences with
    {
        Preferences = (preferences.Preferences ?? Array.Empty<ModelPreference>())
            .Select(p => p with { Models = (p.Models ?? Array.Empty<string>()).Where(m => !string.IsNullOrWhiteSpace(m)).Select(m => m.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray() })
            .Where(p => p.Models.Count > 0)
            .ToArray(),
    };

    private sealed record LegacyPreference(SuperpowersFunction Function, string? Model);

    private sealed record LegacyPreferences(CopilotPlan Plan, IReadOnlyList<LegacyPreference>? Preferences);

    public void Save(ModelPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        Directory.CreateDirectory(paths.StateDirectory);
        var temp = this.PreferencesFile + "." + Guid.NewGuid().ToString("N") + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(preferences, Options));
        File.Move(temp, this.PreferencesFile, overwrite: true);
    }
}
