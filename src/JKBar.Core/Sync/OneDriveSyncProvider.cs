// Ported from JKMon (packages/JKMon/src/JKMon.Core/Sync). Keep behaviour changes in sync with the original.
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Principal;
using JKBar.Core.Interop;
using Microsoft.Win32;

namespace JKBar.Core.Sync;

/// <summary>
/// Reads OneDrive sync state through the Cloud Filter API. Registered sync roots are enumerated at every poll
/// because the count changes when accounts are added, removed or signed out.
/// OneDrive never advances its Cloud Filter provider status beyond IDLE and refuses to hand a status UI source to
/// non-shell callers, so an in-progress sync is inferred from OneDrive's transfer activity instead.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class OneDriveSyncProvider : ISyncProvider
{
    private const string SyncRootManagerKey =
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\SyncRootManager";

    // Offset of ProviderStatus inside CF_SYNC_ROOT_STANDARD_INFO.
    private const int ProviderStatusOffset = 24;
    private const int ProviderNameOffset = 28;
    private const int BufferSize = 8192;

    // Measured on an idle client: ~60 samples sat at 0 KiB/s with an 8.4 KiB/s worst case, so the noise floor is far
    // lower than the 64 KiB/s this once used. That old threshold reported "settled" for any sync slower than itself.
    private static readonly long ActivityThresholdBytesPerSecond = 16 * 1024;

    // A slow sync moves file by file, so the gaps between bursts are longer than a fast hydration's.
    private static readonly TimeSpan ActivityHold = TimeSpan.FromSeconds(15);

    // Idle OneDrive produces occasional short bursts of its own file I/O; one peaked at 988 KiB/s across two
    // samples with nothing syncing. Demanding three consecutive samples rejects those without losing a real
    // transfer, which stays up for far longer than the nine seconds this costs.
    private const int ActivitySamplesToAssert = 3;

    private readonly OneDriveActivityProbe _activity;
    private readonly ActivityGate _gate;
    private readonly TimeProvider _time;
    private readonly Action<string>? _log;
    private string? _lastLogged;

    public OneDriveSyncProvider(
        OneDriveActivityProbe? activity = null, TimeProvider? timeProvider = null, Action<string>? log = null)
    {
        _activity = activity ?? new OneDriveActivityProbe();
        _time = timeProvider ?? TimeProvider.System;
        _log = log;
        _gate = new ActivityGate(ActivityThresholdBytesPerSecond, ActivityHold, ActivitySamplesToAssert);
    }

    /// <summary>Polling repeats every few seconds, so a persistent failure is logged once rather than forever.</summary>
    private void LogOnce(string message)
    {
        if (_lastLogged == message)
        {
            return;
        }

        _lastLogged = message;
        _log?.Invoke(message);
    }

    public string ProviderId => "onedrive";

    public char Initial => 'O';

    public Task<SyncProviderSnapshot> GetSnapshotAsync(CancellationToken cancellationToken) =>
        Task.FromResult(GetSnapshot());

    public SyncProviderSnapshot GetSnapshot()
    {
        if (!_activity.IsRunning())
        {
            return SyncProviderSnapshot.Absent(ProviderId, Initial);
        }

        List<string> roots;
        try
        {
            roots = EnumerateSyncRoots();
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            LogOnce($"onedrive: sync roots unreadable: {ex.GetType().Name}: {ex.Message}");
            return new SyncProviderSnapshot(ProviderId, Initial, SyncState.Unknown, "동기화 폴더 목록을 읽을 수 없습니다");
        }

        if (roots.Count == 0)
        {
            return SyncProviderSnapshot.Absent(ProviderId, Initial);
        }

        var states = new List<SyncState>(roots.Count);
        var rootLines = new List<string>(roots.Count);
        string? unreadable = null;
        foreach (var root in roots)
        {
            var status = TryReadProviderStatus(root, out var failure);
            if (status is null)
            {
                unreadable ??= failure;
                rootLines.Add($"{RootName(root)}: 상태를 읽을 수 없음");
                continue;
            }

            var rootState = OneDriveStatusMapper.ToSyncState(status.Value);
            states.Add(rootState);
            rootLines.Add($"{RootName(root)}: {RootStateName(rootState)}");
        }

        var transferring = _gate.Update(_activity.TotalTransferBytes(), _time.GetUtcNow());

        // Cloud Filter reports Idle even mid-sync, so a status this build cannot read costs only the error states.
        // Reporting grey forever would be worse than judging by transfer activity, which is the signal that works.
        if (states.Count == 0)
        {
            LogOnce($"onedrive: provider status unreadable for all {roots.Count} sync root(s), " +
                    $"falling back to transfer activity. reason: {unreadable}");

            return transferring
                ? new SyncProviderSnapshot(ProviderId, Initial, SyncState.Synchronizing,
                    $"상태를 읽지 못해 전송량으로 판단: 전송 중 ({ByteRate(_gate.LastRateBytesPerSecond)})")
                : new SyncProviderSnapshot(ProviderId, Initial, SyncState.UpToDate,
                    "상태를 읽지 못해 전송량으로 판단: 전송 없음");
        }

        if (unreadable is not null)
        {
            LogOnce($"onedrive: provider status unreadable for some sync roots. reason: {unreadable}");
        }

        var aggregate = OneDriveStatusMapper.Aggregate(states);

        // Errors and disconnections outrank activity; otherwise activity is what distinguishes syncing from settled.
        if (aggregate is SyncState.UpToDate && transferring)
        {
            var rate = ByteRate(_gate.LastRateBytesPerSecond);
            return new SyncProviderSnapshot(
                ProviderId, Initial, SyncState.Synchronizing, string.Join('\n', [$"전송 중 ({rate})", .. rootLines]));
        }

        return new SyncProviderSnapshot(ProviderId, Initial, aggregate, string.Join('\n', rootLines));
    }

    private static string RootName(string root) =>
        Path.GetFileName(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) is { Length: > 0 } name
            ? name
            : root;

    private static string RootStateName(SyncState state) => state switch
    {
        SyncState.UpToDate => "최신",
        SyncState.Synchronizing => "동기화 중",
        SyncState.Error => "오류 또는 연결 끊김",
        _ => "상태를 알 수 없음"
    };

    private static string ByteRate(double bytesPerSecond) =>
        bytesPerSecond >= 1024 * 1024
            ? $"{bytesPerSecond / (1024 * 1024):0.#} MiB/s"
            : $"{bytesPerSecond / 1024:0} KiB/s";

    private static List<string> EnumerateSyncRoots()
    {
        using var identity = WindowsIdentity.GetCurrent();
        using var manager = Registry.LocalMachine.OpenSubKey(SyncRootManagerKey);
        if (manager is null)
        {
            return [];
        }

        return EnumerateSyncRoots(identity.User?.Value, manager.GetSubKeyNames(), name =>
        {
            using var userRoots = manager.OpenSubKey($@"{name}\UserSyncRoots");
            return userRoots?.GetValue(identity.User!.Value);
        }, Directory.Exists);
    }

    internal static List<string> EnumerateSyncRoots(
        string? currentSid, IEnumerable<string> providerNames,
        Func<string, object?> readCurrentUserRoot, Func<string, bool> pathExists)
    {
        var results = new List<string>();
        if (string.IsNullOrWhiteSpace(currentSid))
        {
            return results;
        }

        var prefix = $"OneDrive!{currentSid}!";
        foreach (var name in providerNames)
        {
            if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (readCurrentUserRoot(name) is string path &&
                !string.IsNullOrWhiteSpace(path) && pathExists(path) &&
                !results.Contains(path, StringComparer.OrdinalIgnoreCase))
            {
                results.Add(path);
            }
        }

        return results;
    }

    private static CloudProviderStatus? TryReadProviderStatus(string path, out string failure)
    {
        failure = string.Empty;
        var buffer = Marshal.AllocHGlobal(BufferSize);
        try
        {
            var hr = NativeMethods.CfGetSyncRootInfoByPath(
                path, NativeMethods.CfSyncRootInfoStandard, buffer, BufferSize, out var returned);

            if (hr != 0)
            {
                failure = $"CfGetSyncRootInfoByPath returned 0x{hr:X8}";
                return null;
            }

            if (returned < ProviderNameOffset)
            {
                failure = $"CfGetSyncRootInfoByPath returned only {returned} bytes";
                return null;
            }

            var raw = (uint)Marshal.ReadInt32(buffer, ProviderStatusOffset);
            return (CloudProviderStatus)raw;
        }
        catch (DllNotFoundException ex)
        {
            // A likely path on Windows on ARM, where this x64 build runs emulated.
            failure = $"CldApi.dll could not be loaded: {ex.Message}";
            return null;
        }
        catch (EntryPointNotFoundException ex)
        {
            failure = $"CfGetSyncRootInfoByPath is missing: {ex.Message}";
            return null;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }
}
