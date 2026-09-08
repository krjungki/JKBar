// Ported from JKMon (packages/JKMon/src/JKMon.Core/Update/ReleaseChecksums.cs). Keep behaviour changes in sync.
using System.Security.Cryptography;

namespace JKBar.Core.Update;

public static class ReleaseChecksums
{
    private const int HashLength = 64;

    /// <summary>
    /// Lines look like `&lt;hash&gt;  &lt;name&gt;`. Prose lines in the same file are ignored, so the published notes and the
    /// checksums can share one document.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Parse(string content)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(content))
        {
            return result;
        }

        foreach (var raw in content.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length <= HashLength || line[HashLength] != ' ')
            {
                continue;
            }

            var hash = line[..HashLength];
            if (!IsHex(hash))
            {
                continue;
            }

            var name = line[(HashLength + 1)..].TrimStart('*', ' ').Trim();
            if (name.Length > 0 && !result.ContainsKey(name))
            {
                result[name] = hash.ToUpperInvariant();
            }
        }

        return result;
    }

    public static string HashOf(Stream stream) => Convert.ToHexString(SHA256.HashData(stream));

    public static bool Matches(string? expected, string? actual) =>
        expected is { Length: HashLength }
        && actual is { Length: HashLength }
        && string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase);

    private static bool IsHex(string text)
    {
        foreach (var character in text)
        {
            if (!Uri.IsHexDigit(character))
            {
                return false;
            }
        }

        return true;
    }
}
