# Copilot Model Preferences Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let a user record, per Superpowers function (Plan/Execute/Debug/TDD/Review/Verify/Refactor/Finish/General), which Copilot model they want that function's custom agent to use, and have the extension write that preference into the generated `.agent.md` file(s) so Copilot Chat honors it automatically when that agent is selected.

**Architecture:** A new `TheKameleon.Superpowers.Skills.Models` area adds: (1) a minimal parser for the flat YAML list format GitHub's own `github/docs` repo uses for its supported-models data tables, (2) a bounded, never-throwing HTTP fetcher that pulls two of those YAML files and degrades to "no catalog" on any failure, (3) a local cache of the last successful fetch, and (4) a local store of the user's per-function model choices and self-reported Copilot plan. `AgentFileWriter` is extended to accept an optional model string and to write one agent file per configured function (in addition to the existing single General agent file, which stays backward compatible). `InstallState` gains a schema-versioned list of those extra per-function agent files, migrated in place from the current schema. The status panel gets a new section: a plan selector, a refreshable model list, and an editable list of function → family/model rows, each with free-text entry available regardless of whether the fetch ever succeeds.

**Tech Stack:** C# / .NET 8, `System.Text.Json`, `System.Net.Http`, existing xUnit v3 test projects (`TheKameleon.Superpowers.Tests`), WPF via `Microsoft.VisualStudio.Extensibility.UI` (`ObservableList<T>`, `AsyncCommand`, `[DataContract]`/`[DataMember]`) for the status panel.

**Spec:** No separate spec document — this plan follows directly from an in-chat bounded design (brainstorming skill, bounded path) agreed across several turns of conversation on 2026-09-23. The binding requirements are captured in the Global Constraints below.

## Global Constraints

- Never throw out of the fetch/parse path into the ViewModel or into `SuperpowersSetup.Install`/`Repair` — every failure mode (network, HTTP status, oversized response, unparseable/reshaped YAML) becomes a soft "problem" message and a `null` catalog, never an exception the caller must catch.
- Bound every network read by actual bytes received, not declared `Content-Length` — reuse the `TryCopyWithinLimit` pattern already used in `SkillArchiveReader.cs` and `ApprovedReleaseDownloadService.cs`.
- Manual free-text model entry must always work, independent of whether the catalog fetch has ever succeeded — the curated list is a convenience, never a dependency.
- The self-reported Copilot plan is advisory only; nothing in this feature claims to verify actual entitlement. UI copy must say so.
- `model:` is written into agent-file front matter only when the user configured a non-empty value for that function; an unconfigured function's agent file (or the General file when General has no preference) omits the field entirely, exactly as `AgentFileWriter.BuildContent()` does today.
- All new local state (`model-catalog-cache.json`, `model-preferences.json`) lives under `ProfilePaths.StateDirectory`, saved with the existing atomic temp-file-then-`File.Move` pattern used by `InstallStateStore.Save`.
- `InstallState.SchemaVersion` bumps from 1 to 2; `InstallStateStore.Load()` must upgrade an existing v1 file in memory (empty `FunctionAgentFiles`) rather than reporting it as `Corrupt`.

---

### Task 1: Model catalog contracts and the flat-YAML parser

**Files:**
- Create: `TheKameleon.Superpowers.Skills/Models/CopilotModelCatalog.cs`
- Create: `TheKameleon.Superpowers.Skills/Models/FlatYamlListParser.cs`
- Test: `TheKameleon.Superpowers.Tests/FlatYamlListParserTests.cs`

**Interfaces:**
- Produces: `CopilotModel(string Name, string Provider, string ReleaseStatus)`, `CopilotModelPlanAvailability(string Name, bool Pro, bool ProPlus, bool Max, bool Business, bool Enterprise)`, `CopilotPlan` enum, `CopilotModelCatalog(IReadOnlyList<CopilotModel> Models, IReadOnlyList<CopilotModelPlanAvailability> PlanAvailability, DateTimeOffset FetchedAtUtc)`, and `FlatYamlListParser.Parse(string yaml) -> IReadOnlyList<IReadOnlyDictionary<string, string>>` — every later task in this plan consumes these exact names and signatures.

- [ ] **Step 1: Write the failing parser tests**

```csharp
using TheKameleon.Superpowers.Skills.Models;

namespace TheKameleon.Superpowers.Tests;

public sealed class FlatYamlListParserTests
{
    [Fact]
    public void ParsesQuotedScalarFields()
    {
        const string yaml = """
            # comment
            - name: 'GPT-5 mini'
              provider: 'OpenAI'
              release_status: 'GA'

            - name: 'Claude Opus 5.5'
              provider: 'Anthropic'
              release_status: 'GA'
            """;

        var records = FlatYamlListParser.Parse(yaml);

        Assert.Equal(2, records.Count);
        Assert.Equal("GPT-5 mini", records[0]["name"]);
        Assert.Equal("OpenAI", records[0]["provider"]);
        Assert.Equal("Claude Opus 5.5", records[1]["name"]);
    }

    [Fact]
    public void ParsesUnquotedAndBooleanFields()
    {
        const string yaml = """
            - name: Claude Haiku 4.5
              pro: true
              business: false
            """;

        var records = FlatYamlListParser.Parse(yaml);

        Assert.Single(records);
        Assert.Equal("Claude Haiku 4.5", records[0]["name"]);
        Assert.Equal("true", records[0]["pro"]);
        Assert.Equal("false", records[0]["business"]);
    }

    [Fact]
    public void ThrowsFormatExceptionOnUnsupportedShape()
    {
        const string yaml = """
            models:
              - name: nested under a key, not a top-level list
            """;

        Assert.Throws<FormatException>(() => FlatYamlListParser.Parse(yaml));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `./TheKameleon.Superpowers.Tests/bin/Debug/net8.0/TheKameleon.Superpowers.Tests.exe -class TheKameleon.Superpowers.Tests.FlatYamlListParserTests`
Expected: FAIL — `FlatYamlListParser` and `CopilotModel`/etc. do not exist yet.

- [ ] **Step 3: Create the contracts**

```csharp
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

public sealed record CopilotModelCatalog(
    IReadOnlyList<CopilotModel> Models,
    IReadOnlyList<CopilotModelPlanAvailability> PlanAvailability,
    DateTimeOffset FetchedAtUtc);
```

- [ ] **Step 4: Implement the parser**

```csharp
namespace TheKameleon.Superpowers.Skills.Models;

/// <summary>Parses the narrow YAML shape github/docs uses for its data/tables files: a flat list of
/// records, each starting with "- key: value" and continuing with "  key: value" lines. Anything
/// outside this shape (nesting, anchors, multiline scalars, flow style) throws FormatException
/// rather than guessing — callers treat that as "the upstream format changed" and fall back.</summary>
public static class FlatYamlListParser
{
    public static IReadOnlyList<IReadOnlyDictionary<string, string>> Parse(string yaml)
    {
        ArgumentNullException.ThrowIfNull(yaml);
        var records = new List<Dictionary<string, string>>();
        Dictionary<string, string>? current = null;

        foreach (var rawLine in yaml.ReplaceLineEndings("\n").Split('\n'))
        {
            var line = rawLine.TrimEnd();
            var trimmed = line.TrimStart();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
            {
                continue;
            }

            if (trimmed.StartsWith("- ", StringComparison.Ordinal))
            {
                current = new Dictionary<string, string>(StringComparer.Ordinal);
                records.Add(current);
                trimmed = trimmed[2..];
            }
            else if (current is null || !line.StartsWith("  ", StringComparison.Ordinal))
            {
                throw new FormatException($"Unsupported YAML line outside a list item: '{rawLine}'.");
            }

            var colon = trimmed.IndexOf(':');
            if (colon < 0)
            {
                throw new FormatException($"Expected 'key: value' but found '{rawLine}'.");
            }

            var key = trimmed[..colon].Trim();
            var value = trimmed[(colon + 1)..].Trim();
            if (value.Length >= 2 && ((value[0] == '\'' && value[^1] == '\'') || (value[0] == '"' && value[^1] == '"')))
            {
                value = value[1..^1];
            }

            current![key] = value;
        }

        return records;
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `./TheKameleon.Superpowers.Tests/bin/Debug/net8.0/TheKameleon.Superpowers.Tests.exe -class TheKameleon.Superpowers.Tests.FlatYamlListParserTests`
Expected: PASS

- [ ] **Step 6: Commit**

```bash
git add TheKameleon.Superpowers.Skills/Models/CopilotModelCatalog.cs TheKameleon.Superpowers.Skills/Models/FlatYamlListParser.cs TheKameleon.Superpowers.Tests/FlatYamlListParserTests.cs
git commit -m "feat: add Copilot model catalog contracts and flat-YAML parser"
```

---

### Task 2: Model catalog fetcher

**Files:**
- Create: `TheKameleon.Superpowers.Skills/Models/CopilotModelCatalogFetcher.cs`
- Test: `TheKameleon.Superpowers.Tests/CopilotModelCatalogFetcherTests.cs`

**Interfaces:**
- Consumes: `FlatYamlListParser.Parse`, `CopilotModel`, `CopilotModelPlanAvailability`, `CopilotModelCatalog` from Task 1.
- Produces: `ModelCatalogFetchResult(CopilotModelCatalog? Catalog, IReadOnlyList<string> Problems)` with `bool Succeeded => Catalog is not null`, and `CopilotModelCatalogFetcher(HttpClient httpClient).FetchAsync(CancellationToken) -> Task<ModelCatalogFetchResult>` — Task 6 (ViewModel) calls this exactly.

Use `System.Net.Http.HttpMessageHandler`-backed fake responses for tests (a small in-file `FakeHttpMessageHandler : HttpMessageHandler` that returns a scripted status/body per request URL), matching how `ApprovedReleaseDownloadService` is exercised elsewhere in this test project — check `TheKameleon.Superpowers.Tests/ApprovedReleaseDownloadServiceTests.cs` for the existing fake-handler pattern before writing a new one.

- [ ] **Step 1: Write the failing fetcher tests**

```csharp
using System.Net;
using TheKameleon.Superpowers.Skills.Models;

namespace TheKameleon.Superpowers.Tests;

public sealed class CopilotModelCatalogFetcherTests
{
    private const string ReleaseStatusYaml = """
        - name: 'GPT-5.4'
          provider: 'OpenAI'
          release_status: 'GA'
        """;

    private const string SupportedPlansYaml = """
        - name: GPT-5.4
          pro: true
          pro_plus: true
          max: true
          business: true
          enterprise: true
        """;

    [Fact]
    public async Task ReturnsCatalogOnSuccessfulFetch()
    {
        var handler = new StubHttpMessageHandler(url => url.Contains("supported-plans")
            ? Respond(SupportedPlansYaml)
            : Respond(ReleaseStatusYaml));
        var fetcher = new CopilotModelCatalogFetcher(new HttpClient(handler));

        var result = await fetcher.FetchAsync(CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Single(result.Catalog!.Models);
        Assert.Equal("GPT-5.4", result.Catalog.Models[0].Name);
    }

    [Fact]
    public async Task FallsBackWithProblemOnNotFound()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var fetcher = new CopilotModelCatalogFetcher(new HttpClient(handler));

        var result = await fetcher.FetchAsync(CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Problems, p => p.Contains("moved upstream"));
    }

    [Fact]
    public async Task FallsBackWithProblemOnReshapedYaml()
    {
        var handler = new StubHttpMessageHandler(_ => Respond("not: a\n  - valid: shape\nunder: a key"));
        var fetcher = new CopilotModelCatalogFetcher(new HttpClient(handler));

        var result = await fetcher.FetchAsync(CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Problems, p => p.Contains("format has changed"));
    }

    private static HttpResponseMessage Respond(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body) };

    private sealed class StubHttpMessageHandler(Func<string, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request.RequestUri!.ToString()));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `./TheKameleon.Superpowers.Tests/bin/Debug/net8.0/TheKameleon.Superpowers.Tests.exe -class TheKameleon.Superpowers.Tests.CopilotModelCatalogFetcherTests`
Expected: FAIL — `CopilotModelCatalogFetcher` does not exist yet.

- [ ] **Step 3: Implement the fetcher**

```csharp
using System.Net;
using System.Text;

namespace TheKameleon.Superpowers.Skills.Models;

public sealed record ModelCatalogFetchResult(CopilotModelCatalog? Catalog, IReadOnlyList<string> Problems)
{
    public bool Succeeded => this.Catalog is not null;
}

/// <summary>Fetches the two data files GitHub's own docs site renders its supported-models table
/// from. Never throws into the caller: any failure (network, HTTP status, size, or shape) becomes
/// a Problems entry with Catalog == null, so the caller falls back to manual model entry.</summary>
public sealed class CopilotModelCatalogFetcher(HttpClient httpClient)
{
    public const int MaxDownloadBytes = 512 * 1024;
    private const string ReleaseStatusUrl = "https://raw.githubusercontent.com/github/docs/main/data/tables/copilot/model-release-status.yml";
    private const string SupportedPlansUrl = "https://raw.githubusercontent.com/github/docs/main/data/tables/copilot/model-supported-plans.yml";

    private readonly HttpClient httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

    public async Task<ModelCatalogFetchResult> FetchAsync(CancellationToken cancellationToken)
    {
        var problems = new List<string>();
        var releaseStatusText = await this.DownloadTextAsync(ReleaseStatusUrl, problems, cancellationToken).ConfigureAwait(false);
        var supportedPlansText = await this.DownloadTextAsync(SupportedPlansUrl, problems, cancellationToken).ConfigureAwait(false);
        if (releaseStatusText is null || supportedPlansText is null)
        {
            return new ModelCatalogFetchResult(null, problems);
        }

        IReadOnlyList<CopilotModel> models;
        IReadOnlyList<CopilotModelPlanAvailability> planAvailability;
        try
        {
            models = FlatYamlListParser.Parse(releaseStatusText)
                .Select(record => new CopilotModel(
                    RequireField(record, "name"),
                    RequireField(record, "provider"),
                    RequireField(record, "release_status")))
                .ToArray();

            planAvailability = FlatYamlListParser.Parse(supportedPlansText)
                .Select(record => new CopilotModelPlanAvailability(
                    RequireField(record, "name"),
                    ParseBool(record, "pro"),
                    ParseBool(record, "pro_plus"),
                    ParseBool(record, "max"),
                    ParseBool(record, "business"),
                    ParseBool(record, "enterprise")))
                .ToArray();
        }
        catch (FormatException exception)
        {
            problems.Add($"The model list format has changed upstream and could not be read: {exception.Message}");
            return new ModelCatalogFetchResult(null, problems);
        }

        if (models.Count == 0)
        {
            problems.Add("The model list was empty; using manual entry instead.");
            return new ModelCatalogFetchResult(null, problems);
        }

        return new ModelCatalogFetchResult(new CopilotModelCatalog(models, planAvailability, DateTimeOffset.UtcNow), problems);
    }

    private async Task<string?> DownloadTextAsync(string url, List<string> problems, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            response = await this.httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException exception)
        {
            problems.Add($"Couldn't reach the model list ({url}): {exception.Message}");
            return null;
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                problems.Add($"The model list has moved upstream ({url} returned 404).");
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                problems.Add($"The model list request failed with HTTP {(int)response.StatusCode}.");
                return null;
            }

            if (response.Content.Headers.ContentLength > MaxDownloadBytes)
            {
                problems.Add("The model list response was larger than expected and was rejected.");
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var memory = new MemoryStream();
            var buffer = new byte[8192];
            int read;
            long total = 0;
            while ((read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                total += read;
                if (total > MaxDownloadBytes)
                {
                    problems.Add("The model list response was larger than expected and was rejected.");
                    return null;
                }

                memory.Write(buffer, 0, read);
            }

            return Encoding.UTF8.GetString(memory.ToArray());
        }
    }

    private static string RequireField(IReadOnlyDictionary<string, string> record, string key) =>
        record.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new FormatException($"Missing required field '{key}'.");

    private static bool ParseBool(IReadOnlyDictionary<string, string> record, string key) =>
        record.TryGetValue(key, out var value) && string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `./TheKameleon.Superpowers.Tests/bin/Debug/net8.0/TheKameleon.Superpowers.Tests.exe -class TheKameleon.Superpowers.Tests.CopilotModelCatalogFetcherTests`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add TheKameleon.Superpowers.Skills/Models/CopilotModelCatalogFetcher.cs TheKameleon.Superpowers.Tests/CopilotModelCatalogFetcherTests.cs
git commit -m "feat: add never-throwing fetcher for the Copilot model catalog"
```

---

### Task 3: Model catalog cache store

**Files:**
- Create: `TheKameleon.Superpowers.Skills/Models/ModelCatalogCacheStore.cs`
- Test: `TheKameleon.Superpowers.Tests/ModelCatalogCacheStoreTests.cs`

**Interfaces:**
- Consumes: `CopilotModelCatalog` from Task 1, `ProfilePaths` (existing).
- Produces: `ModelCatalogCacheStore(ProfilePaths paths)` with `CopilotModelCatalog? Load()` and `void Save(CopilotModelCatalog catalog)` — Task 6 calls both.

Follow `InstallStateStore.cs` exactly: `WriteIndented = true`, camelCase, atomic write via a `.tmp` file and `File.Move(..., overwrite: true)`, `Load()` returns `null` (not throw) on missing file or `JsonException`. Use `TestSupport`-style temp profile helpers already present in the test project (see `TempProfile` used by `BundledReleaseInstallTests.cs`) for the test's isolated `ProfilePaths`.

- [ ] **Step 1: Write the failing store tests**

```csharp
using TheKameleon.Superpowers.Skills.Models;

namespace TheKameleon.Superpowers.Tests;

public sealed class ModelCatalogCacheStoreTests
{
    [Fact]
    public void LoadReturnsNullWhenNoCacheExists()
    {
        using var profile = new TempProfile();
        var store = new ModelCatalogCacheStore(profile.Paths);

        Assert.Null(store.Load());
    }

    [Fact]
    public void SaveThenLoadRoundTripsTheCatalog()
    {
        using var profile = new TempProfile();
        var store = new ModelCatalogCacheStore(profile.Paths);
        var catalog = new CopilotModelCatalog(
            new[] { new CopilotModel("GPT-5.4", "OpenAI", "GA") },
            new[] { new CopilotModelPlanAvailability("GPT-5.4", true, true, true, true, true) },
            DateTimeOffset.Parse("2026-09-23T00:00:00Z"));

        store.Save(catalog);
        var loaded = store.Load();

        Assert.NotNull(loaded);
        Assert.Equal("GPT-5.4", loaded!.Models[0].Name);
        Assert.Equal(catalog.FetchedAtUtc, loaded.FetchedAtUtc);
    }

    [Fact]
    public void LoadReturnsNullOnCorruptFile()
    {
        using var profile = new TempProfile();
        var store = new ModelCatalogCacheStore(profile.Paths);
        Directory.CreateDirectory(profile.Paths.StateDirectory);
        File.WriteAllText(store.CacheFile, "{ not valid json");

        Assert.Null(store.Load());
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `./TheKameleon.Superpowers.Tests/bin/Debug/net8.0/TheKameleon.Superpowers.Tests.exe -class TheKameleon.Superpowers.Tests.ModelCatalogCacheStoreTests`
Expected: FAIL — `ModelCatalogCacheStore` does not exist yet.

- [ ] **Step 3: Implement the store**

```csharp
using System.Text.Json;
using TheKameleon.Superpowers.Skills.Install;

namespace TheKameleon.Superpowers.Skills.Models;

public sealed class ModelCatalogCacheStore(ProfilePaths paths)
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public string CacheFile => Path.Combine(paths.StateDirectory, "model-catalog-cache.json");

    public CopilotModelCatalog? Load()
    {
        if (!File.Exists(this.CacheFile))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<CopilotModelCatalog>(File.ReadAllText(this.CacheFile), Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public void Save(CopilotModelCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        Directory.CreateDirectory(paths.StateDirectory);
        var temp = this.CacheFile + "." + Guid.NewGuid().ToString("N") + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(catalog, Options));
        File.Move(temp, this.CacheFile, overwrite: true);
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `./TheKameleon.Superpowers.Tests/bin/Debug/net8.0/TheKameleon.Superpowers.Tests.exe -class TheKameleon.Superpowers.Tests.ModelCatalogCacheStoreTests`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add TheKameleon.Superpowers.Skills/Models/ModelCatalogCacheStore.cs TheKameleon.Superpowers.Tests/ModelCatalogCacheStoreTests.cs
git commit -m "feat: cache the last successfully fetched Copilot model catalog"
```

---

### Task 4: Model preferences store

**Files:**
- Create: `TheKameleon.Superpowers.Skills/Models/ModelPreferences.cs`
- Create: `TheKameleon.Superpowers.Skills/Models/ModelPreferencesStore.cs`
- Test: `TheKameleon.Superpowers.Tests/ModelPreferencesStoreTests.cs`

**Interfaces:**
- Produces: `SuperpowersFunction` enum (`General, Plan, Execute, Debug, Tdd, Review, Verify, Refactor, Finish`), `ModelPreference(SuperpowersFunction Function, string Family, string Model)`, `ModelPreferences { int SchemaVersion, CopilotPlan Plan, IReadOnlyList<ModelPreference> Preferences }` with `ModelPreferences.Empty`, and `ModelPreferencesStore(ProfilePaths paths).Load() -> ModelPreferences` / `.Save(ModelPreferences)` — Task 5 and Task 6 both consume `SuperpowersFunction` and `ModelPreferences` by these exact names.

- [ ] **Step 1: Write the failing store tests**

```csharp
using TheKameleon.Superpowers.Skills.Models;

namespace TheKameleon.Superpowers.Tests;

public sealed class ModelPreferencesStoreTests
{
    [Fact]
    public void LoadReturnsEmptyWhenNoFileExists()
    {
        using var profile = new TempProfile();
        var store = new ModelPreferencesStore(profile.Paths);

        var loaded = store.Load();

        Assert.Equal(CopilotPlan.Unspecified, loaded.Plan);
        Assert.Empty(loaded.Preferences);
    }

    [Fact]
    public void SaveThenLoadRoundTripsPreferencesAndPlan()
    {
        using var profile = new TempProfile();
        var store = new ModelPreferencesStore(profile.Paths);
        var preferences = ModelPreferences.Empty with
        {
            Plan = CopilotPlan.Business,
            Preferences = new[]
            {
                new ModelPreference(SuperpowersFunction.Review, "Claude", "Claude Opus 5.5"),
                new ModelPreference(SuperpowersFunction.Debug, "GPT", "GPT-5.4"),
            },
        };

        store.Save(preferences);
        var loaded = store.Load();

        Assert.Equal(CopilotPlan.Business, loaded.Plan);
        Assert.Equal(2, loaded.Preferences.Count);
        Assert.Contains(loaded.Preferences, p => p.Function == SuperpowersFunction.Review && p.Model == "Claude Opus 5.5");
    }

    [Fact]
    public void LoadReturnsEmptyOnUnsupportedSchemaVersion()
    {
        using var profile = new TempProfile();
        var store = new ModelPreferencesStore(profile.Paths);
        Directory.CreateDirectory(profile.Paths.StateDirectory);
        File.WriteAllText(store.PreferencesFile, """{ "schemaVersion": 999, "plan": "Free", "preferences": [] }""");

        var loaded = store.Load();

        Assert.Empty(loaded.Preferences);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `./TheKameleon.Superpowers.Tests/bin/Debug/net8.0/TheKameleon.Superpowers.Tests.exe -class TheKameleon.Superpowers.Tests.ModelPreferencesStoreTests`
Expected: FAIL — types do not exist yet.

- [ ] **Step 3: Add the contracts**

```csharp
namespace TheKameleon.Superpowers.Skills.Models;

public enum SuperpowersFunction
{
    General,
    Plan,
    Execute,
    Debug,
    Tdd,
    Review,
    Verify,
    Refactor,
    Finish,
}

public sealed record ModelPreference(SuperpowersFunction Function, string Family, string Model);

public sealed record ModelPreferences
{
    public const int CurrentSchemaVersion = 1;

    public static ModelPreferences Empty { get; } = new();

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public CopilotPlan Plan { get; init; } = CopilotPlan.Unspecified;

    public IReadOnlyList<ModelPreference> Preferences { get; init; } = Array.Empty<ModelPreference>();
}
```

- [ ] **Step 4: Implement the store**

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;
using TheKameleon.Superpowers.Skills.Install;

namespace TheKameleon.Superpowers.Skills.Models;

public sealed class ModelPreferencesStore(ProfilePaths paths)
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    public string PreferencesFile => Path.Combine(paths.StateDirectory, "model-preferences.json");

    public ModelPreferences Load()
    {
        if (!File.Exists(this.PreferencesFile))
        {
            return ModelPreferences.Empty;
        }

        try
        {
            var preferences = JsonSerializer.Deserialize<ModelPreferences>(File.ReadAllText(this.PreferencesFile), Options);
            return preferences is null || preferences.SchemaVersion != ModelPreferences.CurrentSchemaVersion
                ? ModelPreferences.Empty
                : preferences;
        }
        catch (JsonException)
        {
            return ModelPreferences.Empty;
        }
    }

    public void Save(ModelPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        Directory.CreateDirectory(paths.StateDirectory);
        var temp = this.PreferencesFile + "." + Guid.NewGuid().ToString("N") + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(preferences, Options));
        File.Move(temp, this.PreferencesFile, overwrite: true);
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `./TheKameleon.Superpowers.Tests/bin/Debug/net8.0/TheKameleon.Superpowers.Tests.exe -class TheKameleon.Superpowers.Tests.ModelPreferencesStoreTests`
Expected: PASS

- [ ] **Step 6: Commit**

```bash
git add TheKameleon.Superpowers.Skills/Models/ModelPreferences.cs TheKameleon.Superpowers.Skills/Models/ModelPreferencesStore.cs TheKameleon.Superpowers.Tests/ModelPreferencesStoreTests.cs
git commit -m "feat: store per-function Copilot model preferences and self-reported plan"
```

---

### Task 5: Extend `AgentFileWriter` for per-function agent files with an optional model

**Files:**
- Modify: `TheKameleon.Superpowers.Skills/Bootstrap/AgentFileWriter.cs`
- Test: `TheKameleon.Superpowers.Tests/AgentFileWriterTests.cs` (create if it does not already exist; if it exists, add to it)

**Interfaces:**
- Consumes: `SuperpowersFunction` from Task 4.
- Produces: `AgentFileWriter.BuildContent(string? model = null)` (existing method, now takes an optional model), `AgentFileWriter.FunctionAgentFilePath(ProfilePaths paths, SuperpowersFunction function)` (new static helper Task 6/7 and `SuperpowersSetup` will use), and `AgentFileWriter.WriteFunctionAgent(SuperpowersFunction function, string? model, InstalledFunctionAgentFile? recorded, bool overwriteEdited) -> AgentFileOutcome` (new instance method).

First check the current file (already read this session — `TheKameleon.Superpowers.Skills/Bootstrap/AgentFileWriter.cs`) so the diff below applies cleanly against `BuildContent`, `Write`, and the existing `AgentFileOutcome`/`AgentFileStatus` types, which are unchanged.

- [ ] **Step 1: Write the failing tests**

```csharp
using TheKameleon.Superpowers.Skills.Bootstrap;
using TheKameleon.Superpowers.Skills.Install;
using TheKameleon.Superpowers.Skills.Models;

namespace TheKameleon.Superpowers.Tests;

public sealed class AgentFileWriterTests
{
    [Fact]
    public void BuildContentOmitsModelFieldWhenNull()
    {
        var content = AgentFileWriter.BuildContent(model: null);

        Assert.DoesNotContain("model:", content);
    }

    [Fact]
    public void BuildContentIncludesModelFieldWhenProvided()
    {
        var content = AgentFileWriter.BuildContent(model: "Claude Opus 5.5");

        Assert.Contains("model: Claude Opus 5.5", content);
    }

    [Fact]
    public void WriteFunctionAgentCreatesANamedFileForNonGeneralFunctions()
    {
        using var profile = new TempProfile();
        var writer = new AgentFileWriter(profile.Paths);

        var outcome = writer.WriteFunctionAgent(SuperpowersFunction.Review, "GPT-5.4", recorded: null, overwriteEdited: false);

        Assert.Equal(AgentFileStatus.Written, outcome.Status);
        var path = AgentFileWriter.FunctionAgentFilePath(profile.Paths, SuperpowersFunction.Review);
        Assert.True(File.Exists(path));
        Assert.Contains("model: GPT-5.4", File.ReadAllText(path));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `./TheKameleon.Superpowers.Tests/bin/Debug/net8.0/TheKameleon.Superpowers.Tests.exe -class TheKameleon.Superpowers.Tests.AgentFileWriterTests`
Expected: FAIL — `BuildContent` does not accept a `model` parameter yet; `WriteFunctionAgent`/`FunctionAgentFilePath` do not exist.

- [ ] **Step 3: Add `InstalledFunctionAgentFile` and bump `InstallState` to schema 2**

Modify `TheKameleon.Superpowers.Skills/Install/InstallState.cs`:

```csharp
namespace TheKameleon.Superpowers.Skills.Install;

public sealed record InstallState
{
    public const int CurrentSchemaVersion = 2;

    public static InstallState Empty { get; } = new();

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public InstalledRelease? Release { get; init; }

    public IReadOnlyList<InstalledSkill> Skills { get; init; } = Array.Empty<InstalledSkill>();

    public InstalledAgentFile? AgentFile { get; init; }

    public IReadOnlyList<InstalledFunctionAgentFile> FunctionAgentFiles { get; init; } = Array.Empty<InstalledFunctionAgentFile>();

    public AlwaysOnState AlwaysOn { get; init; } = new(false, false);
}

public sealed record InstalledRelease(string Tag, string Commit, string Source);

public sealed record InstalledSkill(string Name, IReadOnlyDictionary<string, string> FileHashes);

public sealed record InstalledAgentFile(string Sha256, int BootstrapVersion);

public sealed record InstalledFunctionAgentFile(string Function, string Sha256, int BootstrapVersion);

public sealed record AlwaysOnState(bool Enabled, bool CreatedFile);
```

Modify `TheKameleon.Superpowers.Skills/Install/InstallStateStore.cs` so an existing schema-1 file upgrades in place instead of being reported `Corrupt` (find the block starting `if (state is null`):

```csharp
            var state = JsonSerializer.Deserialize<InstallState>(File.ReadAllText(paths.StateFile), Options);
            if (state is null || state.Skills is null || state.AlwaysOn is null
                || state.Skills.Any(skill => skill?.Name is null || skill.FileHashes is null))
            {
                return new InstallStateLoad(InstallStateStatus.Corrupt, InstallState.Empty);
            }

            if (state.SchemaVersion == 1)
            {
                state = state with { SchemaVersion = InstallState.CurrentSchemaVersion, FunctionAgentFiles = Array.Empty<InstalledFunctionAgentFile>() };
            }
            else if (state.SchemaVersion != InstallState.CurrentSchemaVersion)
            {
                return new InstallStateLoad(InstallStateStatus.Corrupt, InstallState.Empty);
            }

            return new InstallStateLoad(InstallStateStatus.Loaded, state);
```

- [ ] **Step 4: Extend `AgentFileWriter`**

Replace `BuildContent` and add the new members in `TheKameleon.Superpowers.Skills/Bootstrap/AgentFileWriter.cs`:

```csharp
using System.Text;
using TheKameleon.Superpowers.Skills.Install;
using TheKameleon.Superpowers.Skills.Models;

namespace TheKameleon.Superpowers.Skills.Bootstrap;

public enum AgentFileStatus
{
    Written,
    UpToDate,
    EditedKept,
    Removed,
    EditedNotRemoved,
    Missing,
}

public sealed record AgentFileOutcome(AgentFileStatus Status, InstalledAgentFile? Agent);

public sealed record FunctionAgentFileOutcome(AgentFileStatus Status, InstalledFunctionAgentFile? Agent);

public sealed class AgentFileWriter(ProfilePaths paths)
{
    private const string Description = "Agent mode with Superpowers skills — brainstorming, planning, TDD, systematic debugging, code review and verification.";

    public static string BuildContent(string? model = null)
    {
        var frontMatter = "---\nname: Superpowers\ndescription: " + Description;
        if (!string.IsNullOrWhiteSpace(model))
        {
            frontMatter += "\nmodel: " + model;
        }

        return frontMatter + "\n---\n\n" + BootstrapText.Body.ReplaceLineEndings("\n") + "\n";
    }

    public static string FunctionAgentFilePath(ProfilePaths paths, SuperpowersFunction function) =>
        function == SuperpowersFunction.General
            ? paths.AgentFile
            : Path.Combine(paths.AgentsRoot, $"superpowers-{function.ToString().ToLowerInvariant()}.agent.md");

    public bool IsEdited(InstalledAgentFile? recorded)
    {
        return File.Exists(paths.AgentFile)
            && (recorded is null || !string.Equals(ContentHash.OfFile(paths.AgentFile), recorded.Sha256, StringComparison.OrdinalIgnoreCase));
    }

    public AgentFileOutcome Write(InstalledAgentFile? recorded, bool overwriteEdited, string? model = null)
    {
        var content = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(BuildContent(model));
        var hash = ContentHash.Of(content);
        var current = new InstalledAgentFile(hash, BootstrapText.Version);

        if (File.Exists(paths.AgentFile))
        {
            var onDisk = ContentHash.OfFile(paths.AgentFile);
            if (string.Equals(onDisk, hash, StringComparison.OrdinalIgnoreCase))
            {
                return new AgentFileOutcome(AgentFileStatus.UpToDate, current);
            }

            if (IsEdited(recorded) && !overwriteEdited)
            {
                return new AgentFileOutcome(AgentFileStatus.EditedKept, recorded);
            }
        }

        Directory.CreateDirectory(paths.AgentsRoot);
        File.WriteAllBytes(paths.AgentFile, content);
        return new AgentFileOutcome(AgentFileStatus.Written, current);
    }

    public bool IsFunctionAgentEdited(SuperpowersFunction function, InstalledFunctionAgentFile? recorded)
    {
        var path = FunctionAgentFilePath(paths, function);
        return File.Exists(path)
            && (recorded is null || !string.Equals(ContentHash.OfFile(path), recorded.Sha256, StringComparison.OrdinalIgnoreCase));
    }

    public FunctionAgentFileOutcome WriteFunctionAgent(SuperpowersFunction function, string? model, InstalledFunctionAgentFile? recorded, bool overwriteEdited)
    {
        var path = FunctionAgentFilePath(paths, function);
        var content = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(BuildContent(model));
        var hash = ContentHash.Of(content);
        var current = new InstalledFunctionAgentFile(function.ToString(), hash, BootstrapText.Version);

        if (File.Exists(path))
        {
            var onDisk = ContentHash.OfFile(path);
            if (string.Equals(onDisk, hash, StringComparison.OrdinalIgnoreCase))
            {
                return new FunctionAgentFileOutcome(AgentFileStatus.UpToDate, current);
            }

            if (IsFunctionAgentEdited(function, recorded) && !overwriteEdited)
            {
                return new FunctionAgentFileOutcome(AgentFileStatus.EditedKept, recorded);
            }
        }

        Directory.CreateDirectory(paths.AgentsRoot);
        File.WriteAllBytes(path, content);
        return new FunctionAgentFileOutcome(AgentFileStatus.Written, current);
    }

    public FunctionAgentFileOutcome RemoveFunctionAgent(SuperpowersFunction function, InstalledFunctionAgentFile? recorded)
    {
        var path = FunctionAgentFilePath(paths, function);
        if (!File.Exists(path))
        {
            return new FunctionAgentFileOutcome(AgentFileStatus.Missing, null);
        }

        if (IsFunctionAgentEdited(function, recorded))
        {
            return new FunctionAgentFileOutcome(AgentFileStatus.EditedNotRemoved, recorded);
        }

        File.Delete(path);
        return new FunctionAgentFileOutcome(AgentFileStatus.Removed, null);
    }

    public AgentFileOutcome Remove(InstalledAgentFile? recorded)
    {
        if (!File.Exists(paths.AgentFile))
        {
            return new AgentFileOutcome(AgentFileStatus.Missing, null);
        }

        if (IsEdited(recorded))
        {
            return new AgentFileOutcome(AgentFileStatus.EditedNotRemoved, recorded);
        }

        File.Delete(paths.AgentFile);
        return new AgentFileOutcome(AgentFileStatus.Removed, null);
    }
}
```

Note `Write`'s new optional `model` parameter defaults to `null`, so every existing call site (`SuperpowersSetup.InstallCore`, `RefreshAgent`) keeps compiling unchanged until Task 6 updates them deliberately.

- [ ] **Step 5: Run the tests to verify they pass**

Run: `./TheKameleon.Superpowers.Tests/bin/Debug/net8.0/TheKameleon.Superpowers.Tests.exe -class TheKameleon.Superpowers.Tests.AgentFileWriterTests`
Expected: PASS. Also re-run the full unit suite here — this task touches `InstallState`'s schema, and `InstallStateStoreTests.cs` / `BundledReleaseInstallTests.cs` must still pass unchanged.

Run: `./TheKameleon.Superpowers.Tests/bin/Debug/net8.0/TheKameleon.Superpowers.Tests.exe`
Expected: PASS, 0 failures.

- [ ] **Step 6: Commit**

```bash
git add TheKameleon.Superpowers.Skills/Bootstrap/AgentFileWriter.cs TheKameleon.Superpowers.Skills/Install/InstallState.cs TheKameleon.Superpowers.Skills/Install/InstallStateStore.cs TheKameleon.Superpowers.Tests/AgentFileWriterTests.cs
git commit -m "feat: write per-function agent files with an optional model, migrate InstallState to schema 2"
```

---

### Task 6: Wire model preferences into `SuperpowersSetup`

**Files:**
- Modify: `TheKameleon.Superpowers.Skills/Setup/SuperpowersSetup.cs`
- Test: `TheKameleon.Superpowers.Tests/SuperpowersSetupTests.cs` (add to existing file — check it exists first with `find TheKameleon.Superpowers.Tests -iname SuperpowersSetupTests.cs`; if not, create it following the `TempProfile` pattern used elsewhere)

**Interfaces:**
- Consumes: `ModelPreferences`, `ModelPreference`, `SuperpowersFunction` (Task 4); `AgentFileWriter.WriteFunctionAgent`, `RemoveFunctionAgent` (Task 5).
- Produces: `SuperpowersSetup.Install(InstalledRelease, SkillArchiveReadResult, bool overwriteEdited, ModelPreferences? modelPreferences = null)` and the equivalent addition to `Repair` — the ViewModel (Task 7) calls these with the user's saved preferences.

- [ ] **Step 1: Write the failing test**

```csharp
using TheKameleon.Superpowers.Skills.Install;
using TheKameleon.Superpowers.Skills.Models;
using TheKameleon.Superpowers.Skills.Setup;

namespace TheKameleon.Superpowers.Tests;

public sealed class SuperpowersSetupModelPreferencesTests
{
    [Fact]
    public void InstallWritesAFunctionAgentFileForEachConfiguredPreference()
    {
        using var profile = new TempProfile();
        var setup = new SuperpowersSetup(profile.Paths);
        var skill = TestSupport.Skill("brainstorming");
        var preferences = ModelPreferences.Empty with
        {
            Preferences = new[] { new ModelPreference(SuperpowersFunction.Review, "Claude", "Claude Opus 5.5") },
        };

        var result = setup.Install(
            new InstalledRelease("v1.0.0", "commit", "bundled"),
            new SkillArchiveReadResult(new[] { skill }, Array.Empty<string>()),
            overwriteEdited: false,
            preferences);

        Assert.Single(result.State.FunctionAgentFiles);
        Assert.Equal("Review", result.State.FunctionAgentFiles[0].Function);
        var path = AgentFileWriter.FunctionAgentFilePath(profile.Paths, SuperpowersFunction.Review);
        Assert.Contains("model: Claude Opus 5.5", File.ReadAllText(path));
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `./TheKameleon.Superpowers.Tests/bin/Debug/net8.0/TheKameleon.Superpowers.Tests.exe -class TheKameleon.Superpowers.Tests.SuperpowersSetupModelPreferencesTests`
Expected: FAIL — `Install` does not accept a `ModelPreferences` argument yet.

- [ ] **Step 3: Update `SuperpowersSetup`**

In `TheKameleon.Superpowers.Skills/Setup/SuperpowersSetup.cs`, add the `using TheKameleon.Superpowers.Skills.Models;` import, change the `Install` signature and its call into `InstallCore`, and change `InstallCore` itself:

```csharp
    public SetupResult Install(InstalledRelease release, SkillArchiveReadResult source, bool overwriteEdited, ModelPreferences? modelPreferences = null)
    {
        using var _ = InstallLock.Acquire(LockTimeout);
        var load = store.Load();
        if (load.Status == InstallStateStatus.Corrupt)
        {
            return new SetupResult(SetupStatus.Failed, new[] { CorruptMessage }, load.State);
        }

        return InstallCore(load.State, release, source, overwriteEdited, modelPreferences ?? ModelPreferences.Empty);
    }
```

```csharp
    public SetupResult Repair(InstalledRelease release, SkillArchiveReadResult source, ModelPreferences? modelPreferences = null)
    {
        using var _ = InstallLock.Acquire(LockTimeout);
        var load = store.Load();
        var known = load.Status == InstallStateStatus.Loaded
            ? load.State
            : InstallState.Empty with { AlwaysOn = new AlwaysOnState(alwaysOn.IsBlockPresent(), false) };
        var knownNames = known.Skills.Select(skill => skill.Name).ToHashSet(StringComparer.Ordinal);
        var adopted = source.Skills
            .Where(package => !knownNames.Contains(package.Name) && skills.MatchesPackage(package))
            .Select(SkillInstaller.Describe);
        var seed = known with { Skills = known.Skills.Concat(adopted).ToArray() };
        if (seed.AgentFile is null && File.Exists(paths.AgentFile)
            && string.Equals(ContentHash.OfFile(paths.AgentFile), ContentHash.Of(System.Text.Encoding.UTF8.GetBytes(AgentFileWriter.BuildContent())), StringComparison.OrdinalIgnoreCase))
        {
            seed = seed with { AgentFile = new InstalledAgentFile(ContentHash.OfFile(paths.AgentFile), BootstrapText.Version) };
        }

        return InstallCore(seed, release, source, overwriteEdited: false, modelPreferences ?? ModelPreferences.Empty);
    }
```

```csharp
    private SetupResult InstallCore(InstallState seed, InstalledRelease release, SkillArchiveReadResult source, bool overwriteEdited, ModelPreferences modelPreferences)
    {
        var messages = source.Problems.ToList();
        var outcome = skills.Install(source.Skills, seed.Skills, overwriteEdited);
        messages.AddRange(outcome.Issues.Select(issue => issue.Message));
        if (!outcome.Succeeded)
        {
            messages.Add(outcome.FailureReason!);
            return new SetupResult(SetupStatus.Failed, messages, seed);
        }

        var generalModel = modelPreferences.Preferences.FirstOrDefault(p => p.Function == Models.SuperpowersFunction.General)?.Model;
        var agentOutcome = agent.Write(seed.AgentFile, overwriteEdited && seed.AgentFile is not null, generalModel);
        if (agentOutcome.Status == AgentFileStatus.EditedKept)
        {
            messages.Add("Your edited superpowers.agent.md was kept.");
        }

        var knownFunctionFiles = seed.FunctionAgentFiles.ToDictionary(f => f.Function, StringComparer.Ordinal);
        var functionAgentFiles = new List<InstalledFunctionAgentFile>();
        foreach (var preference in modelPreferences.Preferences.Where(p => p.Function != Models.SuperpowersFunction.General))
        {
            knownFunctionFiles.TryGetValue(preference.Function.ToString(), out var recorded);
            var functionOutcome = agent.WriteFunctionAgent(preference.Function, preference.Model, recorded, overwriteEdited);
            if (functionOutcome.Status == AgentFileStatus.EditedKept)
            {
                messages.Add($"Your edited superpowers-{preference.Function.ToString().ToLowerInvariant()}.agent.md was kept.");
            }

            if (functionOutcome.Agent is not null)
            {
                functionAgentFiles.Add(functionOutcome.Agent);
            }
        }

        var state = seed with
        {
            Release = release,
            Skills = outcome.Installed,
            AgentFile = agentOutcome.Agent,
            FunctionAgentFiles = functionAgentFiles,
        };
        store.Save(state);
        return new SetupResult(messages.Count == 0 ? SetupStatus.Succeeded : SetupStatus.Partial, messages, state);
    }
```

Also update `Remove()` to remove any function agent files: replace the `skills.Remove(...)` block's surrounding code so it also calls `agent.RemoveFunctionAgent` for every entry in `load.State.FunctionAgentFiles`, appending an "edited, kept" message for any that come back `EditedNotRemoved`, mirroring the existing `agent.Remove(load.State.AgentFile)` handling immediately below it.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `./TheKameleon.Superpowers.Tests/bin/Debug/net8.0/TheKameleon.Superpowers.Tests.exe`
Expected: PASS, 0 failures (full suite — this task changes a widely-used method signature).

- [ ] **Step 5: Commit**

```bash
git add TheKameleon.Superpowers.Skills/Setup/SuperpowersSetup.cs TheKameleon.Superpowers.Tests/SuperpowersSetupModelPreferencesTests.cs
git commit -m "feat: install/repair/remove per-function agent files alongside skills"
```

---

### Task 7: Status panel — fetch/refresh command and preference rows in the ViewModel

**Files:**
- Modify: `TheKameleon.Superpowers.Vsix/SuperpowersViewModel.cs`

**Interfaces:**
- Consumes: `CopilotModelCatalogFetcher`, `ModelCatalogCacheStore`, `ModelPreferencesStore`, `ModelPreferences`, `ModelPreference`, `SuperpowersFunction`, `CopilotPlan` (Tasks 1–4); `SuperpowersSetup.Install`/`Repair` with the new `modelPreferences` parameter (Task 6).
- Produces: bindable members the XAML in Task 8 references by these exact names: `ModelCatalogStatusText`, `SelectedPlan`, `PlanOptions`, `FunctionRows` (an `ObservableList<ModelPreferenceRow>`), `AddPreferenceRowCommand`, `RemovePreferenceRowCommand`, `RefreshModelCatalogCommand`, `SaveModelPreferencesCommand`.

This is a WPF ViewModel, not a unit-testable pure function — there is no dedicated xUnit test for this task; correctness is verified in Task 9's manual verification pass, consistent with how `SuperpowersViewModel`'s existing command methods (`InstallAsync`, `CheckForUpdatesAsync`, etc.) have no direct unit tests today.

- [ ] **Step 1: Add the row view-model and backing fields**

Add near the top of `SuperpowersViewModel.cs`, after the existing `using` block, a small bindable row type (it needs `[DataContract]`/`[DataMember]` like the rest of this file, and `NotifyPropertyChangedObject` for two-way binding of its own fields):

```csharp
    [DataContract]
    internal sealed class ModelPreferenceRow : NotifyPropertyChangedObject
    {
        private string family = string.Empty;
        private string model = string.Empty;

        public ModelPreferenceRow(SuperpowersFunction function)
        {
            this.Function = function;
        }

        [DataMember]
        public SuperpowersFunction Function { get; }

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

Add `using TheKameleon.Superpowers.Skills.Models;` to the top of the file.

In the `SuperpowersViewModel` class, add fields alongside the existing ones:

```csharp
        private readonly CopilotModelCatalogFetcher modelCatalogFetcher = new(Http);
        private readonly ModelCatalogCacheStore modelCatalogCacheStore;
        private readonly ModelPreferencesStore modelPreferencesStore;
        private CopilotModelCatalog? modelCatalog;
        private string modelCatalogStatusText = "Model list not loaded yet.";
        private CopilotPlan selectedPlan = CopilotPlan.Unspecified;
```

In the constructor, after `this.setup = new SuperpowersSetup(this.paths);`, add:

```csharp
            this.modelCatalogCacheStore = new ModelCatalogCacheStore(this.paths);
            this.modelPreferencesStore = new ModelPreferencesStore(this.paths);
            this.modelCatalog = this.modelCatalogCacheStore.Load();
            this.modelCatalogStatusText = this.modelCatalog is null
                ? "Model list not loaded yet. Type a model name manually, or select Refresh model list."
                : $"Model list last refreshed {this.modelCatalog.FetchedAtUtc:yyyy-MM-dd HH:mm} UTC.";
```

and register the new commands alongside the existing ones:

```csharp
            this.RefreshModelCatalogCommand = new AsyncCommand((parameter, context, cancellationToken) => this.RunAsync(this.RefreshModelCatalogAsync, cancellationToken));
            this.AddPreferenceRowCommand = new AsyncCommand((parameter, context, cancellationToken) => { this.FunctionRows.Add(new ModelPreferenceRow(SuperpowersFunction.General)); return Task.CompletedTask; });
            this.RemovePreferenceRowCommand = new AsyncCommand((parameter, context, cancellationToken) => { if (parameter is ModelPreferenceRow row) { this.FunctionRows.Remove(row); } return Task.CompletedTask; });
            this.SaveModelPreferencesCommand = new AsyncCommand((parameter, context, cancellationToken) => this.RunAsync(this.SaveModelPreferencesAsync, cancellationToken));
```

- [ ] **Step 2: Add the bindable members and command implementations**

Add after the existing `CheckForUpdatesCommand` member:

```csharp
        [DataMember]
        public string ModelCatalogStatusText
        {
            get => this.modelCatalogStatusText;
            set => this.SetProperty(ref this.modelCatalogStatusText, value);
        }

        [DataMember]
        public ObservableList<CopilotPlan> PlanOptions { get; } = new(Enum.GetValues<CopilotPlan>());

        [DataMember]
        public CopilotPlan SelectedPlan
        {
            get => this.selectedPlan;
            set => this.SetProperty(ref this.selectedPlan, value);
        }

        [DataMember]
        public ObservableList<ModelPreferenceRow> FunctionRows { get; } = new();

        [DataMember]
        public IAsyncCommand RefreshModelCatalogCommand { get; }

        [DataMember]
        public IAsyncCommand AddPreferenceRowCommand { get; }

        [DataMember]
        public IAsyncCommand RemovePreferenceRowCommand { get; }

        [DataMember]
        public IAsyncCommand SaveModelPreferencesCommand { get; }
```

Add the method bodies near `CheckForUpdatesAsync`:

```csharp
        private async Task RefreshModelCatalogAsync(CancellationToken cancellationToken)
        {
            var result = await this.modelCatalogFetcher.FetchAsync(cancellationToken).ConfigureAwait(false);
            if (!result.Succeeded)
            {
                this.ModelCatalogStatusText = "Couldn't refresh the model list. " + string.Join(" ", result.Problems)
                    + (this.modelCatalog is null ? " Type a model name manually." : " Keeping the last known list.");
                return;
            }

            this.modelCatalog = result.Catalog;
            this.modelCatalogCacheStore.Save(result.Catalog!);
            this.ModelCatalogStatusText = $"Model list refreshed {result.Catalog!.FetchedAtUtc:yyyy-MM-dd HH:mm} UTC ({result.Catalog.Models.Count} models).";
        }

        private Task SaveModelPreferencesAsync(CancellationToken cancellationToken)
        {
            var preferences = new ModelPreferences
            {
                Plan = this.SelectedPlan,
                Preferences = this.FunctionRows
                    .Where(row => !string.IsNullOrWhiteSpace(row.Model))
                    .Select(row => new ModelPreference(row.Function, row.Family, row.Model))
                    .ToArray(),
            };
            this.modelPreferencesStore.Save(preferences);
            this.StatusText = "Model preferences saved. Select Install or Repair to apply them.";
            return Task.CompletedTask;
        }
```

- [ ] **Step 3: Load saved preferences into `FunctionRows` on startup**

In `LoadAsync`, after the existing `await this.RefreshStatusAsync(cancellationToken).ConfigureAwait(false);` line at the end of the method, add:

```csharp
            var savedPreferences = this.modelPreferencesStore.Load();
            this.SelectedPlan = savedPreferences.Plan;
            this.FunctionRows.Clear();
            this.FunctionRows.AddRange(savedPreferences.Preferences.Select(p => new ModelPreferenceRow(p.Function) { Family = p.Family, Model = p.Model }));
```

- [ ] **Step 4: Pass saved preferences into Install and Repair**

In `InstallAsync`, change the call:

```csharp
            var result = await Task.Run(
                () => this.setup.Install(new InstalledRelease(release.ReleaseTag, release.ResolvedCommit, selected.Source), source, overwrite, this.modelPreferencesStore.Load()),
                cancellationToken).ConfigureAwait(false);
```

In `RepairAsync`, change the call:

```csharp
            var result = await Task.Run(
                () => this.setup.Repair(new InstalledRelease(release.ReleaseTag, release.ResolvedCommit, selected.Source), source, this.modelPreferencesStore.Load()),
                cancellationToken).ConfigureAwait(false);
```

- [ ] **Step 5: Build and run the full unit suite**

Run: `dotnet build TheKameleon.Superpowers.slnx`
Expected: 0 errors.

Run: `./TheKameleon.Superpowers.Tests/bin/Debug/net8.0/TheKameleon.Superpowers.Tests.exe`
Expected: PASS, 0 failures (this task only touches the Vsix project, but confirms nothing downstream broke).

- [ ] **Step 6: Commit**

```bash
git add TheKameleon.Superpowers.Vsix/SuperpowersViewModel.cs
git commit -m "feat: add model catalog refresh and per-function preference rows to the status panel view model"
```

---

### Task 8: Status panel XAML — model preferences section

**Files:**
- Modify: `TheKameleon.Superpowers.Vsix/SuperpowersToolWindowControl.xaml`

**Interfaces:**
- Consumes: every bindable member added in Task 7 (`ModelCatalogStatusText`, `SelectedPlan`, `PlanOptions`, `FunctionRows`, `RefreshModelCatalogCommand`, `AddPreferenceRowCommand`, `RemovePreferenceRowCommand`, `SaveModelPreferencesCommand`, and `ModelPreferenceRow.FunctionLabel`/`Family`/`Model`).

- [ ] **Step 1: Add the section**

Insert a new block immediately after the existing `<TextBlock Text="Using Superpowers" ... />` / `Tips` `ItemsControl` block, before the closing `</StackPanel>` (the one right before `</ScrollViewer>`):

```xml
                <TextBlock Text="Model preferences" FontWeight="SemiBold" Margin="0,16,0,4" />
                <TextBlock TextWrapping="Wrap" Margin="0,0,0,4">
                    <Run Text="Optional. Sets which Copilot model each Superpowers function's agent should use. This is advisory only — the extension cannot verify which models are actually enabled on your plan." />
                </TextBlock>
                <TextBlock Text="{Binding ModelCatalogStatusText}" TextWrapping="Wrap" FontStyle="Italic" Margin="0,0,0,4" />

                <Grid Margin="0,4,0,4">
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="Auto" />
                        <ColumnDefinition Width="*" />
                    </Grid.ColumnDefinitions>
                    <TextBlock Grid.Column="0" Text="Your Copilot plan:" Margin="0,0,8,0" VerticalAlignment="Center" />
                    <ComboBox Grid.Column="1"
                              ItemsSource="{Binding PlanOptions}"
                              SelectedItem="{Binding SelectedPlan, Mode=TwoWay}"
                              AutomationProperties.Name="Your Copilot plan" />
                </Grid>

                <Button Command="{Binding RefreshModelCatalogCommand}" Content="Refresh model list" HorizontalAlignment="Left" Padding="12,2" Margin="0,0,0,8" AutomationProperties.Name="Refresh the Copilot model list from GitHub" />

                <ItemsControl ItemsSource="{Binding FunctionRows}">
                    <ItemsControl.ItemTemplate>
                        <DataTemplate>
                            <Grid Margin="0,2,0,2">
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="90" />
                                    <ColumnDefinition Width="100" />
                                    <ColumnDefinition Width="*" />
                                    <ColumnDefinition Width="Auto" />
                                </Grid.ColumnDefinitions>
                                <TextBlock Grid.Column="0" Text="{Binding FunctionLabel}" VerticalAlignment="Center" />
                                <TextBox Grid.Column="1" Text="{Binding Family, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" Margin="4,0" AutomationProperties.Name="Model family" />
                                <TextBox Grid.Column="2" Text="{Binding Model, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" Margin="4,0" AutomationProperties.Name="Model name" />
                                <Button Grid.Column="3" Command="{Binding DataContext.RemovePreferenceRowCommand, RelativeSource={RelativeSource AncestorType=ItemsControl}}"
                                        CommandParameter="{Binding}" Content="Remove" Padding="8,1" AutomationProperties.Name="Remove this model preference row" />
                            </Grid>
                        </DataTemplate>
                    </ItemsControl.ItemTemplate>
                </ItemsControl>

                <StackPanel Orientation="Horizontal" Margin="0,4,0,0">
                    <Button Command="{Binding AddPreferenceRowCommand}" Content="Add row" Padding="12,2" Margin="0,0,8,0" AutomationProperties.Name="Add a model preference row" />
                    <Button Command="{Binding SaveModelPreferencesCommand}" Content="Save model preferences" Padding="12,2" AutomationProperties.Name="Save model preferences" />
                </StackPanel>
```

- [ ] **Step 2: Build**

Run: `dotnet build TheKameleon.Superpowers.slnx`
Expected: 0 errors. (XAML binding typos surface at runtime, not build time — Task 9 covers manual verification of the bindings.)

- [ ] **Step 3: Commit**

```bash
git add TheKameleon.Superpowers.Vsix/SuperpowersToolWindowControl.xaml
git commit -m "feat: add model preferences section to the status panel"
```

---

### Task 9: Manual verification and status-check wording

**Files:**
- Modify: `TheKameleon.Superpowers.Skills/Status/StatusProbe.cs` (only if a function agent file check is warranted — see Step 1)

**Interfaces:**
- Consumes: `InstallState.FunctionAgentFiles` (Task 5).

- [ ] **Step 1: Decide whether `StatusProbe` needs a function-agent-file check**

Read `TheKameleon.Superpowers.Skills/Status/StatusProbe.cs`'s existing `AlwaysOnCheck` and agent-file checks. If it already reports on `state.AgentFile` generically (e.g., "the Superpowers agent is missing/edited"), add a parallel, best-effort check that reports any `FunctionAgentFiles` entry whose file is missing from disk (edited-or-removed-by-hand is not an error state — same tolerance the existing agent check gives the General file). Keep this to a `StatusLevel.Warning`, never `Fail` — a missing function agent file only means that function falls back to the General agent's model (or none), not a broken install.

- [ ] **Step 2: Build the full solution**

Run: `dotnet build TheKameleon.Superpowers.slnx -c Debug`
Expected: 0 errors.

- [ ] **Step 3: Run both full test suites**

Run: `./TheKameleon.Superpowers.Tests/bin/Debug/net8.0/TheKameleon.Superpowers.Tests.exe`
Expected: PASS, 0 failures.

Run: `./TheKameleon.Superpowers.IntegrationTests/bin/Debug/net8.0-windows8.0/TheKameleon.Superpowers.IntegrationTests.exe`
Expected: PASS, 0 failures.

- [ ] **Step 4: Manual verification in Visual Studio**

Launch the experimental instance (F5 or `devenv /rootsuffix Exp`), open the Superpowers tool window, and verify:
1. The "Model preferences" section renders with an empty row list and the plan dropdown defaulting to "Unspecified".
2. Selecting **Refresh model list** either shows a "Model list refreshed … (N models)" status, or — if offline — a "Couldn't refresh the model list…" message that does not crash the panel or block Install/Repair.
3. Adding a row, setting Family/Model text, and selecting **Save model preferences** persists across closing and reopening the tool window (reads back from `%LOCALAPPDATA%\TheKameleon.Superpowers\model-preferences.json`).
4. Selecting **Install** with a saved Review-function preference produces `%USERPROFILE%\.github\agents\superpowers-review.agent.md` containing `model: <the saved value>` in its front matter, alongside the unchanged `superpowers.agent.md`.
5. In Copilot Chat's agent picker, both `Superpowers` and `Superpowers-review` (or whatever functions were configured) appear as separate agents.

- [ ] **Step 5: Commit any `StatusProbe` change from Step 1**

```bash
git add TheKameleon.Superpowers.Skills/Status/StatusProbe.cs
git commit -m "feat: report missing per-function agent files as a soft warning"
```

(If Step 1 concluded no change was warranted, skip this commit — say so in the SDD ledger instead.)
