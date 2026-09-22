using System.Text.Json.Serialization;

namespace TheKameleon.Superpowers.Core.Contracts.Runs;

public sealed record WorkflowHistoryExportOptions
{
    [JsonConstructor]
    public WorkflowHistoryExportOptions(bool includeSensitiveContent = false)
    {
        IncludeSensitiveContent = includeSensitiveContent;
    }

    public bool IncludeSensitiveContent { get; }
}
