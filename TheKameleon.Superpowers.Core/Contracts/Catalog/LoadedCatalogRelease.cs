namespace TheKameleon.Superpowers.Core.Contracts.Catalog;

/// <summary>
/// A loaded, validated bundled catalog release. <see cref="PlanMetadata"/> remains the
/// backward-compatible accessor for the Plan entry point; <see cref="EntryPoints"/> carries
/// metadata for every wired entry point (Plan, Execute, Debug, TDD, Review, Verify, Refactor,
/// Finish) keyed by entry-point id.
/// </summary>
public sealed record LoadedCatalogRelease(
    string ReleaseTag,
    string ResolvedCommit,
    string LicenseText,
    AdapterManifest AdapterManifest,
    PlanEntryPointMetadata? PlanMetadata,
    IReadOnlyList<DiscoveredSkillEntry> Skills,
    IReadOnlyList<string> Assets,
    IReadOnlyList<ParseDiagnostic> Diagnostics,
    IReadOnlyDictionary<string, PlanEntryPointMetadata>? EntryPoints = null)
{
    private static readonly IReadOnlyDictionary<string, PlanEntryPointMetadata> EmptyEntryPoints =
        new Dictionary<string, PlanEntryPointMetadata>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// All entry-point metadata declared for this release, including "Plan" when
    /// <see cref="PlanMetadata"/> is set but was not separately included in <see cref="EntryPoints"/>.
    /// </summary>
    public IReadOnlyDictionary<string, PlanEntryPointMetadata> AllEntryPoints
    {
        get
        {
            var source = EntryPoints ?? EmptyEntryPoints;
            if (PlanMetadata is null || source.ContainsKey(PlanMetadata.EntryPoint))
            {
                return source;
            }

            var merged = new Dictionary<string, PlanEntryPointMetadata>(source, StringComparer.OrdinalIgnoreCase)
            {
                [PlanMetadata.EntryPoint] = PlanMetadata,
            };
            return merged;
        }
    }

    public PlanEntryPointMetadata? GetEntryPoint(string entryPointId)
    {
        return AllEntryPoints.TryGetValue(entryPointId, out var metadata) ? metadata : null;
    }

    public bool HasErrors => Diagnostics.Any(diagnostic => diagnostic.Severity == ParseDiagnosticSeverity.Error)
        || AdapterManifest.HasErrors
        || (PlanMetadata?.SchemaVersion ?? PlanEntryPointMetadata.CurrentSchemaVersion) <= 0
        || AllEntryPoints.Values.Any(entryPoint => entryPoint.SchemaVersion <= 0)
        || Skills.Any(skill => skill.Diagnostics.Any(diagnostic => diagnostic.Severity == ParseDiagnosticSeverity.Error));
}
