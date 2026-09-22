using TheKameleon.Superpowers.Core.Contracts.Catalog;
using TheKameleon.Superpowers.Skills.Parsing;

namespace TheKameleon.Superpowers.Skills.Discovery;

public static class SkillDiscoveryService
{
    public static DiscoveryResult Discover(
        string? packagedRoot,
        string? userRoot,
        string? solutionRoot)
    {
        var diagnostics = new List<ParseDiagnostic>();
        var discovered = new Dictionary<string, DiscoveredSkillEntry>(StringComparer.OrdinalIgnoreCase);

        DiscoverRoot(packagedRoot, DiscoverySourceKind.Packaged, DiscoveryTrustState.Implicit, discovered, diagnostics);
        DiscoverRoot(userRoot, DiscoverySourceKind.User, DiscoveryTrustState.RequiresApproval, discovered, diagnostics);
        DiscoverRoot(solutionRoot, DiscoverySourceKind.Solution, DiscoveryTrustState.RequiresApproval, discovered, diagnostics);

        return new DiscoveryResult(discovered.Values.ToArray(), diagnostics);
    }

    private static void DiscoverRoot(
        string? root,
        DiscoverySourceKind sourceKind,
        DiscoveryTrustState trustState,
        Dictionary<string, DiscoveredSkillEntry> discovered,
        List<ParseDiagnostic> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(root))
        {
            return;
        }

        string fullRoot;
        try
        {
            fullRoot = Path.GetFullPath(root);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPCAT201", $"Discovery root '{root}' is invalid."));
            return;
        }

        if (!Directory.Exists(fullRoot))
        {
            diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Warning, "SPCAT202", $"Discovery root '{fullRoot}' does not exist."));
            return;
        }

        foreach (var skillFile in Directory.EnumerateFiles(fullRoot, "SKILL.md", SearchOption.AllDirectories))
        {
            var fullPath = Path.GetFullPath(skillFile);
            if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPCAT203", $"Discovered skill '{fullPath}' is outside the approved root '{fullRoot}'."));
                continue;
            }

            var relativePath = Path.GetRelativePath(fullRoot, fullPath).Replace('\\', '/');
            if (relativePath.Contains("..", StringComparison.Ordinal))
            {
                diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPCAT204", $"Discovered skill '{fullPath}' has an unsafe relative path."));
                continue;
            }

            var content = File.ReadAllText(fullPath);
            var parsed = SkillDocumentParser.Parse(content);
            var entryDiagnostics = parsed.Diagnostics.ToList();
            var skillId = string.IsNullOrWhiteSpace(parsed.Name) ? relativePath : parsed.Name;
            var entry = new DiscoveredSkillEntry(skillId, relativePath, fullPath, sourceKind, trustState, parsed, entryDiagnostics);

            if (discovered.TryGetValue(skillId, out var existing))
            {
                if (sourceKind > existing.SourceKind)
                {
                    diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Info, "SPCAT205", $"Skill '{skillId}' from {sourceKind} overrides {existing.SourceKind}."));
                    discovered[skillId] = entry;
                }
                else
                {
                    diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Info, "SPCAT206", $"Skill '{skillId}' from {sourceKind} was ignored because a higher-precedence source already exists."));
                }

                continue;
            }

            discovered.Add(skillId, entry);
        }
    }
}
