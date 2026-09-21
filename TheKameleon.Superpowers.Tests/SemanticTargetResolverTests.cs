using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using TheKameleon.Superpowers.InProcess;

namespace TheKameleon.Superpowers.Tests;

public sealed class SemanticTargetResolverTests
{
    private const string Path = "C:\\Probe\\Target.cs";
    private const string Code = "class Target\n{\n    void Run() { int value = 1; }\n}";

    [Theory]
    [InlineData("Target", "NamedType", "Target", 0)]
    [InlineData("value", "Method", "Run", 2)]
    public async Task ResolvesEnclosingDeclaration(string marker, string kind, string name, int line)
    {
        using var workspace = new AdhocWorkspace();
        var target = await SemanticTargetResolver.ResolveAsync(CreateSolution(workspace), Path,
            SourceText.From(Code), Code.IndexOf(marker, StringComparison.Ordinal), CancellationToken.None);

        Assert.NotNull(target);
        Assert.Equal(kind, target.Kind);
        Assert.Equal(name, target.Name);
        Assert.Equal(Path, target.FilePath);
        Assert.Equal(line, target.StartLine);
        Assert.Contains(name, target.DisplayName);
    }

    [Fact]
    public async Task RejectsAmbiguousLinkedPath()
    {
        using var workspace = new AdhocWorkspace();
        var solution = CreateSolution(workspace);
        var other = ProjectId.CreateNewId();
        solution = solution.AddProject(other, "Other", "Other", LanguageNames.CSharp)
            .AddDocument(DocumentId.CreateNewId(other), "Target.cs", SourceText.From(Code), filePath: Path);

        Assert.Null(await SemanticTargetResolver.ResolveAsync(solution, Path,
            SourceText.From(Code), 6, CancellationToken.None));
    }

    [Fact]
    public async Task RejectsStaleText()
    {
        using var workspace = new AdhocWorkspace();
        Assert.Null(await SemanticTargetResolver.ResolveAsync(CreateSolution(workspace), Path,
            SourceText.From(Code + " "), 6, CancellationToken.None));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1000)]
    public async Task RejectsOutOfRangeCaret(int position)
    {
        using var workspace = new AdhocWorkspace();
        Assert.Null(await SemanticTargetResolver.ResolveAsync(CreateSolution(workspace), Path,
            SourceText.From(Code), position, CancellationToken.None));
    }

    [Fact]
    public async Task ReturnsUnavailableForMissingDocument()
    {
        using var workspace = new AdhocWorkspace();
        Assert.Null(await SemanticTargetResolver.ResolveAsync(CreateSolution(workspace), "C:\\Other.cs",
            SourceText.From(Code), 6, CancellationToken.None));
    }

    [Fact]
    public async Task ReturnsUnavailableForEmptyDocumentAndEndOfFile()
    {
        using var workspace = new AdhocWorkspace();
        Assert.Null(await SemanticTargetResolver.ResolveAsync(CreateSolution(workspace, ""), Path,
            SourceText.From(""), 0, CancellationToken.None));
        Assert.Null(await SemanticTargetResolver.ResolveAsync(CreateSolution(workspace), Path,
            SourceText.From(Code), Code.Length, CancellationToken.None));
    }

    [Fact]
    public async Task HonorsCancellationEvenWithoutDocument()
    {
        using var workspace = new AdhocWorkspace();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => SemanticTargetResolver.ResolveAsync(
            workspace.CurrentSolution, Path, SourceText.From(Code), 6, cancellation.Token));
    }

    private static Solution CreateSolution(AdhocWorkspace workspace, string code = Code)
    {
        var project = ProjectId.CreateNewId();
        return workspace.CurrentSolution.AddProject(project, "Probe", "Probe", LanguageNames.CSharp)
            .AddDocument(DocumentId.CreateNewId(project), "Target.cs", SourceText.From(code), filePath: Path);
    }
}
