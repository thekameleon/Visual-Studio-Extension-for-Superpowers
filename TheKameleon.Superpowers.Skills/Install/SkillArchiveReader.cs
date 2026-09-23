using System.IO.Compression;

namespace TheKameleon.Superpowers.Skills.Install;

public sealed record SkillArchiveLimits(int MaxEntries, long MaxTotalBytes)
{
    public static SkillArchiveLimits Default { get; } = new(5_000, 50L * 1024 * 1024);
}

public sealed record SkillArchiveReadResult(IReadOnlyList<SkillPackage> Skills, IReadOnlyList<string> Problems);

/// <summary>Reads <c>&lt;root&gt;/skills/&lt;name&gt;/**</c> from an upstream GitHub zipball without extracting to disk.</summary>
public static class SkillArchiveReader
{
    public static SkillArchiveReadResult Read(Stream archiveStream, SkillArchiveLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(archiveStream);
        limits ??= SkillArchiveLimits.Default;
        var problems = new List<string>();
        var skills = new SortedDictionary<string, Dictionary<string, byte[]>>(StringComparer.Ordinal);

        using var archive = new ZipArchive(archiveStream, ZipArchiveMode.Read, leaveOpen: true);
        if (archive.Entries.Count > limits.MaxEntries)
        {
            problems.Add($"The archive has {archive.Entries.Count} entries; the limit is {limits.MaxEntries}.");
            return new SkillArchiveReadResult(Array.Empty<SkillPackage>(), problems);
        }

        long totalBytes = 0;
        foreach (var entry in archive.Entries)
        {
            var segments = entry.FullName.Replace('\\', '/').Split('/');
            if (segments.Length < 4 || segments[1] != "skills" || segments[^1].Length == 0)
            {
                continue;
            }

            var name = segments[2];
            var rest = segments[3..];
            if (!IsSafeSegment(name) || rest.Any(segment => !IsSafeSegment(segment)))
            {
                problems.Add($"Unsafe archive path '{entry.FullName}' was skipped.");
                continue;
            }

            using var source = entry.Open();
            using var memory = new MemoryStream();
            if (!TryCopyWithinLimit(source, memory, limits.MaxTotalBytes, ref totalBytes))
            {
                problems.Add($"Skill files exceed {limits.MaxTotalBytes} uncompressed bytes; the archive was rejected.");
                return new SkillArchiveReadResult(Array.Empty<SkillPackage>(), problems);
            }

            if (!skills.TryGetValue(name, out var files))
            {
                files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
                skills[name] = files;
            }

            files[string.Join('/', rest)] = memory.ToArray();
        }

        var packages = skills.Select(pair => new SkillPackage(pair.Key, pair.Value)).ToArray();
        return new SkillArchiveReadResult(packages, problems);
    }

    private static bool IsSafeSegment(string segment)
    {
        return segment.Length > 0
            && segment is not "." and not ".."
            && segment.IndexOfAny(new[] { ':', '\0' }) < 0;
    }

    /// <summary>Copies source to destination in bounded chunks, enforcing maxBytes against ACTUAL bytes
    /// read rather than trusting any declared/metadata size, so a stream that decompresses to more than
    /// it claimed is still caught. Returns false (copy aborted, partial data may already be in destination)
    /// if the limit is exceeded.</summary>
    private static bool TryCopyWithinLimit(Stream source, Stream destination, long maxBytes, ref long totalBytesCopied)
    {
        var buffer = new byte[81920];
        int read;
        while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
        {
            totalBytesCopied += read;
            if (totalBytesCopied > maxBytes)
            {
                return false;
            }

            destination.Write(buffer, 0, read);
        }

        return true;
    }
}
