namespace TheKameleon.Superpowers.Core.Contracts.Catalog;

public sealed record DiscoveredSkillEntry(
    string SkillId,
    string RelativePath,
    string FullPath,
    DiscoverySourceKind SourceKind,
    DiscoveryTrustState TrustState,
    ParsedSkillDocument Document,
    IReadOnlyList<ParseDiagnostic> Diagnostics);
