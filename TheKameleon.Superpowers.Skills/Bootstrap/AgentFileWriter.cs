using System.Text;
using TheKameleon.Superpowers.Skills.Install;

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

public sealed class AgentFileWriter(ProfilePaths paths)
{
    private const string Description = "Agent mode with Superpowers skills — brainstorming, planning, TDD, systematic debugging, code review and verification.";

    public static string BuildContent()
    {
        return "---\nname: Superpowers\ndescription: " + Description + "\n---\n\n" + BootstrapText.Body.ReplaceLineEndings("\n") + "\n";
    }

    public bool IsEdited(InstalledAgentFile? recorded)
    {
        return File.Exists(paths.AgentFile)
            && (recorded is null || !string.Equals(ContentHash.OfFile(paths.AgentFile), recorded.Sha256, StringComparison.OrdinalIgnoreCase));
    }

    public AgentFileOutcome Write(InstalledAgentFile? recorded, bool overwriteEdited)
    {
        var content = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(BuildContent());
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
