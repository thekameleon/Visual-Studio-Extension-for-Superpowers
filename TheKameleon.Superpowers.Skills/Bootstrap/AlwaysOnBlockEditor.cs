namespace TheKameleon.Superpowers.Skills.Bootstrap;

public enum BlockEditStatus
{
    Changed,
    Unchanged,
    MalformedMarkers,
}

public sealed record BlockEditResult(BlockEditStatus Status, string Text);

/// <summary>Pure text edits of the one managed block; everything outside the markers is preserved.</summary>
public static class AlwaysOnBlockEditor
{
    public const string BeginMarker = "<!-- superpowers:begin (managed by Superpowers for Visual Studio; edit outside this block) -->";
    public const string EndMarker = "<!-- superpowers:end -->";
    private const string BeginPrefix = "<!-- superpowers:begin";

    public static bool IsPresent(string text) => TryFind(text, out var start, out _) && start >= 0;

    public static BlockEditResult Apply(string text, string body, string newline)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (!TryFind(text, out var start, out var end))
        {
            return new BlockEditResult(BlockEditStatus.MalformedMarkers, text);
        }

        var block = BeginMarker + newline + body.ReplaceLineEndings(newline).TrimEnd() + newline + EndMarker;
        string result;
        if (start < 0)
        {
            var separator = text.Length == 0 || text.EndsWith('\n') ? string.Empty : newline;
            result = text + separator + block + newline;
        }
        else
        {
            result = text[..start] + block + text[end..];
        }

        return string.Equals(result, text, StringComparison.Ordinal)
            ? new BlockEditResult(BlockEditStatus.Unchanged, text)
            : new BlockEditResult(BlockEditStatus.Changed, result);
    }

    public static BlockEditResult Remove(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (!TryFind(text, out var start, out var end))
        {
            return new BlockEditResult(BlockEditStatus.MalformedMarkers, text);
        }

        if (start < 0)
        {
            return new BlockEditResult(BlockEditStatus.Unchanged, text);
        }

        var after = end;
        if (after < text.Length && text[after] == '\r')
        {
            after++;
        }

        if (after < text.Length && text[after] == '\n')
        {
            after++;
        }

        return new BlockEditResult(BlockEditStatus.Changed, text[..start] + text[after..]);
    }

    private static bool TryFind(string text, out int start, out int end)
    {
        start = -1;
        end = -1;
        var beginCount = CountOccurrences(text, BeginPrefix);
        var endCount = CountOccurrences(text, EndMarker);
        if (beginCount == 0 && endCount == 0)
        {
            return true;
        }

        if (beginCount != 1 || endCount != 1)
        {
            return false;
        }

        start = text.IndexOf(BeginPrefix, StringComparison.Ordinal);
        var endIndex = text.IndexOf(EndMarker, StringComparison.Ordinal);
        if (endIndex < start)
        {
            start = -1;
            return false;
        }

        end = endIndex + EndMarker.Length;
        return true;
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        for (var index = text.IndexOf(value, StringComparison.Ordinal); index >= 0; index = text.IndexOf(value, index + value.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }
}
