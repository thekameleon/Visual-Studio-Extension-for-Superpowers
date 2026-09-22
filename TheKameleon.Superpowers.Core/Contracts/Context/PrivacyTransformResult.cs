namespace TheKameleon.Superpowers.Core.Contracts.Context;

public sealed record PrivacyTransformResult(
    ContextCaptureSnapshot Snapshot,
    IReadOnlyList<ContextCaptureDiagnostic> Diagnostics);
