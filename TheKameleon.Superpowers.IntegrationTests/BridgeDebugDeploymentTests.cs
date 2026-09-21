using System.Diagnostics;
using System.Xml.Linq;

namespace TheKameleon.Superpowers.IntegrationTests;

public sealed class BridgeDebugDeploymentTests
{
    private static XDocument ReadProject() => XDocument.Load(
        Path.Combine(AppContext.BaseDirectory, "DebugDeployment", "Bridge.csproj"));

    [Fact]
    public void OrdinaryBridgeBuildDefaultsToNoDeployment()
    {
        var property = Assert.Single(ReadProject().Descendants("DeployExtension"));
        Assert.Equal("false", property.Value);
        Assert.Equal("'$(DeployExtension)' == ''", (string?)property.Attribute("Condition"));
    }

    [Theory]
    [InlineData("BridgeDebugDeployment", "true")]
    [InlineData("Configuration", "Debug")]
    [InlineData("VSSDKTargetPlatformRegRootSuffix", "Exp")]
    [InlineData("DeployTargetInstanceId", "")]
    [InlineData("MSBuildRuntimeType", "Full")]
    public void DeploymentGuardRequiresExplicitSafeTarget(string property, string value)
    {
        var guard = Assert.Single(ReadProject().Descendants("Target"),
            target => (string?)target.Attribute("Name") == "ValidateBridgeDebugDeployment");
        Assert.Equal("GetVsixDeploymentPath;DeployVsixExtensionFiles", (string?)guard.Attribute("BeforeTargets"));
        Assert.Contains(guard.Elements("Error"), error =>
            ((string?)error.Attribute("Condition"))?.Contains($"'$({property})'", StringComparison.Ordinal) == true &&
            ((string?)error.Attribute("Condition"))?.Contains($"'{value}'", StringComparison.Ordinal) == true);
    }

    [Fact]
    public void ExplicitTargetValidatesBeforeBuildAndDeployment()
    {
        var target = Assert.Single(ReadProject().Descendants("Target"),
            element => (string?)element.Attribute("Name") == "PrepareBridgeDebug");
        Assert.Equal("ValidateBridgeDebugDeployment;Build;DeployVsixExtensionFiles",
            (string?)target.Attribute("DependsOnTargets"));
        Assert.Null(target.Attribute("AfterTargets"));
        Assert.Null(target.Attribute("BeforeTargets"));
    }

    [Fact]
    public void ScriptPinsExpAndRequiresAnIdeSelection()
    {
        var script = File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "DebugDeployment", "Prepare-BridgeDebug.ps1"));
        Assert.Contains("[ValidateSet('2022', '2026')]", script);
        Assert.Contains("SupportsShouldProcess = $true", script);
        Assert.Contains("'/p:VSSDKTargetPlatformRegRootSuffix=Exp'", script);
        Assert.Contains("/p:DeployTargetInstanceId=$($instance.instanceId)", script);
        Assert.Contains("if ($LASTEXITCODE -ne 0)", script);
        Assert.DoesNotContain("Stop-Process", script);
        Assert.DoesNotContain("Remove-Item", script);
    }

    [Fact]
    public void ModernProjectBuildsBridgeWithoutBundlingIt()
    {
        var project = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "DebugDeployment", "Modern.csproj"));
        Assert.Contains(project.Descendants("Import"), import =>
            (string?)import.Attribute("Project") == "..\\build\\BridgeDebugDeployment.targets");
        Assert.Empty(project.Descendants("PreBuildEvent"));
        Assert.Empty(project.Descendants("PostBuildEvent"));
        var reference = Assert.Single(project.Descendants("ProjectReference"));
        Assert.Contains("InProcess", (string?)reference.Attribute("Include"));
        Assert.Equal("false", (string?)reference.Attribute("ReferenceOutputAssembly"));
        Assert.Equal("true", (string?)reference.Attribute("SkipGetTargetFrameworkProperties"));
        Assert.Equal("", (string?)reference.Attribute("IncludeOutputGroupsInVSIX"));
    }

    [Fact]
    public void NativeDeploymentRequiresSafeDebugSelectionAndNeverInstallsDuringBuild()
    {
        var project = ReadHook();
        Assert.Empty(project.Descendants("Exec"));
        Assert.DoesNotContain("DeploymentAssetsOutputGroup", project.ToString());
        var enabled = Assert.Single(project.Descendants("VsixDeployOnDebug"), p => p.Value == "true");
        var condition = (string)enabled.Attribute("Condition")!;
        Assert.Contains("'$(BuildingInsideVisualStudio)' == 'true'", condition);
        Assert.Contains("'$(Configuration)' == 'Debug'", condition);
        Assert.Contains("DeployTargetInstanceId", condition);
        Assert.Contains("'$(VSSDKTargetPlatformRegRootSuffix)' == 'Exp'", condition);
        Assert.Contains("'$(RootSuffix)' == 'Exp'", condition);
        Assert.Contains(project.Descendants("VsixDeployOnDebug"), p => p.Value == "false");
    }

    [Fact]
    public void BridgeUsesModernSelectionAndSolutionDeploysItInDebugOnly()
    {
        Assert.Contains("TheKameleon.Superpowers.Vsix.csproj.user", ReadHook().ToString());
        Assert.Contains(ReadProject().Descendants("Import"), p =>
            (string?)p.Attribute("Project") == "..\\build\\BridgeDebugDeployment.targets");
        var solution = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "DebugDeployment", "Solution.slnx"));
        var bridge = Assert.Single(solution.Descendants("Project"), p =>
            ((string?)p.Attribute("Path"))?.Contains("InProcess", StringComparison.Ordinal) == true);
        Assert.Equal("Debug|*", (string?)Assert.Single(bridge.Elements("Deploy")).Attribute("Solution"));
    }

    private static XDocument ReadHook()
    {
        var project = XDocument.Load(Path.Combine(AppContext.BaseDirectory,
            "DebugDeployment", "BridgeDebugDeployment.targets"));
        return project;
    }

    [Theory]
    [InlineData("-InstanceId", "test-instance", true)]
    [InlineData("-VisualStudioVersion", "2026", true)]
    [InlineData(null, null, false)]
    public async Task ScriptSelectionNeverNeedsInteractiveInput(string? argument, string? value, bool succeeds)
    {
        var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "DebugDeployment", "Prepare-BridgeDebug.ps1"));
        var discovery = source.IndexOf("$vswhere =", StringComparison.Ordinal);
        Assert.True(discovery > 0);
        var path = Path.Combine(Path.GetTempPath(), $"BridgeSelection-{Guid.NewGuid():N}.ps1");
        try
        {
            // Exercise the actual binding and selection guard without discovering or modifying an IDE.
            await File.WriteAllTextAsync(path, source[..discovery] + "\nWrite-Output 'Selection accepted'\n");
            var start = new ProcessStartInfo("pwsh.exe")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            foreach (var item in new[] { "-NoProfile", "-NonInteractive", "-File", path })
            {
                start.ArgumentList.Add(item);
            }
            if (argument is not null)
            {
                start.ArgumentList.Add(argument);
                start.ArgumentList.Add(value!);
            }
            using var process = Process.Start(start)!;
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                process.Kill(entireProcessTree: true);
                throw new TimeoutException("Noninteractive bridge parameter validation exceeded 15 seconds.");
            }
            var output = await stdout;
            var error = await stderr;
            if (succeeds)
            {
                Assert.True(process.ExitCode == 0, error);
                Assert.Contains("Selection accepted", output);
            }
            else
            {
                Assert.NotEqual(0, process.ExitCode);
                Assert.Contains("Specify -InstanceId or -VisualStudioVersion", error);
            }
        }
        finally
        {
            File.Delete(path);
        }
    }
}
