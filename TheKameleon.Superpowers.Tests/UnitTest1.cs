using TheKameleon.Superpowers.Bridge.Contracts;

namespace TheKameleon.Superpowers.Tests
{
    public class UnitTest1
    {
        [Fact]
        public void BridgeProtocolStartsAtVersionOne()
        {
            Assert.Equal(1, BridgeProtocol.CurrentVersion);
        }

        [Fact]
        public void BridgeCapabilityEnumIncludesExplicitTestExplorerGap()
        {
            Assert.Equal(4, (int)BridgeCapability.TestExplorer);
        }

        [Fact]
        public void BridgeContractsContainNoVisualStudioTypes()
        {
            var contractAssembly = typeof(BridgeProtocol).Assembly;

            Assert.DoesNotContain(contractAssembly.GetReferencedAssemblies(), reference =>
                reference.Name?.StartsWith("Microsoft.VisualStudio", StringComparison.Ordinal) == true);
        }
    }
}
