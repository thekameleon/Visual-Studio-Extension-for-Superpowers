using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using TheKameleon.Superpowers.InProcess;

namespace TheKameleon.Superpowers.Tests;

public sealed class DocumentCompilerDiagnosticsTests
{
    private const string FilePath = @"C:\Probe\Target.cs";

    [Fact]
    public async Task ReadsCompilerErrorWithSourceLocation()
    {
        using var workspace = new AdhocWorkspace();
        const string code = "#error TKSPROBE_READ\nclass Target { }";
        var result = await Read(CreateSolution(workspace, code), code);
        Assert.NotNull(result);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("CS1029", diagnostic.Id);
        Assert.Equal("Error", diagnostic.Severity);
        Assert.Contains("TKSPROBE_READ", diagnostic.Message);
        Assert.Equal(FilePath, diagnostic.FilePath);
        Assert.Equal(0, diagnostic.StartLine);
        Assert.Contains("line 1, column", result.Describe(FilePath));
    }

    [Fact]
    public async Task CleanDocumentIsSuccessfulZeroAndExcludesOtherFiles()
    {
        using var workspace = new AdhocWorkspace();
        const string code = "class Target { }";
        var solution = CreateSolution(workspace, code);
        solution = solution.AddDocument(DocumentId.CreateNewId(solution.ProjectIds.Single()), "Other.cs",
            SourceText.From("#error OTHER_FILE"), filePath: @"C:\Probe\Other.cs");
        var result = await Read(solution, code);
        Assert.NotNull(result);
        Assert.Equal(0, result.TotalCount);
        Assert.Empty(result.Diagnostics);
        Assert.Contains("Compiler diagnostics: 0", result.Describe(FilePath));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("stale")]
    [InlineData("linked")]
    public async Task RejectsUnavailableDocument(string reason)
    {
        using var workspace = new AdhocWorkspace();
        const string code = "class Target { }";
        var solution = CreateSolution(workspace, code);
        if (reason == "linked")
        {
            var project = ProjectId.CreateNewId();
            solution = solution.AddProject(project, "Linked", "Linked", LanguageNames.CSharp)
                .AddDocument(DocumentId.CreateNewId(project), "Target.cs", SourceText.From(code), filePath: FilePath);
        }
        var result = await DocumentCompilerDiagnostics.ReadAsync(solution,
            reason == "missing" ? @"C:\Missing.cs" : FilePath,
            SourceText.From(reason == "stale" ? code + " " : code), CancellationToken.None);
        Assert.Null(result);
    }

    [Fact]
    public async Task CancellationIsNotSuccessfulZero()
    {
        using var workspace = new AdhocWorkspace();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => DocumentCompilerDiagnostics.ReadAsync(
            workspace.CurrentSolution, FilePath, SourceText.From(""), new CancellationToken(true)));
    }

    [Fact]
    public async Task BoundsDisplayedCountAndMessageLength()
    {
        using var workspace = new AdhocWorkspace();
        var code = string.Join("\n", Enumerable.Repeat("#error " + new string('X', 400), 12));
        var result = await Read(CreateSolution(workspace, code), code);
        Assert.NotNull(result);
        Assert.Equal(12, result.TotalCount);
        Assert.Equal(10, result.Diagnostics.Count);
        Assert.All(result.Diagnostics, diagnostic => Assert.EndsWith("[truncated]", diagnostic.Message));
        Assert.Contains("Showing 10 of 12", result.Describe(FilePath));
    }

    [Fact]
    public async Task SuppressedWarningIsNotReported()
    {
        using var workspace = new AdhocWorkspace();
        const string code = "#pragma warning disable CS1030\n#warning SUPPRESSED\nclass Target { }";
        var result = await Read(CreateSolution(workspace, code), code);
        Assert.NotNull(result);
        Assert.Equal(0, result.TotalCount);
    }

    private static Task<DocumentCompilerDiagnostics?> Read(Solution solution, string code) =>
        DocumentCompilerDiagnostics.ReadAsync(solution, FilePath, SourceText.From(code), CancellationToken.None);

    private static Solution CreateSolution(AdhocWorkspace workspace, string code)
    {
        var project = ProjectId.CreateNewId();
        return workspace.CurrentSolution.AddProject(project, "Probe", "Probe", LanguageNames.CSharp)
            .WithProjectCompilationOptions(project, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .AddMetadataReference(project, MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
            .AddDocument(DocumentId.CreateNewId(project), "Target.cs", SourceText.From(code), filePath: FilePath);
    }
}
