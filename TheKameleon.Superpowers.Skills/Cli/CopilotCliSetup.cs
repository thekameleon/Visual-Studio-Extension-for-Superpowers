using System.Diagnostics;
using TheKameleon.Superpowers.Skills.Status;

namespace TheKameleon.Superpowers.Skills.Cli;

public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);

public interface IProcessRunner
{
    ProcessResult Run(string command, TimeSpan timeout);
}

/// <summary>Runs a fixed command line through cmd.exe so PATH shims such as npm.cmd and winget resolve.</summary>
public sealed class CmdProcessRunner : IProcessRunner
{
    public ProcessResult Run(string command, TimeSpan timeout)
    {
        var startInfo = new ProcessStartInfo("cmd.exe", $"/d /c {command}")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        try
        {
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return new ProcessResult(-1, string.Empty, $"Could not start: {command}");
            }

            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit((int)timeout.TotalMilliseconds))
            {
                try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
                return new ProcessResult(-1, string.Empty, $"Timed out after {timeout.TotalMinutes:0} minutes: {command}");
            }

            return new ProcessResult(process.ExitCode, output.GetAwaiter().GetResult(), error.GetAwaiter().GetResult());
        }
        catch (System.ComponentModel.Win32Exception exception)
        {
            return new ProcessResult(-1, string.Empty, exception.Message);
        }
    }
}

/// <summary>Installed state of the Copilot CLI. <paramref name="SignedInAs"/> is the GitHub login (or token source) the CLI will use, or null when it is not signed in.</summary>
public sealed record CopilotCliStatus(bool IsInstalled, string? Version, string? SignedInAs);

public sealed record CopilotCliInstallResult(bool Succeeded, string Message);

/// <summary>Result of sending the CLI a real (one-request) prompt to prove its sign-in still works.</summary>
public sealed record CopilotCliVerification(bool Succeeded, string Message);

/// <summary>Detects and, only on explicit request, installs the GitHub Copilot CLI. The CLI is not recorded as owned and is never removed.</summary>
public sealed class CopilotCliSetup(IProcessRunner runner, string configPath, Func<string, string?> getEnvironmentVariable)
{
    public const string WingetInstallCommand = "winget install --id GitHub.Copilot --exact --source winget --accept-package-agreements --accept-source-agreements --disable-interactivity";
    public const string NpmInstallCommand = "npm install -g @github/copilot";
    public const string VerifyCommand = "copilot -p \"Reply with the single word OK.\" -s --no-color";

    private static readonly TimeSpan VerifyTimeout = TimeSpan.FromMinutes(2);

    /// <summary>Token variables the CLI reads, in its documented order of precedence.</summary>
    public static readonly IReadOnlyList<string> TokenVariables = new[] { "COPILOT_GITHUB_TOKEN", "GH_TOKEN", "GITHUB_TOKEN" };

    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan InstallTimeout = TimeSpan.FromMinutes(10);

    public CopilotCliSetup(IProcessRunner runner)
        : this(runner, DefaultConfigPath, Environment.GetEnvironmentVariable)
    {
    }

    public static string DefaultConfigPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".copilot", "config.json");

    public CopilotCliStatus Detect()
    {
        var result = runner.Run("copilot --version", ProbeTimeout);
        if (result.ExitCode != 0)
        {
            return new CopilotCliStatus(false, null, null);
        }

        var version = result.StandardOutput
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();
        return new CopilotCliStatus(true, string.IsNullOrEmpty(version) ? null : version, this.DetectSignIn());
    }

    private string? DetectSignIn()
    {
        foreach (var variable in TokenVariables)
        {
            if (!string.IsNullOrWhiteSpace(getEnvironmentVariable(variable)))
            {
                return $"token from {variable}";
            }
        }

        try
        {
            if (!File.Exists(configPath))
            {
                return null;
            }

            using var document = System.Text.Json.JsonDocument.Parse(
                File.ReadAllText(configPath),
                new System.Text.Json.JsonDocumentOptions { CommentHandling = System.Text.Json.JsonCommentHandling.Skip, AllowTrailingCommas = true });
            var root = document.RootElement;
            if (root.ValueKind != System.Text.Json.JsonValueKind.Object
                || !root.TryGetProperty("loggedInUsers", out var users)
                || users.ValueKind != System.Text.Json.JsonValueKind.Array)
            {
                return null;
            }

            var logins = users.EnumerateArray()
                .Select(user => user.ValueKind == System.Text.Json.JsonValueKind.Object && user.TryGetProperty("login", out var login) ? login.GetString() : null)
                .Where(login => !string.IsNullOrWhiteSpace(login))
                .ToList();
            if (logins.Count == 0)
            {
                return null;
            }

            var last = root.TryGetProperty("lastLoggedInUser", out var lastUser) && lastUser.ValueKind == System.Text.Json.JsonValueKind.Object && lastUser.TryGetProperty("login", out var lastLogin)
                ? lastLogin.GetString()
                : null;
            return last is not null && logins.Contains(last) ? last : logins[0];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidOperationException)
        {
            return null;
        }
    }

    public string? ChooseInstallCommand()
    {
        if (runner.Run("winget --version", ProbeTimeout).ExitCode == 0)
        {
            return WingetInstallCommand;
        }

        return runner.Run("npm --version", ProbeTimeout).ExitCode == 0 ? NpmInstallCommand : null;
    }

    public CopilotCliInstallResult Install(string command)
    {
        if (command != WingetInstallCommand && command != NpmInstallCommand)
        {
            throw new ArgumentException("Only the known Copilot CLI install commands can be run.", nameof(command));
        }

        var result = runner.Run(command, InstallTimeout);
        if (result.ExitCode == 0)
        {
            return new CopilotCliInstallResult(true, "GitHub Copilot CLI installed. Select Sign in to Copilot CLI. You may need to restart Visual Studio so it sees the updated PATH.");
        }

        var detail = string.IsNullOrWhiteSpace(result.StandardError) ? result.StandardOutput : result.StandardError;
        return new CopilotCliInstallResult(false, $"Installing the GitHub Copilot CLI failed (exit code {result.ExitCode}): {detail.Trim()}");
    }

    public CopilotCliVerification Verify()
    {
        var result = runner.Run(VerifyCommand, VerifyTimeout);
        if (result.ExitCode == 0)
        {
            return new CopilotCliVerification(true, "The Copilot CLI answered a test prompt.");
        }

        var detail = string.IsNullOrWhiteSpace(result.StandardError) ? result.StandardOutput : result.StandardError;
        return new CopilotCliVerification(false, $"The Copilot CLI could not answer a test prompt (exit code {result.ExitCode}): {detail.Trim()}");
    }

	public const string InstalledTitle = "Copilot CLI installed";
	public const string SignInTitle = "Copilot CLI signed in";

	public static StatusCheck ToInstalledCheck(CopilotCliStatus status) => status.IsInstalled
		? new StatusCheck(InstalledTitle, StatusLevel.Pass, $"GitHub Copilot CLI {status.Version} is installed.")
		: new StatusCheck(InstalledTitle, StatusLevel.Warning, "GitHub Copilot CLI is not installed. Without it, skills that ask for subagents run their tasks sequentially in the chat. Select Install Copilot CLI to add it.");

	public static StatusCheck ToSignInCheck(CopilotCliStatus status, CopilotCliVerification? verification)
	{
		if (!status.IsInstalled)
		{
			return new StatusCheck(SignInTitle, StatusLevel.Unknown, "Install the Copilot CLI first, then sign in.");
		}

		if (verification is not null)
		{
			return verification.Succeeded
				? new StatusCheck(SignInTitle, StatusLevel.Pass, $"Sign-in verified{(status.SignedInAs is null ? string.Empty : $" for {status.SignedInAs}")}. Skills that need a subagent can dispatch tasks to it.")
				: new StatusCheck(SignInTitle, StatusLevel.Fail, $"{verification.Message} Select Sign in to Copilot CLI.");
		}

		return status.SignedInAs is null
			? new StatusCheck(SignInTitle, StatusLevel.Warning, "Not signed in, so subagent tasks will run sequentially in the chat. Select Sign in to Copilot CLI.")
			: new StatusCheck(SignInTitle, StatusLevel.Pass, $"Signed in as {status.SignedInAs}. Select Verify sign-in to confirm the sign-in still works.");
	}
}
