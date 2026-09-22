using System.Text.Json.Serialization;

namespace TheKameleon.Superpowers.Core.Contracts.Runs;

public sealed record WorkflowHistoryQuery
{
    [JsonConstructor]
    public WorkflowHistoryQuery(string? runId = null, string? releaseTag = null, AdapterRunState? state = null, int? maxResults = null)
    {
        if (maxResults.HasValue && maxResults.Value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxResults), "Max results must be greater than zero when provided.");
        }

        RunId = runId;
        ReleaseTag = releaseTag;
        State = state;
        MaxResults = maxResults;
    }

    public string? RunId { get; }

    public string? ReleaseTag { get; }

    public AdapterRunState? State { get; }

    public int? MaxResults { get; }
}
