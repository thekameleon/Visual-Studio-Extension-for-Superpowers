using TheKameleon.Superpowers.Skills.Catalog;

namespace TheKameleon.Superpowers.Tests;

public sealed class ReleaseSelectionTests
{
    [Fact]
    public void PicksNewestStableRelease()
    {
        var releases = new[]
        {
            TestSupport.Release("v1.0.0", prerelease: false, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)),
            TestSupport.Release("v2.0.0", prerelease: false, new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero)),
            TestSupport.Release("v1.5.0", prerelease: false, new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero)),
        };

        Assert.Equal("v2.0.0", ReleaseSelection.DefaultRelease(releases)!.ReleaseTag);
    }

    [Fact]
    public void IgnoresPrereleasesAndReleasesWithErrors()
    {
        var releases = new[]
        {
            TestSupport.Release("v1.0.0", prerelease: false, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)),
            TestSupport.Release("v3.0.0-beta", prerelease: true, new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero)),
            TestSupport.Release("v2.0.0", prerelease: false, new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero), hasError: true),
        };

        Assert.Equal("v1.0.0", ReleaseSelection.DefaultRelease(releases)!.ReleaseTag);
    }

    [Fact]
    public void ReturnsNullWhenNothingIsEligible()
    {
        Assert.Null(ReleaseSelection.DefaultRelease(Array.Empty<Core.Contracts.Catalog.LoadedCatalogRelease>()));
    }
}
