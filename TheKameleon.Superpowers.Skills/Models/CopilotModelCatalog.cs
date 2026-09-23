namespace TheKameleon.Superpowers.Skills.Models;

public sealed record CopilotModel(string Name, string Provider, string ReleaseStatus);

public sealed record CopilotModelPlanAvailability(
    string Name,
    bool Pro,
    bool ProPlus,
    bool Max,
    bool Business,
    bool Enterprise);

public enum CopilotPlan
{
    Unspecified,
    Free,
    Pro,
    ProPlus,
    Max,
    Business,
    Enterprise,
}

public sealed record CopilotModelCategory(string Model, string Category);

public sealed record CopilotModelCatalog(
    IReadOnlyList<CopilotModel> Models,
    IReadOnlyList<CopilotModelPlanAvailability> PlanAvailability,
    IReadOnlyList<CopilotModelCategory> Categories,
    DateTimeOffset FetchedAtUtc);
