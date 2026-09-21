using System.IO.Compression;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Resources;
using System.Xml.Linq;

namespace TheKameleon.Superpowers.IntegrationTests;

public sealed class InProcessBridgePackageTests
{
    [Theory]
    [InlineData("CodeWindowCommand", "IDM_VS_CTXT_CODEWIN")]
    [InlineData("ProjectCommand", "IDM_VS_CTXT_PROJNODE")]
    [InlineData("ItemCommand", "IDM_VS_CTXT_ITEMNODE")]
    [InlineData("SolutionCommand", "IDM_VS_CTXT_SOLNNODE")]
    [InlineData("CompilerDiagnosticsCommand", "IDM_VS_CTXT_CODEWIN")]
    public void ContextCommandParentsAGroupOnTheExpectedMenu(string commandId, string menuId)
    {
        var table = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "SuperpowersBridge.vsct"));
        XNamespace ns = "http://schemas.microsoft.com/VisualStudio/2005-10-18/CommandTable";
        var button = Assert.Single(table.Descendants(ns + "Button"),
            element => (string?)element.Attribute("id") == commandId);
        var parent = Assert.Single(button.Elements(ns + "Parent"));
        var group = Assert.Single(table.Descendants(ns + "Group"),
            element => (string?)element.Attribute("id") == (string?)parent.Attribute("id") &&
                       (string?)element.Attribute("guid") == (string?)parent.Attribute("guid"));
        var menu = Assert.Single(group.Elements(ns + "Parent"));

        Assert.Equal("guidSHLMainMenu", (string?)menu.Attribute("guid"));
        Assert.Equal(menuId, (string?)menu.Attribute("id"));
    }

    [Fact]
    public void BridgeAssemblyContainsCompiledMenuResource()
    {
        using var package = OpenPackage();
        using var stream = OpenRequiredEntry(package, "TheKameleon.Superpowers.InProcess.dll");
        using var assembly = new MemoryStream();
        stream.CopyTo(assembly);
        assembly.Position = 0;
        using var peReader = new PEReader(assembly);
        var metadata = peReader.GetMetadataReader();

        var resourceNames = new List<string>();
        foreach (var handle in metadata.ManifestResources)
        {
            var resource = metadata.GetManifestResource(handle);
            if (!resource.Implementation.IsNil ||
                !metadata.GetString(resource.Name).EndsWith(".resources", StringComparison.Ordinal))
            {
                continue;
            }

            var section = peReader.GetSectionData(peReader.PEHeaders.CorHeader!.ResourcesDirectory.RelativeVirtualAddress);
            var reader = section.GetReader(checked((int)resource.Offset), section.Length - checked((int)resource.Offset));
            var length = reader.ReadInt32();
            using var resourceStream = new MemoryStream(reader.ReadBytes(length));
            using var resources = new ResourceReader(resourceStream);
            var entries = resources.GetEnumerator();
            while (entries.MoveNext())
            {
                resourceNames.Add((string)entries.Key);
            }
        }

        Assert.Contains("SuperpowersBridge.CTMENU", resourceNames);
        using var registrationStream = OpenRequiredEntry(package, "TheKameleon.Superpowers.InProcess.pkgdef");
        using var registrationReader = new StreamReader(registrationStream);
        Assert.Contains("SuperpowersBridge.CTMENU", registrationReader.ReadToEnd());
    }

    [Fact]
    public void PackageContainsBridgeRegistrationAndContracts()
    {
        using var package = OpenPackage();

        Assert.NotNull(package.GetEntry("TheKameleon.Superpowers.InProcess.dll"));
        Assert.NotNull(package.GetEntry("TheKameleon.Superpowers.InProcess.pkgdef"));
        Assert.NotNull(package.GetEntry("TheKameleon.Superpowers.Bridge.Contracts.dll"));
    }

    [Fact]
    public void ManifestTargetsSupportedVisualStudioGenerations()
    {
        using var package = OpenPackage();
        using var stream = OpenRequiredEntry(package, "extension.vsixmanifest");
        var manifest = XDocument.Load(stream);
        XNamespace ns = "http://schemas.microsoft.com/developer/vsx-schema/2011";

        var target = Assert.Single(manifest.Descendants(ns + "InstallationTarget"));
        Assert.Equal("Microsoft.VisualStudio.Community", (string?)target.Attribute("Id"));
        Assert.Equal("[17.14,19.0)", (string?)target.Attribute("Version"));

        var asset = Assert.Single(manifest.Descendants(ns + "Asset"));
        Assert.Equal("Microsoft.VisualStudio.VsPackage", (string?)asset.Attribute("Type"));
    }

    [Fact]
    public void BridgeAssemblyReferencesContractsButNotProductLayers()
    {
        using var package = OpenPackage();
        using var assemblyStream = OpenRequiredEntry(package, "TheKameleon.Superpowers.InProcess.dll");
        using var assembly = new MemoryStream();
        assemblyStream.CopyTo(assembly);
        assembly.Position = 0;
        using var peReader = new PEReader(assembly);
        var metadata = peReader.GetMetadataReader();
        var references = metadata.AssemblyReferences
            .Select(metadata.GetAssemblyReference)
            .Select(reference => metadata.GetString(reference.Name))
            .ToArray();

        Assert.Contains("TheKameleon.Superpowers.Bridge.Contracts", references);
        Assert.DoesNotContain("TheKameleon.Superpowers.Core", references);
        Assert.DoesNotContain("TheKameleon.Superpowers.Skills", references);
        Assert.DoesNotContain("TheKameleon.Superpowers.Vsix", references);
    }

    private static ZipArchive OpenPackage()
    {
        return ZipFile.OpenRead(Path.Combine(AppContext.BaseDirectory, "TheKameleon.Superpowers.InProcess.vsix"));
    }

    private static Stream OpenRequiredEntry(ZipArchive package, string name)
    {
        var entry = package.GetEntry(name);
        Assert.NotNull(entry);
        return entry.Open();
    }
}