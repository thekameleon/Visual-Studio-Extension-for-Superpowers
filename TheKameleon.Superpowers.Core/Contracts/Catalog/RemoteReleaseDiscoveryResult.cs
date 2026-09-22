namespace TheKameleon.Superpowers.Core.Contracts.Catalog;

public sealed record RemoteReleaseDiscoveryResult(
    string SourceRepositoryUrl,
    IReadOnlyList<DiscoveredRemoteRelease> Releases,
    IReadOnlyList<ParseDiagnostic> Diagnostics)
{
    public bool HasErrors => Diagnostics.Any(diagnostic => diagnostic.Severity == ParseDiagnosticSeverity.Error);
}
