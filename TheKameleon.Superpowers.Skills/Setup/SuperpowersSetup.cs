using TheKameleon.Superpowers.Skills.Bootstrap;
using TheKameleon.Superpowers.Skills.Install;
using TheKameleon.Superpowers.Skills.Models;

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

    public SetupResult Install(InstalledRelease release, SkillArchiveReadResult source, bool overwriteEdited, ModelPreferences? modelPreferences = null)
    {
        using var _ = InstallLock.Acquire(LockTimeout);
        var load = store.Load();
        if (load.Status == InstallStateStatus.Corrupt)
        {
            return new SetupResult(SetupStatus.Failed, new[] { CorruptMessage }, load.State);
        }

        return InstallCore(load.State, release, source, overwriteEdited, modelPreferences ?? ModelPreferences.Empty);
    }

    public SetupResult Repair(InstalledRelease release, SkillArchiveReadResult source, ModelPreferences? modelPreferences = null)
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
            && string.Equals(ContentHash.OfFile(paths.AgentFile), ContentHash.Of(System.Text.Encoding.UTF8.GetBytes(AgentFileWriter.BuildContent(SuperpowersFunction.General))), StringComparison.OrdinalIgnoreCase))
        {
            seed = seed with { AgentFile = new InstalledAgentFile(ContentHash.OfFile(paths.AgentFile), BootstrapText.Version) };
        }

        return InstallCore(seed, release, source, overwriteEdited: false, modelPreferences ?? ModelPreferences.Empty);
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
        foreach (var functionAgentFile in load.State.FunctionAgentFiles)
        {
            if (agent.RemoveFunctionAgent(Enum.Parse<SuperpowersFunction>(functionAgentFile.Function), functionAgentFile).Status == AgentFileStatus.EditedNotRemoved)
            {
                messages.Add($"Your edited superpowers-{functionAgentFile.Function.ToLowerInvariant()}.agent.md was left in place.");
            }
        }

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

    public SetupResult RefreshAgent(ModelPreferences? modelPreferences = null)
    {
        using var _ = InstallLock.Acquire(LockTimeout);
        var load = store.Load();
        if (load.Status != InstallStateStatus.Loaded || load.State.AgentFile is null)
        {
            return new SetupResult(SetupStatus.Succeeded, Array.Empty<string>(), load.State);
        }

        var generalModel = modelPreferences?.Preferences.FirstOrDefault(p => p.Function == SuperpowersFunction.General)?.Model;
        var outcome = agent.Write(load.State.AgentFile, overwriteEdited: false, generalModel);
        if (outcome.Status == AgentFileStatus.EditedKept)
        {
            return new SetupResult(SetupStatus.Partial, new[] { "A newer Superpowers agent is available, but your edited superpowers.agent.md was kept. Use Install to replace it." }, load.State);
        }

        var state = load.State with { AgentFile = outcome.Agent };
        store.Save(state);
        return new SetupResult(SetupStatus.Succeeded, Array.Empty<string>(), state);
    }

    private SetupResult InstallCore(InstallState seed, InstalledRelease release, SkillArchiveReadResult source, bool overwriteEdited, ModelPreferences modelPreferences)
    {
        var messages = source.Problems.ToList();
        var outcome = skills.Install(source.Skills, seed.Skills, overwriteEdited);
        messages.AddRange(outcome.Issues.Select(issue => issue.Message));
        if (!outcome.Succeeded)
        {
            messages.Add(outcome.FailureReason!);
            return new SetupResult(SetupStatus.Failed, messages, seed);
        }

        var generalModel = modelPreferences.Preferences.FirstOrDefault(p => p.Function == SuperpowersFunction.General)?.Model;
        var agentOutcome = agent.Write(seed.AgentFile, overwriteEdited && seed.AgentFile is not null, generalModel);
        if (agentOutcome.Status == AgentFileStatus.EditedKept)
        {
            messages.Add("Your edited superpowers.agent.md was kept.");
        }

        var knownFunctionFiles = seed.FunctionAgentFiles.ToDictionary(f => f.Function, StringComparer.Ordinal);
        var functionAgentFiles = new List<InstalledFunctionAgentFile>();
        var desiredFunctions = modelPreferences.Preferences
            .Where(p => p.Function != SuperpowersFunction.General)
            .Select(p => p.Function)
            .ToHashSet();
        foreach (var preference in modelPreferences.Preferences.Where(p => p.Function != SuperpowersFunction.General))
        {
            knownFunctionFiles.TryGetValue(preference.Function.ToString(), out var recorded);
            var functionOutcome = agent.WriteFunctionAgent(preference.Function, preference.Model, recorded, overwriteEdited);
            if (functionOutcome.Status == AgentFileStatus.EditedKept)
            {
                messages.Add($"Your edited superpowers-{preference.Function.ToString().ToLowerInvariant()}.agent.md was kept.");
            }

            if (functionOutcome.Agent is not null)
            {
                functionAgentFiles.Add(functionOutcome.Agent);
            }
        }

        foreach (var orphan in seed.FunctionAgentFiles.Where(f => !desiredFunctions.Contains(Enum.Parse<SuperpowersFunction>(f.Function))))
        {
            var removeOutcome = agent.RemoveFunctionAgent(Enum.Parse<SuperpowersFunction>(orphan.Function), orphan);
            if (removeOutcome.Status == AgentFileStatus.EditedNotRemoved)
            {
                messages.Add($"Your edited superpowers-{orphan.Function.ToLowerInvariant()}.agent.md was left in place.");
                functionAgentFiles.Add(orphan);
            }
        }

        var state = seed with
        {
            Release = release,
            Skills = outcome.Installed,
            AgentFile = agentOutcome.Agent,
            FunctionAgentFiles = functionAgentFiles,
        };
        store.Save(state);
        return new SetupResult(messages.Count == 0 ? SetupStatus.Succeeded : SetupStatus.Partial, messages, state);
    }
}
