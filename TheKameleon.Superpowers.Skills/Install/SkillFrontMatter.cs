namespace TheKameleon.Superpowers.Skills.Install;

/// <summary>Reads the top-level scalar keys of a SKILL.md YAML front-matter block.</summary>
public sealed record SkillFrontMatter(string? Name, string? Description)
{
    public static SkillFrontMatter? Read(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var lines = text.TrimStart('﻿').Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        if (lines.Length == 0 || lines[0].TrimEnd() != "---")
        {
            return null;
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        string? blockKey = null;
        var folded = false;
        var blockLines = new List<string>();

        for (var index = 1; index < lines.Length; index++)
        {
            var line = lines[index];
            if (line.TrimEnd() == "---")
            {
                FlushBlock();
                return new SkillFrontMatter(Get("name"), Get("description"));
            }

            if (blockKey is not null)
            {
                if (line.Length == 0 || char.IsWhiteSpace(line[0]))
                {
                    blockLines.Add(line.Trim());
                    continue;
                }

                FlushBlock();
            }

            if (line.Length == 0 || char.IsWhiteSpace(line[0]) || line.StartsWith('#'))
            {
                continue;
            }

            var colon = line.IndexOf(':');
            if (colon <= 0)
            {
                continue;
            }

            var key = line[..colon].Trim();
            var value = line[(colon + 1)..].Trim();
            if (value is ">" or ">-" or ">+" or "|" or "|-" or "|+")
            {
                blockKey = key;
                folded = value[0] == '>';
                blockLines.Clear();
                continue;
            }

            values[key] = Unquote(value);
        }

        return null;

        void FlushBlock()
        {
            if (blockKey is null)
            {
                return;
            }

            values[blockKey] = folded
                ? string.Join(" ", blockLines.Where(part => part.Length > 0))
                : string.Join("\n", blockLines).Trim();
            blockKey = null;
        }

        string? Get(string key) => values.TryGetValue(key, out var found) ? found : null;
    }

    private static string Unquote(string value)
    {
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
        {
            return value[1..^1].Replace("\\\"", "\"", StringComparison.Ordinal).Replace("\\\\", "\\", StringComparison.Ordinal);
        }

        if (value.Length >= 2 && value[0] == '\'' && value[^1] == '\'')
        {
            return value[1..^1].Replace("''", "'", StringComparison.Ordinal);
        }

        return value;
    }
}
