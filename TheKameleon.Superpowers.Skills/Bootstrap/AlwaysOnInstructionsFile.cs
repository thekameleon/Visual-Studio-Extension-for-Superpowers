using System.Text;
using TheKameleon.Superpowers.Skills.Install;

namespace TheKameleon.Superpowers.Skills.Bootstrap;

public enum AlwaysOnStatus
{
    Enabled,
    Disabled,
    Unchanged,
    MalformedMarkers,
}

public sealed record AlwaysOnOutcome(AlwaysOnStatus Status, AlwaysOnState State);

/// <summary>Applies <see cref="AlwaysOnBlockEditor"/> to %USERPROFILE%\copilot-instructions.md, preserving its encoding.</summary>
public sealed class AlwaysOnInstructionsFile(ProfilePaths paths)
{
    public bool IsBlockPresent()
    {
        return File.Exists(paths.UserInstructionsFile) && AlwaysOnBlockEditor.IsPresent(Read().Text);
    }

    public AlwaysOnOutcome Enable(AlwaysOnState current)
    {
        var existed = File.Exists(paths.UserInstructionsFile);
        var (text, encoding) = existed ? Read() : (string.Empty, new UTF8Encoding(false));
        var newline = text.Contains("\r\n", StringComparison.Ordinal) || !existed ? "\r\n" : "\n";

        var result = AlwaysOnBlockEditor.Apply(text, BootstrapText.Body, newline);
        if (result.Status == BlockEditStatus.MalformedMarkers)
        {
            return new AlwaysOnOutcome(AlwaysOnStatus.MalformedMarkers, current);
        }

        var state = new AlwaysOnState(true, current.CreatedFile || !existed);
        if (result.Status == BlockEditStatus.Unchanged)
        {
            return new AlwaysOnOutcome(AlwaysOnStatus.Unchanged, state);
        }

        Directory.CreateDirectory(paths.UserProfile);
        Write(result.Text, encoding);
        return new AlwaysOnOutcome(AlwaysOnStatus.Enabled, state);
    }

    public AlwaysOnOutcome Disable(AlwaysOnState current)
    {
        var disabled = new AlwaysOnState(false, false);
        if (!File.Exists(paths.UserInstructionsFile))
        {
            return new AlwaysOnOutcome(AlwaysOnStatus.Unchanged, disabled);
        }

        var (text, encoding) = Read();
        var result = AlwaysOnBlockEditor.Remove(text);
        if (result.Status == BlockEditStatus.MalformedMarkers)
        {
            return new AlwaysOnOutcome(AlwaysOnStatus.MalformedMarkers, current);
        }

        if (result.Status == BlockEditStatus.Unchanged)
        {
            return new AlwaysOnOutcome(AlwaysOnStatus.Unchanged, disabled);
        }

        if (current.CreatedFile && string.IsNullOrWhiteSpace(result.Text))
        {
            File.Delete(paths.UserInstructionsFile);
        }
        else
        {
            Write(result.Text, encoding);
        }

        return new AlwaysOnOutcome(AlwaysOnStatus.Disabled, disabled);
    }

    private (string Text, Encoding Encoding) Read()
    {
        var bytes = File.ReadAllBytes(paths.UserInstructionsFile);
        Encoding encoding = bytes switch
        {
            [0xEF, 0xBB, 0xBF, ..] => new UTF8Encoding(true),
            [0xFF, 0xFE, ..] => new UnicodeEncoding(bigEndian: false, byteOrderMark: true),
            [0xFE, 0xFF, ..] => new UnicodeEncoding(bigEndian: true, byteOrderMark: true),
            _ => new UTF8Encoding(false),
        };
        var preamble = encoding.GetPreamble().Length;
        return (encoding.GetString(bytes, preamble, bytes.Length - preamble), encoding);
    }

    private void Write(string text, Encoding encoding)
    {
        var temporary = paths.UserInstructionsFile + "." + Guid.NewGuid().ToString("N") + ".tmp";
        File.WriteAllBytes(temporary, encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray());
        File.Move(temporary, paths.UserInstructionsFile, overwrite: true);
    }
}
