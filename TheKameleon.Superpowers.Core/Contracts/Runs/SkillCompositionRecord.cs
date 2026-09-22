using System.Text.Json.Serialization;

namespace TheKameleon.Superpowers.Core.Contracts.Runs;

public sealed record SkillCompositionRecord
{
    [JsonConstructor]
    public SkillCompositionRecord(
        string entryPoint,
        IReadOnlyList<ComposedSkillStep>? steps = null,
        IReadOnlyList<AcceptedPlanTask>? acceptedTasks = null,
        HandoffDecision? handoff = null)
    {
        if (string.IsNullOrWhiteSpace(entryPoint))
        {
            throw new ArgumentException("Entry point is required.", nameof(entryPoint));
        }

        EntryPoint = entryPoint;
        Steps = (steps ?? Array.Empty<ComposedSkillStep>()).OrderBy(step => step.Order).ToArray();
        AcceptedTasks = (acceptedTasks ?? Array.Empty<AcceptedPlanTask>()).OrderBy(task => task.Order).ToArray();
        Handoff = handoff;
    }

    public string EntryPoint { get; }

    public IReadOnlyList<ComposedSkillStep> Steps { get; }

    public IReadOnlyList<AcceptedPlanTask> AcceptedTasks { get; }

    public HandoffDecision? Handoff { get; }
}
