using TheKameleon.Superpowers.Skills.Models;

namespace TheKameleon.Superpowers.Tests;

public sealed class SuperpowersFunctionModelSuggestionTests
{
    private static CopilotModelCatalog Catalog(params CopilotModelCategory[] categories) =>
        new(Array.Empty<CopilotModel>(), Array.Empty<CopilotModelPlanAvailability>(), categories, DateTimeOffset.UtcNow);

    [Fact]
    public void ReturnsNullWhenCatalogIsNull()
    {
        Assert.Null(SuperpowersFunctionModelSuggestion.Suggest(null, SuperpowersFunction.Review, CopilotPlan.Unspecified));
    }

    [Fact]
    public void ReturnsNullWhenCatalogHasNoCategories()
    {
        var catalog = Catalog();

        Assert.Null(SuperpowersFunctionModelSuggestion.Suggest(catalog, SuperpowersFunction.Review, CopilotPlan.Unspecified));
    }

    [Fact]
    public void SuggestsAPowerfulModelForReview()
    {
        var catalog = Catalog(
            new CopilotModelCategory("GPT-5.4 mini", "Lightweight"),
            new CopilotModelCategory("Claude Opus 5.5", "Powerful"));

        var suggestion = SuperpowersFunctionModelSuggestion.Suggest(catalog, SuperpowersFunction.Review, CopilotPlan.Unspecified);

        Assert.Equal("Claude Opus 5.5", suggestion);
    }

    [Fact]
    public void SuggestsALightweightModelForTdd()
    {
        var catalog = Catalog(
            new CopilotModelCategory("GPT-5.4 mini", "Lightweight"),
            new CopilotModelCategory("Claude Opus 5.5", "Powerful"));

        var suggestion = SuperpowersFunctionModelSuggestion.Suggest(catalog, SuperpowersFunction.Tdd, CopilotPlan.Unspecified);

        Assert.Equal("GPT-5.4 mini", suggestion);
    }

    [Fact]
    public void ReturnsNullWhenNoCandidateMatchesTheSuggestedCategory()
    {
        var catalog = Catalog(new CopilotModelCategory("GPT-5.4 mini", "Lightweight"));

        Assert.Null(SuperpowersFunctionModelSuggestion.Suggest(catalog, SuperpowersFunction.Review, CopilotPlan.Unspecified));
    }

    [Fact]
    public void PrefersAPlanAvailableCandidateOverTheFirstMatch()
    {
        // Two "Powerful" candidates, with the non-plan-preferred one ("Claude Opus 5.5") listed
        // FIRST in Categories. Only "Claude Opus 5" is available on CopilotPlan.Pro. If plan
        // preference weren't actually applied (e.g. Suggest just returned candidates[0]), this
        // test would fail, because the first-listed candidate is the wrong answer.
        var planAvailability = new[]
        {
            new CopilotModelPlanAvailability("Claude Opus 5", Pro: true, ProPlus: true, Max: true, Business: true, Enterprise: true),
            new CopilotModelPlanAvailability("Claude Opus 5.5", Pro: false, ProPlus: false, Max: false, Business: false, Enterprise: false),
        };
        var categories = new[]
        {
            new CopilotModelCategory("Claude Opus 5.5", "Powerful"),
            new CopilotModelCategory("Claude Opus 5", "Powerful"),
        };
        var catalog = new CopilotModelCatalog(Array.Empty<CopilotModel>(), planAvailability, categories, DateTimeOffset.UtcNow);

        var suggestion = SuperpowersFunctionModelSuggestion.Suggest(catalog, SuperpowersFunction.Review, CopilotPlan.Pro);

        Assert.Equal("Claude Opus 5", suggestion);
    }

    [Fact]
    public void EveryEnumFunctionHasAMappedCategory()
    {
        var catalog = Catalog(
            new CopilotModelCategory("A", "Lightweight"),
            new CopilotModelCategory("B", "Versatile"),
            new CopilotModelCategory("C", "Powerful"));

        foreach (var function in Enum.GetValues<SuperpowersFunction>())
        {
            // Must not throw -- proves every SuperpowersFunction value has a category mapping.
            SuperpowersFunctionModelSuggestion.Suggest(catalog, function, CopilotPlan.Unspecified);
        }
    }
}
