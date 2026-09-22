using System.Text.Json.Serialization;

namespace TheKameleon.Superpowers.Core.Contracts.Settings;

public sealed record ExclusionRule
{
    [JsonConstructor]
    public ExclusionRule(string pattern, bool isBuiltIn = false)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            throw new ArgumentException("Exclusion pattern is required.", nameof(pattern));
        }

        Pattern = pattern;
        IsBuiltIn = isBuiltIn;
    }

    public string Pattern { get; }

    public bool IsBuiltIn { get; }
}
