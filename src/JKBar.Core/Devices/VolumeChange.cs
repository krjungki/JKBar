// Reads the drive bitmask Windows sends with volume arrival and removal broadcasts.
using JKBar.Core.Alerts;

namespace JKBar.Core.Devices;

public static class VolumeChange
{
    public static IReadOnlyList<char> Letters(uint unitMask)
    {
        var letters = new List<char>();
        for (var bit = 0; bit < 26; bit++)
        {
            if ((unitMask & (1u << bit)) != 0)
            {
                letters.Add((char)('A' + bit));
            }
        }

        return letters;
    }

    public static NotchAlert Arrived(char letter) =>
        new(AlertCategory.Device, $"device.volume.arrived.{letter}", $"{letter}: 드라이브 연결됨");

    public static NotchAlert Removed(char letter) =>
        new(AlertCategory.Device, $"device.volume.removed.{letter}", $"{letter}: 드라이브 제거됨");
}
