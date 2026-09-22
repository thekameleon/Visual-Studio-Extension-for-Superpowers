using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using TheKameleon.Superpowers.Core.Contracts.Catalog;
using TheKameleon.Superpowers.Core.Contracts.Settings;

namespace TheKameleon.Superpowers.Skills.Catalog;

public sealed class ApprovedReleaseDiscoveryService(HttpClient httpClient)
{
    public const string ApprovedRepositoryUrl = "https://github.com/obra/superpowers";
    public const string ReleasesApiUrl = "https://api.github.com/repos/obra/superpowers/releases";
    public const int MaxResponseCharacters = 512 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

    public async Task<RemoteReleaseDiscoveryResult> DiscoverAsync(
        ReleaseChannelFilter releaseChannelFilter,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(releaseChannelFilter))
        {
            throw new ArgumentOutOfRangeException(nameof(releaseChannelFilter), "Release channel filter is invalid.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, ReleasesApiUrl);
        if (!httpClient.DefaultRequestHeaders.UserAgent.Any())
        {
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("GitHubCopilot");
        }

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException exception)
        {
            return new RemoteReleaseDiscoveryResult(
                ApprovedRepositoryUrl,
                Array.Empty<DiscoveredRemoteRelease>(),
                new[]
                {
                    new ParseDiagnostic(ParseDiagnosticSeverity.Warning, "SPCAT501", $"Remote release discovery is unavailable offline or due to a network failure: {exception.Message}")
                });
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.Forbidden && IsRateLimited(response))
            {
                return new RemoteReleaseDiscoveryResult(
                    ApprovedRepositoryUrl,
                    Array.Empty<DiscoveredRemoteRelease>(),
                    new[]
                    {
                        new ParseDiagnostic(ParseDiagnosticSeverity.Warning, "SPCAT502", "Remote release discovery is rate-limited by the approved source.")
                    });
            }

            if (!response.IsSuccessStatusCode)
            {
                return new RemoteReleaseDiscoveryResult(
                    ApprovedRepositoryUrl,
                    Array.Empty<DiscoveredRemoteRelease>(),
                    new[]
                    {
                        new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPCAT503", $"Remote release discovery failed with HTTP status {(int)response.StatusCode} ({response.ReasonPhrase}).")
                    });
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (content.Length > MaxResponseCharacters)
            {
                return new RemoteReleaseDiscoveryResult(
                    ApprovedRepositoryUrl,
                    Array.Empty<DiscoveredRemoteRelease>(),
                    new[]
                    {
                        new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPCAT504", $"Remote release discovery response exceeds the supported length of {MaxResponseCharacters} characters.")
                    });
            }

            ReleaseModel[]? payload;
            try
            {
                payload = JsonSerializer.Deserialize<ReleaseModel[]>(content, JsonOptions);
            }
            catch (JsonException exception)
            {
                return new RemoteReleaseDiscoveryResult(
                    ApprovedRepositoryUrl,
                    Array.Empty<DiscoveredRemoteRelease>(),
                    new[]
                    {
                        new ParseDiagnostic(ParseDiagnosticSeverity.Error, "SPCAT505", $"Remote release discovery response is not valid JSON: {exception.Message}")
                    });
            }

            var releases = (payload ?? Array.Empty<ReleaseModel>())
                .Where(release => releaseChannelFilter == ReleaseChannelFilter.IncludePrerelease || !release.Prerelease)
                .Where(release => !string.IsNullOrWhiteSpace(release.TagName)
                    && !string.IsNullOrWhiteSpace(release.PublishedAt)
                    && !string.IsNullOrWhiteSpace(release.HtmlUrl)
                    && !string.IsNullOrWhiteSpace(release.ZipballUrl))
                .Select(release => new
                {
                    Release = release,
                    PublishedAtUtc = DateTimeOffset.TryParse(release.PublishedAt, out var publishedAtUtc)
                        ? publishedAtUtc
                        : (DateTimeOffset?)null,
                })
                .Where(candidate => candidate.PublishedAtUtc.HasValue)
                .Select(candidate => new DiscoveredRemoteRelease(
                    candidate.Release.TagName!,
                    candidate.Release.Name ?? candidate.Release.TagName!,
                    candidate.Release.Prerelease,
                    candidate.PublishedAtUtc!.Value,
                    ApprovedRepositoryUrl,
                    candidate.Release.HtmlUrl!,
                    candidate.Release.ZipballUrl!))
                .OrderByDescending(release => release.PublishedAtUtc)
                .ToArray();

            var diagnostics = new List<ParseDiagnostic>();
            if (payload is not null)
            {
                foreach (var release in payload)
                {
                    if (string.IsNullOrWhiteSpace(release.TagName)
                        || string.IsNullOrWhiteSpace(release.PublishedAt)
                        || string.IsNullOrWhiteSpace(release.HtmlUrl)
                        || string.IsNullOrWhiteSpace(release.ZipballUrl)
                        || !DateTimeOffset.TryParse(release.PublishedAt, out _))
                    {
                        diagnostics.Add(new ParseDiagnostic(ParseDiagnosticSeverity.Warning, "SPCAT506", "Remote release discovery skipped an incomplete or invalid release entry from the approved source."));
                    }
                }
            }

            return new RemoteReleaseDiscoveryResult(ApprovedRepositoryUrl, releases, diagnostics);
        }
    }

    private static bool IsRateLimited(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("X-RateLimit-Remaining", out var values))
        {
            return false;
        }

        return values.Any(value => string.Equals(value, "0", StringComparison.Ordinal));
    }

    private sealed class ReleaseModel
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; init; }

        public string? Name { get; init; }

        public bool Prerelease { get; init; }

        [JsonPropertyName("published_at")]
        public string? PublishedAt { get; init; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; init; }

        [JsonPropertyName("zipball_url")]
        public string? ZipballUrl { get; init; }
    }
}
