using System.Text.Json;
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
            var preferences = JsonSerializer.Deserialize<ModelPreferences>(File.ReadAllText(this.PreferencesFile), Options);
            return preferences is null || preferences.SchemaVersion != ModelPreferences.CurrentSchemaVersion
                ? ModelPreferences.Empty
                : preferences;
        }
        catch (JsonException)
        {
            return ModelPreferences.Empty;
        }
    }

    public void Save(ModelPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        Directory.CreateDirectory(paths.StateDirectory);
        var temp = this.PreferencesFile + "." + Guid.NewGuid().ToString("N") + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(preferences, Options));
        File.Move(temp, this.PreferencesFile, overwrite: true);
    }
}
