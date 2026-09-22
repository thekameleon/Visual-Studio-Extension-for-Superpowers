using System.Net;
using System.Net.Http.Headers;
using System.Text;
using TheKameleon.Superpowers.Core.Contracts.Settings;
using TheKameleon.Superpowers.Skills.Catalog;

namespace TheKameleon.Superpowers.Tests;

public sealed class ApprovedReleaseDiscoveryServiceTests
{
    [Fact]
    public async Task ReturnsStableReleasesOnlyWhenStableFilterIsSelected()
    {
        using var client = CreateClient(_ => CreateJsonResponse("""
        [
          {
            "tag_name": "v2.0.0-beta",
            "name": "v2.0.0-beta",
            "prerelease": true,
            "published_at": "2026-09-20T00:00:00Z",
            "html_url": "https://github.com/obra/superpowers/releases/tag/v2.0.0-beta",
            "zipball_url": "https://api.github.com/repos/obra/superpowers/zipball/v2.0.0-beta"
          },
          {
            "tag_name": "v1.9.0",
            "name": "v1.9.0",
            "prerelease": false,
            "published_at": "2026-09-19T00:00:00Z",
            "html_url": "https://github.com/obra/superpowers/releases/tag/v1.9.0",
            "zipball_url": "https://api.github.com/repos/obra/superpowers/zipball/v1.9.0"
          }
        ]
        """));
        var service = new ApprovedReleaseDiscoveryService(client);

        var result = await service.DiscoverAsync(ReleaseChannelFilter.StableOnly, CancellationToken.None);

        Assert.False(result.HasErrors);
        var release = Assert.Single(result.Releases);
        Assert.Equal("v1.9.0", release.ReleaseTag);
        Assert.False(release.IsPrerelease);
    }

    [Fact]
    public async Task IncludesPrereleasesWhenConfigured()
    {
        using var client = CreateClient(_ => CreateJsonResponse("""
        [
          {
            "tag_name": "v2.0.0-beta",
            "name": "v2.0.0-beta",
            "prerelease": true,
            "published_at": "2026-09-20T00:00:00Z",
            "html_url": "https://github.com/obra/superpowers/releases/tag/v2.0.0-beta",
            "zipball_url": "https://api.github.com/repos/obra/superpowers/zipball/v2.0.0-beta"
          },
          {
            "tag_name": "v1.9.0",
            "name": "v1.9.0",
            "prerelease": false,
            "published_at": "2026-09-19T00:00:00Z",
            "html_url": "https://github.com/obra/superpowers/releases/tag/v1.9.0",
            "zipball_url": "https://api.github.com/repos/obra/superpowers/zipball/v1.9.0"
          }
        ]
        """));
        var service = new ApprovedReleaseDiscoveryService(client);

        var result = await service.DiscoverAsync(ReleaseChannelFilter.IncludePrerelease, CancellationToken.None);

        Assert.False(result.HasErrors);
        Assert.Equal(2, result.Releases.Count);
        Assert.Equal("v2.0.0-beta", result.Releases[0].ReleaseTag);
        Assert.True(result.Releases[0].IsPrerelease);
    }

    [Fact]
    public async Task PropagatesCancellation()
    {
        using var client = CreateClient(async cancellationToken =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var service = new ApprovedReleaseDiscoveryService(client);
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.DiscoverAsync(ReleaseChannelFilter.StableOnly, cancellationTokenSource.Token));
    }

    [Fact]
    public async Task ReportsOfflineNetworkFailureAsWarning()
    {
        using var client = CreateClient(cancellationToken => Task.FromException<HttpResponseMessage>(new HttpRequestException("No such host is known.")));
        var service = new ApprovedReleaseDiscoveryService(client);

        var result = await service.DiscoverAsync(ReleaseChannelFilter.StableOnly, CancellationToken.None);

        Assert.Empty(result.Releases);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("SPCAT501", diagnostic.Code);
        Assert.Equal(TheKameleon.Superpowers.Core.Contracts.Catalog.ParseDiagnosticSeverity.Warning, diagnostic.Severity);
    }

    [Fact]
    public async Task ReportsRateLimitAsWarning()
    {
        using var client = CreateClient(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.Forbidden);
            response.Headers.Add("X-RateLimit-Remaining", "0");
            return response;
        });
        var service = new ApprovedReleaseDiscoveryService(client);

        var result = await service.DiscoverAsync(ReleaseChannelFilter.StableOnly, CancellationToken.None);

        Assert.Empty(result.Releases);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("SPCAT502", diagnostic.Code);
        Assert.Equal(TheKameleon.Superpowers.Core.Contracts.Catalog.ParseDiagnosticSeverity.Warning, diagnostic.Severity);
    }

    [Fact]
    public async Task SkipsIncompleteEntriesWithWarning()
    {
        using var client = CreateClient(_ => CreateJsonResponse("""
        [
          {
            "tag_name": "v1.9.0",
            "name": "v1.9.0",
            "prerelease": false,
            "published_at": "2026-09-19T00:00:00Z",
            "html_url": "https://github.com/obra/superpowers/releases/tag/v1.9.0",
            "zipball_url": "https://api.github.com/repos/obra/superpowers/zipball/v1.9.0"
          },
          {
            "tag_name": "broken",
            "name": "broken",
            "prerelease": false,
            "published_at": "",
            "html_url": "https://github.com/obra/superpowers/releases/tag/broken",
            "zipball_url": "https://api.github.com/repos/obra/superpowers/zipball/broken"
          }
        ]
        """));
        var service = new ApprovedReleaseDiscoveryService(client);

        var result = await service.DiscoverAsync(ReleaseChannelFilter.StableOnly, CancellationToken.None);

        Assert.Single(result.Releases);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "SPCAT506");
    }

    [Fact]
    public async Task IncludesEveryPublishedEntryWhenPrereleasesAreEnabled()
    {
        using var client = CreateClient(_ => CreateJsonResponse("""
        [
          {
            "tag_name": "v3.0.0-preview",
            "name": "v3.0.0-preview",
            "prerelease": true,
            "published_at": "2026-09-21T00:00:00Z",
            "html_url": "https://github.com/obra/superpowers/releases/tag/v3.0.0-preview",
            "zipball_url": "https://api.github.com/repos/obra/superpowers/zipball/v3.0.0-preview"
          },
          {
            "tag_name": "v2.5.0",
            "name": "v2.5.0",
            "prerelease": false,
            "published_at": "2026-09-20T00:00:00Z",
            "html_url": "https://github.com/obra/superpowers/releases/tag/v2.5.0",
            "zipball_url": "https://api.github.com/repos/obra/superpowers/zipball/v2.5.0"
          },
          {
            "tag_name": "v2.4.0",
            "name": "v2.4.0",
            "prerelease": false,
            "published_at": "2026-09-19T00:00:00Z",
            "html_url": "https://github.com/obra/superpowers/releases/tag/v2.4.0",
            "zipball_url": "https://api.github.com/repos/obra/superpowers/zipball/v2.4.0"
          }
        ]
        """));
        var service = new ApprovedReleaseDiscoveryService(client);

        var result = await service.DiscoverAsync(ReleaseChannelFilter.IncludePrerelease, CancellationToken.None);

        Assert.False(result.HasErrors);
        Assert.Equal(new[] { "v3.0.0-preview", "v2.5.0", "v2.4.0" }, result.Releases.Select(release => release.ReleaseTag).ToArray());
    }

    [Fact]
    public async Task ReportsNonSuccessStatusAsError()
    {
        using var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.BadGateway)
        {
            ReasonPhrase = "Bad Gateway"
        });
        var service = new ApprovedReleaseDiscoveryService(client);

        var result = await service.DiscoverAsync(ReleaseChannelFilter.StableOnly, CancellationToken.None);

        Assert.True(result.HasErrors);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "SPCAT503");
    }

    private static HttpClient CreateClient(Func<CancellationToken, HttpResponseMessage> responseFactory)
    {
        return CreateClient(cancellationToken => Task.FromResult(responseFactory(cancellationToken)));
    }

    private static HttpClient CreateClient(Func<CancellationToken, Task<HttpResponseMessage>> responseFactory)
    {
        return new HttpClient(new StubHttpMessageHandler(async (_, cancellationToken) => await responseFactory(cancellationToken)))
        {
            BaseAddress = new Uri("https://api.github.com/")
        };
    }

    private static HttpResponseMessage CreateJsonResponse(string json)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return responseFactory(request, cancellationToken);
        }
    }
}
