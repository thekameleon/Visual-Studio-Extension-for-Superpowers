using TheKameleon.Superpowers.Bridge.Contracts;
using TheKameleon.Superpowers.Core.Contracts.Context;
using TheKameleon.Superpowers.Skills.Context;

namespace TheKameleon.Superpowers.Tests;

public sealed class ActiveDocumentBridgeResolverTests
{
    private static readonly ContextProvenance Provenance = new("test", DateTimeOffset.UtcNow);

    [Fact]
    public void EvaluateCapabilityReturnsNullWhenDocumentTextCapabilityIsAvailable()
    {
        var capabilities = new[]
        {
            new BridgeCapabilityResult
            {
                ProtocolVersion = 1,
                Capability = BridgeCapability.DocumentText,
                IsAvailable = true,
                Detail = string.Empty,
            },
        };

        var diagnostic = ActiveDocumentBridgeResolver.EvaluateCapability(capabilities);

        Assert.Null(diagnostic);
    }

    [Fact]
    public void EvaluateCapabilityReturnsDiagnosticWhenDocumentTextCapabilityIsMissing()
    {
        var diagnostic = ActiveDocumentBridgeResolver.EvaluateCapability(Array.Empty<BridgeCapabilityResult>());

        Assert.NotNull(diagnostic);
        Assert.Equal("SPCTX707", diagnostic!.Code);
        Assert.Equal("active-document", diagnostic.Scope);
    }

    [Fact]
    public void EvaluateCapabilityReturnsDiagnosticWithBridgeDetailWhenCapabilityIsUnavailable()
    {
        var capabilities = new[]
        {
            new BridgeCapabilityResult
            {
                ProtocolVersion = 1,
                Capability = BridgeCapability.DocumentText,
                IsAvailable = false,
                Detail = "Pipe transport unavailable.",
            },
        };

        var diagnostic = ActiveDocumentBridgeResolver.EvaluateCapability(capabilities);

        Assert.NotNull(diagnostic);
        Assert.Equal("SPCTX707", diagnostic!.Code);
        Assert.Equal("Pipe transport unavailable.", diagnostic.Message);
    }

    [Fact]
    public void EvaluateDocumentReturnsNullWhenBridgeDocumentIsAvailable()
    {
        var document = new DocumentContextSnapshot(
            ContextValueState.Available,
            Provenance,
            "C:/repo/File.cs",
            "File.cs",
            isOpen: true,
            isDirty: false,
            new CapturedTextValue(ContextValueState.Available, "content", 7, 7));

        var diagnostic = ActiveDocumentBridgeResolver.EvaluateDocument(document);

        Assert.Null(diagnostic);
    }

    [Fact]
    public void EvaluateDocumentReturnsDiagnosticWhenBridgeDocumentIsUnavailable()
    {
        var document = new DocumentContextSnapshot(ContextValueState.Unavailable, Provenance);

        var diagnostic = ActiveDocumentBridgeResolver.EvaluateDocument(document);

        Assert.NotNull(diagnostic);
        Assert.Equal("SPCTX708", diagnostic!.Code);
        Assert.Equal("active-document", diagnostic.Scope);
    }

    [Fact]
    public void EvaluateDocumentReturnsNullWhenBridgeDocumentIsPartial()
    {
        var document = new DocumentContextSnapshot(
            ContextValueState.Partial,
            Provenance,
            "C:/repo/Big.cs",
            "Big.cs",
            isOpen: true,
            isDirty: false,
            new CapturedTextValue(ContextValueState.Partial, "partial", 1000, 7));

        var diagnostic = ActiveDocumentBridgeResolver.EvaluateDocument(document);

        Assert.Null(diagnostic);
    }

    [Fact]
    public void EvaluateSemanticTargetCapabilityReturnsNullWhenCapabilityIsAvailable()
    {
        var capabilities = new[]
        {
            new BridgeCapabilityResult
            {
                ProtocolVersion = 1,
                Capability = BridgeCapability.SemanticTarget,
                IsAvailable = true,
                Detail = string.Empty,
            },
        };

        var diagnostic = ActiveDocumentBridgeResolver.EvaluateSemanticTargetCapability(capabilities);

        Assert.Null(diagnostic);
    }

    [Fact]
    public void EvaluateSemanticTargetCapabilityReturnsDiagnosticWhenCapabilityIsMissing()
    {
        var diagnostic = ActiveDocumentBridgeResolver.EvaluateSemanticTargetCapability(Array.Empty<BridgeCapabilityResult>());

        Assert.NotNull(diagnostic);
        Assert.Equal("SPCTX709", diagnostic!.Code);
        Assert.Equal("semantic-target", diagnostic.Scope);
    }

    [Fact]
    public void EvaluateSemanticTargetCapabilityReturnsDiagnosticWithBridgeDetailWhenCapabilityIsUnavailable()
    {
        var capabilities = new[]
        {
            new BridgeCapabilityResult
            {
                ProtocolVersion = 1,
                Capability = BridgeCapability.SemanticTarget,
                IsAvailable = false,
                Detail = "Roslyn workspace unavailable.",
            },
        };

        var diagnostic = ActiveDocumentBridgeResolver.EvaluateSemanticTargetCapability(capabilities);

        Assert.NotNull(diagnostic);
        Assert.Equal("SPCTX709", diagnostic!.Code);
        Assert.Equal("Roslyn workspace unavailable.", diagnostic.Message);
    }

    [Fact]
    public void EvaluateCompilerDiagnosticsCapabilityReturnsNullWhenCapabilityIsAvailable()
    {
        var capabilities = new[]
        {
            new BridgeCapabilityResult
            {
                ProtocolVersion = 1,
                Capability = BridgeCapability.CompilerDiagnostics,
                IsAvailable = true,
                Detail = string.Empty,
            },
        };

        var diagnostic = ActiveDocumentBridgeResolver.EvaluateCompilerDiagnosticsCapability(capabilities);

        Assert.Null(diagnostic);
    }

    [Fact]
    public void EvaluateCompilerDiagnosticsCapabilityReturnsDiagnosticWhenCapabilityIsMissing()
    {
        var diagnostic = ActiveDocumentBridgeResolver.EvaluateCompilerDiagnosticsCapability(Array.Empty<BridgeCapabilityResult>());

        Assert.NotNull(diagnostic);
        Assert.Equal("SPCTX714", diagnostic!.Code);
        Assert.Equal("compiler-diagnostics", diagnostic.Scope);
    }

    [Fact]
    public void EvaluateCompilerDiagnosticsCapabilityReturnsDiagnosticWithBridgeDetailWhenCapabilityIsUnavailable()
    {
        var capabilities = new[]
        {
            new BridgeCapabilityResult
            {
                ProtocolVersion = 1,
                Capability = BridgeCapability.CompilerDiagnostics,
                IsAvailable = false,
                Detail = "Roslyn workspace unavailable.",
            },
        };

        var diagnostic = ActiveDocumentBridgeResolver.EvaluateCompilerDiagnosticsCapability(capabilities);

        Assert.NotNull(diagnostic);
        Assert.Equal("SPCTX714", diagnostic!.Code);
        Assert.Equal("Roslyn workspace unavailable.", diagnostic.Message);
    }
}
