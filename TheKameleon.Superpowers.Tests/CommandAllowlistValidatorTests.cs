using TheKameleon.Superpowers.Core.Contracts.Settings;
using TheKameleon.Superpowers.Skills.Execution;

namespace TheKameleon.Superpowers.Tests;

public sealed class CommandAllowlistValidatorTests
{
    private static readonly CustomCommandAllowlistEntry Entry = new(
        "format-check",
        @"C:\tools\format.exe",
        new[] { "--verify", "--no-restore" });

    [Fact]
    public void AllowsExactMatchOfCommandExecutableAndArguments()
    {
        var result = CommandAllowlistValidator.Validate(new[] { Entry }, "format-check", @"C:\tools\format.exe", new[] { "--verify" });

        Assert.True(result.IsAllowed);
    }

    [Fact]
    public void DeniesUnknownCommandId()
    {
        var result = CommandAllowlistValidator.Validate(new[] { Entry }, "unknown-command", @"C:\tools\format.exe", Array.Empty<string>());

        Assert.False(result.IsAllowed);
        Assert.Equal("SPCMD403", result.DiagnosticCode);
    }

    [Fact]
    public void DeniesMismatchedExecutablePath()
    {
        var result = CommandAllowlistValidator.Validate(new[] { Entry }, "format-check", @"C:\evil\format.exe", Array.Empty<string>());

        Assert.False(result.IsAllowed);
        Assert.Equal("SPCMD404", result.DiagnosticCode);
    }

    [Fact]
    public void DeniesArgumentNotInAllowedSet()
    {
        var result = CommandAllowlistValidator.Validate(new[] { Entry }, "format-check", @"C:\tools\format.exe", new[] { "--verify", "--delete-everything" });

        Assert.False(result.IsAllowed);
        Assert.Equal("SPCMD405", result.DiagnosticCode);
    }

    [Fact]
    public void DeniesEmptyAllowlist()
    {
        var result = CommandAllowlistValidator.Validate(Array.Empty<CustomCommandAllowlistEntry>(), "format-check", @"C:\tools\format.exe", Array.Empty<string>());

        Assert.False(result.IsAllowed);
    }
}
