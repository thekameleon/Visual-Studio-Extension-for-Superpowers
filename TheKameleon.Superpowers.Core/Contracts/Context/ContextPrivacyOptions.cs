using System.Text.Json.Serialization;

namespace TheKameleon.Superpowers.Core.Contracts.Context;

public sealed record ContextPrivacyOptions
{
    [JsonConstructor]
    public ContextPrivacyOptions(int maxCharacters, IReadOnlyList<string>? exclusionPatterns = null, bool redactSecrets = true)
    {
        if (maxCharacters <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxCharacters), "Max characters must be greater than zero.");
        }

        MaxCharacters = maxCharacters;
        ExclusionPatterns = (exclusionPatterns ?? Array.Empty<string>()).Where(pattern => !string.IsNullOrWhiteSpace(pattern)).ToArray();
        RedactSecrets = redactSecrets;
    }

    public int MaxCharacters { get; }

    public IReadOnlyList<string> ExclusionPatterns { get; }

    public bool RedactSecrets { get; }
}
