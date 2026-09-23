namespace TheKameleon.Superpowers.Skills.Install;

public enum SkillIssueKind
{
    Conflict,
    Invalid,
    EditedKept,
    EditedOverwritten,
    RemovedEditedKept,
    RemovalFailed,
}

public sealed record SkillIssue(string SkillName, SkillIssueKind Kind, string Message);

public sealed record SkillInstallOutcome(
    bool Succeeded,
    IReadOnlyList<InstalledSkill> Installed,
    IReadOnlyList<SkillIssue> Issues,
    string? FailureReason);

public sealed class SkillInstaller(ProfilePaths paths)
{
    public static InstalledSkill Describe(SkillPackage package)
    {
        return new InstalledSkill(
            package.Name,
            package.Files.ToDictionary(file => file.Key, file => ContentHash.Of(file.Value), StringComparer.Ordinal));
    }

    public IReadOnlyList<string> FindConflicts(IEnumerable<SkillPackage> packages, IReadOnlyList<InstalledSkill> owned)
    {
        var ownedNames = owned.Select(skill => skill.Name).ToHashSet(StringComparer.Ordinal);
        return packages
            .Where(package => !ownedNames.Contains(package.Name) && Directory.Exists(SkillDirectory(package.Name)))
            .Select(package => package.Name)
            .ToArray();
    }

    public bool IsEdited(InstalledSkill skill)
    {
        var onDisk = ReadHashes(skill.Name);
        return onDisk is not null && !SameHashes(onDisk, skill.FileHashes);
    }

    public bool MatchesPackage(SkillPackage package)
    {
        var onDisk = ReadHashes(package.Name);
        return onDisk is not null && SameHashes(onDisk, Describe(package).FileHashes);
    }

    public SkillInstallOutcome Install(IReadOnlyList<SkillPackage> packages, IReadOnlyList<InstalledSkill> owned, bool overwriteEdited)
    {
        ArgumentNullException.ThrowIfNull(packages);
        ArgumentNullException.ThrowIfNull(owned);

        var issues = new List<SkillIssue>();
        var installed = new List<InstalledSkill>();
        var ownedByName = owned.ToDictionary(skill => skill.Name, StringComparer.Ordinal);
        var swaps = new List<(string Target, string? Backup)>();
        var stagingRoot = Path.Combine(paths.StagingRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(paths.SkillsRoot);

        try
        {
            foreach (var package in packages)
            {
                ownedByName.TryGetValue(package.Name, out var previous);
                var problems = SkillValidator.Validate(package);
                if (problems.Count > 0)
                {
                    issues.Add(new SkillIssue(package.Name, SkillIssueKind.Invalid, $"Skipped '{package.Name}': {string.Join(" ", problems)}"));
                    if (previous is not null)
                    {
                        installed.Add(previous);
                    }

                    continue;
                }

                var target = SkillDirectory(package.Name);
                if (previous is null && Directory.Exists(target))
                {
                    issues.Add(new SkillIssue(package.Name, SkillIssueKind.Conflict, $"A '{package.Name}' skill that Superpowers did not install already exists; it was left unchanged."));
                    continue;
                }

                if (previous is not null && IsEdited(previous))
                {
                    if (!overwriteEdited)
                    {
                        issues.Add(new SkillIssue(package.Name, SkillIssueKind.EditedKept, $"Your edited '{package.Name}' skill was kept."));
                        installed.Add(previous);
                        continue;
                    }

                    issues.Add(new SkillIssue(package.Name, SkillIssueKind.EditedOverwritten, $"Your edits to '{package.Name}' were replaced."));
                }

                var staged = Path.Combine(stagingRoot, package.Name);
                WriteFiles(staged, package.Files);

                string? backup = null;
                if (Directory.Exists(target))
                {
                    backup = target + ".superpowers-backup-" + Guid.NewGuid().ToString("N");
                    Directory.Move(target, backup);
                }

                swaps.Add((target, backup));
                Directory.Move(staged, target);
                installed.Add(Describe(package));
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            RollBack(swaps);
            return new SkillInstallOutcome(false, owned, issues, $"Installation failed and was rolled back: {exception.Message}");
        }
        finally
        {
            TryDeleteDirectory(stagingRoot);
        }

        foreach (var (_, backup) in swaps)
        {
            if (backup is not null)
            {
                TryDeleteDirectory(backup);
            }
        }

        var newNames = packages.Select(package => package.Name).ToHashSet(StringComparer.Ordinal);
        issues.AddRange(Remove(owned.Where(skill => !newNames.Contains(skill.Name)).ToArray()));
        return new SkillInstallOutcome(true, installed, issues, null);
    }

    public IReadOnlyList<SkillIssue> Remove(IReadOnlyList<InstalledSkill> owned)
    {
        var issues = new List<SkillIssue>();
        foreach (var skill in owned)
        {
            var directory = SkillDirectory(skill.Name);
            if (!Directory.Exists(directory))
            {
                continue;
            }

            try
            {
                if (IsEdited(skill))
                {
                    issues.Add(new SkillIssue(skill.Name, SkillIssueKind.RemovedEditedKept, $"Your edited '{skill.Name}' skill was left in place and is no longer managed by Superpowers."));
                    continue;
                }

                Directory.Delete(directory, recursive: true);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                issues.Add(new SkillIssue(skill.Name, SkillIssueKind.RemovalFailed, $"Removing '{skill.Name}' failed and it was left in place: {exception.Message}"));
            }
        }

        return issues;
    }

    private string SkillDirectory(string name) => Path.Combine(paths.SkillsRoot, name);

    private Dictionary<string, string>? ReadHashes(string name)
    {
        var directory = SkillDirectory(name);
        if (!Directory.Exists(directory))
        {
            return null;
        }

        return Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).ToDictionary(
            file => Path.GetRelativePath(directory, file).Replace('\\', '/'),
            ContentHash.OfFile,
            StringComparer.Ordinal);
    }

    private static bool SameHashes(IReadOnlyDictionary<string, string> left, IReadOnlyDictionary<string, string> right)
    {
        return left.Count == right.Count
            && left.All(pair => right.TryGetValue(pair.Key, out var hash) && string.Equals(hash, pair.Value, StringComparison.OrdinalIgnoreCase));
    }

    private static void WriteFiles(string directory, IReadOnlyDictionary<string, byte[]> files)
    {
        var root = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        foreach (var (relativePath, content) in files)
        {
            var destination = Path.GetFullPath(Path.Combine(root, relativePath));
            if (!destination.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                throw new IOException($"Skill file path '{relativePath}' escapes its folder.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.WriteAllBytes(destination, content);
        }
    }

    private static void RollBack(List<(string Target, string? Backup)> swaps)
    {
        for (var index = swaps.Count - 1; index >= 0; index--)
        {
            var (target, backup) = swaps[index];
            TryDeleteDirectory(target);
            if (backup is not null && Directory.Exists(backup))
            {
                Directory.Move(backup, target);
            }
        }
    }

    private static void TryDeleteDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Best effort: leftover staging or backup folders are harmless and retried next time.
        }
    }
}
