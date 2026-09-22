using System.Text.Json;
using TheKameleon.Superpowers.Core.Contracts.Catalog;
using TheKameleon.Superpowers.Core.Contracts.Runs;
using TheKameleon.Superpowers.Core.Contracts.Settings;

namespace TheKameleon.Superpowers.Skills.Workflow;

public static class WorkflowHistoryService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public static WorkflowHistoryMutationResult Append(string workspaceDirectory, WorkflowHistoryEntry entry, SuperpowersSettings? settings = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceDirectory);
        ArgumentNullException.ThrowIfNull(entry);

        settings ??= new SuperpowersSettings();
        return WorkflowStore.AppendHistory(
            workspaceDirectory,
            entry,
            settings.HistoryRetentionDays,
            settings.RetainSensitiveHistoryContent);
    }

    public static WorkflowHistoryQueryResult Query(string workspaceDirectory, WorkflowHistoryQuery query)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceDirectory);
        ArgumentNullException.ThrowIfNull(query);

        var diagnostics = new List<ParseDiagnostic>();
        var snapshot = WorkflowStore.ReadHistorySnapshot(workspaceDirectory);
        IEnumerable<WorkflowHistoryEntry> entries = snapshot.Entries;

        if (!string.IsNullOrWhiteSpace(query.RunId))
        {
            entries = entries.Where(entry => string.Equals(entry.RunId, query.RunId, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query.ReleaseTag))
        {
            entries = entries.Where(entry => string.Equals(entry.ReleaseTag, query.ReleaseTag, StringComparison.OrdinalIgnoreCase));
        }

        if (query.State.HasValue)
        {
            entries = entries.Where(entry => entry.State == query.State.Value);
        }

        if (query.MaxResults.HasValue)
        {
            entries = entries.Take(query.MaxResults.Value);
        }

        return new WorkflowHistoryQueryResult(new WorkflowHistorySnapshot(entries.ToArray()), diagnostics);
    }

    public static WorkflowHistoryExportResult Export(string workspaceDirectory, string exportPath, WorkflowHistoryExportOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(exportPath);
        ArgumentNullException.ThrowIfNull(options);

        var diagnostics = new List<ParseDiagnostic>();
        try
        {
            var snapshot = WorkflowStore.ReadHistorySnapshot(workspaceDirectory);
            if (!options.IncludeSensitiveContent)
            {
                snapshot = new WorkflowHistorySnapshot(snapshot.Entries
                    .Select(entry => new WorkflowHistoryEntry(
                        entry.RunId,
                        entry.ReleaseTag,
                        entry.ExecutionMode,
                        entry.State,
                        entry.CapturedAtUtc,
                        entry.SelectedSkillId,
                        entry.HadSensitiveContent,
                        retainedContent: null))
                    .ToArray());
            }

            Directory.CreateDirectory(Path.GetDirectoryName(exportPath)!);
            File.WriteAllText(exportPath, JsonSerializer.Serialize(snapshot, JsonOptions));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPRUN407", $"Workflow history could not be exported: {exception.Message}"));
        }

        return new WorkflowHistoryExportResult(exportPath, diagnostics);
    }

    public static WorkflowHistoryMutationResult Delete(string workspaceDirectory, WorkflowHistoryQuery query)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceDirectory);
        ArgumentNullException.ThrowIfNull(query);

        return WorkflowStore.DeleteHistory(workspaceDirectory, entry =>
            (string.IsNullOrWhiteSpace(query.RunId) || string.Equals(entry.RunId, query.RunId, StringComparison.OrdinalIgnoreCase))
            && (string.IsNullOrWhiteSpace(query.ReleaseTag) || string.Equals(entry.ReleaseTag, query.ReleaseTag, StringComparison.OrdinalIgnoreCase))
            && (!query.State.HasValue || entry.State == query.State.Value));
    }
}
