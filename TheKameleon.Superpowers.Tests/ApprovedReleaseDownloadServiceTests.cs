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
    public async Task RejectsPlainHttpUrls()
    {
        using var client = CreateClient(_ => CreateZipResponse(CreateValidArchive()));
        var release = CreateApprovedRelease() with { ZipballUrl = "http://api.github.com/repos/obra/superpowers/zipball/v1.0.0" };

        var result = await new ApprovedReleaseDownloadService(client).DownloadAndActivateAsync(release, Path.Combine(tempRoot, "t"), true, CancellationToken.None);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "SPCAT602");
    }

    [Fact]
    public async Task StopsReadingOversizedDownloadsWhileStreaming()
    {
        var oversized = new byte[ApprovedReleaseDownloadService.MaxDownloadBytes + 1];
        using var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new MemoryStream(oversized)) });

        var result = await new ApprovedReleaseDownloadService(client).DownloadAndActivateAsync(CreateApprovedRelease(), Path.Combine(tempRoot, "t"), true, CancellationToken.None);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "SPCAT610");
    }

    [Fact]
    public async Task RejectsTraversalIntoASiblingFolderWithTheSamePrefix()
    {
        using var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "release-root/LICENSE", "MIT License");
            WriteEntry(archive, "../expanded-evil/owned.txt", "nope");
        }

        using var client = CreateClient(_ => CreateZipResponse(memory.ToArray()));

        var result = await new ApprovedReleaseDownloadService(client).DownloadAndActivateAsync(CreateApprovedRelease(), Path.Combine(tempRoot, "t"), true, CancellationToken.None);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "SPCAT606");
    }

    [Fact]
    public async Task KeepsTheActiveReleaseWhenItCannotBeMovedAside()
    {
        var target = Path.Combine(tempRoot, "active-catalog");
        Directory.CreateDirectory(target);
        var sentinel = Path.Combine(target, "sentinel.txt");
        await File.WriteAllTextAsync(sentinel, "keep");
        using var client = CreateClient(_ => CreateZipResponse(CreateValidArchive()));

        DownloadActivationResult result;
        using (new FileStream(sentinel, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            result = await new ApprovedReleaseDownloadService(client).DownloadAndActivateAsync(CreateApprovedRelease(), target, true, CancellationToken.None);
        }

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "SPCAT611");
        Assert.Equal("keep", await File.ReadAllTextAsync(sentinel));
    }

    [Fact]
    public async Task PreservesExistingActiveDirectoryWhenLicenseIsMissing()
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

    [Fact]
    public async Task RejectsMaliciousArchivePathTraversal()
    {
        var targetDirectory = Path.Combine(tempRoot, "active-catalog");
        using var client = CreateClient(_ => CreateZipResponse(CreateArchiveWithTraversalPath()));
        var service = new ApprovedReleaseDownloadService(client);

        var result = await service.DownloadAndActivateAsync(CreateApprovedRelease(), targetDirectory, approvalGranted: true, CancellationToken.None);

        Assert.True(result.HasErrors);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "SPCAT606");
        Assert.False(Directory.Exists(targetDirectory));
    }

    [Fact]
    public async Task PropagatesCancelledDownload()
    {
        using var client = CreateClient(async cancellationToken =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var service = new ApprovedReleaseDownloadService(client);
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.DownloadAndActivateAsync(CreateApprovedRelease(), Path.Combine(tempRoot, "active-catalog"), approvalGranted: true, cancellationTokenSource.Token));
    }

    [Fact]
    public async Task ExtractsArchiveContentCorrectlyAfterBoundingByActualBytes()
    {
        // ExtractArchiveSafely's MaxExtractedBytes is a public const on the class with no override
        // parameter for tests to inject a smaller limit, and adding one would expand this fix's scope
        // beyond the zip-bomb hardening it targets. This archive's real content is well under the
        // production 100MB limit, so this is a regression/smoke test proving the refactored
        // TryCopyWithinLimit path (bounding by actual bytes read instead of declared entry.Length)
        // still extracts normal-sized content correctly, rather than a test of the adversarial
        // declared-vs-actual mismatch itself (which SkillArchiveReaderTests covers for the sibling
        // reader that does not write to disk).
        var targetDirectory = Path.Combine(tempRoot, "active-catalog");
        using var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "release-root/LICENSE", "MIT License");
            WriteEntry(archive, "release-root/skills/brainstorming/SKILL.md", "---\nname: brainstorming\ndescription: Plan\n---\n" + new string('z', 200_000));
            WriteEntry(archive, "release-root/skills/writing-plans/SKILL.md", "---\nname: writing-plans\ndescription: Plan writer\n---\nbody\n");
        }

        using var client = CreateClient(_ => CreateZipResponse(memory.ToArray()));
        var service = new ApprovedReleaseDownloadService(client);

        var result = await service.DownloadAndActivateAsync(CreateApprovedRelease(), targetDirectory, approvalGranted: true, CancellationToken.None);

        Assert.False(result.HasErrors, string.Join(" ", result.Diagnostics.Select(d => d.Message)));
    }

    [Fact]
    public async Task RejectsIncompatibleDownloadedCatalog()
    {
        var targetDirectory = Path.Combine(tempRoot, "active-catalog");
        using var client = CreateClient(_ => CreateZipResponse(CreateArchiveWithoutRequiredSkill()));
        var service = new ApprovedReleaseDownloadService(client);

        var result = await service.DownloadAndActivateAsync(CreateApprovedRelease(), targetDirectory, approvalGranted: true, CancellationToken.None);

        Assert.True(result.HasErrors);
        Assert.NotNull(result.Validation);
        Assert.Contains(result.Validation.Releases.SelectMany(release => release.Diagnostics), diagnostic => diagnostic.Code == "SPCAT422");
        Assert.False(Directory.Exists(targetDirectory));
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

    private static byte[] CreateArchiveWithTraversalPath()
    {
        using var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "../outside.txt", "nope");
        }

        return memory.ToArray();
    }

    private static byte[] CreateArchiveWithoutRequiredSkill()
    {
        using var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "release-root/LICENSE", "MIT License");
            WriteEntry(archive, "release-root/skills/brainstorming/SKILL.md", "---\nname: brainstorming\ndescription: Plan\n---\nbody\n");
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
        return CreateClient(cancellationToken => Task.FromResult(responseFactory(cancellationToken)));
    }

    private static HttpClient CreateClient(Func<CancellationToken, Task<HttpResponseMessage>> responseFactory)
    {
        return new HttpClient(new StubHttpMessageHandler((_, cancellationToken) => responseFactory(cancellationToken)));
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
