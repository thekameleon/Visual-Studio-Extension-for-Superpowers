namespace TheKameleon.Superpowers.Skills.Context;

/// <summary>
/// Pure helper for inferring the <c>Kind</c> of a selected workspace path (file vs. folder),
/// extracted so this inference is unit testable without a running Visual Studio host.
/// </summary>
public static class SelectionKindResolver
{
    public const string FileKind = "File";
    public const string FolderKind = "Folder";

    /// <summary>
    /// Infers whether a selected path refers to a file or a folder. Falls back to
    /// <see cref="FileKind"/> when the path has a file extension, and <see cref="FolderKind"/>
    /// when it does not (e.g. a directory or extension-less path segment).
    /// </summary>
    public static string InferKind(string? localPath)
    {
        if (string.IsNullOrWhiteSpace(localPath))
        {
            return FolderKind;
        }

        return Path.HasExtension(localPath) ? FileKind : FolderKind;
    }
}
