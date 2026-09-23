using TheKameleon.Superpowers.Core.Contracts.Runs;
using TheKameleon.Superpowers.Skills.Execution;

namespace TheKameleon.Superpowers.Tests;

public sealed class PreviewHandoffServiceTests
{
    [Fact]
    public void CreateProducesAwaitingExternalActionRecord()
    {
        var record = PreviewHandoffService.Create("run-1", "prompt text", HandoffFallbackKind.PreviewCopy, DateTimeOffset.UtcNow);

        Assert.Equal(PreviewHandoffState.AwaitingExternalAction, record.State);
        Assert.Null(record.ImportedResult);
    }

    [Fact]
    public void ImportResultSucceedsFromAwaitingExternalAction()
    {
        var record = PreviewHandoffService.Create("run-1", "prompt text", HandoffFallbackKind.ManualImport, DateTimeOffset.UtcNow);

        var result = PreviewHandoffService.ImportResult(record, "the pasted Copilot response", DateTimeOffset.UtcNow);

        Assert.True(result.Succeeded);
        Assert.Equal(PreviewHandoffState.ResultImported, result.Record.State);
        Assert.Equal("the pasted Copilot response", result.Record.ImportedResult);
    }

    [Fact]
    public void ImportResultFailsWhenAlreadyImported()
    {
        var record = PreviewHandoffService.Create("run-1", "prompt text", HandoffFallbackKind.ManualImport, DateTimeOffset.UtcNow);
        var imported = PreviewHandoffService.ImportResult(record, "first import", DateTimeOffset.UtcNow).Record;

        var result = PreviewHandoffService.ImportResult(imported, "second import", DateTimeOffset.UtcNow);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void ImportResultFailsForBlankResult()
    {
        var record = PreviewHandoffService.Create("run-1", "prompt text", HandoffFallbackKind.ManualImport, DateTimeOffset.UtcNow);

        var result = PreviewHandoffService.ImportResult(record, "   ", DateTimeOffset.UtcNow);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void DiscardSucceedsFromAwaitingExternalAction()
    {
        var record = PreviewHandoffService.Create("run-1", "prompt text", HandoffFallbackKind.PreviewCopy, DateTimeOffset.UtcNow);

        var result = PreviewHandoffService.Discard(record);

        Assert.True(result.Succeeded);
        Assert.Equal(PreviewHandoffState.Discarded, result.Record.State);
    }

    [Fact]
    public void DiscardFailsWhenAlreadyDiscarded()
    {
        var record = PreviewHandoffService.Create("run-1", "prompt text", HandoffFallbackKind.PreviewCopy, DateTimeOffset.UtcNow);
        var discarded = PreviewHandoffService.Discard(record).Record;

        var result = PreviewHandoffService.Discard(discarded);

        Assert.False(result.Succeeded);
    }
}
