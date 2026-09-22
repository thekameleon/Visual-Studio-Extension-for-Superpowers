using System.IO.Compression;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using System.Xml.Linq;

namespace TheKameleon.Superpowers.IntegrationTests
{
    public class PlanToolWindowPackageTests
    {
        [Fact]
        public void LocalizedPackageUsesNewFileVersionWithStableAssemblyIdentity()
        {
            using var package = OpenPackage();
            using var manifestStream = OpenRequiredEntry(package, "extension.vsixmanifest");
            var manifest = XDocument.Load(manifestStream);
            XNamespace manifestNamespace = "http://schemas.microsoft.com/developer/vsx-schema/2011";
            var identity = Assert.Single(manifest.Descendants(manifestNamespace + "Identity"));
            Assert.True(Version.TryParse((string?)identity.Attribute("Version"), out var packageVersion));
            Assert.NotNull(packageVersion);
            Assert.True(packageVersion > new Version(1, 0, 0, 0),
                "The localized package must update the original 1.0.0.0 installation, not reuse its cached metadata identity.");

            using var assemblyStream = OpenRequiredEntry(package, "TheKameleon.Superpowers.Vsix.dll");
            using var assembly = new MemoryStream();
            assemblyStream.CopyTo(assembly);
            assembly.Position = 0;
            using var peReader = new PEReader(assembly);
            var metadata = peReader.GetMetadataReader();
            var definition = metadata.GetAssemblyDefinition();
            Assert.Equal(new Version(1, 0, 1, 0), definition.Version);
            var fileVersion = Assert.Single(definition.GetCustomAttributes(), handle =>
                GetAttributeTypeName(metadata, handle) == "AssemblyFileVersionAttribute");
            var reader = metadata.GetBlobReader(metadata.GetCustomAttribute(fileVersion).Value);
            Assert.Equal(1, reader.ReadUInt16());
            Assert.Equal(packageVersion, Version.Parse(reader.ReadSerializedString()!));
        }

        [Fact]
        public void PackageRegistersPlanCommand()
        {
            using var package = OpenPackage();
            using var stream = OpenRequiredEntry(package, ".vsextension/extension.json");
            using var registration = JsonDocument.Parse(stream);
            var commands = registration.RootElement.GetProperty("commandSets").EnumerateArray()
                .SelectMany(commandSet => commandSet.GetProperty("commands").EnumerateArray());

            var command = Assert.Single(commands, command =>
                command.GetProperty("name").GetString() == "TheKameleon.Superpowers.Vsix.PlanCommand");
            AssertLocalizedDisplayName(package, command, "Superpowers.PlanCommand.DisplayName", "Plan");
            Assert.Equal("None", command.GetProperty("flags").GetString());
        }

        [Theory]
        [InlineData("ContextProbeCommand", "Superpowers.ContextProbeCommand.DisplayName", "Probe Context")]
        [InlineData("PublishDiagnosticProbeCommand", "Superpowers.PublishDiagnosticProbeCommand.DisplayName", "Publish Probe Diagnostic")]
        [InlineData("ClearDiagnosticProbeCommand", "Superpowers.ClearDiagnosticProbeCommand.DisplayName", "Clear Probe Diagnostic")]
        [InlineData("BuildProbeCommand", "Superpowers.BuildProbeCommand.DisplayName", "Probe Selected Project Build")]
        public void PackageRegistersCapabilityProbeCommand(string typeName, string resourceId, string displayName)
        {
            using var package = OpenPackage();
            using var stream = OpenRequiredEntry(package, ".vsextension/extension.json");
            using var registration = JsonDocument.Parse(stream);
            var commands = registration.RootElement.GetProperty("commandSets").EnumerateArray()
                .SelectMany(commandSet => commandSet.GetProperty("commands").EnumerateArray());

            var command = Assert.Single(commands, command =>
                command.GetProperty("name").GetString() == $"TheKameleon.Superpowers.Vsix.{typeName}");
            AssertLocalizedDisplayName(package, command, resourceId, displayName);
            Assert.Equal("None", command.GetProperty("flags").GetString());
        }

        [Fact]
        public void PlanCommandIsPlacedUnderSuperpowersInExtensionsMenu()
        {
            using var package = OpenPackage();
            using var stream = OpenRequiredEntry(package, ".vsextension/extension.json");
            using var registration = JsonDocument.Parse(stream);
            var root = registration.RootElement;
            var menu = Assert.Single(root.GetProperty("controlContainers").EnumerateArray(), container =>
                container.GetProperty("name").GetString() == "TheKameleon.Superpowers.Vsix.SuperpowersExtension.SuperpowersMenu");
            Assert.Equal("Menu", menu.GetProperty("type").GetString());
            AssertLocalizedDisplayName(package, menu, "Superpowers.Menu.DisplayName", "Superpowers");

            var placements = root.GetProperty("controlPlacements").EnumerateArray().ToArray();
            var menuPlacement = Assert.Single(placements, placement =>
                placement.GetProperty("controlName").GetString() == menu.GetProperty("name").GetString());
            var legacyParent = menuPlacement.GetProperty("parent").GetProperty("legacyParentId");
            Assert.Equal("d309f791-903f-11d0-9efc-00a0c911004f", legacyParent.GetProperty("guid").GetString());
            Assert.Equal(24576, legacyParent.GetProperty("id").GetInt32());

            var commandPlacement = Assert.Single(placements, placement =>
                placement.GetProperty("controlName").GetString() == "TheKameleon.Superpowers.Vsix.PlanCommand");
            var groupName = commandPlacement.GetProperty("parent").GetProperty("parentName").GetString();
            var groupPlacement = Assert.Single(placements, placement =>
                placement.GetProperty("controlName").GetString() == groupName);
            Assert.Equal(menu.GetProperty("name").GetString(), groupPlacement.GetProperty("parent").GetProperty("parentName").GetString());
        }

        [Fact]
        public void PackageRegistersOneSuperpowersWindowWithOutOfProcessProvider()
        {
            using var package = OpenPackage();
            using var stream = OpenRequiredEntry(package, ".vsextension/extension.json");
            using var registration = JsonDocument.Parse(stream);
            var root = registration.RootElement;
            var window = Assert.Single(root.GetProperty("toolWindows").EnumerateArray(), candidate =>
                candidate.GetProperty("identifier").GetString() == "TheKameleon.Superpowers.Vsix.SuperpowersToolWindow");
            Assert.Equal("DocumentWell", window.GetProperty("placement").GetString());
            Assert.True(window.GetProperty("allowAutoCreation").GetBoolean());
            var provider = Assert.Single(root.GetProperty("services").EnumerateArray(), service =>
                $"{service.GetProperty("name").GetString()};{service.GetProperty("version").GetString()}" == window.GetProperty("serviceMoniker").GetString());
            Assert.Equal("dotnetExtensibility", provider.GetProperty("host").GetString());
            Assert.False(provider.GetProperty("allowHostingInProcess").GetBoolean());
        }

        [Fact]
        public void PackageEmbedsRemoteViewWithProbeResultBindings()
        {
            using var package = OpenPackage();
            using var assemblyStream = OpenRequiredEntry(package, "TheKameleon.Superpowers.Vsix.dll");
            using var assembly = new MemoryStream();
            assemblyStream.CopyTo(assembly);
            assembly.Position = 0;
            using var peReader = new PEReader(assembly);
            var metadata = peReader.GetMetadataReader();
            var resource = Assert.Single(metadata.ManifestResources.Select(metadata.GetManifestResource), candidate =>
                metadata.GetString(candidate.Name) == "TheKameleon.Superpowers.Vsix.SuperpowersToolWindowControl.xaml");

            Assert.True(resource.Implementation.IsNil, "Remote UI XAML must be embedded, not externally linked.");
            var header = peReader.PEHeaders.CorHeader;
            Assert.NotNull(header);
            var reader = peReader.GetSectionData(header.ResourcesDirectory.RelativeVirtualAddress).GetReader();
            reader.Offset = checked((int)resource.Offset);
            var length = reader.ReadInt32();
            Assert.InRange(length, 1, reader.RemainingBytes);
            using var xamlStream = new MemoryStream(reader.ReadBytes(length));
            var view = XDocument.Load(xamlStream);
            XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

            Assert.Equal(presentation + "DataTemplate", view.Root!.Name);
            var textValues = view.Descendants(presentation + "TextBlock")
                .Select(element => (string?)element.Attribute("Text"))
                .ToArray();
            Assert.Contains("Superpowers capability probes", textValues);
            Assert.Contains("{Binding Status}", textValues);
            Assert.Contains("{Binding Details}", textValues);
            Assert.Contains("{Binding Notes}", textValues);
        }

        [Fact]
        public void ProbeResultModelDeclaresRemoteUiSerializationAttributes()
        {
            using var package = OpenPackage();
            using var assemblyStream = OpenRequiredEntry(package, "TheKameleon.Superpowers.Vsix.dll");
            using var assembly = new MemoryStream();
            assemblyStream.CopyTo(assembly);
            assembly.Position = 0;
            using var peReader = new PEReader(assembly);
            var metadata = peReader.GetMetadataReader();
            var type = Assert.Single(metadata.TypeDefinitions
                .Select(metadata.GetTypeDefinition), candidate =>
                    metadata.GetString(candidate.Name) == "ProbeResultsViewModel");

            Assert.Contains(type.GetCustomAttributes(), attribute =>
                GetAttributeTypeName(metadata, attribute) == "DataContractAttribute");

            foreach (var propertyName in new[] { "ProbeName", "Timestamp", "Status", "Details", "Notes" })
            {
                var property = Assert.Single(type.GetProperties()
                    .Select(metadata.GetPropertyDefinition), candidate =>
                        metadata.GetString(candidate.Name) == propertyName);
                Assert.Contains(property.GetCustomAttributes(), attribute =>
                    GetAttributeTypeName(metadata, attribute) == "DataMemberAttribute");
            }
        }

        private static string? GetAttributeTypeName(MetadataReader metadata, CustomAttributeHandle handle)
        {
            var constructor = metadata.GetCustomAttribute(handle).Constructor;
            EntityHandle declaringType = constructor.Kind switch
            {
                HandleKind.MemberReference => metadata.GetMemberReference((MemberReferenceHandle)constructor).Parent,
                HandleKind.MethodDefinition => metadata.GetMethodDefinition((MethodDefinitionHandle)constructor).GetDeclaringType(),
                _ => default,
            };

            return declaringType.Kind switch
            {
                HandleKind.TypeReference => metadata.GetString(metadata.GetTypeReference((TypeReferenceHandle)declaringType).Name),
                HandleKind.TypeDefinition => metadata.GetString(metadata.GetTypeDefinition((TypeDefinitionHandle)declaringType).Name),
                _ => null,
            };
        }

        private static void AssertLocalizedDisplayName(ZipArchive package, JsonElement contribution, string resourceId, string expectedText)
        {
            Assert.Equal($"%{resourceId}%", contribution.GetProperty("displayName").GetString());
            using var stream = OpenRequiredEntry(package, ".vsextension/string-resources.json");
            using var resources = JsonDocument.Parse(stream);
            Assert.Equal(expectedText, resources.RootElement.GetProperty(resourceId).GetString());
        }

        private static ZipArchive OpenPackage()
        {
            return ZipFile.OpenRead(Path.Combine(AppContext.BaseDirectory, "TheKameleon.Superpowers.Vsix.vsix"));
        }

        private static Stream OpenRequiredEntry(ZipArchive package, string name)
        {
            var entry = package.GetEntry(name);
            Assert.NotNull(entry);
            return entry.Open();
        }
    }
}
