namespace TheKameleon.Superpowers.Skills.Models;

public enum SuperpowersFunction
{
    General,
    Plan,
    Execute,
    Debug,
    Tdd,
    Review,
    Verify,
    Refactor,
    Finish,
}

public sealed record ModelPreference(SuperpowersFunction Function, string Model);

public sealed record ModelPreferences
{
    public const int CurrentSchemaVersion = 1;

    public static ModelPreferences Empty { get; } = new();

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public CopilotPlan Plan { get; init; } = CopilotPlan.Unspecified;

    public IReadOnlyList<ModelPreference> Preferences { get; init; } = Array.Empty<ModelPreference>();
}
