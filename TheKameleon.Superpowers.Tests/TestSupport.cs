using System.Text;
using TheKameleon.Superpowers.Core.Contracts.Catalog;
using TheKameleon.Superpowers.Skills.Install;

namespace TheKameleon.Superpowers.Tests;

internal static class TestSupport
{
    public static string RepositoryPath(params string[] segments)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "TheKameleon.Superpowers.slnx")))
            {
                return Path.Combine(new[] { current.FullName }.Concat(segments).ToArray());
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root from the test output directory.");
    }

    public static string BundledCatalogRoot => RepositoryPath("bundled-catalog", "obra.superpowers", "2026-09-21");

    public static LoadedCatalogRelease Release(string tag, bool prerelease, DateTimeOffset published, bool hasError = false)
    {
        var diagnostics = hasError
            ? new[] { new ParseDiagnostic(ParseDiagnosticSeverity.Error, "TEST", "broken") }
            : Array.Empty<ParseDiagnostic>();
        return new LoadedCatalogRelease(
            tag,
            "commit-" + tag,
            "MIT",
            new AdapterManifest(1, Array.Empty<AdapterManifestAction>(), Array.Empty<ParseDiagnostic>()),
            null,
            Array.Empty<DiscoveredSkillEntry>(),
            Array.Empty<string>(),
            diagnostics)
        {
            ArchivePath = $"releases/{tag}/source.zip",
            IsPrerelease = prerelease,
            PublishedAtUtc = published,
        };
    }

    public static SkillPackage Skill(string name, params (string Path, string Content)[] extraFiles)
    {
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["SKILL.md"] = Encoding.UTF8.GetBytes($"---\nname: {name}\ndescription: Use when testing {name}.\n---\n\nBody of {name}.\n"),
        };
        foreach (var (path, content) in extraFiles)
        {
            files[path] = Encoding.UTF8.GetBytes(content);
        }

        return new SkillPackage(name, files);
    }

    public static SkillPackage SkillWithMarkdown(string folderName, string skillMarkdown)
    {
        return new SkillPackage(folderName, new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["SKILL.md"] = Encoding.UTF8.GetBytes(skillMarkdown),
        });
    }
}
