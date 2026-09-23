using TheKameleon.Superpowers.Core.Contracts.Catalog;
using TheKameleon.Superpowers.Core.Contracts.Context;
using TheKameleon.Superpowers.Core.Contracts.Settings;
using TheKameleon.Superpowers.Skills.Workflow;

namespace TheKameleon.Superpowers.Tests;

public sealed class PromptComposerTests
{
    private static ParsedSkillDocument CreateSkill() => new(
        "Plan",
        "Plan a change before implementing it.",
        "Follow the upstream brainstorming and writing-plans methodology.",
        Array.Empty<SkillReference>(),
        Array.Empty<ParseDiagnostic>());

    private static ContextCaptureSnapshot CreateContext(
        DocumentContextSnapshot? activeDocument = null,
        SelectionContextSnapshot? selection = null,
        IReadOnlyList<ContextCaptureDiagnostic>? diagnostics = null)
    {
        var provenance = new ContextProvenance("test", DateTimeOffset.UtcNow);
        var solution = new SolutionContextSnapshot(ContextValueState.Available, provenance, "MySolution", @"C:\repo\My.sln");
        return new ContextCaptureSnapshot(provenance, solution, activeDocument: activeDocument, selection: selection, diagnostics: diagnostics);
    }

    [Fact]
    public void ComposeIncludesSkillNameDescriptionModeAndBody()
    {
        var prompt = PromptComposer.Compose(CreateSkill(), ExecutionMode.Guided, CreateContext());

        Assert.Contains("# Plan", prompt.Text);
        Assert.Contains("Plan a change before implementing it.", prompt.Text);
        Assert.Contains("Execution mode: Guided", prompt.Text);
        Assert.Contains("Follow the upstream brainstorming", prompt.Text);
        Assert.Equal("Plan", prompt.SkillName);
        Assert.Equal(ExecutionMode.Guided, prompt.ExecutionMode);
    }

    [Fact]
    public void ComposeDescribesSolutionState()
    {
        var prompt = PromptComposer.Compose(CreateSkill(), ExecutionMode.Full, CreateContext());

        Assert.Contains("Solution: captured (MySolution).", prompt.Text);
    }

    [Fact]
    public void ComposeReportsUnavailableActiveDocumentHonestly()
    {
        var prompt = PromptComposer.Compose(CreateSkill(), ExecutionMode.ApprovalRequired, CreateContext());

        Assert.Contains("Active document: not available.", prompt.Text);
    }

    [Fact]
    public void ComposeIncludesRedactedDocumentStateWithoutContent()
    {
        var provenance = new ContextProvenance("test", DateTimeOffset.UtcNow);
        var redactedDocument = new DocumentContextSnapshot(
            ContextValueState.Redacted,
            provenance,
            filePath: "secrets.config",
            displayName: "secrets.config",
            content: new CapturedTextValue(ContextValueState.Redacted, null, detail: "Excluded by privacy rules."));

        var prompt = PromptComposer.Compose(CreateSkill(), ExecutionMode.Guided, CreateContext(activeDocument: redactedDocument));

        Assert.Contains("Active document: redacted by privacy rules.", prompt.Text);
        Assert.DoesNotContain("```", prompt.Text);
    }

    [Fact]
    public void ComposeIncludesCapturedDocumentContent()
    {
        var provenance = new ContextProvenance("test", DateTimeOffset.UtcNow);
        var document = new DocumentContextSnapshot(
            ContextValueState.Available,
            provenance,
            filePath: "Program.cs",
            displayName: "Program.cs",
            content: new CapturedTextValue(ContextValueState.Available, "Console.WriteLine(\"hi\");"));

        var prompt = PromptComposer.Compose(CreateSkill(), ExecutionMode.Guided, CreateContext(activeDocument: document));

        Assert.Contains("Console.WriteLine(\"hi\");", prompt.Text);
    }

    [Fact]
    public void ComposeAppendsContextDiagnostics()
    {
        var diagnostics = new[] { new ContextCaptureDiagnostic("SPCTX601", "Excluded a file.", "Info") };

        var prompt = PromptComposer.Compose(CreateSkill(), ExecutionMode.Guided, CreateContext(diagnostics: diagnostics));

        Assert.Contains("## Context diagnostics", prompt.Text);
        Assert.Contains("SPCTX601: Excluded a file.", prompt.Text);
    }

    [Fact]
    public void ComposeThrowsForNullSkillOrContext()
    {
        Assert.Throws<ArgumentNullException>(() => PromptComposer.Compose(null!, ExecutionMode.Guided, CreateContext()));
        Assert.Throws<ArgumentNullException>(() => PromptComposer.Compose(CreateSkill(), ExecutionMode.Guided, null!));
    }
}
