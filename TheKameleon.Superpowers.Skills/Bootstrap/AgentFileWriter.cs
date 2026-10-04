using System.Text;
using TheKameleon.Superpowers.Skills.Install;
using TheKameleon.Superpowers.Skills.Models;

namespace TheKameleon.Superpowers.Skills.Bootstrap;

public enum AgentFileStatus
{
    Written,
    UpToDate,
    EditedKept,
    Removed,
    EditedNotRemoved,
    Missing,
}

public sealed record AgentFileOutcome(AgentFileStatus Status, InstalledAgentFile? Agent);

public sealed record FunctionAgentFileOutcome(AgentFileStatus Status, InstalledFunctionAgentFile? Agent);

public sealed class AgentFileWriter(ProfilePaths paths)
{
    private const string Description = "Agent mode with Superpowers skills — brainstorming, planning, TDD, systematic debugging, code review and verification.";

    public static string BuildContent(SuperpowersFunction function, ModelPreferences? modelPreferences = null)
    {
        var name = function == SuperpowersFunction.General ? "Superpowers" : $"Superpowers ({function})";
        var description = function == SuperpowersFunction.General ? Description : $"{Description} Configured for the {function} step.";
        var frontMatter = "---\nname: " + name + "\ndescription: " + description;
        var body = BootstrapText.Body.ReplaceLineEndings("\n") + "\n";
        var guidance = BuildSubagentModelGuidance(modelPreferences);
        return frontMatter + "\n---\n\n" + body + (guidance.Length == 0 ? string.Empty : "\n" + guidance);
    }

    /// <summary>Lists the user's suggested Copilot CLI sub-agent models for each step. Empty when none are set.</summary>
    public static string BuildSubagentModelGuidance(ModelPreferences? modelPreferences)
    {
        var preferences = (modelPreferences?.Preferences ?? Array.Empty<ModelPreference>())
            .Where(p => p.Models.Count > 0)
            .OrderBy(p => p.Function)
            .ToArray();
        if (preferences.Length == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        builder.Append("Suggested sub-agent models (set by the user; apply only to Copilot CLI sub-agents, never to this chat):\n");
        foreach (var preference in preferences)
        {
            var step = preference.Function == SuperpowersFunction.General ? "Any other step" : preference.Function.ToString();
            builder.Append("- ").Append(step).Append(": ").Append(string.Join(", ", preference.Models.Select(m => "`" + m + "`"))).Append('\n');
        }

        return builder.ToString();
    }

    public static string FunctionAgentFilePath(ProfilePaths paths, SuperpowersFunction function) =>
        function == SuperpowersFunction.General
            ? paths.AgentFile
            : Path.Combine(paths.AgentsRoot, $"superpowers-{function.ToString().ToLowerInvariant()}.agent.md");

    public bool IsEdited(InstalledAgentFile? recorded)
    {
        return File.Exists(paths.AgentFile)
            && (recorded is null || !string.Equals(ContentHash.OfFile(paths.AgentFile), recorded.Sha256, StringComparison.OrdinalIgnoreCase));
    }

    public AgentFileOutcome Write(InstalledAgentFile? recorded, bool overwriteEdited, ModelPreferences? modelPreferences = null)
    {
        var content = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(BuildContent(SuperpowersFunction.General, modelPreferences));
        var hash = ContentHash.Of(content);
        var current = new InstalledAgentFile(hash, BootstrapText.Version);

        if (File.Exists(paths.AgentFile))
        {
            var onDisk = ContentHash.OfFile(paths.AgentFile);
            if (string.Equals(onDisk, hash, StringComparison.OrdinalIgnoreCase))
            {
                return new AgentFileOutcome(AgentFileStatus.UpToDate, current);
            }

            if (IsEdited(recorded) && !overwriteEdited)
            {
                return new AgentFileOutcome(AgentFileStatus.EditedKept, recorded);
            }
        }

        Directory.CreateDirectory(paths.AgentsRoot);
        File.WriteAllBytes(paths.AgentFile, content);
        return new AgentFileOutcome(AgentFileStatus.Written, current);
    }

    public bool IsFunctionAgentEdited(SuperpowersFunction function, InstalledFunctionAgentFile? recorded)
    {
        var path = FunctionAgentFilePath(paths, function);
        return File.Exists(path)
            && (recorded is null || !string.Equals(ContentHash.OfFile(path), recorded.Sha256, StringComparison.OrdinalIgnoreCase));
    }

    public FunctionAgentFileOutcome RemoveFunctionAgent(SuperpowersFunction function, InstalledFunctionAgentFile? recorded)
    {
        var path = FunctionAgentFilePath(paths, function);
        if (!File.Exists(path))
        {
            return new FunctionAgentFileOutcome(AgentFileStatus.Missing, null);
        }

        if (IsFunctionAgentEdited(function, recorded))
        {
            return new FunctionAgentFileOutcome(AgentFileStatus.EditedNotRemoved, recorded);
        }

        File.Delete(path);
        return new FunctionAgentFileOutcome(AgentFileStatus.Removed, null);
    }

    public AgentFileOutcome Remove(InstalledAgentFile? recorded)
    {
        if (!File.Exists(paths.AgentFile))
        {
            return new AgentFileOutcome(AgentFileStatus.Missing, null);
        }

        if (IsEdited(recorded))
        {
            return new AgentFileOutcome(AgentFileStatus.EditedNotRemoved, recorded);
        }

        File.Delete(paths.AgentFile);
        return new AgentFileOutcome(AgentFileStatus.Removed, null);
    }
}
