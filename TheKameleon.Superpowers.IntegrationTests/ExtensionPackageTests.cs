using System.IO.Compression;
using System.Text.Json;
using System.Xml.Linq;

namespace TheKameleon.Superpowers.IntegrationTests
{
    public class ExtensionPackageTests
    {
        private const string PackageName = "TheKameleon.Superpowers.Vsix.vsix";
        private const string AssemblyName = "TheKameleon.Superpowers.Vsix.dll";
        private static readonly XNamespace ManifestNamespace = "http://schemas.microsoft.com/developer/vsx-schema/2011";

        [Fact]
        public void ManifestPreservesExtensionIdentity()
        {
            using var package = OpenPackage();
            var manifest = ReadManifest(package);
            var metadata = manifest.Root!.Element(ManifestNamespace + "Metadata");

            Assert.NotNull(metadata);
            var identity = metadata.Element(ManifestNamespace + "Identity");
            Assert.NotNull(identity);
            Assert.Equal("TheKameleon.Superpowers.Vsix.8a7fab37-7cfc-4314-ae3c-946698f3ff5d", (string?)identity.Attribute("Id"));
            Assert.Equal("TheKameleon", (string?)identity.Attribute("Publisher"));
            Assert.True(Version.TryParse((string?)identity.Attribute("Version"), out _));
            Assert.Equal("Superpowers for Visual Studio", (string?)metadata.Element(ManifestNamespace + "DisplayName"));
        }

        [Fact]
        public void ManifestSelectsModernDotNetHost()
        {
            using var package = OpenPackage();
            var installation = ReadManifest(package).Root!.Element(ManifestNamespace + "Installation");

            Assert.NotNull(installation);
            Assert.Equal("VisualStudio.Extensibility", (string?)installation.Attribute("ExtensionType"));
            Assert.Equal("net8.0", (string?)installation.Element(ManifestNamespace + "DotnetTargetVersions"));
        }

        [Theory]
        [InlineData("amd64")]
        [InlineData("arm64")]
        public void ManifestDeclaresSupportedVisualStudioArchitecture(string architecture)
        {
            using var package = OpenPackage();
            var targets = ReadManifest(package).Descendants(ManifestNamespace + "InstallationTarget");
            var target = Assert.Single(targets, candidate =>
                (string?)candidate.Element(ManifestNamespace + "ProductArchitecture") == architecture);

            Assert.Equal("Microsoft.VisualStudio.Community", (string?)target.Attribute("Id"));
            Assert.Equal("[17.14,)", (string?)target.Attribute("Version"));
        }

        [Fact]
        public void ExtensionRegistersOutOfProcessEntryPoint()
        {
            using var package = OpenPackage();
            using var registrationStream = OpenRequiredEntry(package, ".vsextension/extension.json");
            using var registration = JsonDocument.Parse(registrationStream);
            var services = registration.RootElement.GetProperty("services").EnumerateArray().ToArray();

            Assert.NotEmpty(services);
            Assert.All(services, service =>
            {
                Assert.Equal("dotnetExtensibility", service.GetProperty("host").GetString());
                Assert.False(service.GetProperty("allowHostingInProcess").GetBoolean());
                var entryPoint = service.GetProperty("entryPoint");
                Assert.Equal("TheKameleon.Superpowers.Vsix.SuperpowersExtension", entryPoint.GetProperty("fullClassName").GetString());
                Assert.Equal(AssemblyName, entryPoint.GetProperty("assemblyPath").GetString());
            });
            var assembly = package.GetEntry(AssemblyName);
            Assert.NotNull(assembly);
            Assert.True(assembly.Length > 0, "The registered extension assembly must not be empty.");
        }

        [Theory]
        [InlineData("Microsoft.VisualStudio.VsPackage")]
        [InlineData("Microsoft.VisualStudio.MefComponent")]
        public void ManifestDoesNotRegisterTraditionalInProcessAssets(string assetType)
        {
            using var package = OpenPackage();
            var assets = ReadManifest(package).Descendants(ManifestNamespace + "Asset");

            Assert.DoesNotContain(assets, asset => (string?)asset.Attribute("Type") == assetType);
        }

        [Fact]
        public void ManifestDoesNotRequireLegacyDotNetFramework()
        {
            using var package = OpenPackage();
            var dependencies = ReadManifest(package).Descendants(ManifestNamespace + "Dependency");

            Assert.DoesNotContain(dependencies, dependency =>
                (string?)dependency.Attribute("Id") == "Microsoft.Framework.NDP");
        }

        private static ZipArchive OpenPackage()
        {
            var path = Path.Combine(AppContext.BaseDirectory, PackageName);
            Assert.True(File.Exists(path), $"Build the integration test project to generate the extension package: {path}");
            return ZipFile.OpenRead(path);
        }

        private static XDocument ReadManifest(ZipArchive package)
        {
            using var stream = OpenRequiredEntry(package, "extension.vsixmanifest");
            return XDocument.Load(stream);
        }

        private static Stream OpenRequiredEntry(ZipArchive package, string entryName)
        {
            var entry = package.GetEntry(entryName);
            Assert.NotNull(entry);
            return entry.Open();
        }
    }
}
