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
        if (releaseStatusText is null)
        {
            return new ModelCatalogFetchResult(null, problems);
        }

        var supportedPlansText = await this.DownloadTextAsync(SupportedPlansUrl, problems, cancellationToken).ConfigureAwait(false);
        if (supportedPlansText is null)
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
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            problems.Add($"Couldn't reach the model list ({url}): the request timed out.");
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

            using var bodyTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, bodyTimeout.Token);
            try
            {
                await using var stream = await response.Content.ReadAsStreamAsync(linked.Token).ConfigureAwait(false);
                using var memory = new MemoryStream();
                var buffer = new byte[8192];
                int read;
                long total = 0;
                while ((read = await stream.ReadAsync(buffer, linked.Token).ConfigureAwait(false)) > 0)
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
            catch (IOException exception)
            {
                problems.Add($"Couldn't read the model list ({url}): {exception.Message}");
                return null;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                problems.Add($"Couldn't read the model list ({url}): the request timed out.");
                return null;
            }
        }
    }

    private static string RequireField(IReadOnlyDictionary<string, string> record, string key) =>
        record.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new FormatException($"Missing required field '{key}'.");

    private static bool ParseBool(IReadOnlyDictionary<string, string> record, string key) =>
        record.TryGetValue(key, out var value) && string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
}
