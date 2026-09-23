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
    public async Task ReturnsCatalogOnSuccessfulFetch()
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
        Assert.Single(result.Catalog!.Models);
        Assert.Equal("GPT-5.4", result.Catalog.Models[0].Name);
    }

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
        Assert.NotEmpty(result.Problems);
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

    [Fact]
    public async Task FallsBackWithProblemOnInternalHttpClientTimeout()
    {
        var handler = new TimeoutHttpMessageHandler();
        var fetcher = new CopilotModelCatalogFetcher(new HttpClient(handler));

        var result = await fetcher.FetchAsync(CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Problems, p => p.Contains("timed out"));
    }

    private static HttpResponseMessage Respond(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body) };

    private sealed class StubHttpMessageHandler(Func<string, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request.RequestUri!.ToString()));
    }

    /// <summary>Simulates HttpClient's own internal Timeout firing: throws TaskCanceledException
    /// unconditionally, without regard to the CancellationToken the caller passed in, so tests can
    /// distinguish "internal timeout" from "caller-requested cancellation".</summary>
    private sealed class TimeoutHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new TaskCanceledException("Simulated HttpClient timeout");
    }
}
