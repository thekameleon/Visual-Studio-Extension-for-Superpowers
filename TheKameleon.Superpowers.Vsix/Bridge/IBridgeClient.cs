using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TheKameleon.Superpowers.Bridge.Contracts;

namespace TheKameleon.Superpowers.Vsix.Bridge;

public interface IBridgeClient
{
    Task<IReadOnlyList<BridgeCapabilityResult>> GetCapabilitiesAsync(CancellationToken cancellationToken);

    Task<DocumentTextInfo?> GetActiveDocumentTextAsync(CancellationToken cancellationToken);
}