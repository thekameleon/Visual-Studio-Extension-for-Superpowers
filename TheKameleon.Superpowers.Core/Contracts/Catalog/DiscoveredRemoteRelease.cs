namespace TheKameleon.Superpowers.Core.Contracts.Catalog;

public sealed record DiscoveredRemoteRelease(
    string ReleaseTag,
    string ReleaseName,
    bool IsPrerelease,
    DateTimeOffset PublishedAtUtc,
    string SourceRepositoryUrl,
    string DetailsUrl,
    string ZipballUrl);
