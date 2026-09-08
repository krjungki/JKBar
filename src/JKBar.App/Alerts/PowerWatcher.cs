// Reports mains power changes; battery charge itself is left to Windows.
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using JKBar.Core.Alerts;

namespace JKBar.App.Alerts;

[SupportedOSPlatform("windows")]
internal sealed class PowerWatcher
{
    private const byte Offline = 0;
    private const byte Online = 1;

    private byte? _lastLine;

    internal NotchAlert? Observe()
    {
        if (!GetSystemPowerStatus(out var status))
        {
            return null;
        }

        var line = status.AcLineStatus;
        if (line is not (Offline or Online))
        {
            return null;
        }

        var previous = _lastLine;
        _lastLine = line;
        if (previous is null || previous == line)
        {
            return null;
        }

        return line == Online
            ? new NotchAlert(AlertCategory.Power, "power.ac.online", "전원 연결됨")
            : new NotchAlert(AlertCategory.Power, "power.ac.offline", "배터리로 전환됨");
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemPowerStatus
    {
        internal byte AcLineStatus;
        internal byte BatteryFlag;
        internal byte BatteryLifePercent;
        internal byte SystemStatusFlag;
        internal uint BatteryLifeTime;
        internal uint BatteryFullLifeTime;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemPowerStatus(out SystemPowerStatus status);
}
