using System.IO.Compression;
using System.Net;
using System.Text;
using TheKameleon.Superpowers.Core.Contracts.Catalog;
using TheKameleon.Superpowers.Skills.Catalog;

namespace TheKameleon.Superpowers.Tests;

public sealed class CatalogCacheManagerTests : IDisposable
{
    private readonly string tempRoot = Path.Combine(Path.GetTempPath(), "SuperpowersTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ImportsActivatedReleaseIntoVersionedCache()
    {
        var activation = await CreateActivationResultAsync("v1.0.0");
        var cacheRoot = Path.Combine(tempRoot, "cache");

        var state = CatalogCacheManager.ImportDownloadedRelease(activation, cacheRoot, retentionCount: 2, reloadState: null);

        Assert.False(state.HasErrors);
        Assert.Equal("v1.0.0", state.ActiveReleaseTag);
        var release = Assert.Single(state.Releases);
        Assert.True(release.IsActive);
        Assert.True(File.Exists(Path.Combine(cacheRoot, "releases", "v1.0.0", "catalog.json")));
    }

    [Fact]
    public async Task RollsBackToPreviouslyCachedRelease()
    {
        var cacheRoot = Path.Combine(tempRoot, "cache");
        var first = await CreateActivationResultAsync("v1.0.0");
        var second = await CreateActivationResultAsync("v2.0.0");
        CatalogCacheManager.ImportDownloadedRelease(first, cacheRoot, retentionCount: 3, reloadState: null);
        CatalogCacheManager.ImportDownloadedRelease(second, cacheRoot, retentionCount: 3, reloadState: null);

        var rollback = CatalogCacheManager.RollbackToCachedRelease(cacheRoot, "v1.0.0", reloadState: null);

        Assert.False(rollback.HasErrors);
        Assert.Equal("v1.0.0", rollback.ActivatedReleaseTag);
        Assert.Equal("v1.0.0", rollback.CacheState.ActiveReleaseTag);
    }

    [Fact]
    public async Task PrunesOldUnpinnedReleasesBeyondRetentionCount()
    {
        var cacheRoot = Path.Combine(tempRoot, "cache");
        CatalogCacheManager.ImportDownloadedRelease(await CreateActivationResultAsync("v1.0.0"), cacheRoot, retentionCount: 5, reloadState: null);
        await Task.Delay(20);
        CatalogCacheManager.ImportDownloadedRelease(await CreateActivationResultAsync("v2.0.0"), cacheRoot, retentionCount: 5, reloadState: null);
        await Task.Delay(20);
        CatalogCacheManager.ImportDownloadedRelease(await CreateActivationResultAsync("v3.0.0"), cacheRoot, retentionCount: 1, reloadState: null);

        var state = CatalogCacheManager.GetCacheState(cacheRoot, reloadState: null);

        Assert.DoesNotContain(state.Releases, release => release.ReleaseTag == "v1.0.0");
        Assert.DoesNotContain(state.Releases, release => release.ReleaseTag == "v2.0.0");
        Assert.Contains(state.Releases, release => release.ReleaseTag == "v3.0.0");
    }

    [Fact]
    public async Task PreservesPinnedCachedReleaseDuringPruning()
    {
        var cacheRoot = Path.Combine(tempRoot, "cache");
        CatalogCacheManager.ImportDownloadedRelease(await CreateActivationResultAsync("v1.0.0"), cacheRoot, retentionCount: 5, reloadState: null);
        var pinnedPath = Path.Combine(cacheRoot, "releases", "v1.0.0", "skills", "brainstorming", "SKILL.md");
        var reloadState = new CatalogReloadState("v1.0.0", new[] { new ActiveRunPin("run-1", "brainstorming", pinnedPath) });
        await Task.Delay(20);
        CatalogCacheManager.ImportDownloadedRelease(await CreateActivationResultAsync("v2.0.0"), cacheRoot, retentionCount: 1, reloadState: reloadState);

        var state = CatalogCacheManager.GetCacheState(cacheRoot, reloadState);

        Assert.Contains(state.Releases, release => release.ReleaseTag == "v1.0.0" && release.IsPinnedByActiveRun);
        Assert.Contains(state.Releases, release => release.ReleaseTag == "v2.0.0" && release.IsActive);
    }

    [Fact]
    public void ReportsErrorWhenRollingBackToMissingCachedRelease()
    {
        var cacheRoot = Path.Combine(tempRoot, "cache");

        var rollback = CatalogCacheManager.RollbackToCachedRelease(cacheRoot, "missing", reloadState: null);

        Assert.True(rollback.HasErrors);
        Assert.Contains(rollback.Diagnostics, diagnostic => diagnostic.Code == "SPCAT703");
    }

    public void Dispose()
    {
        if (Directory.Exists(tempRoot))
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private async Task<DownloadActivationResult> CreateActivationResultAsync(string releaseTag)
    {
        var targetDirectory = Path.Combine(tempRoot, "downloads", releaseTag, "active-catalog");
        using var client = CreateClient(_ => CreateZipResponse(CreateValidArchive()));
        var service = new ApprovedReleaseDownloadService(client);
        var release = new DiscoveredRemoteRelease(
            releaseTag,
            releaseTag,
            false,
            DateTimeOffset.UtcNow,
            ApprovedReleaseDiscoveryService.ApprovedRepositoryUrl,
            $"https://github.com/obra/superpowers/releases/tag/{releaseTag}",
            $"https://api.github.com/repos/obra/superpowers/zipball/{releaseTag}");

        return await service.DownloadAndActivateAsync(release, targetDirectory, approvalGranted: true, CancellationToken.None);
    }

    private static byte[] CreateValidArchive()
    {
        using var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "release-root/LICENSE", "MIT License");
            WriteEntry(archive, "release-root/skills/brainstorming/SKILL.md", "---\nname: brainstorming\ndescription: Plan\n---\nbody\n");
            WriteEntry(archive, "release-root/skills/writing-plans/SKILL.md", "---\nname: writing-plans\ndescription: Plan writer\n---\nbody\n");
        }

        return memory.ToArray();
    }

    private static void WriteEntry(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path);
        using var writer = new StreamWriter(entry.Open(), Encoding.UTF8, leaveOpen: false);
        writer.Write(content);
    }

    private static HttpClient CreateClient(Func<CancellationToken, HttpResponseMessage> responseFactory)
    {
        return new HttpClient(new StubHttpMessageHandler((_, cancellationToken) => Task.FromResult(responseFactory(cancellationToken))));
    }

    private static HttpResponseMessage CreateZipResponse(byte[] bytes)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(bytes)
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
