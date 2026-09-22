using TheKameleon.Superpowers.Skills.Catalog;

namespace TheKameleon.Superpowers.IntegrationTests;

public sealed class BundledCatalogLoaderPackageTests
{
    [Fact]
    public void LoaderReadsBundledCatalogFromVsixPackage()
    {
        var packagePath = Path.Combine(AppContext.BaseDirectory, "TheKameleon.Superpowers.Vsix.vsix");

        var result = BundledCatalogLoader.LoadFromPackage(packagePath, "bundled-catalog/obra.superpowers/2026-09-21");

        Assert.False(result.HasErrors);
        Assert.Equal("https://github.com/obra/superpowers", result.SourceRepositoryUrl);
        var release = Assert.Single(result.Releases, candidate => candidate.ReleaseTag == "v6.4.1");
        Assert.Equal("5bf4e78011075bcfc0dc295f0724994cd123ee71", release.ResolvedCommit);
        Assert.Contains(release.Skills, skill => skill.RelativePath == "skills/brainstorming/SKILL.md");
        Assert.Contains(release.Skills, skill => skill.RelativePath == "skills/writing-plans/SKILL.md");
    }
}
