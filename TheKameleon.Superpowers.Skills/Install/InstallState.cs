namespace TheKameleon.Superpowers.Skills.Install;

public sealed record InstallState
{
    public const int CurrentSchemaVersion = 2;

    public static InstallState Empty { get; } = new();

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public InstalledRelease? Release { get; init; }

    public IReadOnlyList<InstalledSkill> Skills { get; init; } = Array.Empty<InstalledSkill>();

    public InstalledAgentFile? AgentFile { get; init; }

    public IReadOnlyList<InstalledFunctionAgentFile> FunctionAgentFiles { get; init; } = Array.Empty<InstalledFunctionAgentFile>();

    public AlwaysOnState AlwaysOn { get; init; } = new(false, false);
}

public sealed record InstalledRelease(string Tag, string Commit, string Source);

public sealed record InstalledSkill(string Name, IReadOnlyDictionary<string, string> FileHashes);

public sealed record InstalledAgentFile(string Sha256, int BootstrapVersion);

public sealed record InstalledFunctionAgentFile(string Function, string Sha256, int BootstrapVersion);

public sealed record AlwaysOnState(bool Enabled, bool CreatedFile);
