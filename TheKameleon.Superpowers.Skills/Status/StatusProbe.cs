using TheKameleon.Superpowers.Skills.Bootstrap;
using TheKameleon.Superpowers.Skills.Install;
using TheKameleon.Superpowers.Skills.Models;

namespace TheKameleon.Superpowers.Skills.Status;

public sealed class StatusProbe(ProfilePaths paths)
{
    public IReadOnlyList<StatusCheck> Run(string? copilotLogDirectory)
    {
        var load = new InstallStateStore(paths).Load();
        if (load.Status == InstallStateStatus.Corrupt)
        {
            return new[] { new StatusCheck("Install record", StatusLevel.Fail, "The Superpowers install record is unreadable. Use Repair.") };
        }

        var state = load.State;
        if (state.Release is null)
        {
            return new[] { new StatusCheck("Skills", StatusLevel.Warning, "Superpowers is not installed yet. Choose a release and select Install.") };
        }

        var checks = new List<StatusCheck> { SkillsCheck(state) };

        var duplicates = state.Skills
            .Select(skill => skill.Name)
            .Where(name => paths.OtherPersonalSkillRoots.Any(root => Directory.Exists(Path.Combine(root, name))))
            .ToArray();
        if (duplicates.Length > 0)
        {
            checks.Add(new StatusCheck("Duplicate skills", StatusLevel.Warning,
                $"These skills also exist in ~/.claude/skills or ~/.agents/skills, so Copilot may see two copies: {string.Join(", ", duplicates)}."));
        }

        checks.Add(AgentCheck(state));

        var missingFunctionAgents = state.FunctionAgentFiles
            .Where(agent => !File.Exists(AgentFileWriter.FunctionAgentFilePath(paths, Enum.Parse<SuperpowersFunction>(agent.Function))))
            .Select(agent => agent.Function)
            .ToArray();
        if (missingFunctionAgents.Length > 0)
        {
            checks.Add(new StatusCheck("Per-function agents", StatusLevel.Warning,
                $"These function agent files are missing and will fall back to the General agent's model (or none): {string.Join(", ", missingFunctionAgents)}. Use Repair to recreate them."));
        }

        checks.Add(AlwaysOnCheck(state));

        if (copilotLogDirectory is not null)
        {
            checks.Add(CopilotLogDiagnostic.Diagnose(copilotLogDirectory, paths));
        }

        return checks;
    }

    private StatusCheck SkillsCheck(InstallState state)
    {
        var installer = new SkillInstaller(paths);
        if (state.Skills.Count == 0)
        {
            return new StatusCheck("Skills", StatusLevel.Fail, $"No skills from {state.Release!.Tag} could be installed — every skill folder already existed and was not created by Superpowers. If you already installed these skills another way (for example for GitHub Copilot CLI), use Repair to adopt matching folders.");
        }

        var missing = state.Skills.Where(skill => !Directory.Exists(Path.Combine(paths.SkillsRoot, skill.Name))).Select(skill => skill.Name).ToArray();
        var edited = state.Skills.Where(installer.IsEdited).Select(skill => skill.Name).ToArray();
        var summary = $"{state.Skills.Count} skills from {state.Release!.Tag} are installed in {paths.SkillsRoot}.";
        if (missing.Length > 0)
        {
            return new StatusCheck("Skills", StatusLevel.Fail, $"{summary} Missing: {string.Join(", ", missing)}. Use Repair.");
        }

        return edited.Length > 0
            ? new StatusCheck("Skills", StatusLevel.Warning, $"{summary} Edited by you: {string.Join(", ", edited)}.")
            : new StatusCheck("Skills", StatusLevel.Pass, summary);
    }

    private StatusCheck AgentCheck(InstallState state)
    {
        const string title = "Superpowers agent";
        if (state.AgentFile is null || !File.Exists(paths.AgentFile))
        {
            return new StatusCheck(title, StatusLevel.Fail, "The Superpowers agent file is missing. Use Repair.");
        }

        return new AgentFileWriter(paths).IsEdited(state.AgentFile)
            ? new StatusCheck(title, StatusLevel.Warning, "You have edited superpowers.agent.md; updates will not replace it automatically.")
            : new StatusCheck(title, StatusLevel.Pass, $"Select Superpowers in the Copilot agent picker (bootstrap version {state.AgentFile.BootstrapVersion}).");
    }

    private StatusCheck AlwaysOnCheck(InstallState state)
    {
        const string title = "Always-on";
        var blockPresent = new AlwaysOnInstructionsFile(paths).IsBlockPresent();
        if (!state.AlwaysOn.Enabled)
        {
            return blockPresent
                ? new StatusCheck(title, StatusLevel.Warning, "Off, but a Superpowers block is still present in copilot-instructions.md. Use Repair or Remove to clean it up.")
                : new StatusCheck(title, StatusLevel.Pass, "Off. Superpowers applies only when you select the Superpowers agent.");
        }

        return blockPresent
            ? new StatusCheck(title, StatusLevel.Pass, "On. Every Agent-mode chat receives the Superpowers bootstrap.")
            : new StatusCheck(title, StatusLevel.Fail, "On, but the block is missing from copilot-instructions.md. Turn always-on off and on again.");
    }
}
