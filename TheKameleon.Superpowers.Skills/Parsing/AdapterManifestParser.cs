using System.Text.Json;
using TheKameleon.Superpowers.Core.Contracts.Catalog;

namespace TheKameleon.Superpowers.Skills.Parsing;

public static class AdapterManifestParser
{
    public const int CurrentSchemaVersion = 1;
    public const int MaxManifestLength = 128 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static AdapterManifest Parse(string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var diagnostics = new List<ParseDiagnostic>();
        if (content.Length > MaxManifestLength)
        {
            diagnostics.Add(new ParseDiagnostic(
                ParseDiagnosticSeverity.Error,
                "SPCAT101",
                $"Adapter manifest exceeds the maximum supported length of {MaxManifestLength} characters."));
            return new AdapterManifest(0, Array.Empty<AdapterManifestAction>(), diagnostics);
        }

        ManifestModel? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<ManifestModel>(content, JsonOptions);
        }
        catch (JsonException exception)
        {
            diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPCAT102", $"Adapter manifest is not valid JSON: {exception.Message}"));
            return new AdapterManifest(0, Array.Empty<AdapterManifestAction>(), diagnostics);
        }

        if (manifest is null)
        {
            diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPCAT103", "Adapter manifest content is empty."));
            return new AdapterManifest(0, Array.Empty<AdapterManifestAction>(), diagnostics);
        }

        if (manifest.SchemaVersion != CurrentSchemaVersion)
        {
            diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPCAT104", $"Unsupported adapter manifest schema version '{manifest.SchemaVersion}'."));
        }

        var actions = new List<AdapterManifestAction>();
        foreach (var action in manifest.Actions ?? Array.Empty<ManifestActionModel>())
        {
            if (string.IsNullOrWhiteSpace(action.ActionId) || string.IsNullOrWhiteSpace(action.SkillPath))
            {
                diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPCAT105", "Adapter manifest actions must include actionId and skillPath."));
                continue;
            }

            if (!IsSupportedRelativeReference(action.SkillPath))
            {
                diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPCAT106", $"Adapter manifest skillPath '{action.SkillPath}' is unsupported or unsafe."));
                continue;
            }

            actions.Add(new AdapterManifestAction(action.ActionId, action.SkillPath, action.RequiresApproval));
        }

        return new AdapterManifest(manifest.SchemaVersion, actions, diagnostics);
    }

    private static bool IsSupportedRelativeReference(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        if (Uri.TryCreate(path, UriKind.Absolute, out _))
        {
            return false;
        }

        if (Path.IsPathRooted(path))
        {
            return false;
        }

        if (path.Contains("..", StringComparison.Ordinal) || path.Contains('\\'))
        {
            return false;
        }

        return true;
    }

    private sealed record ManifestModel(int SchemaVersion, ManifestActionModel[]? Actions);

    private sealed record ManifestActionModel(string ActionId, string SkillPath, bool RequiresApproval);
}
