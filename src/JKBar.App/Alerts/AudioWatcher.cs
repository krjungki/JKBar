// Watches the default output endpoint so volume, mute and device switches reach the notch.
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using JKBar.Core.Alerts;

namespace JKBar.App.Alerts;

[SupportedOSPlatform("windows")]
internal sealed class AudioWatcher : IDisposable
{
    // Volume and mute are dragged and toggled in bursts, so they replace each other instead of waiting out a cooldown.
    private static readonly TimeSpan NoCooldown = TimeSpan.Zero;

    private IMMDeviceEnumerator? _enumerator;
    private IAudioEndpointVolume? _volume;
    private string? _deviceId;
    private int? _lastPercent;
    private bool? _lastMuted;
    private bool _unavailable;

    internal IReadOnlyList<NotchAlert> Observe()
    {
        if (_unavailable)
        {
            return [];
        }

        try
        {
            return Read();
        }
        catch (COMException)
        {
            _unavailable = true;
            return [];
        }
        catch (InvalidCastException)
        {
            _unavailable = true;
            return [];
        }
    }

    public void Dispose()
    {
        ReleaseVolume();
        Release(ref _enumerator);
    }

    private List<NotchAlert> Read()
    {
        var enumerator = _enumerator ??= CreateEnumerator();
        if (enumerator.GetDefaultAudioEndpoint(CoreAudio.RenderFlow, CoreAudio.ConsoleRole, out var device) != 0)
        {
            Forget();
            return [];
        }

        try
        {
            return ReadDevice(device);
        }
        finally
        {
            Marshal.ReleaseComObject(device);
        }
    }

    private List<NotchAlert> ReadDevice(IMMDevice device)
    {
        var alerts = new List<NotchAlert>();
        if (device.GetId(out var id) != 0)
        {
            return alerts;
        }

        if (id != _deviceId)
        {
            var known = _deviceId is not null;
            ReleaseVolume();
            _deviceId = id;
            _lastPercent = null;
            _lastMuted = null;
            _volume = ActivateVolume(device);

            if (known && FriendlyName(device) is { Length: > 0 } name)
            {
                alerts.Add(new NotchAlert(AlertCategory.Audio, "audio.device", name, "출력 장치 변경됨"));
            }
        }

        if (_volume is null)
        {
            return alerts;
        }

        if (_volume.GetMute(out var muted) == 0)
        {
            var previous = _lastMuted;
            _lastMuted = muted;
            if (previous is not null && previous != muted)
            {
                alerts.Add(muted
                    ? new NotchAlert(AlertCategory.Audio, "audio.mute.on", "음소거", Cooldown: NoCooldown)
                    : new NotchAlert(AlertCategory.Audio, "audio.mute.off", "음소거 해제", Cooldown: NoCooldown));
            }
        }

        if (_volume.GetMasterVolumeLevelScalar(out var level) == 0)
        {
            var percent = (int)Math.Round(Math.Clamp(level, 0f, 1f) * 100f);
            var previous = _lastPercent;
            _lastPercent = percent;
            if (previous is not null && previous != percent && _lastMuted != true)
            {
                alerts.Add(new NotchAlert(AlertCategory.Audio, "audio.volume", $"음량 {percent}%", Cooldown: NoCooldown));
            }
        }

        return alerts;
    }

    private static IMMDeviceEnumerator CreateEnumerator()
    {
        var type = Type.GetTypeFromCLSID(CoreAudio.DeviceEnumeratorClass)
            ?? throw new COMException("오디오 장치 열거자를 찾을 수 없습니다.");
        return (IMMDeviceEnumerator)(Activator.CreateInstance(type)
            ?? throw new COMException("오디오 장치 열거자를 만들 수 없습니다."));
    }

    private static IAudioEndpointVolume? ActivateVolume(IMMDevice device)
    {
        var id = typeof(IAudioEndpointVolume).GUID;
        return device.Activate(ref id, CoreAudio.AllContexts, IntPtr.Zero, out var instance) == 0
            ? instance as IAudioEndpointVolume
            : null;
    }

    private static string? FriendlyName(IMMDevice device)
    {
        if (device.OpenPropertyStore(CoreAudio.ReadOnlyStore, out var store) != 0)
        {
            return null;
        }

        try
        {
            var key = CoreAudio.FriendlyNameKey;
            if (store.GetValue(ref key, out var value) != 0)
            {
                return null;
            }

            try
            {
                return value.ValueType == CoreAudio.WideStringValue ? Marshal.PtrToStringUni(value.Pointer) : null;
            }
            finally
            {
                CoreAudio.PropVariantClear(ref value);
            }
        }
        finally
        {
            Marshal.ReleaseComObject(store);
        }
    }

    private void Forget()
    {
        ReleaseVolume();
        _deviceId = null;
        _lastPercent = null;
        _lastMuted = null;
    }

    private void ReleaseVolume() => Release(ref _volume);

    private static void Release<T>(ref T? instance) where T : class
    {
        if (instance is null)
        {
            return;
        }

        Marshal.ReleaseComObject(instance);
        instance = null;
    }
}
