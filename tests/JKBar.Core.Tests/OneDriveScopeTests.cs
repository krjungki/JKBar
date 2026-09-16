// Verifies OneDrive root ownership without reading or changing the Windows registry.
using System.Diagnostics;
using JKBar.Core.Presentation;
using JKBar.Core.Sync;

namespace JKBar.Core.Tests;

public class OneDriveScopeTests
{
    private const string CurrentSid = "S-1-12-1-111-222-333-444";
    private const string OtherSid = "S-1-5-21-111-222-333-1001";

    [Fact]
    public void ActivityProbeAcceptsOnlyItsOwnSession()
    {
        using var process = Process.GetCurrentProcess();
        Assert.True(new OneDriveActivityProbe().IsCurrentSession(process));
        Assert.False(new OneDriveActivityProbe(int.MaxValue).IsCurrentSession(process));
    }

    [Fact]
    public void UnavailableProcessIsNotAssumedToBelongToTheSession()
    {
        using var process = new Process();
        Assert.False(new OneDriveActivityProbe().IsCurrentSession(process));
    }

    [Fact]
    public void SessionWithoutOneDriveHasNoActivityAndIsAbsent()
    {
        var activity = new OneDriveActivityProbe(int.MaxValue);
        Assert.False(activity.IsRunning());
        Assert.Equal(0, activity.TotalTransferBytes());
        Assert.Equal(SyncState.Absent, new OneDriveSyncProvider(activity).GetSnapshot().State);
    }

    [Theory]
    [InlineData(CloudProviderStatus.Disconnected)]
    [InlineData(CloudProviderStatus.Error)]
    public void ForeignRootCannotCauseAttention(CloudProviderStatus foreignStatus)
    {
        var providers = new[]
        {
            $"OneDrive!{CurrentSid}!Business1|library-a",
            $"OneDrive!{CurrentSid}!Business1|library-b",
            $"OneDrive!{CurrentSid}!Personal|account",
            $"OneDrive!{OtherSid}!Personal|account"
        };
        var statusByRoot = providers.ToDictionary(provider => provider,
            provider => provider.Contains(OtherSid) ? foreignStatus : CloudProviderStatus.Idle);

        var roots = OneDriveSyncProvider.EnumerateSyncRoots(CurrentSid, providers, provider =>
        {
            Assert.DoesNotContain(OtherSid, provider);
            return provider;
        }, _ => true);

        Assert.Equal(providers.Take(3), roots);
        Assert.NotEqual(SyncState.UpToDate,
            OneDriveStatusMapper.Aggregate(statusByRoot.Values.Select(OneDriveStatusMapper.ToSyncState).ToArray()));
        var state = OneDriveStatusMapper.Aggregate(
            roots.Select(root => OneDriveStatusMapper.ToSyncState(statusByRoot[root])).ToArray());
        Assert.Equal(SyncState.UpToDate, state);
        Assert.Equal(BandItemBadge.Good, SyncStatusSource.Item(new("onedrive", 'O', state, ""))?.Badge);
    }

    [Theory]
    [InlineData(CloudProviderStatus.Disconnected, SyncState.Unknown)]
    [InlineData(CloudProviderStatus.Error, SyncState.Error)]
    public void CurrentUserFaultStillCausesAttention(CloudProviderStatus status, SyncState expected)
    {
        var roots = OneDriveSyncProvider.EnumerateSyncRoots(CurrentSid,
            [$"OneDrive!{CurrentSid}!Personal|account"], _ => "root", _ => true);

        var state = OneDriveStatusMapper.Aggregate(roots.Select(_ => OneDriveStatusMapper.ToSyncState(status)).ToArray());
        Assert.Equal(expected, state);
        Assert.Equal(BandItemBadge.Attention, SyncStatusSource.Item(new("onedrive", 'O', state, ""))?.Badge);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void MissingIdentityNeverFallsBackToAllUsers(string? currentSid)
    {
        Assert.Empty(OneDriveSyncProvider.EnumerateSyncRoots(currentSid,
            [$"OneDrive!{OtherSid}!Personal|account"],
            _ => throw new InvalidOperationException("Must not read other users"), _ => true));
    }

    [Fact]
    public void ForeignAndSimilarProviderNamesAreNotRead()
    {
        Assert.Empty(OneDriveSyncProvider.EnumerateSyncRoots(CurrentSid,
        [
            $"OneDrive!{OtherSid}!Personal|account",
            $"OneDrive!{CurrentSid}0!Personal|account",
            $"OneDriveOther!{CurrentSid}!Personal|account",
            "OneDrive",
            $"Other!{CurrentSid}!Personal|account"
        ], _ => throw new InvalidOperationException("Must not read unrelated providers"), _ => true));
    }

    [Fact]
    public void MissingNonStringBlankAndDeletedRootsAreExcluded()
    {
        object?[] values = [null, 42, "", " ", "deleted"];
        var providers = values.Select((_, index) => $"OneDrive!{CurrentSid}!Business{index}").ToArray();
        Assert.Empty(OneDriveSyncProvider.EnumerateSyncRoots(CurrentSid, providers,
            provider => values[Array.IndexOf(providers, provider)], path =>
            {
                Assert.Equal("deleted", path);
                return false;
            }));
    }

    [Fact]
    public void ProviderMatchingAndPathDeduplicationAreCaseInsensitive()
    {
        var roots = OneDriveSyncProvider.EnumerateSyncRoots(CurrentSid,
            [$"onedrive!{CurrentSid.ToLowerInvariant()}!Personal", $"OneDrive!{CurrentSid}!Business1"],
            provider => provider.StartsWith("onedrive", StringComparison.Ordinal) ? "ROOT" : "root", _ => true);

        Assert.Equal(["ROOT"], roots);
    }
}