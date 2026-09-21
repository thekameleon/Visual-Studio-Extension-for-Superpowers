using TheKameleon.Superpowers.InProcess;

namespace TheKameleon.Superpowers.Tests;

public sealed class SelectionIdentityTests
{
    [Theory]
    [InlineData("Solution", "Solution", @"C:\Repo\App.slnx")]
    [InlineData("Project", "Project", @"C:\Repo\App.csproj")]
    [InlineData("Item", "File", @"C:\Repo\Target.cs")]
    [InlineData("Item", "File", @"\\server\share\Target.cs")]
    public void ReportsCapturedIdentity(string scope, string kind, string path)
    {
        var result = new SelectionIdentity(kind, "Target", path, "Owner", @"C:\Repo\Owner.csproj").Describe(scope);
        Assert.Contains($"Selected kind: {kind}", result);
        Assert.Contains("Name: Target", result);
        Assert.Contains($"Path: {path}", result);
        Assert.Contains("no IPC performed", result);
        if (kind == "File")
        {
            Assert.Contains("Owning project: Owner", result);
            Assert.Contains(@"Project path: C:\Repo\Owner.csproj", result);
        }
    }

    [Theory]
    [InlineData("Project", "File")]
    [InlineData("Solution", "Project")]
    [InlineData("Item", "Folder")]
    [InlineData("Editor", "Editor")]
    public void RejectsScopeMismatch(string scope, string kind)
    {
        Assert.StartsWith("Unavailable:", new SelectionIdentity(kind, "Target", @"C:\Repo\Target.cs").Describe(scope));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Target.cs")]
    [InlineData(@"C:Target.cs")]
    [InlineData(@"\Target.cs")]
    [InlineData("https://example.test/Target.cs")]
    public void RejectsIncompletePaths(string? path)
    {
        Assert.StartsWith("Unavailable:", new SelectionIdentity("Project", "Target", path).Describe("Project"));
    }

    [Fact]
    public void RejectsMissingNameOrOwner()
    {
        Assert.StartsWith("Unavailable:", new SelectionIdentity("Solution", null, @"C:\Repo\App.slnx").Describe("Solution"));
        Assert.StartsWith("Unavailable:", new SelectionIdentity("File", "Target", @"C:\Repo\Target.cs").Describe("Item"));
    }

    [Fact]
    public void KeepsLinkedFileOwnershipDistinct()
    {
        var first = new SelectionIdentity("File", "Target.cs", @"C:\Shared\Target.cs", "First", @"C:\Repo\First.csproj").Describe("Item");
        var second = new SelectionIdentity("File", "Target.cs", @"C:\Shared\Target.cs", "Second", @"C:\Repo\Second.csproj").Describe("Item");
        Assert.Contains("Owning project: First", first);
        Assert.Contains("Owning project: Second", second);
        Assert.NotEqual(first, second);
    }
}
