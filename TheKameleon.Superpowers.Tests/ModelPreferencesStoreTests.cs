using TheKameleon.Superpowers.Skills.Models;

namespace TheKameleon.Superpowers.Tests;

public sealed class ModelPreferencesStoreTests
{
    [Fact]
    public void LoadReturnsEmptyWhenNoFileExists()
    {
        using var profile = new TempProfile();
        var store = new ModelPreferencesStore(profile.Paths);

        var loaded = store.Load();

        Assert.Equal(CopilotPlan.Unspecified, loaded.Plan);
        Assert.Empty(loaded.Preferences);
    }

    [Fact]
    public void ShowCliWindowsDefaultsToTrueAndRoundTrips()
    {
        using var profile = new TempProfile();
        var store = new ModelPreferencesStore(profile.Paths);

        Assert.True(store.Load().ShowCliWindows);
        store.Save(ModelPreferences.Empty with { ShowCliWindows = false });

        Assert.False(store.Load().ShowCliWindows);
    }

    [Fact]
    public void SaveThenLoadRoundTripsPreferencesAndPlan()
    {
        using var profile = new TempProfile();
        var store = new ModelPreferencesStore(profile.Paths);
        var preferences = ModelPreferences.Empty with
        {
            Plan = CopilotPlan.Business,
            Preferences = new[]
            {
                new ModelPreference(SuperpowersFunction.Review, new[] { "Claude Opus 5.5", "GPT-5.4" }),
                new ModelPreference(SuperpowersFunction.Debug, new[] { "GPT-5.4" }),
            },
        };

        store.Save(preferences);
        var loaded = store.Load();

        Assert.Equal(CopilotPlan.Business, loaded.Plan);
        Assert.Equal(2, loaded.Preferences.Count);
        Assert.Contains(loaded.Preferences, p => p.Function == SuperpowersFunction.Review && p.Models.SequenceEqual(new[] { "Claude Opus 5.5", "GPT-5.4" }));
    }

    [Fact]
    public void LoadMigratesASingleModelVersion1File()
    {
        using var profile = new TempProfile();
        var store = new ModelPreferencesStore(profile.Paths);
        Directory.CreateDirectory(profile.Paths.StateDirectory);
        File.WriteAllText(store.PreferencesFile, """
            {
              "schemaVersion": 1,
              "plan": "Business",
              "preferences": [
                { "function": "Review", "family": "Claude", "model": "Claude Opus 5.5" }
              ]
            }
            """);

        var loaded = store.Load();

        Assert.Equal(CopilotPlan.Business, loaded.Plan);
        Assert.Contains(loaded.Preferences, p => p.Function == SuperpowersFunction.Review && p.Models.SequenceEqual(new[] { "Claude Opus 5.5" }));
    }

    [Fact]
    public void LoadReturnsEmptyOnUnsupportedSchemaVersion()
    {
        using var profile = new TempProfile();
        var store = new ModelPreferencesStore(profile.Paths);
        Directory.CreateDirectory(profile.Paths.StateDirectory);
        File.WriteAllText(store.PreferencesFile, """{ "schemaVersion": 999, "plan": "Free", "preferences": [] }""");

        var loaded = store.Load();

        Assert.Empty(loaded.Preferences);
    }
}
