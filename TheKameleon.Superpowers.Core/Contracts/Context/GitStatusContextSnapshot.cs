using System.Text.Json.Serialization;

namespace TheKameleon.Superpowers.Core.Contracts.Context;

public sealed record GitStatusContextSnapshot
{
    [JsonConstructor]
    public GitStatusContextSnapshot(
        ContextValueState state,
        ContextProvenance provenance,
        string? branch = null,
        CapturedTextValue? statusSummary = null,
        IReadOnlyList<string>? recentCommits = null)
    {
        if (!Enum.IsDefined(state))
        {
            throw new ArgumentOutOfRangeException(nameof(state), "Context value state is invalid.");
        }

        ArgumentNullException.ThrowIfNull(provenance);

        State = state;
        Provenance = provenance;
        Branch = branch;
        StatusSummary = statusSummary;
        RecentCommits = (recentCommits ?? Array.Empty<string>()).ToArray();
    }

    public ContextValueState State { get; }

    public ContextProvenance Provenance { get; }

    public string? Branch { get; }

    public CapturedTextValue? StatusSummary { get; }

    public IReadOnlyList<string> RecentCommits { get; }
}
