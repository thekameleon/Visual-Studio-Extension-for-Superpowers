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

    public static string BuildContent(string? model = null)
    {
        var frontMatter = "---\nname: Superpowers\ndescription: " + Description;
        if (!string.IsNullOrWhiteSpace(model))
        {
            frontMatter += "\nmodel: " + model;
        }

        return frontMatter + "\n---\n\n" + BootstrapText.Body.ReplaceLineEndings("\n") + "\n";
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

    public AgentFileOutcome Write(InstalledAgentFile? recorded, bool overwriteEdited, string? model = null)
    {
        var content = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(BuildContent(model));
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

    public FunctionAgentFileOutcome WriteFunctionAgent(SuperpowersFunction function, string? model, InstalledFunctionAgentFile? recorded, bool overwriteEdited)
    {
        var path = FunctionAgentFilePath(paths, function);
        var content = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(BuildContent(model));
        var hash = ContentHash.Of(content);
        var current = new InstalledFunctionAgentFile(function.ToString(), hash, BootstrapText.Version);

        if (File.Exists(path))
        {
            var onDisk = ContentHash.OfFile(path);
            if (string.Equals(onDisk, hash, StringComparison.OrdinalIgnoreCase))
            {
                return new FunctionAgentFileOutcome(AgentFileStatus.UpToDate, current);
            }

            if (IsFunctionAgentEdited(function, recorded) && !overwriteEdited)
            {
                return new FunctionAgentFileOutcome(AgentFileStatus.EditedKept, recorded);
            }
        }

        Directory.CreateDirectory(paths.AgentsRoot);
        File.WriteAllBytes(path, content);
        return new FunctionAgentFileOutcome(AgentFileStatus.Written, current);
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
