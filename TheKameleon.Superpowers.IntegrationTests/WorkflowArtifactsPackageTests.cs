using System.IO.Compression;

namespace TheKameleon.Superpowers.IntegrationTests;

public sealed class WorkflowArtifactsPackageTests
{
    [Fact]
    public void ExtensionPackageStillIncludesBundledCatalogAfterWorkflowChanges()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TheKameleon.Superpowers.Vsix.vsix");
        Assert.True(File.Exists(path), $"Build the integration test project to generate the extension package: {path}");

        using var package = ZipFile.OpenRead(path);
        Assert.NotNull(package.GetEntry("bundled-catalog/obra.superpowers/2026-09-21/catalog.json"));
    }
}
