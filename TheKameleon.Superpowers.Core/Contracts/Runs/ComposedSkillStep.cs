using System.Text.Json.Serialization;
using TheKameleon.Superpowers.Core.Contracts.Catalog;

namespace TheKameleon.Superpowers.Core.Contracts.Runs;

public sealed record ComposedSkillStep
{
    [JsonConstructor]
    public ComposedSkillStep(int order, string purpose, DiscoveredSkillEntry skill)
    {
        if (order <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(order), "Composition order must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(purpose))
        {
            throw new ArgumentException("Composition purpose is required.", nameof(purpose));
        }

        ArgumentNullException.ThrowIfNull(skill);

        Order = order;
        Purpose = purpose;
        Skill = skill;
    }

    public int Order { get; }

    public string Purpose { get; }

    public DiscoveredSkillEntry Skill { get; }
}
