using System;
using System.Collections.Generic;
using System.Linq;
using TheKameleon.Superpowers.Bridge.Contracts;
using TheKameleon.Superpowers.Core.Contracts.Context;

namespace TheKameleon.Superpowers.Skills.Context;

/// <summary>
/// Pure decision logic for whether active-document context capture should trust the in-process
/// bridge's reported capability/document state, or fall back to metadata-only capture. Extracted
/// from <c>VisualStudioContextCollector</c> so this branching can be unit tested without a running
/// Visual Studio host.
/// </summary>
public static class ActiveDocumentBridgeResolver
{
    private const string CapabilityUnavailableDetail =
        "Active document text bridge is not available; metadata-only capture was used.";

    private const string DocumentUnavailableDetail =
        "Active document text was unavailable from the in-process bridge; metadata-only capture was used.";

    private const string SemanticTargetCapabilityUnavailableDetail =
        "Semantic target bridge capability is not available; semantic target capture was skipped.";

    private const string CompilerDiagnosticsCapabilityUnavailableDetail =
        "Compiler diagnostics bridge capability is not available; compiler diagnostics capture was skipped.";

    /// <summary>
    /// Determines whether the <see cref="BridgeCapability.DocumentText"/> capability reported by the
    /// bridge is usable. Returns <see langword="null"/> when the bridge document text should be
    /// fetched; otherwise returns the diagnostic explaining why metadata-only fallback must be used.
    /// </summary>
    public static ContextCaptureDiagnostic? EvaluateCapability(IReadOnlyList<BridgeCapabilityResult> bridgeCapabilities)
    {
        ArgumentNullException.ThrowIfNull(bridgeCapabilities);

        var documentTextCapability = bridgeCapabilities.FirstOrDefault(capability => capability.Capability == BridgeCapability.DocumentText);
        if (documentTextCapability is null || !documentTextCapability.IsAvailable)
        {
            return new ContextCaptureDiagnostic(
                "SPCTX707",
                documentTextCapability?.Detail ?? CapabilityUnavailableDetail,
                "Info",
                "active-document");
        }

        return null;
    }

    /// <summary>
    /// Determines whether a bridge-mapped document snapshot should be trusted. Returns
    /// <see langword="null"/> when the bridge snapshot should be used; otherwise returns the
    /// diagnostic explaining why metadata-only fallback must be used.
    /// </summary>
    public static ContextCaptureDiagnostic? EvaluateDocument(DocumentContextSnapshot bridgeDocument)
    {
        ArgumentNullException.ThrowIfNull(bridgeDocument);

        if (bridgeDocument.State == ContextValueState.Unavailable)
        {
            return new ContextCaptureDiagnostic("SPCTX708", DocumentUnavailableDetail, "Info", "active-document");
        }

        return null;
    }

    /// <summary>
    /// Determines whether the <see cref="BridgeCapability.SemanticTarget"/> capability reported by the
    /// bridge is usable. Returns <see langword="null"/> when semantic-target resolution should be
    /// attempted; otherwise returns the diagnostic explaining why capture must be skipped.
    /// </summary>
    public static ContextCaptureDiagnostic? EvaluateSemanticTargetCapability(IReadOnlyList<BridgeCapabilityResult> bridgeCapabilities)
    {
        ArgumentNullException.ThrowIfNull(bridgeCapabilities);

        var semanticTargetCapability = bridgeCapabilities.FirstOrDefault(capability => capability.Capability == BridgeCapability.SemanticTarget);
        if (semanticTargetCapability is null || !semanticTargetCapability.IsAvailable)
        {
            return new ContextCaptureDiagnostic(
                "SPCTX709",
                semanticTargetCapability?.Detail ?? SemanticTargetCapabilityUnavailableDetail,
                "Info",
                "semantic-target");
        }

        return null;
    }

    /// <summary>
    /// Determines whether the <see cref="BridgeCapability.CompilerDiagnostics"/> capability reported by the
    /// bridge is usable. Returns <see langword="null"/> when compiler diagnostics capture should be
    /// attempted; otherwise returns the diagnostic explaining why capture must be skipped.
    /// </summary>
    public static ContextCaptureDiagnostic? EvaluateCompilerDiagnosticsCapability(IReadOnlyList<BridgeCapabilityResult> bridgeCapabilities)
    {
        ArgumentNullException.ThrowIfNull(bridgeCapabilities);

        var compilerDiagnosticsCapability = bridgeCapabilities.FirstOrDefault(capability => capability.Capability == BridgeCapability.CompilerDiagnostics);
        if (compilerDiagnosticsCapability is null || !compilerDiagnosticsCapability.IsAvailable)
        {
            return new ContextCaptureDiagnostic(
                "SPCTX714",
                compilerDiagnosticsCapability?.Detail ?? CompilerDiagnosticsCapabilityUnavailableDetail,
                "Info",
                "compiler-diagnostics");
        }

        return null;
    }
}
