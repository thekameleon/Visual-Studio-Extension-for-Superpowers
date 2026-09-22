namespace TheKameleon.Superpowers.Core.Contracts.Catalog;

public sealed record DownloadedReleaseStage(
    string ReleaseTag,
    string SourceRepositoryUrl,
    string SourceZipUrl,
    string StagingDirectory,
    string CatalogRootDirectory,
    string? ActivatedDirectory);
