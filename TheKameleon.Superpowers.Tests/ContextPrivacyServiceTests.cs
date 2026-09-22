using TheKameleon.Superpowers.Core.Contracts.Context;
using TheKameleon.Superpowers.Core.Contracts.Settings;
using TheKameleon.Superpowers.Skills.Context;

namespace TheKameleon.Superpowers.Tests;

public sealed class ContextPrivacyServiceTests
{
    [Fact]
    public void ApplyRedactsExcludedAndSensitiveContentAndTruncatesLongText()
    {
        var provenance = new ContextProvenance("test", DateTimeOffset.UtcNow);
        var snapshot = new ContextCaptureSnapshot(
            provenance,
            new SolutionContextSnapshot(ContextValueState.Available, provenance, "Solution", "C:/repo/TheKameleon.Superpowers.slnx"),
            activeDocument: new DocumentContextSnapshot(
                ContextValueState.Available,
                provenance,
                "C:/repo/secrets.env",
                "secrets.env",
                isOpen: true,
                isDirty: true,
                new CapturedTextValue(ContextValueState.Available, "API_KEY=abcdef", 14, 14, containsSensitiveContent: true)),
            openDocuments: new[]
            {
                new DocumentContextSnapshot(
                    ContextValueState.Available,
                    provenance,
                    "C:/repo/Notes.txt",
                    "Notes.txt",
                    isOpen: true,
                    isDirty: false,
                    new CapturedTextValue(ContextValueState.Available, new string('a', 50), 50, 50))
            },
            diagnostics: Array.Empty<ContextCaptureDiagnostic>());

        var settings = new SuperpowersSettings(
            maxContextCharacters: 10,
            exclusions: new[] { new ExclusionRule(".env") });

        var result = ContextPrivacyService.Apply(snapshot, settings);

        Assert.Equal(ContextValueState.Redacted, result.Snapshot.ActiveDocument!.State);
        Assert.NotNull(result.Snapshot.ActiveDocument.Content);
        Assert.Equal(ContextValueState.Redacted, result.Snapshot.ActiveDocument.Content!.State);
        Assert.Equal(ContextValueState.Truncated, result.Snapshot.OpenDocuments[0].Content!.State);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "SPCTX601");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "SPCTX603");
    }

    [Fact]
    public void BridgeMapperCreatesPortableCompilerAndSemanticSnapshots()
    {
        var compiler = BridgeContextMapper.MapCompilerDiagnostics(
            "bridge",
            2,
            new[]
            {
                new TheKameleon.Superpowers.Bridge.Contracts.CompilerDiagnosticInfo
                {
                    Id = "CS1000",
                    Severity = "Error",
                    Message = "Bad",
                    FilePath = "C:/repo/File.cs",
                    StartLine = 1,
                    StartColumn = 2
                }
            },
            DateTimeOffset.UtcNow);
        var semantic = BridgeContextMapper.MapSemanticTarget(
            "bridge",
            new TheKameleon.Superpowers.Bridge.Contracts.SemanticTargetInfo
            {
                Kind = "Method",
                Name = "Run",
                DisplayName = "Program.Run()",
                FilePath = "C:/repo/File.cs",
                StartLine = 3,
                StartColumn = 4
            },
            DateTimeOffset.UtcNow);

        Assert.Equal(ContextValueState.Available, compiler.State);
        Assert.Single(compiler.Diagnostics);
        Assert.Equal(2, compiler.TotalCount);
        Assert.Equal(ContextValueState.Available, semantic.State);
        Assert.Equal("Run", semantic.Name);
    }

    [Fact]
    public void BridgeMapperCreatesPortableActiveDocumentSnapshot()
    {
        var document = BridgeContextMapper.MapDocumentText(
            "bridge",
            new TheKameleon.Superpowers.Bridge.Contracts.DocumentTextInfo
            {
                FilePath = "C:/repo/File.cs",
                DisplayName = "File.cs",
                Text = "token=abcd",
                OriginalLength = 10,
                CapturedLength = 10,
                IsOpen = true,
                IsDirty = true,
                Detail = "Captured by bridge."
            },
            DateTimeOffset.UtcNow);

        Assert.Equal(ContextValueState.Available, document.State);
        Assert.Equal("C:/repo/File.cs", document.FilePath);
        Assert.True(document.IsDirty);
        Assert.NotNull(document.Content);
        Assert.Equal("token=abcd", document.Content!.Value);
        Assert.True(document.Provenance.IsBridgeData);
    }

    [Fact]
    public void BridgeMapperMarksUnavailableActiveDocumentTextHonestly()
    {
        var document = BridgeContextMapper.MapDocumentText("bridge", null, DateTimeOffset.UtcNow);

        Assert.Equal(ContextValueState.Unavailable, document.State);
        Assert.NotNull(document.Content);
        Assert.Equal(ContextValueState.Unavailable, document.Content!.State);
        Assert.Contains("unavailable", document.Content.Detail!, StringComparison.OrdinalIgnoreCase);
    }
}
