using JKBar.Core.Alerts;
using JKBar.Core.Presentation;
using JKBar.Core.Settings;
using JKBar.Core.Sync;

namespace JKBar.Core.Tests;

public sealed class SyncStatusWatcherTests
{
    private static readonly Func<string, BandItemBadge, bool> AllEnabled = (_, _) => true;

    [Fact]
    public void SaysNothingAboutTheFirstReading()
    {
        var watcher = new SyncStatusWatcher();

        Assert.Empty(watcher.Observe([Snapshot(SyncProviderCatalog.OneDrive, SyncState.Error)], AllEnabled));
    }

    [Fact]
    public void SaysNothingWhileTheStateHoldsStill()
    {
        var watcher = new SyncStatusWatcher();
        watcher.Observe([Snapshot(SyncProviderCatalog.OneDrive, SyncState.UpToDate)], AllEnabled);

        Assert.Empty(watcher.Observe([Snapshot(SyncProviderCatalog.OneDrive, SyncState.UpToDate)], AllEnabled));
    }

    [Fact]
    public void AnnouncesTroubleAsAWarning()
    {
        var watcher = new SyncStatusWatcher();
        watcher.Observe([Snapshot(SyncProviderCatalog.OneDrive, SyncState.UpToDate)], AllEnabled);

        var alert = Assert.Single(
            watcher.Observe([Snapshot(SyncProviderCatalog.OneDrive, SyncState.Error)], AllEnabled));

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
        watcher.Observe([Snapshot(SyncProviderCatalog.GlobalSecureAccess, SyncState.Error)], AllEnabled);

        var alert = Assert.Single(
            watcher.Observe([Snapshot(SyncProviderCatalog.GlobalSecureAccess, SyncState.UpToDate)], AllEnabled));

        Assert.Equal("sync.gsa.good", alert.Key);
        Assert.Equal("Global Secure Access 정상", alert.Title);
        Assert.Equal(AlertSeverity.Done, alert.Severity);
    }

    [Fact]
    public void AnnouncesRecoveryAfterSynchronizationCompletes()
    {
        var watcher = new SyncStatusWatcher();
        watcher.Observe([Snapshot(SyncProviderCatalog.OneDrive, SyncState.Synchronizing)], AllEnabled);

        var alert = Assert.Single(
            watcher.Observe([Snapshot(SyncProviderCatalog.OneDrive, SyncState.UpToDate)], AllEnabled));

        Assert.Equal("sync.onedrive.good", alert.Key);
        Assert.Equal("OneDrive 정상", alert.Title);
        Assert.Equal("최신 상태입니다", alert.Detail);
        Assert.Equal(AlertSeverity.Done, alert.Severity);
    }

    [Fact]
    public void AnnouncesSynchronizationAsAChangeInsteadOfAWarning()
    {
        var watcher = new SyncStatusWatcher();
        watcher.Observe([Snapshot(SyncProviderCatalog.Syncthing, SyncState.UpToDate)], AllEnabled);

        var alert = Assert.Single(
            watcher.Observe([Snapshot(SyncProviderCatalog.Syncthing, SyncState.Synchronizing)], AllEnabled));

        Assert.Equal("sync.syncthing.synchronizing", alert.Key);
        Assert.Equal("Syncthing 동기화 중", alert.Title);
        Assert.Equal("동기화가 진행 중입니다", alert.Detail);
        Assert.Equal(AlertSeverity.Change, alert.Severity);
    }

    [Fact]
    public void StaysQuietForAMutedAttentionState()
    {
        var watcher = new SyncStatusWatcher();
        var settings = new SyncAlertSettings { MutedAttentionProviders = [SyncProviderCatalog.OneDrive] };

        watcher.Observe([Snapshot(SyncProviderCatalog.OneDrive, SyncState.UpToDate)], settings.Allows);

        Assert.Empty(watcher.Observe([Snapshot(SyncProviderCatalog.OneDrive, SyncState.Error)], settings.Allows));
        Assert.Empty(watcher.Observe([Snapshot(SyncProviderCatalog.OneDrive, SyncState.Synchronizing)], settings.Allows));
    }

    [Fact]
    public void AnnouncesAWarningWhenSynchronizationTurnsIntoAnError()
    {
        var watcher = new SyncStatusWatcher();
        watcher.Observe([Snapshot(SyncProviderCatalog.OneDrive, SyncState.Synchronizing)], AllEnabled);

        var alert = Assert.Single(
            watcher.Observe([Snapshot(SyncProviderCatalog.OneDrive, SyncState.Error)], AllEnabled));

        Assert.Equal("sync.onedrive.attention", alert.Key);
        Assert.Equal("OneDrive 주의", alert.Title);
        Assert.Equal("오류가 보고되었습니다", alert.Detail);
        Assert.Equal(AlertSeverity.Warning, alert.Severity);
    }

    [Fact]
    public void AnnouncesEnabledRecoveryAfterMutedAttention()
    {
        var watcher = new SyncStatusWatcher();
        Func<string, BandItemBadge, bool> goodOnly = (_, badge) => badge == BandItemBadge.Good;
        watcher.Observe([Snapshot(SyncProviderCatalog.OneDrive, SyncState.UpToDate)], goodOnly);
        Assert.Empty(watcher.Observe([Snapshot(SyncProviderCatalog.OneDrive, SyncState.Error)], goodOnly));

        var recovery = Assert.Single(
            watcher.Observe([Snapshot(SyncProviderCatalog.OneDrive, SyncState.UpToDate)], goodOnly));

        Assert.Equal("sync.onedrive.good", recovery.Key);
    }

    [Fact]
    public void AnnouncesEnabledAttentionAfterMutedNormalState()
    {
        var watcher = new SyncStatusWatcher();
        Func<string, BandItemBadge, bool> attentionOnly = (_, badge) => badge == BandItemBadge.Attention;
        watcher.Observe([Snapshot(SyncProviderCatalog.OneDrive, SyncState.Error)], attentionOnly);
        Assert.Empty(watcher.Observe([Snapshot(SyncProviderCatalog.OneDrive, SyncState.UpToDate)], attentionOnly));

        var attention = Assert.Single(
            watcher.Observe([Snapshot(SyncProviderCatalog.OneDrive, SyncState.Error)], attentionOnly));

        Assert.Equal("sync.onedrive.attention", attention.Key);
    }

    [Fact]
    public void StartsOverWhenAHiddenProviderIsShownAgain()
    {
        var watcher = new SyncStatusWatcher();
        watcher.Observe([Snapshot(SyncProviderCatalog.OneDrive, SyncState.UpToDate)], AllEnabled);
        watcher.Observe([Snapshot(SyncProviderCatalog.OneDrive, SyncState.Error)], (_, _) => false);

        // Whatever happened out of sight is not worth interrupting for once it is back.
        Assert.Empty(watcher.Observe([Snapshot(SyncProviderCatalog.OneDrive, SyncState.Error)], AllEnabled));
    }

    [Fact]
    public void StartsOverWhenAProviderStopsAndRunsAgain()
    {
        var watcher = new SyncStatusWatcher();
        watcher.Observe([Snapshot(SyncProviderCatalog.Syncthing, SyncState.Error)], AllEnabled);
        watcher.Observe([Snapshot(SyncProviderCatalog.Syncthing, SyncState.Absent)], AllEnabled);

        Assert.Empty(watcher.Observe([Snapshot(SyncProviderCatalog.Syncthing, SyncState.UpToDate)], AllEnabled));
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
            AllEnabled);

        var alerts = watcher.Observe(
            [
                Snapshot(SyncProviderCatalog.OneDrive, SyncState.Error),
                Snapshot(SyncProviderCatalog.Syncthing, SyncState.UpToDate)
            ],
            AllEnabled);

        Assert.Equal("sync.onedrive.attention", Assert.Single(alerts).Key);
    }

    [Fact]
    public void WaitsBeforeSayingTheSameThingAgain()
    {
        var watcher = new SyncStatusWatcher();
        watcher.Observe([Snapshot(SyncProviderCatalog.OneDrive, SyncState.UpToDate)], AllEnabled);
        var alert = Assert.Single(
            watcher.Observe([Snapshot(SyncProviderCatalog.OneDrive, SyncState.Error)], AllEnabled));

        Assert.Equal(SyncStatusWatcher.Cooldown, alert.Cooldown);
    }

    [Fact]
    public void PointsBackAtTheProviderSoTheNotchCanShowItsIcon()
    {
        var watcher = new SyncStatusWatcher();
        watcher.Observe([Snapshot(SyncProviderCatalog.Syncthing, SyncState.UpToDate)], AllEnabled);
        var alert = Assert.Single(
            watcher.Observe([Snapshot(SyncProviderCatalog.Syncthing, SyncState.Error)], AllEnabled));

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
