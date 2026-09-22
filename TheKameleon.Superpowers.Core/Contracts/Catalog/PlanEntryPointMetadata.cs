using System.Text.Json.Serialization;

namespace TheKameleon.Superpowers.Core.Contracts.Catalog;

public sealed record PlanEntryPointMetadata
{
    public const int CurrentSchemaVersion = 1;

    [JsonConstructor]
    public PlanEntryPointMetadata(
        string entryPoint,
        string sourceRepository,
        string releaseTag,
        IReadOnlyList<PlanCompositionStep>? composition = null,
        IReadOnlyList<string>? notes = null,
        int schemaVersion = CurrentSchemaVersion)
    {
        if (string.IsNullOrWhiteSpace(entryPoint))
        {
            throw new ArgumentException("Entry point is required.", nameof(entryPoint));
        }

        if (string.IsNullOrWhiteSpace(sourceRepository))
        {
            throw new ArgumentException("Source repository is required.", nameof(sourceRepository));
        }

        if (string.IsNullOrWhiteSpace(releaseTag))
        {
            throw new ArgumentException("Release tag is required.", nameof(releaseTag));
        }

        if (schemaVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(schemaVersion), "Schema version must be greater than zero.");
        }

        EntryPoint = entryPoint;
        SourceRepository = sourceRepository;
        ReleaseTag = releaseTag;
        Composition = (composition ?? Array.Empty<PlanCompositionStep>()).OrderBy(step => step.Order).ToArray();
        Notes = (notes ?? Array.Empty<string>()).Where(note => !string.IsNullOrWhiteSpace(note)).ToArray();
        SchemaVersion = schemaVersion;
    }

    public string EntryPoint { get; }

    public string SourceRepository { get; }

    public string ReleaseTag { get; }

    public IReadOnlyList<PlanCompositionStep> Composition { get; }

    public IReadOnlyList<string> Notes { get; }

    public int SchemaVersion { get; }
}
