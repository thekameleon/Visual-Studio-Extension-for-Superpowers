using TheKameleon.Superpowers.Skills.Bootstrap;
using TheKameleon.Superpowers.Skills.Install;

namespace TheKameleon.Superpowers.Skills.Setup;

public sealed record InstallPreview(IReadOnlyList<string> Conflicts, IReadOnlyList<string> EditedSkills, bool AgentFileEdited);

public enum SetupStatus
{
    Succeeded,
    Partial,
    Failed,
}

public sealed record SetupResult(SetupStatus Status, IReadOnlyList<string> Messages, InstallState State);

/// <summary>The one entry point the UI uses. Every mutating call holds the cross-process install lock.</summary>
public sealed class SuperpowersSetup(ProfilePaths paths)
{
    private static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(30);
    private const string CorruptMessage = "The Superpowers install record is unreadable. Use Repair.";

    private readonly InstallStateStore store = new(paths);
    private readonly SkillInstaller skills = new(paths);
    private readonly AgentFileWriter agent = new(paths);
    private readonly AlwaysOnInstructionsFile alwaysOn = new(paths);

    public InstallStateLoad LoadState() => store.Load();

    public InstallPreview Preview(IReadOnlyList<SkillPackage> packages)
    {
        var state = store.Load().State;
        return new InstallPreview(
            skills.FindConflicts(packages, state.Skills),
            state.Skills.Where(skills.IsEdited).Select(skill => skill.Name).ToArray(),
            state.AgentFile is not null && agent.IsEdited(state.AgentFile));
    }

    public SetupResult Install(InstalledRelease release, SkillArchiveReadResult source, bool overwriteEdited)
    {
        using var _ = InstallLock.Acquire(LockTimeout);
        var load = store.Load();
        if (load.Status == InstallStateStatus.Corrupt)
        {
            return new SetupResult(SetupStatus.Failed, new[] { CorruptMessage }, load.State);
        }

        return InstallCore(load.State, release, source, overwriteEdited);
    }

    public SetupResult Repair(InstalledRelease release, SkillArchiveReadResult source)
    {
        using var _ = InstallLock.Acquire(LockTimeout);
        var load = store.Load();
        var known = load.Status == InstallStateStatus.Loaded
            ? load.State
            : InstallState.Empty with { AlwaysOn = new AlwaysOnState(alwaysOn.IsBlockPresent(), false) };
        var knownNames = known.Skills.Select(skill => skill.Name).ToHashSet(StringComparer.Ordinal);
        var adopted = source.Skills
            .Where(package => !knownNames.Contains(package.Name) && skills.MatchesPackage(package))
            .Select(SkillInstaller.Describe);
        var seed = known with { Skills = known.Skills.Concat(adopted).ToArray() };
        if (seed.AgentFile is null && File.Exists(paths.AgentFile)
            && string.Equals(ContentHash.OfFile(paths.AgentFile), ContentHash.Of(System.Text.Encoding.UTF8.GetBytes(AgentFileWriter.BuildContent())), StringComparison.OrdinalIgnoreCase))
        {
            seed = seed with { AgentFile = new InstalledAgentFile(ContentHash.OfFile(paths.AgentFile), BootstrapText.Version) };
        }

        return InstallCore(seed, release, source, overwriteEdited: false);
    }

    public SetupResult Remove()
    {
        using var _ = InstallLock.Acquire(LockTimeout);
        var load = store.Load();
        if (load.Status == InstallStateStatus.Corrupt)
        {
            return new SetupResult(SetupStatus.Failed, new[] { CorruptMessage }, load.State);
        }

        var messages = skills.Remove(load.State.Skills).Select(issue => issue.Message).ToList();
        if (agent.Remove(load.State.AgentFile).Status == AgentFileStatus.EditedNotRemoved)
        {
            messages.Add("Your edited superpowers.agent.md was left in place.");
        }

        if (alwaysOn.IsBlockPresent() && alwaysOn.Disable(load.State.AlwaysOn).Status == AlwaysOnStatus.MalformedMarkers)
        {
            messages.Add("The always-on block in copilot-instructions.md has damaged markers; remove it by hand.");
        }

        store.Delete();
        return new SetupResult(messages.Count == 0 ? SetupStatus.Succeeded : SetupStatus.Partial, messages, InstallState.Empty);
    }

    public SetupResult SetAlwaysOn(bool enabled)
    {
        using var _ = InstallLock.Acquire(LockTimeout);
        var load = store.Load();
        if (load.Status == InstallStateStatus.Corrupt)
        {
            return new SetupResult(SetupStatus.Failed, new[] { CorruptMessage }, load.State);
        }

        var outcome = enabled ? alwaysOn.Enable(load.State.AlwaysOn) : alwaysOn.Disable(load.State.AlwaysOn);
        if (outcome.Status == AlwaysOnStatus.MalformedMarkers)
        {
            return new SetupResult(SetupStatus.Failed, new[] { "copilot-instructions.md has damaged Superpowers markers; fix or remove them by hand, then try again." }, load.State);
        }

        var state = load.State with { AlwaysOn = outcome.State };
        store.Save(state);
        return new SetupResult(SetupStatus.Succeeded, Array.Empty<string>(), state);
    }

    public SetupResult RefreshAgent()
    {
        using var _ = InstallLock.Acquire(LockTimeout);
        var load = store.Load();
        if (load.Status != InstallStateStatus.Loaded || load.State.AgentFile is null)
        {
            return new SetupResult(SetupStatus.Succeeded, Array.Empty<string>(), load.State);
        }

        var outcome = agent.Write(load.State.AgentFile, overwriteEdited: false);
        if (outcome.Status == AgentFileStatus.EditedKept)
        {
            return new SetupResult(SetupStatus.Partial, new[] { "A newer Superpowers agent is available, but your edited superpowers.agent.md was kept. Use Install to replace it." }, load.State);
        }

        var state = load.State with { AgentFile = outcome.Agent };
        store.Save(state);
        return new SetupResult(SetupStatus.Succeeded, Array.Empty<string>(), state);
    }

    private SetupResult InstallCore(InstallState seed, InstalledRelease release, SkillArchiveReadResult source, bool overwriteEdited)
    {
        var messages = source.Problems.ToList();
        var outcome = skills.Install(source.Skills, seed.Skills, overwriteEdited);
        messages.AddRange(outcome.Issues.Select(issue => issue.Message));
        if (!outcome.Succeeded)
        {
            messages.Add(outcome.FailureReason!);
            return new SetupResult(SetupStatus.Failed, messages, seed);
        }

        var agentOutcome = agent.Write(seed.AgentFile, overwriteEdited && seed.AgentFile is not null);
        if (agentOutcome.Status == AgentFileStatus.EditedKept)
        {
            messages.Add("Your edited superpowers.agent.md was kept.");
        }

        var state = seed with { Release = release, Skills = outcome.Installed, AgentFile = agentOutcome.Agent };
        store.Save(state);
        return new SetupResult(messages.Count == 0 ? SetupStatus.Succeeded : SetupStatus.Partial, messages, state);
    }
}
