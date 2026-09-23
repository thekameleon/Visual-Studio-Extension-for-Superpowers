using System.Text.Json.Serialization;

namespace TheKameleon.Superpowers.Core.Contracts.Settings;

public sealed record SuperpowersSettings
{
    public const int CurrentSchemaVersion = 1;

    [JsonConstructor]
    public SuperpowersSettings(
        int schemaVersion = CurrentSchemaVersion,
        ExecutionMode defaultExecutionMode = ExecutionMode.Guided,
        ReleaseChannelFilter releaseChannelFilter = ReleaseChannelFilter.StableOnly,
        bool enableUpdateChecks = true,
        int maxContextCharacters = 20000,
        int historyRetentionDays = 30,
        bool retainSensitiveHistoryContent = false,
        IReadOnlyList<ExclusionRule>? exclusions = null,
        CriticalWarningPolicy? criticalWarningPolicy = null,
        IReadOnlyList<CustomCommandAllowlistEntry>? customCommandAllowlist = null)
    {
        if (schemaVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(schemaVersion), "Schema version must be greater than zero.");
        }

        if (!Enum.IsDefined(defaultExecutionMode))
        {
            throw new ArgumentOutOfRangeException(nameof(defaultExecutionMode), "Execution mode is invalid.");
        }

        if (!Enum.IsDefined(releaseChannelFilter))
        {
            throw new ArgumentOutOfRangeException(nameof(releaseChannelFilter), "Release channel filter is invalid.");
        }

        if (maxContextCharacters <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxContextCharacters), "Context character budget must be greater than zero.");
        }

        if (historyRetentionDays <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(historyRetentionDays), "History retention must be greater than zero days.");
        }

        SchemaVersion = schemaVersion;
        DefaultExecutionMode = defaultExecutionMode;
        ReleaseChannelFilter = releaseChannelFilter;
        EnableUpdateChecks = enableUpdateChecks;
        MaxContextCharacters = maxContextCharacters;
        HistoryRetentionDays = historyRetentionDays;
        RetainSensitiveHistoryContent = retainSensitiveHistoryContent;
        Exclusions = (exclusions ?? Array.Empty<ExclusionRule>()).ToArray();
        CriticalWarningPolicy = criticalWarningPolicy ?? new CriticalWarningPolicy();
        CustomCommandAllowlist = (customCommandAllowlist ?? Array.Empty<CustomCommandAllowlistEntry>()).ToArray();
    }

    public int SchemaVersion { get; }

    public ExecutionMode DefaultExecutionMode { get; }

    public ReleaseChannelFilter ReleaseChannelFilter { get; }

    public bool EnableUpdateChecks { get; }

    public int MaxContextCharacters { get; }

    public int HistoryRetentionDays { get; }

    public bool RetainSensitiveHistoryContent { get; }

    public IReadOnlyList<ExclusionRule> Exclusions { get; }

    public CriticalWarningPolicy CriticalWarningPolicy { get; }

    public IReadOnlyList<CustomCommandAllowlistEntry> CustomCommandAllowlist { get; }
}
