using System.Text.Json.Serialization;

namespace TheKameleon.Superpowers.Core.Contracts.Catalog;

public sealed record PlanCompositionStep
{
    [JsonConstructor]
    public PlanCompositionStep(int order, string skillPath, string purpose)
    {
        if (order <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(order), "Composition order must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(skillPath))
        {
            throw new ArgumentException("Skill path is required.", nameof(skillPath));
        }

        if (string.IsNullOrWhiteSpace(purpose))
        {
            throw new ArgumentException("Composition purpose is required.", nameof(purpose));
        }

        Order = order;
        SkillPath = skillPath;
        Purpose = purpose;
    }

    public int Order { get; }

    public string SkillPath { get; }

    public string Purpose { get; }
}
