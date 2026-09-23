# Copilot Model List Suggestions Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let a user actually see the models the extension fetched (I4, deferred from the prior "Copilot model preferences" plan), and, when they pick a function for a preference row, pre-fill a sensible suggested model for that function's blank Model field — mirroring the mechanical/integration/architecture-and-review model tiering `subagent-driven-development` already uses for Claude Code's own subagent dispatch, applied here to Copilot's own model categories instead.

**Architecture:** `CopilotModelCatalogFetcher` gains a third, best-effort data-file fetch — `models-and-pricing.yml`, the same github/docs file that backs GitHub's own "category" column (Lightweight/Versatile/Powerful) — added to `CopilotModelCatalog` as a new `Categories` list. A new pure-function lookup, `SuperpowersFunctionModelSuggestion`, maps each `SuperpowersFunction` to a suggested category and picks a concrete model name from the catalog, preferring one available on the user's self-reported plan when that narrows the choice. The ViewModel wires this into `ModelPreferenceRow`: changing a row's function fills a blank Model field with the suggestion (never overwrites a non-blank one). The XAML's Model field becomes an editable `ComboBox` sourced from the fetched model names, so free text still works but the actual downloaded list is now visible and pickable.

**Tech Stack:** C# / .NET 8, existing xUnit v3 test project (`TheKameleon.Superpowers.Tests`), WPF via `Microsoft.VisualStudio.Extensibility.UI`.

**Spec:** No separate spec document — follows directly from the in-chat design agreed on 2026-09-23, itself a deferred finding (I4) from the final whole-branch review of `docs/superpowers/plans/2026-09-23-copilot-model-preferences.md` (already merged to `master`). Read that prior plan's Global Constraints too; they still bind the code this plan extends.

## Global Constraints

- The new `models-and-pricing.yml` fetch is **best-effort, not required**: if it fails or its shape has changed, `CopilotModelCatalog.Categories` is empty and a problem message is appended, but `Models`/`PlanAvailability` (and therefore whether the fetch as a whole "succeeds") are unaffected. This is a deliberate asymmetry from the existing two required files — state it explicitly in code comments where it diverges from their pattern.
- A suggested model **never overwrites a non-empty `Model` field** — it only fills a currently-blank one, and only in direct response to that row's `Function` changing (not on every refresh, not retroactively for existing rows when the catalog updates).
- The function→category mapping is a **hardcoded editorial default in code**, not fetched from anywhere and not user-configurable in this plan. It is Superpowers' own opinion about which functions benefit from more capability, mirroring `subagent-driven-development`'s Model Selection guidance — never claim it as a GitHub or Anthropic recommendation in any UI copy or code comment.
- The full model dropdown list is **never filtered by the self-reported plan** — only the *suggested default* considers plan availability, since the plan is advisory/unverified (per the prior plan's constraint) and hiding real options based on a possibly-wrong self-report would be actively harmful, not just unhelpful.
- Manual free-text entry must keep working in every scenario — satisfied by using an editable `ComboBox` (`IsEditable="True"` bound via `Text`, not `SelectedItem`) rather than a plain non-editable dropdown.

---

### Task 1: Fetch and parse `models-and-pricing.yml` as a best-effort third source

**Files:**
- Modify: `TheKameleon.Superpowers.Skills/Models/CopilotModelCatalog.cs`
- Modify: `TheKameleon.Superpowers.Skills/Models/CopilotModelCatalogFetcher.cs`
- Modify: `TheKameleon.Superpowers.Tests/CopilotModelCatalogFetcherTests.cs` (existing constructor calls to `new CopilotModelCatalog(...)` need a fourth argument)
- Modify: `TheKameleon.Superpowers.Tests/ModelCatalogCacheStoreTests.cs` (same — it constructs a `CopilotModelCatalog` directly for its round-trip test)
- Test: add new cases to `TheKameleon.Superpowers.Tests/CopilotModelCatalogFetcherTests.cs`

**Interfaces:**
- Consumes: `FlatYamlListParser.Parse`, `CopilotModel`, `CopilotModelPlanAvailability` (existing, unchanged).
- Produces: `CopilotModelCategory(string Model, string Category)` and `CopilotModelCatalog.Categories` (`IReadOnlyList<CopilotModelCategory>`) — Task 2 consumes both by these exact names.

First read `TheKameleon.Superpowers.Skills/Models/CopilotModelCatalog.cs` and `TheKameleon.Superpowers.Skills/Models/CopilotModelCatalogFetcher.cs` as they currently exist (both already in the repo, post the prior plan's merge) so the diffs below apply cleanly.

- [ ] **Step 1: Write the failing tests**

Add to `TheKameleon.Superpowers.Tests/CopilotModelCatalogFetcherTests.cs` (alongside its existing tests — do not remove any):

```csharp
    private const string ModelsAndPricingYaml = """
        - model: 'GPT-5.4'
          provider: openai
          release_status: GA
          category: Versatile
          input: $1.00

        - model: 'Claude Opus 5.5'
          provider: anthropic
          release_status: GA
          category: Powerful
          input: $5.00
        """;

    [Fact]
    public async Task IncludesCategoriesWhenAllThreeFilesFetchSuccessfully()
    {
        var handler = new StubHttpMessageHandler(url => url switch
        {
            var u when u.Contains("supported-plans") => Respond(SupportedPlansYaml),
            var u when u.Contains("models-and-pricing") => Respond(ModelsAndPricingYaml),
            _ => Respond(ReleaseStatusYaml),
        });
        var fetcher = new CopilotModelCatalogFetcher(new HttpClient(handler));

        var result = await fetcher.FetchAsync(CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Contains(result.Catalog!.Categories, c => c.Model == "GPT-5.4" && c.Category == "Versatile");
    }

    [Fact]
    public async Task SucceedsWithEmptyCategoriesWhenThePricingFetchFails()
    {
        var handler = new StubHttpMessageHandler(url => url.Contains("models-and-pricing")
            ? new HttpResponseMessage(HttpStatusCode.NotFound)
            : url.Contains("supported-plans") ? Respond(SupportedPlansYaml) : Respond(ReleaseStatusYaml));
        var fetcher = new CopilotModelCatalogFetcher(new HttpClient(handler));

        var result = await fetcher.FetchAsync(CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Empty(result.Catalog!.Categories);
        Assert.Contains(result.Problems, p => p.Contains("category"));
    }

    [Fact]
    public async Task SucceedsWithEmptyCategoriesWhenThePricingShapeIsReshaped()
    {
        var handler = new StubHttpMessageHandler(url => url.Contains("models-and-pricing")
            ? Respond("not:\n  - a: valid shape\nunder: a key")
            : url.Contains("supported-plans") ? Respond(SupportedPlansYaml) : Respond(ReleaseStatusYaml));
        var fetcher = new CopilotModelCatalogFetcher(new HttpClient(handler));

        var result = await fetcher.FetchAsync(CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Empty(result.Catalog!.Categories);
    }

    [Fact]
    public async Task DedupesRepeatedModelRowsInThePricingFile()
    {
        const string duplicated = """
            - model: 'GPT-5.4'
              category: Versatile
              input: $1.00

            - model: 'GPT-5.4'
              category: Versatile
              input: $1.00
            """;
        var handler = new StubHttpMessageHandler(url => url.Contains("models-and-pricing")
            ? Respond(duplicated)
            : url.Contains("supported-plans") ? Respond(SupportedPlansYaml) : Respond(ReleaseStatusYaml));
        var fetcher = new CopilotModelCatalogFetcher(new HttpClient(handler));

        var result = await fetcher.FetchAsync(CancellationToken.None);

        Assert.Single(result.Catalog!.Categories, c => c.Model == "GPT-5.4");
    }
```

Also fix the existing `ReturnsCatalogOnSuccessfulFetch` test's `StubHttpMessageHandler` routing (it currently only branches on `supported-plans` vs. everything-else) so it still passes once a third URL exists — check it routes the new `models-and-pricing` URL somewhere sensible (an empty/valid YAML response is fine there; it doesn't need categories for that test to still verify its own assertions).

And update the two existing direct `new CopilotModelCatalog(...)` call sites (one in this file, one in `ModelCatalogCacheStoreTests.cs`) to pass an empty `Array.Empty<CopilotModelCategory>()` as the new third positional argument (before `FetchedAtUtc`).

- [ ] **Step 2: Run the tests to verify they fail**

Run: `./TheKameleon.Superpowers.Tests/bin/Debug/net8.0/TheKameleon.Superpowers.Tests.exe -class TheKameleon.Superpowers.Tests.CopilotModelCatalogFetcherTests`
Expected: FAIL to compile — `CopilotModelCategory` and `Categories` don't exist yet.

- [ ] **Step 3: Add the contract**

In `TheKameleon.Superpowers.Skills/Models/CopilotModelCatalog.cs`, add the new record and extend `CopilotModelCatalog`:

```csharp
public sealed record CopilotModelCategory(string Model, string Category);
```

Change the `CopilotModelCatalog` record to:

```csharp
public sealed record CopilotModelCatalog(
    IReadOnlyList<CopilotModel> Models,
    IReadOnlyList<CopilotModelPlanAvailability> PlanAvailability,
    IReadOnlyList<CopilotModelCategory> Categories,
    DateTimeOffset FetchedAtUtc);
```

- [ ] **Step 4: Fetch and parse the third file, best-effort**

In `TheKameleon.Superpowers.Skills/Models/CopilotModelCatalogFetcher.cs`, add the URL constant next to the existing two:

```csharp
    private const string ModelsAndPricingUrl = "https://raw.githubusercontent.com/github/docs/main/data/tables/copilot/models-and-pricing.yml";
```

In `FetchAsync`, after the existing required `supportedPlansText` fetch and before constructing the final `CopilotModelCatalog`, add a best-effort third fetch. The method currently ends with:

```csharp
        if (models.Count == 0)
        {
            problems.Add("The model list was empty; using manual entry instead.");
            return new ModelCatalogFetchResult(null, problems);
        }

        return new ModelCatalogFetchResult(new CopilotModelCatalog(models, planAvailability, DateTimeOffset.UtcNow), problems);
    }
```

Replace that tail with:

```csharp
        if (models.Count == 0)
        {
            problems.Add("The model list was empty; using manual entry instead.");
            return new ModelCatalogFetchResult(null, problems);
        }

        // Best-effort only: categories power the suggested-model feature, not the core
        // model/plan-availability lists, so a failure here never fails the whole fetch.
        var categories = new List<CopilotModelCategory>();
        var pricingText = await this.DownloadTextAsync(ModelsAndPricingUrl, problems, cancellationToken).ConfigureAwait(false);
        if (pricingText is not null)
        {
            try
            {
                categories = FlatYamlListParser.Parse(pricingText)
                    .Select(record => new CopilotModelCategory(RequireField(record, "model"), RequireField(record, "category")))
                    .GroupBy(category => category.Model, StringComparer.Ordinal)
                    .Select(group => group.First())
                    .ToList();
            }
            catch (FormatException exception)
            {
                problems.Add($"The model category list format has changed upstream and could not be read: {exception.Message}");
            }
        }

        return new ModelCatalogFetchResult(new CopilotModelCatalog(models, planAvailability, categories, DateTimeOffset.UtcNow), problems);
    }
```

Note `DownloadTextAsync`'s existing problem messages ("Couldn't reach the model list...", "...has moved upstream...") already contain generic wording, not file-specific — that's fine and unchanged; the `SucceedsWithEmptyCategoriesWhenThePricingFetchFails` test only asserts a problem exists containing "category", which the `FormatException`-branch message satisfies for the reshaped case, and for the plain-404 case the existing `DownloadTextAsync` 404 message already contains the URL, not the word "category" — reread that test's assertion once you're implementing and loosen it if needed to match what `DownloadTextAsync` actually produces (assert on `Problems` being non-empty and `Categories` being empty, rather than requiring the literal substring "category", if the 404 case's message doesn't naturally contain it).

- [ ] **Step 5: Run the tests to verify they pass**

Run: `./TheKameleon.Superpowers.Tests/bin/Debug/net8.0/TheKameleon.Superpowers.Tests.exe -class TheKameleon.Superpowers.Tests.CopilotModelCatalogFetcherTests`
Expected: PASS

Run: `./TheKameleon.Superpowers.Tests/bin/Debug/net8.0/TheKameleon.Superpowers.Tests.exe -class TheKameleon.Superpowers.Tests.ModelCatalogCacheStoreTests`
Expected: PASS (after updating its `new CopilotModelCatalog(...)` call site)

- [ ] **Step 6: Commit**

```bash
git add TheKameleon.Superpowers.Skills/Models/CopilotModelCatalog.cs TheKameleon.Superpowers.Skills/Models/CopilotModelCatalogFetcher.cs TheKameleon.Superpowers.Tests/CopilotModelCatalogFetcherTests.cs TheKameleon.Superpowers.Tests/ModelCatalogCacheStoreTests.cs
git commit -m "feat: fetch model weight-class categories as a best-effort third source"
```

---

### Task 2: Suggested-model lookup

**Files:**
- Create: `TheKameleon.Superpowers.Skills/Models/SuperpowersFunctionModelSuggestion.cs`
- Test: `TheKameleon.Superpowers.Tests/SuperpowersFunctionModelSuggestionTests.cs`

**Interfaces:**
- Consumes: `CopilotModelCatalog`, `CopilotModelCategory`, `CopilotModelPlanAvailability`, `CopilotPlan`, `SuperpowersFunction` (all existing).
- Produces: `SuperpowersFunctionModelSuggestion.Suggest(CopilotModelCatalog? catalog, SuperpowersFunction function, CopilotPlan plan) -> string?` — Task 3 calls this exactly.

- [ ] **Step 1: Write the failing tests**

```csharp
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
        var catalog = new CopilotModelCatalog(
            Array.Empty<CopilotModel>(),
            new[] { new CopilotModelPlanAvailability("Claude Opus 5.5", Pro: false, ProPlus: false, Max: false, Business: false, Enterprise: false)
                { }, },
            new[]
            {
                new CopilotModelCategory("Claude Opus 5", "Powerful"),
                new CopilotModelCategory("Claude Opus 5.5", "Powerful"),
            },
            DateTimeOffset.UtcNow) with
        {
            PlanAvailability = new[]
            {
                new CopilotModelPlanAvailability("Claude Opus 5", true, true, true, true, true),
                new CopilotModelPlanAvailability("Claude Opus 5.5", false, false, false, false, false),
            },
        };

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
```

(Fix the slightly malformed `PrefersAPlanAvailableCandidateOverTheFirstMatch` test's construction above while implementing it — it was written with a stray record-init artifact; simplify to a single clean `new CopilotModelCatalog(Array.Empty<CopilotModel>(), planAvailability, categories, DateTimeOffset.UtcNow)` call with `planAvailability` and `categories` built as local variables first. The intent is clear: two Powerful models, only one of which (`Claude Opus 5`) is available on the `Pro` plan; the suggestion must prefer it over the alphabetically/insertion-order-first `Claude Opus 5` — wait, ensure the two model names in the category list are ordered so the NON-preferred one would be picked first if plan preference weren't applied, i.e. put `Claude Opus 5.5` before `Claude Opus 5` in the `Categories` array so the test actually proves the plan-preference logic works rather than passing by coincidence of list order.)

- [ ] **Step 2: Run the tests to verify they fail**

Run: `./TheKameleon.Superpowers.Tests/bin/Debug/net8.0/TheKameleon.Superpowers.Tests.exe -class TheKameleon.Superpowers.Tests.SuperpowersFunctionModelSuggestionTests`
Expected: FAIL — `SuperpowersFunctionModelSuggestion` does not exist yet.

- [ ] **Step 3: Implement the lookup**

```csharp
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
```

The `SuggestedCategory[function]` indexer access is deliberate, not defensive `TryGetValue` — the dictionary is a closed, code-controlled mapping covering every `SuperpowersFunction` value; a missing entry is a maintenance bug (someone added an enum value without updating this table) that should fail loudly in tests (see `EveryEnumFunctionHasAMappedCategory` above), not be silently swallowed at runtime.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `./TheKameleon.Superpowers.Tests/bin/Debug/net8.0/TheKameleon.Superpowers.Tests.exe -class TheKameleon.Superpowers.Tests.SuperpowersFunctionModelSuggestionTests`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add TheKameleon.Superpowers.Skills/Models/SuperpowersFunctionModelSuggestion.cs TheKameleon.Superpowers.Tests/SuperpowersFunctionModelSuggestionTests.cs
git commit -m "feat: suggest a model per Superpowers function from the fetched catalog"
```

---

### Task 3: Wire suggestions and the visible model list into the ViewModel

**Files:**
- Modify: `TheKameleon.Superpowers.Vsix/SuperpowersViewModel.cs`

**Interfaces:**
- Consumes: `SuperpowersFunctionModelSuggestion.Suggest` (Task 2).
- Produces: `ModelNameOptions` (`ObservableList<string>`, bindable) — Task 4's XAML binds to this by name. `ModelPreferenceRow`'s constructor gains an optional suggestion callback; its `Function` setter now fills a blank `Model` via that callback.

No dedicated xUnit test — this is a WPF ViewModel, consistent with the rest of this file's untested command methods (same as Task 7 of the prior plan).

- [ ] **Step 1: Add `ModelNameOptions` and a suggestion helper**

Add a bindable member near the existing `FunctionOptions`:

```csharp
        [DataMember]
        public ObservableList<string> ModelNameOptions { get; } = new();
```

Add a private helper method (near `RefreshModelCatalogAsync`):

```csharp
        private void RefreshModelNameOptions()
        {
            this.ModelNameOptions.Clear();
            if (this.modelCatalog is not null)
            {
                this.ModelNameOptions.AddRange(this.modelCatalog.Models.Select(model => model.Name));
            }
        }

        private string? SuggestModel(SuperpowersFunction function) =>
            SuperpowersFunctionModelSuggestion.Suggest(this.modelCatalog, function, this.SelectedPlan);
```

- [ ] **Step 2: Populate `ModelNameOptions` on construction and on successful refresh**

In the constructor, immediately after the existing line `this.modelCatalog = this.modelCatalogCacheStore.Load();`, add:

```csharp
            this.RefreshModelNameOptions();
```

In `RefreshModelCatalogAsync`, in the success branch (after `this.modelCatalogCacheStore.Save(result.Catalog!);`), add a call to `this.RefreshModelNameOptions();` before setting `this.ModelCatalogStatusText`.

- [ ] **Step 3: Make `ModelPreferenceRow.Function` trigger a suggestion when `Model` is blank**

Change `ModelPreferenceRow`'s constructor and `Function` setter:

```csharp
    [DataContract]
    internal sealed class ModelPreferenceRow : NotifyPropertyChangedObject
    {
        private readonly Func<SuperpowersFunction, string?>? suggestModel;
        private SuperpowersFunction function;
        private string family = string.Empty;
        private string model = string.Empty;

        public ModelPreferenceRow(SuperpowersFunction function, Func<SuperpowersFunction, string?>? suggestModel = null)
        {
            this.suggestModel = suggestModel;
            this.function = function;
            this.model = suggestModel?.Invoke(function) ?? string.Empty;
        }

        [DataMember]
        public SuperpowersFunction Function
        {
            get => this.function;
            set
            {
                if (this.SetProperty(ref this.function, value))
                {
                    this.RaiseNotifyPropertyChangedEvent(nameof(this.FunctionLabel));
                    if (string.IsNullOrWhiteSpace(this.model))
                    {
                        this.Model = this.suggestModel?.Invoke(value) ?? this.model;
                    }
                }
            }
        }

        [DataMember]
        public string FunctionLabel => this.Function.ToString();

        [DataMember]
        public string Family
        {
            get => this.family;
            set => this.SetProperty(ref this.family, value);
        }

        [DataMember]
        public string Model
        {
            get => this.model;
            set => this.SetProperty(ref this.model, value);
        }
    }
```

- [ ] **Step 4: Wire the suggestion callback into row construction**

In the constructor's `AddPreferenceRowCommand` registration, change:

```csharp
            this.AddPreferenceRowCommand = new AsyncCommand((parameter, context, cancellationToken) => { this.FunctionRows.Add(new ModelPreferenceRow(SuperpowersFunction.General)); return Task.CompletedTask; });
```

to:

```csharp
            this.AddPreferenceRowCommand = new AsyncCommand((parameter, context, cancellationToken) => { this.FunctionRows.Add(new ModelPreferenceRow(SuperpowersFunction.General, this.SuggestModel)); return Task.CompletedTask; });
```

In `LoadAsync`, the line that reconstructs rows from saved preferences must NOT pass the suggestion callback (the row's `Model` is already set explicitly from saved data via the object initializer, and passing the callback would just do a pointless, wasted suggestion lookup during construction before the initializer immediately overwrites it). Leave that line exactly as it is today:

```csharp
            this.FunctionRows.AddRange(savedPreferences.Preferences.Select(p => new ModelPreferenceRow(p.Function) { Family = p.Family, Model = p.Model }));
```

(No suggestion callback there — this is intentional, not an oversight; do not "fix" it to also pass `this.SuggestModel`.)

- [ ] **Step 5: Build and run the full unit suite**

Run: `dotnet build TheKameleon.Superpowers.slnx`
Expected: 0 errors.

Run: `./TheKameleon.Superpowers.Tests/bin/Debug/net8.0/TheKameleon.Superpowers.Tests.exe`
Expected: PASS, 0 failures.

- [ ] **Step 6: Commit**

```bash
git add TheKameleon.Superpowers.Vsix/SuperpowersViewModel.cs
git commit -m "feat: suggest a model when a preference row's function changes, expose the fetched model names"
```

---

### Task 4: XAML — editable Model dropdown

**Files:**
- Modify: `TheKameleon.Superpowers.Vsix/SuperpowersToolWindowControl.xaml`

**Interfaces:**
- Consumes: `ModelNameOptions` (Task 3).

- [ ] **Step 1: Replace the Model field's `TextBox` with an editable `ComboBox`**

In the `FunctionRows` `ItemsControl`'s `DataTemplate`, find:

```xml
                                <TextBox Grid.Column="2" Text="{Binding Model, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" Margin="4,0" AutomationProperties.Name="Model name" />
```

Replace it with:

```xml
                                <ComboBox Grid.Column="2" IsEditable="True"
                                          Text="{Binding Model, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"
                                          ItemsSource="{Binding DataContext.ModelNameOptions, RelativeSource={RelativeSource AncestorType=ItemsControl}}"
                                          Margin="4,0" AutomationProperties.Name="Model name" />
```

`IsEditable="True"` with `Text` bound (not `SelectedItem`) is what keeps free-text entry working exactly as before — the `ItemsSource` only supplies the dropdown suggestions; typing something not in the list is unaffected.

- [ ] **Step 2: Build**

Run: `dotnet build TheKameleon.Superpowers.slnx`
Expected: 0 errors.

- [ ] **Step 3: Commit**

```bash
git add TheKameleon.Superpowers.Vsix/SuperpowersToolWindowControl.xaml
git commit -m "feat: make the model preference field an editable dropdown of fetched models"
```

---

### Task 5: Manual verification

**Files:** none (verification only)

- [ ] **Step 1: Build the full solution**

Run: `dotnet build TheKameleon.Superpowers.slnx -c Debug`
Expected: 0 errors.

- [ ] **Step 2: Run both full test suites**

Run: `./TheKameleon.Superpowers.Tests/bin/Debug/net8.0/TheKameleon.Superpowers.Tests.exe`
Expected: PASS, 0 failures.

Run: `./TheKameleon.Superpowers.IntegrationTests/bin/Debug/net8.0-windows8.0/TheKameleon.Superpowers.IntegrationTests.exe`
Expected: PASS, 0 failures. (Check whether `ToolWindowPackageTests.cs`'s XAML-structure assertions reference the row template's `TextBox`/`ComboBox` shape at all — if any assertion counts `TextBox` elements in a way this change affects, update it the same way the prior plan's Task 9 follow-up fix did for the `ComboBox` count.)

- [ ] **Step 3: Manual verification in Visual Studio**

Launch the experimental instance (F5), open the Superpowers tool window, and verify:
1. Select **Refresh model list** — confirm `ModelCatalogStatusText` reports a model count as before.
2. Add a preference row, open the Model field's dropdown — confirm it shows real fetched model names (not empty, not placeholder text).
3. Change the row's Function to **Review** — confirm the Model field auto-fills with a suggested model, and that this suggestion is *not* forced (you can still clear it and type anything, or pick a different dropdown entry).
4. Change the row's Function again to **Tdd** — with the Model field still holding the *previous* suggestion (non-blank), confirm the field does **not** get overwritten — the "only fills a blank field" rule must hold.
5. Add a second, fresh row, set **Your Copilot plan** to something other than Unspecified first, then pick a Function — confirm the suggested model (if the catalog has plan-availability data for that tier) prefers one available on the selected plan over one that isn't, when both exist in the catalog.
6. Save preferences, reopen the tool window — confirm rows still load with their saved (not re-suggested) Family/Model values, unaffected by this change.

- [ ] **Step 4: No commit for this task** — verification only. If Step 2's integration suite requires a fix, that becomes its own small follow-up commit outside this plan's numbered tasks, exactly as the prior plan's Task 9 handled its own integration-test surprise.
