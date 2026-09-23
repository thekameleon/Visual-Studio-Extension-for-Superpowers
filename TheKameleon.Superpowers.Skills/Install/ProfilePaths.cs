namespace TheKameleon.Superpowers.Skills.Install;

/// <summary>Every location the extension reads or writes, rooted so tests can use a temporary profile.</summary>
public sealed record ProfilePaths(string UserProfile, string LocalAppData)
{
    public static ProfilePaths ForCurrentUser() => new(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));

    public string SkillsRoot => Path.Combine(UserProfile, ".copilot", "skills");

    public string StagingRoot => Path.Combine(UserProfile, ".copilot", ".superpowers-staging");

    public string AgentsRoot => Path.Combine(UserProfile, ".github", "agents");

    public string AgentFile => Path.Combine(AgentsRoot, "superpowers.agent.md");

    public string UserInstructionsFile => Path.Combine(UserProfile, "copilot-instructions.md");

    public string StateDirectory => Path.Combine(LocalAppData, "TheKameleon.Superpowers");

    public string StateFile => Path.Combine(StateDirectory, "install-state.json");

    public string DownloadsRoot => Path.Combine(StateDirectory, "downloads");

    public IReadOnlyList<string> OtherPersonalSkillRoots => new[]
    {
        Path.Combine(UserProfile, ".claude", "skills"),
        Path.Combine(UserProfile, ".agents", "skills"),
    };
}
