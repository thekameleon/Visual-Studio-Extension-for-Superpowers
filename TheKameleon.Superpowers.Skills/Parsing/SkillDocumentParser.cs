using System.Text.RegularExpressions;
using TheKameleon.Superpowers.Core.Contracts.Catalog;

namespace TheKameleon.Superpowers.Skills.Parsing;

public static class SkillDocumentParser
{
    public const int MaxDocumentLength = 256 * 1024;

    private static readonly Regex ReferenceRegex = new(@"\[[^\]]+\]\(([^)]+)\)", RegexOptions.Compiled);

    public static ParsedSkillDocument Parse(string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var diagnostics = new List<ParseDiagnostic>();
        if (content.Length > MaxDocumentLength)
        {
            diagnostics.Add(new ParseDiagnostic(
                ParseDiagnosticSeverity.Error,
                "SPCAT001",
                $"Skill document exceeds the maximum supported length of {MaxDocumentLength} characters."));
            return new ParsedSkillDocument(string.Empty, string.Empty, string.Empty, Array.Empty<SkillReference>(), diagnostics);
        }

        var normalized = content.Replace("\r\n", "\n");
        if (!normalized.StartsWith("---\n", StringComparison.Ordinal))
        {
            diagnostics.Add(new ParseDiagnostic(
                ParseDiagnosticSeverity.Error,
                "SPCAT002",
                "Skill document is missing the required YAML front matter delimiter."));
            return new ParsedSkillDocument(string.Empty, string.Empty, normalized, Array.Empty<SkillReference>(), diagnostics);
        }

        var endIndex = normalized.IndexOf("\n---\n", 4, StringComparison.Ordinal);
        if (endIndex < 0)
        {
            diagnostics.Add(new ParseDiagnostic(
                ParseDiagnosticSeverity.Error,
                "SPCAT003",
                "Skill document front matter is not properly terminated."));
            return new ParsedSkillDocument(string.Empty, string.Empty, string.Empty, Array.Empty<SkillReference>(), diagnostics);
        }

        var frontMatter = normalized.Substring(4, endIndex - 4);
        var body = normalized[(endIndex + 5)..];

        var values = ParseFrontMatter(frontMatter, diagnostics);
        values.TryGetValue("name", out var name);
        values.TryGetValue("description", out var description);

        if (string.IsNullOrWhiteSpace(name))
        {
            diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPCAT004", "Skill front matter must include a non-empty name."));
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPCAT005", "Skill front matter must include a non-empty description."));
        }

        var references = ParseReferences(body, diagnostics);
        return new ParsedSkillDocument(name ?? string.Empty, description ?? string.Empty, body, references, diagnostics);
    }

    private static Dictionary<string, string> ParseFrontMatter(string frontMatter, List<ParseDiagnostic> diagnostics)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var lines = frontMatter.Split('\n');
        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            var separatorIndex = line.IndexOf(':');
            if (separatorIndex <= 0)
            {
                diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPCAT006", $"Unsupported front matter line '{line}'."));
                continue;
            }

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim().Trim('"');
            values[key] = value;
        }

        return values;
    }

    private static IReadOnlyList<SkillReference> ParseReferences(string body, List<ParseDiagnostic> diagnostics)
    {
        var references = new List<SkillReference>();
        foreach (Match match in ReferenceRegex.Matches(body))
        {
            var path = match.Groups[1].Value.Trim();
            if (!IsSupportedRelativeReference(path))
            {
                diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPCAT007", $"Unsupported or unsafe reference path '{path}'."));
                continue;
            }

            references.Add(new SkillReference(path));
        }

        return references;
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

        if (path.Contains('\\', StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }
}
