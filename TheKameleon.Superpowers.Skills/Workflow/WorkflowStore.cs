using System.Text.Json;
using TheKameleon.Superpowers.Core.Contracts.Catalog;
using TheKameleon.Superpowers.Core.Contracts.Runs;

namespace TheKameleon.Superpowers.Skills.Workflow;

public static class WorkflowStore
{
    private const string ActiveFileName = "active-run.json";
    private const string HistoryFileName = "history.json";
    private const string BackupExtension = ".bak";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public static WorkflowPersistenceSaveResult Save(string workspaceDirectory, WorkflowPersistenceEnvelope envelope)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceDirectory);
        ArgumentNullException.ThrowIfNull(envelope);

        var diagnostics = new List<ParseDiagnostic>();
        var targetPath = Path.Combine(workspaceDirectory, ActiveFileName);

        try
        {
            Directory.CreateDirectory(workspaceDirectory);
            WriteAtomically(targetPath, JsonSerializer.Serialize(envelope, JsonOptions));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPRUN301", $"Workflow state could not be persisted atomically: {exception.Message}"));
        }

        return new WorkflowPersistenceSaveResult(targetPath, diagnostics);
    }

    public static WorkflowPersistenceLoadResult Load(string workspaceDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceDirectory);

        var diagnostics = new List<ParseDiagnostic>();
        var targetPath = Path.Combine(workspaceDirectory, ActiveFileName);
        if (!File.Exists(targetPath))
        {
            return new WorkflowPersistenceLoadResult(null, diagnostics);
        }

        var content = TryReadWithRecovery(targetPath, diagnostics);
        if (content is null)
        {
            return new WorkflowPersistenceLoadResult(null, diagnostics);
        }

        try
        {
            var envelope = DeserializeEnvelope(content, diagnostics, targetPath, allowBackupRecovery: true);
            if (envelope is null)
            {
                diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPRUN302", "Workflow state file is empty."));
                return new WorkflowPersistenceLoadResult(null, diagnostics);
            }

            if (envelope.SchemaVersion > WorkflowPersistenceEnvelope.CurrentSchemaVersion)
            {
                diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPRUN310", $"Workflow state schema version '{envelope.SchemaVersion}' is newer than the supported version '{WorkflowPersistenceEnvelope.CurrentSchemaVersion}'."));
                return new WorkflowPersistenceLoadResult(null, diagnostics);
            }

            return new WorkflowPersistenceLoadResult(envelope, diagnostics);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPRUN304", $"Workflow state file could not be loaded: {exception.Message}"));
            return new WorkflowPersistenceLoadResult(null, diagnostics);
        }
    }

    public static WorkflowHistoryMutationResult AppendHistory(
        string workspaceDirectory,
        WorkflowHistoryEntry entry,
        int retentionDays = 30,
        bool retainSensitiveContent = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceDirectory);
        ArgumentNullException.ThrowIfNull(entry);

        var diagnostics = new List<ParseDiagnostic>();
        if (retentionDays <= 0)
        {
            diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPRUN313", "Workflow history retention must be greater than zero days."));
            return new WorkflowHistoryMutationResult(new WorkflowHistorySnapshot(Array.Empty<WorkflowHistoryEntry>()), diagnostics);
        }

        var historyPath = Path.Combine(workspaceDirectory, HistoryFileName);
        WorkflowHistorySnapshot updated;

        try
        {
            Directory.CreateDirectory(workspaceDirectory);
            var snapshot = ReadHistory(historyPath, diagnostics);
            var sanitizedEntry = ApplyHistoryRetention(entry, retainSensitiveContent);
            var cutoff = DateTimeOffset.UtcNow.AddDays(-retentionDays);
            updated = new WorkflowHistorySnapshot(snapshot.Entries
                .Concat(new[] { sanitizedEntry })
                .Where(candidate => candidate.CapturedAtUtc >= cutoff)
                .OrderByDescending(candidate => candidate.CapturedAtUtc)
                .ToArray());

            WriteAtomically(historyPath, JsonSerializer.Serialize(updated, JsonOptions));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPRUN305", $"Workflow history could not be persisted atomically: {exception.Message}"));
            updated = new WorkflowHistorySnapshot(Array.Empty<WorkflowHistoryEntry>());
        }

        return new WorkflowHistoryMutationResult(updated, diagnostics);
    }

    private static WorkflowHistoryEntry ApplyHistoryRetention(WorkflowHistoryEntry entry, bool retainSensitiveContent)
    {
        return new WorkflowHistoryEntry(
            entry.RunId,
            entry.ReleaseTag,
            entry.ExecutionMode,
            entry.State,
            entry.CapturedAtUtc,
            entry.SelectedSkillId,
            entry.HadSensitiveContent,
            retainSensitiveContent ? entry.RetainedContent : null);
    }

    public static WorkflowHistorySnapshot ReadHistorySnapshot(string workspaceDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceDirectory);
        var diagnostics = new List<ParseDiagnostic>();
        return ReadHistory(Path.Combine(workspaceDirectory, HistoryFileName), diagnostics);
    }

    public static WorkflowHistoryMutationResult DeleteHistory(string workspaceDirectory, Func<WorkflowHistoryEntry, bool>? predicate = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceDirectory);

        var diagnostics = new List<ParseDiagnostic>();
        var historyPath = Path.Combine(workspaceDirectory, HistoryFileName);
        var snapshot = ReadHistory(historyPath, diagnostics);
        var filtered = predicate is null
            ? Array.Empty<WorkflowHistoryEntry>()
            : snapshot.Entries.Where(entry => !predicate(entry)).ToArray();
        var updated = new WorkflowHistorySnapshot(filtered);

        try
        {
            WriteAtomically(historyPath, JsonSerializer.Serialize(updated, JsonOptions));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPRUN306", $"Workflow history could not be updated atomically: {exception.Message}"));
        }

        return new WorkflowHistoryMutationResult(updated, diagnostics);
    }

    private static WorkflowHistorySnapshot ReadHistory(string historyPath, List<ParseDiagnostic> diagnostics)
    {
        if (!File.Exists(historyPath))
        {
            return new WorkflowHistorySnapshot(Array.Empty<WorkflowHistoryEntry>());
        }

        var content = TryReadWithRecovery(historyPath, diagnostics);
        if (content is null)
        {
            return new WorkflowHistorySnapshot(Array.Empty<WorkflowHistoryEntry>());
        }

        try
        {
            return JsonSerializer.Deserialize<WorkflowHistorySnapshot>(content, JsonOptions)
                ?? new WorkflowHistorySnapshot(Array.Empty<WorkflowHistoryEntry>());
        }
        catch (JsonException exception)
        {
            diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPRUN307", $"Workflow history is not valid JSON: {exception.Message}"));
            return new WorkflowHistorySnapshot(Array.Empty<WorkflowHistoryEntry>());
        }
    }

    private static WorkflowPersistenceEnvelope? DeserializeEnvelope(string content, List<ParseDiagnostic> diagnostics, string targetPath, bool allowBackupRecovery)
    {
        try
        {
            return JsonSerializer.Deserialize<WorkflowPersistenceEnvelope>(content, JsonOptions);
        }
        catch (JsonException exception)
        {
            if (allowBackupRecovery)
            {
                var backupEnvelope = TryLoadEnvelopeFromBackup(targetPath, diagnostics, exception);
                if (backupEnvelope is not null)
                {
                    return backupEnvelope;
                }
            }

            diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPRUN303", $"Workflow state file is not valid JSON: {exception.Message}"));
            return null;
        }
    }

    private static WorkflowPersistenceEnvelope? TryLoadEnvelopeFromBackup(string targetPath, List<ParseDiagnostic> diagnostics, JsonException primaryException)
    {
        var backupPath = targetPath + BackupExtension;
        if (!File.Exists(backupPath))
        {
            return null;
        }

        try
        {
            var backupContent = File.ReadAllText(backupPath);
            var backupEnvelope = DeserializeEnvelope(backupContent, diagnostics, targetPath, allowBackupRecovery: false);
            if (backupEnvelope is not null)
            {
                diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Warning, "SPRUN311", $"Recovered workflow state from backup after primary JSON corruption: {primaryException.Message}"));
            }

            return backupEnvelope;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPRUN312", $"Workflow backup could not be loaded after primary JSON corruption: {exception.Message}"));
            return null;
        }
    }

    private static string? TryReadWithRecovery(string targetPath, List<ParseDiagnostic> diagnostics)
    {
        try
        {
            return File.ReadAllText(targetPath);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            var backupPath = targetPath + BackupExtension;
            if (File.Exists(backupPath))
            {
                diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Warning, "SPRUN308", $"Recovered workflow data from backup after primary read failure: {exception.Message}"));
                return File.ReadAllText(backupPath);
            }

            diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPRUN309", $"Workflow data could not be read and no backup was available: {exception.Message}"));
            return null;
        }
    }

    private static void WriteAtomically(string targetPath, string content)
    {
        var directory = Path.GetDirectoryName(targetPath)!;
        Directory.CreateDirectory(directory);
        var tempPath = targetPath + ".tmp";
        var backupPath = targetPath + BackupExtension;

        File.WriteAllText(tempPath, content);

        if (File.Exists(targetPath))
        {
            File.Replace(tempPath, targetPath, backupPath, ignoreMetadataErrors: true);
        }
        else
        {
            File.Move(tempPath, targetPath);
            File.WriteAllText(backupPath, content);
        }
    }
}
