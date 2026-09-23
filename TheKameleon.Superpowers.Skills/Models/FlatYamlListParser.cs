namespace TheKameleon.Superpowers.Skills.Models;

/// <summary>Parses the narrow YAML shape github/docs uses for its data/tables files: a flat list of
/// records, each starting with "- key: value" and continuing with "  key: value" lines. Anything
/// outside this shape (nesting, anchors, multiline scalars, flow style) throws FormatException
/// rather than guessing — callers treat that as "the upstream format changed" and fall back.</summary>
public static class FlatYamlListParser
{
    public static IReadOnlyList<IReadOnlyDictionary<string, string>> Parse(string yaml)
    {
        ArgumentNullException.ThrowIfNull(yaml);
        var records = new List<Dictionary<string, string>>();
        Dictionary<string, string>? current = null;

        foreach (var rawLine in yaml.ReplaceLineEndings("\n").Split('\n'))
        {
            var line = rawLine.TrimEnd();
            var trimmed = line.TrimStart();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
            {
                continue;
            }

            if (trimmed.StartsWith("- ", StringComparison.Ordinal))
            {
                current = new Dictionary<string, string>(StringComparer.Ordinal);
                records.Add(current);
                trimmed = trimmed[2..];
            }
            else if (current is null || !line.StartsWith("  ", StringComparison.Ordinal))
            {
                throw new FormatException($"Unsupported YAML line outside a list item: '{rawLine}'.");
            }

            var colon = trimmed.IndexOf(':');
            if (colon < 0)
            {
                throw new FormatException($"Expected 'key: value' but found '{rawLine}'.");
            }

            var key = trimmed[..colon].Trim();
            var value = trimmed[(colon + 1)..].Trim();
            if (value.Length >= 2 && ((value[0] == '\'' && value[^1] == '\'') || (value[0] == '"' && value[^1] == '"')))
            {
                value = value[1..^1];
            }

            current![key] = value;
        }

        return records;
    }
}
