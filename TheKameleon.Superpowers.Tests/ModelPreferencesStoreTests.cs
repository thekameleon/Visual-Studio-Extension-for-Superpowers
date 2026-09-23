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
    public void SaveThenLoadRoundTripsPreferencesAndPlan()
    {
        using var profile = new TempProfile();
        var store = new ModelPreferencesStore(profile.Paths);
        var preferences = ModelPreferences.Empty with
        {
            Plan = CopilotPlan.Business,
            Preferences = new[]
            {
                new ModelPreference(SuperpowersFunction.Review, "Claude Opus 5.5"),
                new ModelPreference(SuperpowersFunction.Debug, "GPT-5.4"),
            },
        };

        store.Save(preferences);
        var loaded = store.Load();

        Assert.Equal(CopilotPlan.Business, loaded.Plan);
        Assert.Equal(2, loaded.Preferences.Count);
        Assert.Contains(loaded.Preferences, p => p.Function == SuperpowersFunction.Review && p.Model == "Claude Opus 5.5");
    }

    [Fact]
    public void LoadIgnoresALeftoverFamilyPropertyFromAnOlderFileFormat()
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
        Assert.Contains(loaded.Preferences, p => p.Function == SuperpowersFunction.Review && p.Model == "Claude Opus 5.5");
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
