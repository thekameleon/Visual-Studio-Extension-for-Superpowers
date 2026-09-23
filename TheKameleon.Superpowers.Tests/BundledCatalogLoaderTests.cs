using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using TheKameleon.Superpowers.Skills.Catalog;

namespace TheKameleon.Superpowers.Tests;

public sealed class BundledCatalogLoaderTests : IDisposable
{
    private readonly string tempRoot = Path.Combine(Path.GetTempPath(), "SuperpowersTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void LoadsBundledCatalogFromRealDirectory()
    {
        var root = GetRepositoryRelativePath("bundled-catalog", "obra.superpowers", "2026-09-21");

        var result = BundledCatalogLoader.LoadFromDirectory(root);

        var diagnostics = string.Join(
            Environment.NewLine,
            result.Diagnostics.Select(diagnostic => $"CAT {diagnostic.Code}: {diagnostic.Message}")
                .Concat(result.Releases.SelectMany(release => release.Diagnostics.Select(diagnostic =>
                    $"REL {release.ReleaseTag} {diagnostic.Code}: {diagnostic.Message}")))
                .Concat(result.Releases.SelectMany(release => release.Skills.SelectMany(skill =>
                    skill.Diagnostics.Select(diagnostic => $"SKILL {release.ReleaseTag} {skill.RelativePath} {diagnostic.Code}: {diagnostic.Message}")))));

        Assert.False(result.HasErrors, diagnostics);
        Assert.Equal("https://github.com/obra/superpowers", result.SourceRepositoryUrl);
        Assert.Equal(13, result.Releases.Count);
        var latest = Assert.Single(result.Releases, release => release.ReleaseTag == "v6.4.1");
        Assert.Equal("5bf4e78011075bcfc0dc295f0724994cd123ee71", latest.ResolvedCommit);
        Assert.NotNull(latest.PlanMetadata);
        Assert.Equal("Plan", latest.PlanMetadata!.EntryPoint);
        Assert.Equal(2, latest.PlanMetadata.Composition.Count);
        Assert.Contains(latest.Skills, skill => skill.RelativePath == "skills/brainstorming/SKILL.md");
        Assert.Contains(latest.Skills, skill => skill.RelativePath == "skills/writing-plans/SKILL.md");

        foreach (var entryPointId in new[] { "Plan", "Execute", "Debug", "TDD", "Review", "Verify", "Refactor", "Finish" })
        {
            var entryPointMetadata = latest.GetEntryPoint(entryPointId);
            Assert.True(entryPointMetadata is not null, $"Release '{latest.ReleaseTag}' is missing entry point '{entryPointId}'.");
            Assert.Equal(entryPointId, entryPointMetadata!.EntryPoint);
            Assert.NotEmpty(entryPointMetadata.Composition);
        }
    }

    [Fact]
    public void RejectsHashMismatchInBundledRelease()
    {
        var root = CreateTamperedCopy();
        File.AppendAllText(Path.Combine(root, "releases", "v6.4.1", "LICENSE.txt"), "tampered");

        var result = BundledCatalogLoader.LoadFromDirectory(root);

        var release = Assert.Single(result.Releases, candidate => candidate.ReleaseTag == "v6.4.1");
        Assert.True(release.HasErrors);
        Assert.Contains(release.Diagnostics, diagnostic => diagnostic.Code == "SPCAT421");
    }

    [Fact]
    public void RejectsMissingReferencedAssetInBundledSourceArchive()
    {
        var root = CreateSyntheticCatalogRoot();

        var result = BundledCatalogLoader.LoadFromDirectory(root);

        var release = Assert.Single(result.Releases);
        Assert.True(release.HasErrors);
        Assert.Contains(release.Diagnostics, diagnostic => diagnostic.Code == "SPCAT423");
    }

    public void Dispose()
    {
        if (Directory.Exists(tempRoot))
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private string CreateTamperedCopy()
    {
        var sourceRoot = GetRepositoryRelativePath("bundled-catalog", "obra.superpowers", "2026-09-21");
        var destinationRoot = Path.Combine(tempRoot, "tampered-catalog");
        CopyDirectory(sourceRoot, destinationRoot);
        return destinationRoot;
    }

    private string CreateSyntheticCatalogRoot()
    {
        var root = Path.Combine(tempRoot, "synthetic-catalog");
        var releaseRoot = Path.Combine(root, "releases", "v1.0.0");
        Directory.CreateDirectory(releaseRoot);

        var licensePath = Path.Combine(releaseRoot, "LICENSE.txt");
        File.WriteAllText(licensePath, "MIT License");

        var adapterManifestPath = Path.Combine(releaseRoot, "adapter-manifest.json");
        File.WriteAllText(adapterManifestPath, """
        {
          "schemaVersion": 1,
          "actions": [
            {
              "actionId": "Plan",
              "skillPath": "skills/plan/SKILL.md",
              "requiresApproval": false
            }
          ]
        }
        """);

        var planMetadataPath = Path.Combine(releaseRoot, "plan-metadata.json");
        File.WriteAllText(planMetadataPath, """
        {
          "schemaVersion": 1,
          "entryPoint": "Plan",
          "sourceRepository": "https://example.test/superpowers",
          "releaseTag": "v1.0.0",
          "composition": [
            {
              "order": 1,
              "skillPath": "skills/plan/SKILL.md",
              "purpose": "Draft the plan"
            }
          ],
          "notes": [
            "Synthetic test metadata"
          ]
        }
        """);

        var archivePath = Path.Combine(releaseRoot, "source.zip");
        using (var stream = File.Create(archivePath))
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: false))
        {
            var skillEntry = archive.CreateEntry("synthetic-release/skills/plan/SKILL.md");
            using var writer = new StreamWriter(skillEntry.Open());
            writer.Write("""
            ---
            name: Plan
            description: Synthetic plan
            ---
            See [companion](visual-companion.md)
            """);
        }

        var provenancePath = Path.Combine(releaseRoot, "provenance.json");
        var provenance = new
        {
            schemaVersion = 1,
            releaseTag = "v1.0.0",
            resolvedCommit = "abc123",
            files = new[]
            {
                new { path = "source.zip", sha256 = ComputeSha256(archivePath) },
                new { path = "LICENSE.txt", sha256 = ComputeSha256(licensePath) },
                new { path = "adapter-manifest.json", sha256 = ComputeSha256(adapterManifestPath) },
                new { path = "plan-metadata.json", sha256 = ComputeSha256(planMetadataPath) }
            }
        };
        File.WriteAllText(provenancePath, JsonSerializer.Serialize(provenance));

        var catalog = new
        {
            schemaVersion = 1,
            sourceRepositoryUrl = "https://example.test/superpowers",
            cutoffCapturedAtUtc = "2026-09-21T00:00:00Z",
            releases = new[]
            {
                new
                {
                    releaseTag = "v1.0.0",
                    resolvedCommit = "abc123",
                    licensePath = "releases/v1.0.0/LICENSE.txt",
                    archivePath = "releases/v1.0.0/source.zip",
                    adapterManifestPath = "releases/v1.0.0/adapter-manifest.json",
                    planMetadataPath = "releases/v1.0.0/plan-metadata.json",
                    provenancePath = "releases/v1.0.0/provenance.json"
                }
            }
        };
        File.WriteAllText(Path.Combine(root, "catalog.json"), JsonSerializer.Serialize(catalog));
        return root;
    }

    private static string GetRepositoryRelativePath(params string[] segments)
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

    private static string ComputeSha256(string path)
    {
        return Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
        }

        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var destinationFile = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(destinationFile)!);
            File.Copy(file, destinationFile, overwrite: true);
        }
    }
}
