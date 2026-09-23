using System.Text.Json;
using TheKameleon.Superpowers.Skills.Install;

namespace TheKameleon.Superpowers.Skills.Models;

public sealed class ModelCatalogCacheStore(ProfilePaths paths)
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public string CacheFile => Path.Combine(paths.StateDirectory, "model-catalog-cache.json");

    public CopilotModelCatalog? Load()
    {
        if (!File.Exists(this.CacheFile))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<CopilotModelCatalog>(File.ReadAllText(this.CacheFile), Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public void Save(CopilotModelCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        Directory.CreateDirectory(paths.StateDirectory);
        var temp = this.CacheFile + "." + Guid.NewGuid().ToString("N") + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(catalog, Options));
        File.Move(temp, this.CacheFile, overwrite: true);
    }
}
