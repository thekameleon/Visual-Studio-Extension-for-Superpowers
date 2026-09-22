using System.IO.Compression;
using System.Net;
using System.Text;
using TheKameleon.Superpowers.Core.Contracts.Catalog;
using TheKameleon.Superpowers.Skills.Catalog;

namespace TheKameleon.Superpowers.Tests;

public sealed class ApprovedReleaseDownloadServiceTests : IDisposable
{
    private readonly string tempRoot = Path.Combine(Path.GetTempPath(), "SuperpowersTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task DownloadsAndActivatesApprovedRelease()
    {
        var targetDirectory = Path.Combine(tempRoot, "active-catalog");
        using var client = CreateClient(_ => CreateZipResponse(CreateValidArchive()));
        var service = new ApprovedReleaseDownloadService(client);
        var release = CreateApprovedRelease();

        var result = await service.DownloadAndActivateAsync(release, targetDirectory, approvalGranted: true, CancellationToken.None);

        Assert.False(result.HasErrors);
        Assert.NotNull(result.Stage);
        Assert.Equal(targetDirectory, result.Stage.ActivatedDirectory);
        Assert.NotNull(result.Validation);
        Assert.False(result.Validation.HasErrors);
        Assert.True(File.Exists(Path.Combine(targetDirectory, "catalog.json")));
        Assert.True(File.Exists(Path.Combine(targetDirectory, "releases", release.ReleaseTag, "source.zip")));
    }

    [Fact]
    public async Task RejectsReleaseFromUnapprovedSource()
    {
        var targetDirectory = Path.Combine(tempRoot, "active-catalog");
        using var client = CreateClient(_ => CreateZipResponse(CreateValidArchive()));
        var service = new ApprovedReleaseDownloadService(client);
        var release = new DiscoveredRemoteRelease(
            "v1.0.0",
            "v1.0.0",
            false,
            DateTimeOffset.UtcNow,
            "https://github.com/example/other",
            "https://github.com/example/other/releases/tag/v1.0.0",
            "https://api.github.com/repos/example/other/zipball/v1.0.0");

        var result = await service.DownloadAndActivateAsync(release, targetDirectory, approvalGranted: true, CancellationToken.None);

        Assert.True(result.HasErrors);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "SPCAT602");
        Assert.False(Directory.Exists(targetDirectory));
    }

    [Fact]
    public async Task RejectsInvalidStagedContentAndDoesNotActivate()
    {
        var targetDirectory = Path.Combine(tempRoot, "active-catalog");
        using var client = CreateClient(_ => CreateZipResponse(CreateArchiveWithoutLicense()));
        var service = new ApprovedReleaseDownloadService(client);
        var release = CreateApprovedRelease();

        var result = await service.DownloadAndActivateAsync(release, targetDirectory, approvalGranted: true, CancellationToken.None);

        Assert.True(result.HasErrors);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "SPCAT605");
        Assert.False(Directory.Exists(targetDirectory));
    }

    [Fact]
    public async Task PreservesExistingActiveDirectoryWhenActivationFails()
    {
        Directory.CreateDirectory(tempRoot);
        var existingTarget = Path.Combine(tempRoot, "active-catalog");
        var targetDirectory = existingTarget;
        Directory.CreateDirectory(existingTarget);
        await File.WriteAllTextAsync(Path.Combine(existingTarget, "sentinel.txt"), "keep");

        using var client = CreateClient(_ => CreateZipResponse(CreateArchiveWithoutLicense()));
        var service = new ApprovedReleaseDownloadService(client);
        var release = CreateApprovedRelease();

        var result = await service.DownloadAndActivateAsync(release, targetDirectory, approvalGranted: true, CancellationToken.None);

        Assert.True(result.HasErrors);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "SPCAT605");
        Assert.True(File.Exists(Path.Combine(existingTarget, "sentinel.txt")));
    }

    public void Dispose()
    {
        if (Directory.Exists(tempRoot))
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static DiscoveredRemoteRelease CreateApprovedRelease()
    {
        return new DiscoveredRemoteRelease(
            "v1.0.0",
            "v1.0.0",
            false,
            DateTimeOffset.UtcNow,
            ApprovedReleaseDiscoveryService.ApprovedRepositoryUrl,
            "https://github.com/obra/superpowers/releases/tag/v1.0.0",
            "https://api.github.com/repos/obra/superpowers/zipball/v1.0.0");
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

    private static byte[] CreateArchiveWithoutLicense()
    {
        using var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
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
