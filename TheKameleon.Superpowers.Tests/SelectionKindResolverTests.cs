using TheKameleon.Superpowers.Skills.Context;

namespace TheKameleon.Superpowers.Tests;

public sealed class SelectionKindResolverTests
{
    [Theory]
    [InlineData(@"C:\repo\File.cs", SelectionKindResolver.FileKind)]
    [InlineData(@"C:\repo\Sub\Other.txt", SelectionKindResolver.FileKind)]
    public void InferKindReturnsFileWhenPathHasExtension(string path, string expectedKind)
    {
        var kind = SelectionKindResolver.InferKind(path);

        Assert.Equal(expectedKind, kind);
    }

    [Theory]
    [InlineData(@"C:\repo\Folder")]
    [InlineData(@"C:\repo\Folder\")]
    public void InferKindReturnsFolderWhenPathHasNoExtension(string path)
    {
        var kind = SelectionKindResolver.InferKind(path);

        Assert.Equal(SelectionKindResolver.FolderKind, kind);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void InferKindReturnsFolderWhenPathIsNullOrWhitespace(string? path)
    {
        var kind = SelectionKindResolver.InferKind(path);

        Assert.Equal(SelectionKindResolver.FolderKind, kind);
    }
}
