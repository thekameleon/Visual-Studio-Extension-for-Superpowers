using System.Text.Json.Serialization;

namespace TheKameleon.Superpowers.Core.Contracts.Settings;

public sealed record CriticalWarningPolicy
{
    [JsonConstructor]
    public CriticalWarningPolicy(IReadOnlyList<string>? warningCodes = null)
    {
        WarningCodes = (warningCodes ?? Array.Empty<string>())
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public IReadOnlyList<string> WarningCodes { get; }

    public bool TreatWarningsAsCritical => WarningCodes.Count > 0;
}
