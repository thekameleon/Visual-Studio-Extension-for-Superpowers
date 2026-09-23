using TheKameleon.Superpowers.Skills.Catalog;

namespace TheKameleon.Superpowers.Tests;

public sealed class DownloadedReleasesTests : IDisposable
{
    private readonly TempProfile profile = new();

    [Theory]
    [InlineData("../evil")]
    [InlineData("v1/../../x")]
    [InlineData("")]
    public void RejectsUnsafeTags(string tag)
    {
        Assert.Throws<ArgumentException>(() => DownloadedReleases.TargetDirectory(profile.Paths, tag));
    }

    [Fact]
    public void TargetIsUnderTheDownloadsRoot()
    {
        Assert.Equal(Path.Combine(profile.Paths.DownloadsRoot, "v6.5.0-beta.1"), DownloadedReleases.TargetDirectory(profile.Paths, "v6.5.0-beta.1"));
    }

    [Fact]
    public void LoadsEachDownloadedCatalogRoot()
    {
        var target = DownloadedReleases.TargetDirectory(profile.Paths, "v6.4.1");
        CopyDirectory(TestSupport.BundledCatalogRoot, target);

        var available = DownloadedReleases.Load(profile.Paths);

        Assert.NotEmpty(available);
        Assert.All(available, release => Assert.Equal("download", release.Source));
        Assert.Contains(available, release => release.Release.ReleaseTag == "v6.4.1" && release.CatalogRoot == target);
    }

    [Fact]
    public void NoDownloadsYieldsEmpty()
    {
        Assert.Empty(DownloadedReleases.Load(profile.Paths));
    }

    private static void CopyDirectory(string source, string destination)
    {
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }

    public void Dispose() => profile.Dispose();
}
