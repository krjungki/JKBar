using JKBar.Core.Alerts;
using JKBar.Core.Presentation;
using JKBar.Core.Sync;

namespace JKBar.Core.Tests;

public sealed class SyncStatusWatcherTests
{
    private static readonly Func<BandItemKind, bool> AllVisible = _ => true;

    [Fact]
    public void SaysNothingAboutTheFirstReading()
    {
        var watcher = new SyncStatusWatcher();

        Assert.Empty(watcher.Observe([Snapshot(SyncProviderCatalog.OneDrive, SyncState.Error)], AllVisible));
    }

    [Fact]
    public void SaysNothingWhileTheStateHoldsStill()
    {
        var watcher = new SyncStatusWatcher();
        watcher.Observe([Snapshot(SyncProviderCatalog.OneDrive, SyncState.UpToDate)], AllVisible);

        Assert.Empty(watcher.Observe([Snapshot(SyncProviderCatalog.OneDrive, SyncState.UpToDate)], AllVisible));
    }

    [Fact]
    public void AnnouncesTroubleAsAWarning()
    {
        var watcher = new SyncStatusWatcher();
        watcher.Observe([Snapshot(SyncProviderCatalog.OneDrive, SyncState.UpToDate)], AllVisible);

        var alert = Assert.Single(
            watcher.Observe([Snapshot(SyncProviderCatalog.OneDrive, SyncState.Error)], AllVisible));

        Assert.Equal(AlertCategory.Sync, alert.Category);
        Assert.Equal("sync.onedrive.attention", alert.Key);
        Assert.Equal("OneDrive 주의", alert.Title);
        Assert.Equal("오류가 보고되었습니다", alert.Detail);
        Assert.Equal(AlertSeverity.Warning, alert.Severity);
    }

    [Fact]
    public void AnnouncesRecoveryAsDone()
    {
        var watcher = new SyncStatusWatcher();
        watcher.Observe([Snapshot(SyncProviderCatalog.GlobalSecureAccess, SyncState.Error)], AllVisible);

        var alert = Assert.Single(
            watcher.Observe([Snapshot(SyncProviderCatalog.GlobalSecureAccess, SyncState.UpToDate)], AllVisible));

        Assert.Equal("sync.gsa.good", alert.Key);
        Assert.Equal("Global Secure Access 정상", alert.Title);
        Assert.Equal(AlertSeverity.Done, alert.Severity);
    }

    [Fact]
    public void TreatsEveryStateThatIsNotUpToDateAsOneBadge()
    {
        var watcher = new SyncStatusWatcher();
        watcher.Observe([Snapshot(SyncProviderCatalog.Syncthing, SyncState.Error)], AllVisible);

        // Both are the red badge, so moving between them is not a change the user can see.
        Assert.Empty(watcher.Observe([Snapshot(SyncProviderCatalog.Syncthing, SyncState.Synchronizing)], AllVisible));
    }

    [Fact]
    public void StaysQuietForAProviderTheUserUnchecked()
    {
        var watcher = new SyncStatusWatcher();
        Func<BandItemKind, bool> withoutOneDrive = kind => kind != BandItemKind.OneDrive;

        watcher.Observe([Snapshot(SyncProviderCatalog.OneDrive, SyncState.UpToDate)], withoutOneDrive);

        Assert.Empty(watcher.Observe([Snapshot(SyncProviderCatalog.OneDrive, SyncState.Error)], withoutOneDrive));
    }

    [Fact]
    public void StartsOverWhenAHiddenProviderIsShownAgain()
    {
        var watcher = new SyncStatusWatcher();
        watcher.Observe([Snapshot(SyncProviderCatalog.OneDrive, SyncState.UpToDate)], AllVisible);
        watcher.Observe([Snapshot(SyncProviderCatalog.OneDrive, SyncState.Error)], kind => kind != BandItemKind.OneDrive);

        // Whatever happened out of sight is not worth interrupting for once it is back.
        Assert.Empty(watcher.Observe([Snapshot(SyncProviderCatalog.OneDrive, SyncState.Error)], AllVisible));
    }

    [Fact]
    public void StartsOverWhenAProviderStopsAndRunsAgain()
    {
        var watcher = new SyncStatusWatcher();
        watcher.Observe([Snapshot(SyncProviderCatalog.Syncthing, SyncState.Error)], AllVisible);
        watcher.Observe([Snapshot(SyncProviderCatalog.Syncthing, SyncState.Absent)], AllVisible);

        Assert.Empty(watcher.Observe([Snapshot(SyncProviderCatalog.Syncthing, SyncState.UpToDate)], AllVisible));
    }

    [Fact]
    public void FollowsEachProviderSeparately()
    {
        var watcher = new SyncStatusWatcher();
        watcher.Observe(
            [
                Snapshot(SyncProviderCatalog.OneDrive, SyncState.UpToDate),
                Snapshot(SyncProviderCatalog.Syncthing, SyncState.UpToDate)
            ],
            AllVisible);

        var alerts = watcher.Observe(
            [
                Snapshot(SyncProviderCatalog.OneDrive, SyncState.Error),
                Snapshot(SyncProviderCatalog.Syncthing, SyncState.UpToDate)
            ],
            AllVisible);

        Assert.Equal("sync.onedrive.attention", Assert.Single(alerts).Key);
    }

    [Fact]
    public void WaitsBeforeSayingTheSameThingAgain()
    {
        var watcher = new SyncStatusWatcher();
        watcher.Observe([Snapshot(SyncProviderCatalog.OneDrive, SyncState.UpToDate)], AllVisible);
        var alert = Assert.Single(
            watcher.Observe([Snapshot(SyncProviderCatalog.OneDrive, SyncState.Error)], AllVisible));

        Assert.Equal(SyncStatusWatcher.Cooldown, alert.Cooldown);
    }

    [Fact]
    public void PointsBackAtTheProviderSoTheNotchCanShowItsIcon()
    {
        var watcher = new SyncStatusWatcher();
        watcher.Observe([Snapshot(SyncProviderCatalog.Syncthing, SyncState.UpToDate)], AllVisible);
        var alert = Assert.Single(
            watcher.Observe([Snapshot(SyncProviderCatalog.Syncthing, SyncState.Error)], AllVisible));

        Assert.Equal(SyncProviderCatalog.Syncthing, SyncStatusWatcher.ProviderIdOf(alert));
    }

    [Fact]
    public void PointsNowhereForAnAlertThatIsNotAboutSyncing()
    {
        var alert = new NotchAlert(AlertCategory.Power, "power.ac.online", "전원 연결됨");

        Assert.Null(SyncStatusWatcher.ProviderIdOf(alert));
    }

    private static SyncProviderSnapshot Snapshot(string providerId, SyncState state) =>
        new(providerId, providerId[0], state, "테스트");
}
