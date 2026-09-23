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
