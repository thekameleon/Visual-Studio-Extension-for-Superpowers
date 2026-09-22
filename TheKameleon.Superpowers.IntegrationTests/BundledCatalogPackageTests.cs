using System.IO.Compression;
using System.Text.Json;

namespace TheKameleon.Superpowers.IntegrationTests;

public sealed class BundledCatalogPackageTests
{
    private const string PackageName = "TheKameleon.Superpowers.Vsix.vsix";
    private const string CatalogRoot = "bundled-catalog/obra.superpowers/2026-09-21";

    [Fact]
    public void PackageIncludesBundledCatalogSnapshot()
    {
        using var package = OpenPackage();

        Assert.Contains(package.Entries, entry =>
            string.Equals(entry.FullName, $"{CatalogRoot}/catalog.json", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(package.Entries, entry =>
            string.Equals(entry.FullName, $"{CatalogRoot}/README.md", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BundledCatalogListsAllPublishedStableReleasesAtTheCutoff()
    {
        using var package = OpenPackage();
        using var stream = OpenRequiredEntry(package, $"{CatalogRoot}/catalog.json");
        using var document = JsonDocument.Parse(stream);
        var root = document.RootElement;

        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("https://github.com/obra/superpowers", root.GetProperty("sourceRepositoryUrl").GetString());
        Assert.Equal(13, root.GetProperty("bundledStableReleaseCount").GetInt32());
        Assert.Equal(0, root.GetProperty("bundledPrereleaseCount").GetInt32());

        var releases = root.GetProperty("releases").EnumerateArray().ToArray();
        Assert.Equal(13, releases.Length);
        Assert.Equal("v6.4.1", releases[0].GetProperty("releaseTag").GetString());
        Assert.Equal("v5.0.4", releases[^1].GetProperty("releaseTag").GetString());
        Assert.All(releases, release => Assert.False(release.GetProperty("prerelease").GetBoolean()));
    }

    [Fact]
    public void BundledReleaseIncludesLicenseProvenanceAndSeparateMetadata()
    {
        using var package = OpenPackage();

        using var provenanceStream = OpenRequiredEntry(package, $"{CatalogRoot}/releases/v6.4.1/provenance.json");
        using var provenanceDocument = JsonDocument.Parse(provenanceStream);
        var provenanceRoot = provenanceDocument.RootElement;
        Assert.Equal("5bf4e78011075bcfc0dc295f0724994cd123ee71", provenanceRoot.GetProperty("resolvedCommit").GetString());
        Assert.Equal("https://raw.githubusercontent.com/obra/superpowers/v6.4.1/LICENSE", provenanceRoot.GetProperty("licenseSourceUrl").GetString());
        var files = provenanceRoot.GetProperty("files").EnumerateArray().ToArray();
        Assert.Contains(files, file => file.GetProperty("path").GetString() == "source.zip");
        Assert.Contains(files, file => file.GetProperty("path").GetString() == "LICENSE.txt");
        Assert.Contains(files, file => file.GetProperty("path").GetString() == "adapter-manifest.json");
        Assert.Contains(files, file => file.GetProperty("path").GetString() == "plan-metadata.json");
        Assert.Contains(files, file => file.GetProperty("path").GetString() == "platform-metadata.json");
        Assert.All(files, file =>
            Assert.Matches("^[0-9a-f]{64}$", file.GetProperty("sha256").GetString()!));

        using var planStream = OpenRequiredEntry(package, $"{CatalogRoot}/releases/v6.4.1/plan-metadata.json");
        using var planDocument = JsonDocument.Parse(planStream);
        Assert.Equal("Plan", planDocument.RootElement.GetProperty("entryPoint").GetString());

        using var platformStream = OpenRequiredEntry(package, $"{CatalogRoot}/releases/v6.4.1/platform-metadata.json");
        using var platformDocument = JsonDocument.Parse(platformStream);
        Assert.Equal("SuperPowers for Visual Studio", platformDocument.RootElement.GetProperty("product").GetString());
        Assert.Equal("Superpowers", platformDocument.RootElement.GetProperty("menuLabel").GetString());

        using var licenseStream = OpenRequiredEntry(package, $"{CatalogRoot}/releases/v6.4.1/LICENSE.txt");
        using var licenseReader = new StreamReader(licenseStream);
        var licenseText = licenseReader.ReadToEnd();
        Assert.Contains("MIT License", licenseText, StringComparison.Ordinal);

        using var archiveStream = OpenRequiredEntry(package, $"{CatalogRoot}/releases/v6.4.1/source.zip");
        Assert.True(archiveStream.Length > 0);
    }

    [Fact]
    public void PackageSizeStaysWithinCurrentExpectedBound()
    {
        var packagePath = Path.Combine(AppContext.BaseDirectory, PackageName);
        var packageLength = new FileInfo(packagePath).Length;

        Assert.InRange(packageLength, 1_000_000, 20_000_000);
    }

    private static ZipArchive OpenPackage()
    {
        var path = Path.Combine(AppContext.BaseDirectory, PackageName);
        Assert.True(File.Exists(path), $"Build the integration test project to generate the extension package: {path}");
        return ZipFile.OpenRead(path);
    }

    private static Stream OpenRequiredEntry(ZipArchive package, string entryName)
    {
        var entry = package.GetEntry(entryName);
        Assert.NotNull(entry);
        return entry.Open();
    }
}
