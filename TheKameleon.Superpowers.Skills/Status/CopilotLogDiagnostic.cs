using TheKameleon.Superpowers.Skills.Install;

namespace TheKameleon.Superpowers.Skills.Status;

public enum StatusLevel
{
    Pass,
    Warning,
    Fail,
    Unknown,
}

public sealed record StatusCheck(string Title, StatusLevel Level, string Message);

/// <summary>Best-effort read of Copilot's undocumented chat log. Never reports Fail; log content is never returned.</summary>
public static class CopilotLogDiagnostic
{
    public const string Title = "Copilot discovery (diagnostic)";

    public static string DefaultLogDirectory => Path.Combine(Path.GetTempPath(), "VSGitHubCopilotLogs");

    public static StatusCheck Diagnose(string logDirectory, ProfilePaths paths)
    {
        try
        {
            var logs = Directory.Exists(logDirectory)
                ? new DirectoryInfo(logDirectory).EnumerateFiles("*.chat.log").OrderByDescending(file => file.LastWriteTimeUtc).Take(5).ToList()
                : new List<FileInfo>();
            if (logs.Count == 0)
            {
                return new StatusCheck(Title, StatusLevel.Unknown, "No Copilot chat log was found. Open Copilot Chat, then select Refresh.");
            }

            var skillsNeedle = "skill files in: " + paths.SkillsRoot;
            var agentNeedle = "Registered custom agent: " + paths.AgentFile;
            foreach (var log in logs)
            {
                bool foundSkills = false, foundAgent = false;
                using var stream = new FileStream(log.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream);
                while (reader.ReadLine() is { } line && !(foundSkills && foundAgent))
                {
                    foundSkills |= line.Contains("Found ", StringComparison.Ordinal)
                        && !line.Contains("Found 0 ", StringComparison.Ordinal)
                        && line.TrimEnd().EndsWith(skillsNeedle, StringComparison.OrdinalIgnoreCase);
                    foundAgent |= line.Contains(agentNeedle, StringComparison.OrdinalIgnoreCase);
                }

                if (foundSkills && foundAgent)
                {
                    return new StatusCheck(Title, StatusLevel.Pass, "Copilot's log shows it found the skills and the Superpowers agent.");
                }
            }

            return new StatusCheck(Title, StatusLevel.Unknown, "Copilot's log does not confirm discovery yet. Start a new chat thread or restart Visual Studio, then select Refresh.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new StatusCheck(Title, StatusLevel.Unknown, "Copilot's log could not be read.");
        }
    }
}
