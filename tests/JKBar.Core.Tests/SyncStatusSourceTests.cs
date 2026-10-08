// Pins how provider snapshots become band icons: absent providers vanish, faults get badged.
using JKBar.Core.Presentation;
using JKBar.Core.Sync;

namespace JKBar.Core.Tests;

public class SyncStatusSourceTests
{
    private static SyncProviderSnapshot Snapshot(SyncState state) =>
        new(SyncProviderCatalog.OneDrive, 'O', state, "detail");

    [Fact]
    public void DropsAProviderThatIsNotRunning() =>
        Assert.Null(SyncStatusSource.Item(Snapshot(SyncState.Absent)));

    [Fact]
    public void DrawsTheProviderInitialAsAnIcon()
    {
        var item = SyncStatusSource.Item(Snapshot(SyncState.UpToDate));

        Assert.Equal("O", item?.Label);
        Assert.Equal(BandItemLayout.StatusIcon, item?.Layout);
        Assert.Equal(BandItemKind.OneDrive, item?.Kind);
        Assert.Empty(item?.Values ?? []);
    }

    [Fact]
    public void DistinguishesHealthySynchronizingAndAttentionStates()
    {
        Assert.Equal(BandItemBadge.Good, SyncStatusSource.Item(Snapshot(SyncState.UpToDate))?.Badge);
        Assert.Equal(BandItemBadge.Attention, SyncStatusSource.Item(Snapshot(SyncState.Error))?.Badge);
        Assert.Equal(BandItemBadge.Synchronizing, SyncStatusSource.Item(Snapshot(SyncState.Synchronizing))?.Badge);
        Assert.Equal(BandItemBadge.Attention, SyncStatusSource.Item(Snapshot(SyncState.Unknown))?.Badge);
    }

    [Theory]
    [InlineData(BandItemKind.OneDrive, SyncProviderCatalog.OneDrive)]
    [InlineData(BandItemKind.Syncthing, SyncProviderCatalog.Syncthing)]
    [InlineData(BandItemKind.GlobalSecureAccess, SyncProviderCatalog.GlobalSecureAccess)]
    [InlineData(BandItemKind.Cpu, null)]
    public void NamesTheProviderWhoseIconAnItemShows(BandItemKind kind, string? expected) =>
        Assert.Equal(expected, SyncStatusSource.ProviderIdFor(kind));

    [Fact]
    public void GivesEachStateItsOwnColour()
    {
        var colours = new[] { SyncState.Synchronizing, SyncState.UpToDate, SyncState.Error, SyncState.Unknown }
            .Select(SyncStatusSource.ColourFor)
            .ToArray();

        Assert.Equal(colours.Length, colours.Distinct().Count());
        Assert.Equal(Color.FromArgb(255, 185, 0), SyncStatusSource.ColourFor(SyncState.Synchronizing));
    }

    [Theory]
    [InlineData(SyncState.UpToDate, "정상")]
    [InlineData(SyncState.Synchronizing, "동기화 중")]
    [InlineData(SyncState.Error, "주의")]
    [InlineData(SyncState.Unknown, "확인 불가")]
    public void LabelsTheBadgeForThePopup(SyncState state, string expected) =>
        Assert.Equal(expected, SyncStatusSource.StateLabel(state));

    [Fact]
    public void SplitsTheProviderDetailIntoPopupLines()
    {
        var snapshot = new SyncProviderSnapshot(SyncProviderCatalog.Syncthing, 'S', SyncState.UpToDate, "첫 줄\n\n 둘째 줄 \n");

        Assert.Equal(["첫 줄", "둘째 줄"], SyncStatusSource.DetailLines(snapshot));
    }

    [Theory]
    [InlineData(SyncProviderCatalog.OneDrive, BandItemKind.OneDrive)]
    [InlineData(SyncProviderCatalog.Syncthing, BandItemKind.Syncthing)]
    [InlineData(SyncProviderCatalog.GlobalSecureAccess, BandItemKind.GlobalSecureAccess)]
    public void MapsEveryKnownProviderToAConfigurableItem(string providerId, BandItemKind expected) =>
        Assert.Equal(expected, SyncStatusSource.KindFor(providerId));

    [Fact]
    public void KeepsOnlyTheVisibleProvidersInOrder()
    {
        var items = SyncStatusSource.Items(
        [
            new SyncProviderSnapshot(SyncProviderCatalog.GlobalSecureAccess, 'G', SyncState.UpToDate, ""),
            new SyncProviderSnapshot(SyncProviderCatalog.OneDrive, 'O', SyncState.Absent, ""),
            new SyncProviderSnapshot(SyncProviderCatalog.Syncthing, 'S', SyncState.Error, "")
        ]);

        Assert.Equal(["G", "S"], items.Select(item => item.Label));
    }

    [Fact]
    public void MarksOnlyExplicitlyAbsentProvidersAsUnavailable()
    {
        var unavailable = SyncStatusSource.UnavailableKinds(
        [
            new SyncProviderSnapshot(SyncProviderCatalog.GlobalSecureAccess, 'G', SyncState.Absent, ""),
            new SyncProviderSnapshot(SyncProviderCatalog.OneDrive, 'O', SyncState.Unknown, ""),
            new SyncProviderSnapshot("future", 'F', SyncState.Absent, "")
        ]);

        Assert.Equal([BandItemKind.GlobalSecureAccess], unavailable);
        Assert.Empty(SyncStatusSource.UnavailableKinds([]));
    }
}
