// Ported from JKMon (packages/JKMon/src/JKMon.Core/Update/StagingPaths.cs). Keep behaviour changes in sync.
using IoPath = System.IO.Path;

namespace JKBar.Core.Update;

/// <summary>
/// Where a download is unpacked before it replaces the installed copy. Named so the applier can recognise its own
/// working folder and refuse to delete anything else.
/// </summary>
public static class StagingPaths
{
    public const string Prefix = "JKBar.update.";
    public const string StagedFolderName = "staged";

    public static string RootFor(string temporaryFolder, ReleaseVersion version) =>
        IoPath.Combine(temporaryFolder, Prefix + version);

    /// <summary>True only for a folder this app made, so a mistyped argument cannot delete a user's directory.</summary>
    public static bool IsStagingRoot(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var name = IoPath.GetFileName(path.TrimEnd(IoPath.DirectorySeparatorChar, IoPath.AltDirectorySeparatorChar));

        return name.StartsWith(Prefix, StringComparison.Ordinal) && name.Length > Prefix.Length;
    }
}
