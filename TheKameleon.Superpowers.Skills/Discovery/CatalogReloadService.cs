using TheKameleon.Superpowers.Core.Contracts.Catalog;

namespace TheKameleon.Superpowers.Skills.Discovery;

public static class CatalogReloadService
{
    public static CatalogReloadResult Reload(
        DiscoveryResult discovered,
        CatalogReloadState? previousState)
    {
        ArgumentNullException.ThrowIfNull(discovered);

        var diagnostics = new List<ParseDiagnostic>();
        var previous = previousState ?? new CatalogReloadState(null, Array.Empty<ActiveRunPin>());
        var skillsById = discovered.Skills.ToDictionary(skill => skill.SkillId, StringComparer.OrdinalIgnoreCase);
        var skillsByPath = discovered.Skills.ToDictionary(skill => skill.FullPath, StringComparer.OrdinalIgnoreCase);

        foreach (var pin in previous.ActiveRunPins)
        {
            if (!skillsByPath.ContainsKey(pin.FullPath))
            {
                diagnostics.Add(new ParseDiagnostic(
                    ParseDiagnosticSeverity.Error,
                    "SPCAT301",
                    $"Reload blocked because active run '{pin.RunId}' pins missing content '{pin.FullPath}'."));
            }
        }

        if (diagnostics.Any(diagnostic => diagnostic.Severity == ParseDiagnosticSeverity.Error))
        {
            return new CatalogReloadResult(discovered, previous, diagnostics);
        }

        string? selectedSkillId = previous.SelectedSkillId;
        if (string.IsNullOrWhiteSpace(selectedSkillId) || !skillsById.ContainsKey(selectedSkillId))
        {
            selectedSkillId = discovered.Skills.FirstOrDefault()?.SkillId;
            if (!string.IsNullOrWhiteSpace(previous.SelectedSkillId) && !string.Equals(previous.SelectedSkillId, selectedSkillId, StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add(new ParseDiagnostic(
                    ParseDiagnosticSeverity.Warning,
                    "SPCAT302",
                    $"Previously selected skill '{previous.SelectedSkillId}' is unavailable after reload; selection fell back to '{selectedSkillId ?? string.Empty}'."));
            }
        }

        var nextState = new CatalogReloadState(selectedSkillId, previous.ActiveRunPins);
        return new CatalogReloadResult(discovered, nextState, diagnostics);
    }
}
