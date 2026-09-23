namespace TheKameleon.Superpowers.Skills.Models;

/// <summary>Maps each Superpowers function to a suggested model weight class (Lightweight /
/// Versatile / Powerful, per github/docs' own models-and-pricing.yml categories), mirroring the
/// mechanical/integration/architecture-and-review tiering Superpowers already uses for its own
/// subagent dispatch in Claude Code. This is Superpowers' own editorial default -- not a GitHub-
/// or Anthropic-published recommendation -- and it is always overridable, never enforced.</summary>
public static class SuperpowersFunctionModelSuggestion
{
    private static readonly IReadOnlyDictionary<SuperpowersFunction, string> SuggestedCategory = new Dictionary<SuperpowersFunction, string>
    {
        [SuperpowersFunction.General] = "Versatile",
        [SuperpowersFunction.Plan] = "Powerful",
        [SuperpowersFunction.Execute] = "Versatile",
        [SuperpowersFunction.Debug] = "Versatile",
        [SuperpowersFunction.Tdd] = "Lightweight",
        [SuperpowersFunction.Review] = "Powerful",
        [SuperpowersFunction.Verify] = "Versatile",
        [SuperpowersFunction.Refactor] = "Lightweight",
        [SuperpowersFunction.Finish] = "Lightweight",
    };

    /// <summary>Picks a suggested model name for the given function from the catalog, preferring
    /// one available on the given plan when plan data narrows the choice. Returns null when there
    /// is no catalog, no category data, or no match -- callers treat null as "no suggestion",
    /// never as an error.</summary>
    public static string? Suggest(CopilotModelCatalog? catalog, SuperpowersFunction function, CopilotPlan plan)
    {
        if (catalog is null || catalog.Categories.Count == 0)
        {
            return null;
        }

        var category = SuggestedCategory[function];
        var candidates = catalog.Categories
            .Where(entry => string.Equals(entry.Category, category, StringComparison.OrdinalIgnoreCase))
            .Select(entry => entry.Model)
            .ToArray();
        if (candidates.Length == 0)
        {
            return null;
        }

        var planAvailable = PlanAvailableModels(catalog, plan);
        return candidates.FirstOrDefault(planAvailable.Contains) ?? candidates[0];
    }

    private static IReadOnlySet<string> PlanAvailableModels(CopilotModelCatalog catalog, CopilotPlan plan)
    {
        if (plan == CopilotPlan.Unspecified)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        return catalog.PlanAvailability
            .Where(entry => IsAvailable(entry, plan))
            .Select(entry => entry.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsAvailable(CopilotModelPlanAvailability entry, CopilotPlan plan) => plan switch
    {
        CopilotPlan.Free => false,
        CopilotPlan.Pro => entry.Pro,
        CopilotPlan.ProPlus => entry.ProPlus,
        CopilotPlan.Max => entry.Max,
        CopilotPlan.Business => entry.Business,
        CopilotPlan.Enterprise => entry.Enterprise,
        _ => false,
    };
}
