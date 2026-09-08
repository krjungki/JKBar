// Persists JKBar settings atomically, beside the executable when that folder is writable.
using System.Text;
using System.Text.Json;
using IoPath = System.IO.Path;

namespace JKBar.Core.Settings;

public sealed class SettingsStore(string path)
{
    private const string FileName = "settings.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public string Path { get; } = path;

    /// <summary>Where the settings file lives, which is also where JKBar keeps its copy of the band image.</summary>
    public string Folder => IoPath.GetDirectoryName(Path) is { Length: > 0 } folder ? folder : ExecutableDirectory();

    /// <summary>Beside the executable, so a copied folder carries its settings with it.</summary>
    public static string PortablePath => IoPath.Combine(ExecutableDirectory(), FileName);

    /// <summary>The fallback for an executable installed somewhere the user cannot write.</summary>
    public static string ProfilePath => IoPath.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "JKBar",
        FileName);

    /// <summary>Picks the file to use and brings an earlier profile copy along the first time.</summary>
    public static SettingsStore ForApp()
    {
        if (!CanWriteTo(ExecutableDirectory()))
        {
            return new SettingsStore(ProfilePath);
        }

        var portable = PortablePath;
        Adopt(ProfilePath, portable);
        return new SettingsStore(portable);
    }

    public static bool CanWriteTo(string directory)
    {
        var probe = IoPath.Combine(directory, $".jkbar-write-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllBytes(probe, []);
            File.Delete(probe);
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return false;
        }
    }

    /// <summary>Only ever fills an empty destination, so a portable file is never overwritten by an older copy.</summary>
    public static bool Adopt(string source, string destination)
    {
        if (File.Exists(destination) || !File.Exists(source))
        {
            return false;
        }

        try
        {
            var directory = IoPath.GetDirectoryName(destination);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.Copy(source, destination);
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public JkBarSettings Load()
    {
        try
        {
            return (JsonSerializer.Deserialize<JkBarSettings>(File.ReadAllText(Path)) ?? new JkBarSettings()).Normalized();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return new JkBarSettings();
        }
    }

    public void Save(JkBarSettings settings)
    {
        var directory = IoPath.GetDirectoryName(Path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporary = Path + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(settings.Normalized(), JsonOptions), new UTF8Encoding(false));
            File.Move(temporary, Path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    private static string ExecutableDirectory() =>
        IoPath.GetDirectoryName(Environment.ProcessPath) is { Length: > 0 } directory
            ? directory
            : AppContext.BaseDirectory;
}