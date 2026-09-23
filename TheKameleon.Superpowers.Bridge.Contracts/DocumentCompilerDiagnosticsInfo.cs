using System.Collections.Generic;

namespace TheKameleon.Superpowers.Bridge.Contracts;

/// <summary>Compiler diagnostics scoped to a single document, transported across the bridge.</summary>
public sealed class DocumentCompilerDiagnosticsInfo
{
    public int TotalCount { get; set; }

    public IReadOnlyList<CompilerDiagnosticInfo> Diagnostics { get; set; } = System.Array.Empty<CompilerDiagnosticInfo>();
}
