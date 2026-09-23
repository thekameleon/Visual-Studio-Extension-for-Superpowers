using System.Diagnostics;
using System.Xml.Linq;

namespace TheKameleon.Superpowers.IntegrationTests;

public sealed class BuildVersionTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "SuperpowersBuildVersionTests", Guid.NewGuid().ToString("N"));
    private string StateDirectory => Path.Combine(directory, "state");
    private string CounterPath => Path.Combine(StateDirectory, "revision.txt");

    public BuildVersionTests()
    {
        Directory.CreateDirectory(directory);
        var project = new XDocument(new XElement("Project",
            new XElement("PropertyGroup",
                new XElement("AssemblyVersion", "1.0.0.0"),
                new XElement("IntermediateOutputPath", Path.Combine(directory, "obj") + Path.DirectorySeparatorChar),
                new XElement("SuperpowersBuildStateDirectory", StateDirectory)),
            new XElement("Import", new XAttribute("Project", Path.Combine(AppContext.BaseDirectory, "BuildVersion", "SuperpowersBuildVersion.targets"))),
            new XElement("Target", new XAttribute("Name", "GetAssemblyVersion")),
            new XElement("Target", new XAttribute("Name", "CoreCompile")),
            new XElement("Target", new XAttribute("Name", "Build"), new XAttribute("DependsOnTargets", "GetAssemblyVersion;CoreCompile"),
                new XElement("WriteLinesToFile", new XAttribute("File", "$(SuperpowersBuildVersionFile).result"),
                    new XAttribute("Lines", "$(FileVersion)"), new XAttribute("Overwrite", "true")))));
        project.Save(Path.Combine(directory, "fixture.proj"));
    }

    [Fact]
    public async Task ConsecutiveBuildsIncrementAndConsumerUsesOwnersVersion()
    {
        await AssertBuildAsync("Owner");
        Assert.Equal("1.0.1.1", ReadVersion());
        await AssertBuildAsync("Consumer");
        Assert.Equal("1.0.1.1", ReadVersion());
        Assert.Equal("1", File.ReadAllText(CounterPath));
        await AssertBuildAsync("Owner");
        await AssertBuildAsync("Consumer");
        Assert.Equal("1.0.1.2", ReadVersion());
        Assert.Equal("2", File.ReadAllText(CounterPath));
    }

    [Theory]
    [InlineData("Owner")]
    [InlineData("Consumer")]
    public async Task DesignTimeBuildDoesNotAllocateOrRequireARevision(string role)
    {
        await AssertBuildAsync(role, designTime: true);
        Assert.False(File.Exists(CounterPath));
        Assert.False(File.Exists(Path.Combine(directory, "shared.txt")));
    }

    [Fact]
    public async Task DesignTimeMetadataDoesNotOverwriteRealBuildConstants()
    {
        await AssertBuildAsync("Owner");
        await AssertBuildAsync("Consumer");
        var source = Path.Combine(directory, "obj", "SuperpowersBuildVersion.g.cs");
        var realConstants = File.ReadAllText(source);
        await AssertBuildAsync("Consumer", designTime: true);
        Assert.Equal(realConstants, File.ReadAllText(source));
        Assert.Contains("Revision = 1;", realConstants);
        Assert.Contains("Revision = 0;", File.ReadAllText(Path.Combine(directory, "obj", "SuperpowersBuildVersion.designtime.g.cs")));
        Assert.Equal("1", File.ReadAllText(CounterPath));
    }

    [Fact]
    public async Task RevisionRollsOverWithoutExceedingAssemblyVersionLimits()
    {
        Directory.CreateDirectory(StateDirectory);
        File.WriteAllText(CounterPath, "65534");
        await AssertBuildAsync("Owner");
        Assert.Equal("1.0.2.1", ReadVersion());
    }

    [Fact]
    public async Task InvalidCounterFailsWithoutResettingIt()
    {
        Directory.CreateDirectory(StateDirectory);
        File.WriteAllText(CounterPath, "invalid");
        var result = await RunBuildAsync("Owner");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Equal("invalid", File.ReadAllText(CounterPath));
    }

    [Fact]
    public async Task ConsumerFailsWhenDependencyStampIsMissing()
    {
        var result = await RunBuildAsync("Consumer");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("shared build version is missing", result.Output);
        Assert.False(File.Exists(CounterPath));
    }

    [Fact]
    public async Task ConcurrentOwnersReceiveDistinctRevisions()
    {
        await Task.WhenAll(Enumerable.Range(0, 3).Select(index => AssertBuildAsync("Owner", $"parallel-{index}.txt")));
        var versions = Enumerable.Range(0, 3).Select(index => ReadVersion($"parallel-{index}.txt")).Order().ToArray();
        Assert.Equal(new[] { "1.0.1.1", "1.0.1.2", "1.0.1.3" }, versions);
        Assert.Equal("3", File.ReadAllText(CounterPath));
    }

    [Fact]
    public void RuntimeIdentityUsesExecutingAssemblyFileVersionAndPath()
    {
        var path = typeof(BuildVersionTests).Assembly.Location;
        Assert.Equal(path, BuildIdentity.AssemblyPath);
        Assert.Equal(FileVersionInfo.GetVersionInfo(path).FileVersion, BuildIdentity.Version);
        Assert.Contains($"Loaded build: {BuildIdentity.Version}", BuildIdentity.Describe());
        Assert.Contains($"Loaded DLL: {path}", BuildIdentity.Describe());
    }

    private string ReadVersion(string stamp = "shared.txt") => File.ReadAllText(Path.Combine(directory, stamp + ".result")).Trim();

    private async Task AssertBuildAsync(string role, string stamp = "shared.txt", bool designTime = false)
    {
        var result = await RunBuildAsync(role, stamp, designTime);
        Assert.True(result.ExitCode == 0, result.Output);
    }

    private async Task<(int ExitCode, string Output)> RunBuildAsync(string role, string stamp = "shared.txt", bool designTime = false)
    {
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        {
            WorkingDirectory = directory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (var argument in new[] { "msbuild", "fixture.proj", "-nologo", "-v:minimal", "-t:Build", "-nr:false",
            $"-p:SuperpowersBuildRole={role}", $"-p:SuperpowersBuildVersionFile={Path.Combine(directory, stamp)}",
            $"-p:DesignTimeBuild={designTime.ToString().ToLowerInvariant()}" })
        {
            start.ArgumentList.Add(argument);
        }
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            throw;
        }
        return (process.ExitCode, await output + await error);
    }

    public void Dispose() => Directory.Delete(directory, recursive: true);
}
