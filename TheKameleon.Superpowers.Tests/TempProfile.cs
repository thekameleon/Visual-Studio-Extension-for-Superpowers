using TheKameleon.Superpowers.Skills.Install;

namespace TheKameleon.Superpowers.Tests;

internal sealed class TempProfile : IDisposable
{
    public TempProfile()
    {
        Root = Path.Combine(Path.GetTempPath(), "SuperpowersTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
        Paths = new ProfilePaths(Path.Combine(Root, "profile"), Path.Combine(Root, "localappdata"));
    }

    public string Root { get; }

    public ProfilePaths Paths { get; }

    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }
}
