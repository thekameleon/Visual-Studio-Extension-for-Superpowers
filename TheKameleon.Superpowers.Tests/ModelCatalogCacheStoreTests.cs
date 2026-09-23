using TheKameleon.Superpowers.Skills.Models;

namespace TheKameleon.Superpowers.Tests;

public sealed class ModelCatalogCacheStoreTests
{
    [Fact]
    public void LoadReturnsNullWhenNoCacheExists()
    {
        using var profile = new TempProfile();
        var store = new ModelCatalogCacheStore(profile.Paths);

        Assert.Null(store.Load());
    }

    [Fact]
    public void SaveThenLoadRoundTripsTheCatalog()
    {
        using var profile = new TempProfile();
        var store = new ModelCatalogCacheStore(profile.Paths);
        var catalog = new CopilotModelCatalog(
            new[] { new CopilotModel("GPT-5.4", "OpenAI", "GA") },
            new[] { new CopilotModelPlanAvailability("GPT-5.4", true, true, true, true, true) },
            Array.Empty<CopilotModelCategory>(),
            DateTimeOffset.Parse("2026-09-23T00:00:00Z"));

        store.Save(catalog);
        var loaded = store.Load();

        Assert.NotNull(loaded);
        Assert.Equal("GPT-5.4", loaded!.Models[0].Name);
        Assert.Equal(catalog.FetchedAtUtc, loaded.FetchedAtUtc);
    }

    [Fact]
    public void LoadNormalizesOldFormatCacheWithNoCategoriesProperty()
    {
        using var profile = new TempProfile();
        var store = new ModelCatalogCacheStore(profile.Paths);
        Directory.CreateDirectory(profile.Paths.StateDirectory);
        File.WriteAllText(
            store.CacheFile,
            "{\"models\":[{\"name\":\"GPT-5.4\",\"provider\":\"OpenAI\",\"releaseStatus\":\"GA\"}],\"planAvailability\":[],\"fetchedAtUtc\":\"2026-09-23T00:00:00Z\"}");

        var loaded = store.Load();

        Assert.NotNull(loaded);
        Assert.NotNull(loaded!.Categories);
        Assert.Empty(loaded.Categories);
    }

    [Fact]
    public void LoadReturnsNullOnCorruptFile()
    {
        using var profile = new TempProfile();
        var store = new ModelCatalogCacheStore(profile.Paths);
        Directory.CreateDirectory(profile.Paths.StateDirectory);
        File.WriteAllText(store.CacheFile, "{ not valid json");

        Assert.Null(store.Load());
    }
}
