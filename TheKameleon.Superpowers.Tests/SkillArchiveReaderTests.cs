using System.IO.Compression;
using System.Text;
using TheKameleon.Superpowers.Skills.Catalog;
using TheKameleon.Superpowers.Skills.Install;

namespace TheKameleon.Superpowers.Tests;

public sealed class SkillArchiveReaderTests
{
    [Fact]
    public void ReadsEachSkillFolderWithSupportingFiles()
    {
        using var archive = BuildArchive(
            ("root/README.md", "ignored"),
            ("root/skills/alpha/SKILL.md", "alpha"),
            ("root/skills/alpha/scripts/run.sh", "echo hi"),
            ("root/skills/beta/SKILL.md", "beta"));

        var result = SkillArchiveReader.Read(archive);

        Assert.Empty(result.Problems);
        Assert.Equal(new[] { "alpha", "beta" }, result.Skills.Select(skill => skill.Name));
        var alpha = result.Skills[0];
        Assert.Equal(new[] { "SKILL.md", "scripts/run.sh" }, alpha.Files.Keys.OrderBy(key => key, StringComparer.Ordinal));
        Assert.Equal("echo hi", Encoding.UTF8.GetString(alpha.Files["scripts/run.sh"]));
    }

    [Theory]
    [InlineData("root/skills/../evil/SKILL.md")]
    [InlineData("root/skills/alpha/../../evil.txt")]
    [InlineData("root/skills/alpha/C:/evil.txt")]
    public void SkipsUnsafePaths(string unsafePath)
    {
        using var archive = BuildArchive(("root/skills/alpha/SKILL.md", "alpha"), (unsafePath, "bad"));

        var result = SkillArchiveReader.Read(archive);

        Assert.Contains(result.Problems, problem => problem.Contains("Unsafe archive path", StringComparison.Ordinal));
        var alpha = Assert.Single(result.Skills);
        Assert.Equal(new[] { "SKILL.md" }, alpha.Files.Keys);
    }

    [Fact]
    public void RejectsArchiveOverEntryLimit()
    {
        using var archive = BuildArchive(("root/skills/a/SKILL.md", "a"), ("root/skills/b/SKILL.md", "b"), ("root/skills/c/SKILL.md", "c"));

        var result = SkillArchiveReader.Read(archive, new SkillArchiveLimits(MaxEntries: 2, MaxTotalBytes: 1_000_000));

        Assert.Empty(result.Skills);
        Assert.Contains(result.Problems, problem => problem.Contains("entries", StringComparison.Ordinal));
    }

    [Fact]
    public void RejectsArchiveOverSizeLimit()
    {
        using var archive = BuildArchive(("root/skills/a/SKILL.md", new string('x', 2_000)));

        var result = SkillArchiveReader.Read(archive, new SkillArchiveLimits(MaxEntries: 100, MaxTotalBytes: 1_000));

        Assert.Empty(result.Skills);
        Assert.Contains(result.Problems, problem => problem.Contains("uncompressed", StringComparison.Ordinal));
    }

    [Fact]
    public void RejectsEntryWhoseActualContentExceedsTheLimitRegardlessOfDeclaredSize()
    {
        using var archive = BuildArchive(("root/skills/a/SKILL.md", new string('y', 5_000)));

        var result = SkillArchiveReader.Read(archive, new SkillArchiveLimits(MaxEntries: 100, MaxTotalBytes: 1_000));

        Assert.Empty(result.Skills);
        Assert.Contains(result.Problems, problem => problem.Contains("uncompressed", StringComparison.Ordinal));
    }

    [Fact]
    public void LoadsAllSkillsFromTheNewestBundledRelease()
    {
        var catalog = BundledCatalogLoader.LoadFromDirectory(TestSupport.BundledCatalogRoot);
        var release = Assert.Single(catalog.Releases, candidate => candidate.ReleaseTag == "v6.4.1");

        var result = ReleaseSkillLoader.Load(TestSupport.BundledCatalogRoot, release);

        Assert.Empty(result.Problems);
        Assert.Equal(15, result.Skills.Count);
        Assert.All(result.Skills, skill => Assert.Empty(SkillValidator.Validate(skill)));
        Assert.Contains(result.Skills, skill => skill.Name == "using-superpowers");
    }

    [Fact]
    public void RefusesReleaseWithCatalogErrors()
    {
        var broken = TestSupport.Release("v9.9.9", prerelease: false, DateTimeOffset.UtcNow, hasError: true);

        var result = ReleaseSkillLoader.Load(TestSupport.BundledCatalogRoot, broken);

        Assert.Empty(result.Skills);
        Assert.Contains(result.Problems, problem => problem.Contains("failed catalog validation", StringComparison.Ordinal));
    }

    private static MemoryStream BuildArchive(params (string Path, string Content)[] entries)
    {
        var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (path, content) in entries)
            {
                using var writer = new StreamWriter(archive.CreateEntry(path).Open(), new UTF8Encoding(false));
                writer.Write(content);
            }
        }

        memory.Position = 0;
        return memory;
    }
}
