using TheKameleon.Superpowers.Skills.Execution;

namespace TheKameleon.Superpowers.Tests;

public sealed class PreimageValidatorTests
{
    [Fact]
    public void SucceedsWhenPreimageMatchesCurrentTextAtRange()
    {
        var result = PreimageValidator.Validate("Hello, world!", 7, 5, "world");

        Assert.True(result.IsValid);
    }

    [Fact]
    public void FailsWhenPreimageNoLongerMatchesCurrentText()
    {
        var result = PreimageValidator.Validate("Hello, there!", 7, 5, "world");

        Assert.False(result.IsValid);
        Assert.Equal("SPEDIT305", result.DiagnosticCode);
    }

    [Fact]
    public void FailsWhenRangeExceedsDocumentLength()
    {
        var result = PreimageValidator.Validate("short", 10, 5, "world");

        Assert.False(result.IsValid);
        Assert.Equal("SPEDIT303", result.DiagnosticCode);
    }

    [Fact]
    public void FailsWhenLengthExtendsBeyondDocumentEnd()
    {
        var result = PreimageValidator.Validate("short", 2, 100, "ort-extra");

        Assert.False(result.IsValid);
        Assert.Equal("SPEDIT304", result.DiagnosticCode);
    }

    [Fact]
    public void FailsForNegativeStartOffset()
    {
        var result = PreimageValidator.Validate("short", -1, 2, "sh");

        Assert.False(result.IsValid);
        Assert.Equal("SPEDIT301", result.DiagnosticCode);
    }

    [Fact]
    public void FailsForNegativeLength()
    {
        var result = PreimageValidator.Validate("short", 0, -2, "sh");

        Assert.False(result.IsValid);
        Assert.Equal("SPEDIT302", result.DiagnosticCode);
    }
}
