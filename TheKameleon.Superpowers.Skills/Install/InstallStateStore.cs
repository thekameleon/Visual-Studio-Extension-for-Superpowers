using System.Text.Json;

namespace TheKameleon.Superpowers.Skills.Install;

public enum InstallStateStatus
{
    Missing,
    Loaded,
    Corrupt,
}

public sealed record InstallStateLoad(InstallStateStatus Status, InstallState State);

public sealed class InstallStateStore(ProfilePaths paths)
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public InstallStateLoad Load()
    {
        if (!File.Exists(paths.StateFile))
        {
            return new InstallStateLoad(InstallStateStatus.Missing, InstallState.Empty);
        }

        try
        {
            var state = JsonSerializer.Deserialize<InstallState>(File.ReadAllText(paths.StateFile), Options);
            if (state is null
                || state.SchemaVersion != InstallState.CurrentSchemaVersion
                || state.Skills is null
                || state.AlwaysOn is null
                || state.Skills.Any(skill => skill?.Name is null || skill.FileHashes is null))
            {
                return new InstallStateLoad(InstallStateStatus.Corrupt, InstallState.Empty);
            }

            return new InstallStateLoad(InstallStateStatus.Loaded, state);
        }
        catch (JsonException)
        {
            return new InstallStateLoad(InstallStateStatus.Corrupt, InstallState.Empty);
        }
    }

    public void Save(InstallState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        Directory.CreateDirectory(paths.StateDirectory);
        var temporary = paths.StateFile + "." + Guid.NewGuid().ToString("N") + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(state, Options));
        File.Move(temporary, paths.StateFile, overwrite: true);
    }

    public void Delete()
    {
        if (File.Exists(paths.StateFile))
        {
            File.Delete(paths.StateFile);
        }
    }
}
