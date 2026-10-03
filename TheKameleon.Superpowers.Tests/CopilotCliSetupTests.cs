using TheKameleon.Superpowers.Skills.Cli;
using TheKameleon.Superpowers.Skills.Status;

namespace TheKameleon.Superpowers.Tests;

public sealed class CopilotCliSetupTests
{
    private sealed class FakeRunner(Dictionary<string, ProcessResult> results) : IProcessRunner
    {
        public List<string> Calls { get; } = new();

        public ProcessResult Run(string command, TimeSpan timeout)
        {
            this.Calls.Add(command);
            return results.TryGetValue(command, out var result) ? result : new ProcessResult(1, string.Empty, "not found");
        }
    }

    [Fact]
    public void DetectsInstalledCliAndVersion()
    {
        var runner = new FakeRunner(new() { ["copilot --version"] = new ProcessResult(0, "0.0.330\nCommit: abc\n", string.Empty) });

        var status = new CopilotCliSetup(runner).Detect();

        Assert.True(status.IsInstalled);
        Assert.Equal("0.0.330", status.Version);
    }

    [Fact]
    public void ReportsMissingCli()
    {
        var status = new CopilotCliSetup(new FakeRunner(new())).Detect();

        Assert.False(status.IsInstalled);
        Assert.Null(status.Version);
    }

    [Fact]
    public void PrefersWingetWhenAvailable()
    {
        var runner = new FakeRunner(new()
        {
            ["winget --version"] = new ProcessResult(0, "v1.9", string.Empty),
            ["npm --version"] = new ProcessResult(0, "10.0.0", string.Empty),
        });

        Assert.Equal(CopilotCliSetup.WingetInstallCommand, new CopilotCliSetup(runner).ChooseInstallCommand());
    }

    [Fact]
    public void FallsBackToNpm()
    {
        var runner = new FakeRunner(new() { ["npm --version"] = new ProcessResult(0, "10.0.0", string.Empty) });

        Assert.Equal(CopilotCliSetup.NpmInstallCommand, new CopilotCliSetup(runner).ChooseInstallCommand());
    }

    [Fact]
    public void NoInstallerAvailableReturnsNull()
    {
        Assert.Null(new CopilotCliSetup(new FakeRunner(new())).ChooseInstallCommand());
    }

    [Fact]
    public void InstallRunsOnlyTheGivenCommandAndReportsFailure()
    {
        var runner = new FakeRunner(new() { [CopilotCliSetup.NpmInstallCommand] = new ProcessResult(1, string.Empty, "EACCES") });

        var result = new CopilotCliSetup(runner).Install(CopilotCliSetup.NpmInstallCommand);

        Assert.False(result.Succeeded);
        Assert.Contains("EACCES", result.Message, StringComparison.Ordinal);
        Assert.Equal(new[] { CopilotCliSetup.NpmInstallCommand }, runner.Calls);
    }

    [Fact]
    public void InstallRejectsUnknownCommands()
    {
        var runner = new FakeRunner(new());

        Assert.Throws<ArgumentException>(() => new CopilotCliSetup(runner).Install("rm -rf /"));
        Assert.Empty(runner.Calls);
    }

	[Fact]
	public void InstalledCheckWarnsWhenMissingAndPassesWithVersion()
	{
		var missing = CopilotCliSetup.ToInstalledCheck(new CopilotCliStatus(false, null, null));
		var installed = CopilotCliSetup.ToInstalledCheck(new CopilotCliStatus(true, "0.0.330", null));

		Assert.Equal(CopilotCliSetup.InstalledTitle, missing.Title);
		Assert.Equal(StatusLevel.Warning, missing.Level);
		Assert.Contains("Install Copilot CLI", missing.Message, StringComparison.Ordinal);
		Assert.Equal(StatusLevel.Pass, installed.Level);
		Assert.Contains("0.0.330", installed.Message, StringComparison.Ordinal);
	}

	[Fact]
	public void SignInCheckReflectsRecordedSignIn()
	{
		var missing = CopilotCliSetup.ToSignInCheck(new CopilotCliStatus(false, null, null), null);
		var signedOut = CopilotCliSetup.ToSignInCheck(new CopilotCliStatus(true, "0.0.330", null), null);
		var signedIn = CopilotCliSetup.ToSignInCheck(new CopilotCliStatus(true, "0.0.330", "octocat"), null);

		Assert.Equal(CopilotCliSetup.SignInTitle, signedIn.Title);
		Assert.Equal(StatusLevel.Unknown, missing.Level);
		Assert.Contains("Install", missing.Message, StringComparison.Ordinal);
		Assert.Equal(StatusLevel.Warning, signedOut.Level);
		Assert.Contains("Sign in to Copilot CLI", signedOut.Message, StringComparison.Ordinal);
		Assert.Equal(StatusLevel.Pass, signedIn.Level);
		Assert.Contains("octocat", signedIn.Message, StringComparison.Ordinal);
	}

	[Fact]
	public void SignInCheckUsesVerificationResult()
	{
		var signedIn = new CopilotCliStatus(true, "1.0.91", "octocat");

		var failed = CopilotCliSetup.ToSignInCheck(signedIn, new CopilotCliVerification(false, "token revoked"));
		var verified = CopilotCliSetup.ToSignInCheck(signedIn, new CopilotCliVerification(true, "ok"));

		Assert.Equal(StatusLevel.Fail, failed.Level);
		Assert.Contains("token revoked", failed.Message, StringComparison.Ordinal);
		Assert.Equal(StatusLevel.Pass, verified.Level);
		Assert.Contains("verified", verified.Message, StringComparison.Ordinal);
	}

    [Fact]
    public void VerifySucceedsWhenThePromptRuns()
    {
        var runner = new FakeRunner(new() { [CopilotCliSetup.VerifyCommand] = new ProcessResult(0, "OK\n", string.Empty) });

        var result = new CopilotCliSetup(runner).Verify();

        Assert.True(result.Succeeded);
        Assert.Equal(new[] { CopilotCliSetup.VerifyCommand }, runner.Calls);
    }

    [Fact]
    public void VerifyReportsTheCliErrorWhenThePromptFails()
    {
        var runner = new FakeRunner(new() { [CopilotCliSetup.VerifyCommand] = new ProcessResult(1, string.Empty, "Error: authentication required") });

        var result = new CopilotCliSetup(runner).Verify();

        Assert.False(result.Succeeded);
        Assert.Contains("authentication required", result.Message, StringComparison.Ordinal);
    }


    private static readonly Dictionary<string, ProcessResult> Installed = new() { ["copilot --version"] = new ProcessResult(0, "1.0.91", string.Empty) };

    private static string WriteConfig(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), $"copilot-config-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        return path;
    }

    [Fact]
    public void DetectsSignedInUserFromCliConfig()
    {
        var path = WriteConfig("{ \"lastLoggedInUser\": { \"host\": \"https://github.com\", \"login\": \"octocat\" }, \"loggedInUsers\": [ { \"host\": \"https://github.com\", \"login\": \"octocat\" } ] }");
        try
        {
            var status = new CopilotCliSetup(new FakeRunner(Installed), path, _ => null).Detect();

            Assert.Equal("octocat", status.SignedInAs);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void DetectsSignedInUserWhenConfigHasCommentHeaderAndTrailingCommas()
    {
        var path = WriteConfig("// User settings belong in settings.json.\n// This file is managed automatically.\n{\n  \"loggedInUsers\": [ { \"host\": \"https://github.com\", \"login\": \"octocat\", }, ],\n}\n");
        try
        {
            Assert.Equal("octocat", new CopilotCliSetup(new FakeRunner(Installed), path, _ => null).Detect().SignedInAs);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("{ \"loggedInUsers\": [] }")]
    [InlineData("{ \"firstLaunchAt\": \"2026-01-01\" }")]
    [InlineData("not json")]
    public void ReportsSignedOutWhenConfigHasNoUsersOrIsUnreadable(string json)
    {
        var path = WriteConfig(json);
        try
        {
            Assert.Null(new CopilotCliSetup(new FakeRunner(Installed), path, _ => null).Detect().SignedInAs);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ReportsSignedOutWhenConfigIsMissing()
    {
        var path = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.json");

        Assert.Null(new CopilotCliSetup(new FakeRunner(Installed), path, _ => null).Detect().SignedInAs);
    }

    [Theory]
    [InlineData("COPILOT_GITHUB_TOKEN")]
    [InlineData("GH_TOKEN")]
    [InlineData("GITHUB_TOKEN")]
    public void TreatsTokenEnvironmentVariableAsSignedIn(string variable)
    {
        var path = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.json");

        var status = new CopilotCliSetup(new FakeRunner(Installed), path, name => name == variable ? "secret" : null).Detect();

        Assert.Equal($"token from {variable}", status.SignedInAs);
        Assert.DoesNotContain("secret", status.SignedInAs, StringComparison.Ordinal);
    }

    [Fact]
    public void DoesNotCheckSignInWhenCliIsMissing()
    {
        var path = WriteConfig("{ \"loggedInUsers\": [ { \"login\": \"octocat\" } ] }");
        try
        {
            Assert.Null(new CopilotCliSetup(new FakeRunner(new()), path, _ => null).Detect().SignedInAs);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
