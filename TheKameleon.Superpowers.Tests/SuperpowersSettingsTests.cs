using System.Text.Json;
using TheKameleon.Superpowers.Core.Contracts.Settings;

namespace TheKameleon.Superpowers.Tests;

public sealed class SuperpowersSettingsTests
{
    [Fact]
    public void DefaultsMatchApprovedPortableSettingsBaseline()
    {
        var settings = new SuperpowersSettings();

        Assert.Equal(SuperpowersSettings.CurrentSchemaVersion, settings.SchemaVersion);
        Assert.Equal(ExecutionMode.Guided, settings.DefaultExecutionMode);
        Assert.Equal(ReleaseChannelFilter.StableOnly, settings.ReleaseChannelFilter);
        Assert.True(settings.EnableUpdateChecks);
        Assert.Equal(20000, settings.MaxContextCharacters);
        Assert.Equal(30, settings.HistoryRetentionDays);
        Assert.False(settings.RetainSensitiveHistoryContent);
        Assert.Empty(settings.Exclusions);
        Assert.False(settings.CriticalWarningPolicy.TreatWarningsAsCritical);
        Assert.Empty(settings.CriticalWarningPolicy.WarningCodes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RejectsInvalidSchemaVersion(int schemaVersion)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new SuperpowersSettings(schemaVersion: schemaVersion));

        Assert.Equal("schemaVersion", exception.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-25)]
    public void RejectsInvalidContextBudget(int budget)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new SuperpowersSettings(maxContextCharacters: budget));

        Assert.Equal("maxContextCharacters", exception.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-7)]
    public void RejectsInvalidHistoryRetention(int days)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new SuperpowersSettings(historyRetentionDays: days));

        Assert.Equal("historyRetentionDays", exception.ParamName);
    }

    [Fact]
    public void RejectsUnknownExecutionMode()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SuperpowersSettings(defaultExecutionMode: (ExecutionMode)99));

        Assert.Equal("defaultExecutionMode", exception.ParamName);
    }

    [Fact]
    public void RejectsUnknownReleaseChannelFilter()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SuperpowersSettings(releaseChannelFilter: (ReleaseChannelFilter)99));

        Assert.Equal("releaseChannelFilter", exception.ParamName);
    }

    [Fact]
    public void RejectsBlankExclusionPattern()
    {
        var exception = Assert.Throws<ArgumentException>(() => new ExclusionRule(" "));

        Assert.Equal("pattern", exception.ParamName);
    }

    [Fact]
    public void CriticalWarningPolicyNormalizesAndDeduplicatesCodes()
    {
        var policy = new CriticalWarningPolicy(new[] { "CS8602", "cs8602", "", "NU1900" });

        Assert.True(policy.TreatWarningsAsCritical);
        Assert.Equal(new[] { "CS8602", "NU1900" }, policy.WarningCodes);
    }

    [Fact]
    public void SettingsRoundTripThroughJson()
    {
        var settings = new SuperpowersSettings(
            defaultExecutionMode: ExecutionMode.ApprovalRequired,
            releaseChannelFilter: ReleaseChannelFilter.IncludePrerelease,
            enableUpdateChecks: false,
            maxContextCharacters: 4096,
            historyRetentionDays: 14,
            retainSensitiveHistoryContent: true,
            exclusions: new[]
            {
                new ExclusionRule("**/*.env", isBuiltIn: true),
                new ExclusionRule("**/*.key")
            },
            criticalWarningPolicy: new CriticalWarningPolicy(new[] { "CS8602", "NU1900" }));

        var json = JsonSerializer.Serialize(settings);
        var roundTripped = JsonSerializer.Deserialize<SuperpowersSettings>(json);

        Assert.NotNull(roundTripped);
        Assert.Equal(settings.SchemaVersion, roundTripped.SchemaVersion);
        Assert.Equal(settings.DefaultExecutionMode, roundTripped.DefaultExecutionMode);
        Assert.Equal(settings.ReleaseChannelFilter, roundTripped.ReleaseChannelFilter);
        Assert.Equal(settings.EnableUpdateChecks, roundTripped.EnableUpdateChecks);
        Assert.Equal(settings.MaxContextCharacters, roundTripped.MaxContextCharacters);
        Assert.Equal(settings.HistoryRetentionDays, roundTripped.HistoryRetentionDays);
        Assert.Equal(settings.RetainSensitiveHistoryContent, roundTripped.RetainSensitiveHistoryContent);
        Assert.Equal(settings.Exclusions, roundTripped.Exclusions);
        Assert.Equal(settings.CriticalWarningPolicy.WarningCodes, roundTripped.CriticalWarningPolicy.WarningCodes);
        Assert.Equal(settings.CriticalWarningPolicy.TreatWarningsAsCritical, roundTripped.CriticalWarningPolicy.TreatWarningsAsCritical);
    }

    [Fact]
    public void UnknownJsonPropertiesAreIgnoredDuringRoundTrip()
    {
        const string json = """
        {
          "SchemaVersion": 1,
          "DefaultExecutionMode": 0,
          "ReleaseChannelFilter": 0,
          "EnableUpdateChecks": true,
          "MaxContextCharacters": 1000,
          "HistoryRetentionDays": 10,
          "RetainSensitiveHistoryContent": true,
          "Exclusions": [],
          "CriticalWarningPolicy": { "WarningCodes": [] },
          "FutureSetting": "ignored"
        }
        """;

        var deserialized = JsonSerializer.Deserialize<SuperpowersSettings>(json);

        Assert.NotNull(deserialized);
        Assert.Equal(ExecutionMode.Guided, deserialized.DefaultExecutionMode);
        Assert.Equal(ReleaseChannelFilter.StableOnly, deserialized.ReleaseChannelFilter);
        Assert.Equal(1000, deserialized.MaxContextCharacters);
        Assert.Equal(10, deserialized.HistoryRetentionDays);
        Assert.True(deserialized.RetainSensitiveHistoryContent);
    }
}
