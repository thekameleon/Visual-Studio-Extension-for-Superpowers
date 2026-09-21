using System.IO.Compression;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Xml.Linq;

namespace TheKameleon.Superpowers.IntegrationTests;

public sealed class BuildIdentityPackageTests
{
    [Fact]
    public void BothPackagesAndTheirDllsShareOneBuildVersion()
    {
        var bridge = ReadIdentity("TheKameleon.Superpowers.InProcess");
        var modern = ReadIdentity("TheKameleon.Superpowers.Vsix");

        Assert.Equal(bridge.PackageVersion, modern.PackageVersion);
        Assert.Equal(bridge.PackageVersion, bridge.FileVersion);
        Assert.Equal(modern.PackageVersion, modern.FileVersion);
        Assert.Equal(bridge.FileVersion, bridge.InformationalVersion);
        Assert.Equal(modern.FileVersion, modern.InformationalVersion);
        Assert.True(Version.Parse(bridge.PackageVersion).Revision > 0);
        Assert.Equal(new Version(1, 0, 0, 0), bridge.AssemblyVersion);
        Assert.Equal(new Version(1, 0, 1, 0), modern.AssemblyVersion);
    }

    private static (string PackageVersion, string FileVersion, string InformationalVersion, Version AssemblyVersion) ReadIdentity(string name)
    {
        using var package = ZipFile.OpenRead(Path.Combine(AppContext.BaseDirectory, name + ".vsix"));
        using var manifestStream = package.GetEntry("extension.vsixmanifest")!.Open();
        var manifest = XDocument.Load(manifestStream);
        XNamespace ns = "http://schemas.microsoft.com/developer/vsx-schema/2011";
        var version = (string)Assert.Single(manifest.Descendants(ns + "Identity")).Attribute("Version")!;
        using var stream = package.GetEntry(name + ".dll")!.Open();
        using var assembly = new MemoryStream();
        stream.CopyTo(assembly);
        assembly.Position = 0;
        using var pe = new PEReader(assembly);
        var metadata = pe.GetMetadataReader();
        var definition = metadata.GetAssemblyDefinition();
        var attributes = definition.GetCustomAttributes().Select(metadata.GetCustomAttribute).ToArray();

        string ReadAttribute(string typeName)
        {
            var attribute = Assert.Single(attributes, attribute =>
            {
                if (attribute.Constructor.Kind != HandleKind.MemberReference) return false;
                var constructor = metadata.GetMemberReference((MemberReferenceHandle)attribute.Constructor);
                return constructor.Parent.Kind == HandleKind.TypeReference &&
                    metadata.GetString(metadata.GetTypeReference((TypeReferenceHandle)constructor.Parent).Name) == typeName;
            });
            var reader = metadata.GetBlobReader(attribute.Value);
            Assert.Equal(1, reader.ReadUInt16());
            return reader.ReadSerializedString()!;
        }

        Assert.Contains(metadata.TypeDefinitions, handle =>
            metadata.GetString(metadata.GetTypeDefinition(handle).Name) == "BuildIdentity");
        return (version, ReadAttribute("AssemblyFileVersionAttribute"),
            ReadAttribute("AssemblyInformationalVersionAttribute"), definition.Version);
    }
}
