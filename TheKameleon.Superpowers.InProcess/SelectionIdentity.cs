using System;

namespace TheKameleon.Superpowers.InProcess;

internal sealed class SelectionIdentity
{
    public SelectionIdentity(string kind, string? name, string? path, string? projectName = null, string? projectPath = null)
    {
        Kind = kind;
        Name = name;
        Path = path;
        ProjectName = projectName;
        ProjectPath = projectPath;
    }

    public string Kind { get; }
    public string? Name { get; }
    public string? Path { get; }
    public string? ProjectName { get; }
    public string? ProjectPath { get; }

    public string Describe(string scope)
    {
        var expectedKind = scope == "Item" ? "File" : scope;
        if ((scope != "Solution" && scope != "Project" && scope != "Item") || Kind != expectedKind)
        {
            return "Unavailable: selected node does not match the invoked command scope.";
        }

        if (string.IsNullOrWhiteSpace(Name) || !IsAbsoluteFilePath(Path) ||
            (Kind == "File" && (string.IsNullOrWhiteSpace(ProjectName) || !IsAbsoluteFilePath(ProjectPath))))
        {
            return "Unavailable: selected node or owning project has no complete name/path identity.";
        }

        var result = $"Selected kind: {Kind}{Environment.NewLine}Name: {Name}{Environment.NewLine}Path: {Path}";
        if (Kind == "File")
        {
            result += $"{Environment.NewLine}Owning project: {ProjectName}{Environment.NewLine}Project path: {ProjectPath}";
        }

        return result + $"{Environment.NewLine}Read-only selection probe; no IPC performed.";
    }

    private static bool IsAbsoluteFilePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var drivePath = path!.Length >= 3 && char.IsLetter(path[0]) && path[1] == ':' &&
            (path[2] == '\\' || path[2] == '/');
        var uncPath = path.StartsWith(@"\\", StringComparison.Ordinal);
        return (drivePath || uncPath) && Uri.TryCreate(path, UriKind.Absolute, out var uri) && uri.IsFile;
    }
}
