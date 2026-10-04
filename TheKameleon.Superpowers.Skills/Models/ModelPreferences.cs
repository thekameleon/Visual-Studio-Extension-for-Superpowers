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

/// <summary>Models suggested for Copilot CLI sub-agents dispatched during one Superpowers step.
/// The main Copilot Chat model is never set; it stays whatever the user picked in Chat.</summary>
public sealed record ModelPreference(SuperpowersFunction Function, IReadOnlyList<string> Models);

public sealed record ModelPreferences
{
    public const int CurrentSchemaVersion = 2;

    public static ModelPreferences Empty { get; } = new();

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public CopilotPlan Plan { get; init; } = CopilotPlan.Unspecified;

    public IReadOnlyList<ModelPreference> Preferences { get; init; } = Array.Empty<ModelPreference>();
}
