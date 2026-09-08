// Verifies durable news settings and fallback behavior for damaged local state.
using JKBar.Core.Settings;
using JKBar.Core.Presentation;
using JKBar.Core.Layout;

namespace JKBar.Core.Tests;

public class SettingsStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"JKBar.Tests.{Guid.NewGuid():N}");

    [Fact]
    public void ReturnsDefaultsWhenTheFileDoesNotExist()
    {
        var settings = Store().Load();

        Assert.True(settings.News.Enabled);
        Assert.Equal(NewsSettings.DefaultFeedUrl, settings.News.FeedUrl);
        Assert.Equal(BandTypographySettings.DefaultFontFamily, settings.Typography.FontFamily);
        Assert.True(settings.Typography.Bold);
        Assert.Equal(BandTypographySettings.DefaultTextColourArgb, settings.Typography.TextColourArgb);
        Assert.Equal(IdleNotchContent.Empty, settings.Notch.IdleContent);
        Assert.Equal(OverlapMode.ReserveTopEdge, settings.Appearance.Overlap);
        Assert.Equal(25, settings.Appearance.BandOpacityPercent);
    }

    [Fact]
    public void SavesAndLoadsNewsSettings()
    {
        var store = Store();
        var expected = new JkBarSettings
        {
            News = new NewsSettings
            {
                Enabled = false,
                FeedUrl = "https://example.com/custom.xml",
                RefreshMinutes = 30,
                RotationSeconds = 20
            }
        };

        store.Save(expected);

        Assert.Equal(expected.News, store.Load().News);
    }

    [Fact]
    public void FallsBackToDefaultsForDamagedJson()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "settings.json"), "not-json");

        var settings = Store().Load();
        Assert.Equal(new NewsSettings(), settings.News);
        Assert.Equal(new BandTypographySettings(), settings.Typography);
        Assert.Equal(BandItemsSettings.DefaultOrder, settings.BandItems.Order);
        Assert.Empty(settings.BandItems.Hidden);
    }

    [Fact]
    public void NormalizesInvalidNewsValues()
    {
        var normalized = new NewsSettings
        {
            FeedUrl = "file:///c:/private.xml",
            RefreshMinutes = 0,
            RotationSeconds = 1000
        }.Normalized();

        Assert.Equal(NewsSettings.DefaultFeedUrl, normalized.FeedUrl);
        Assert.Equal(5, normalized.RefreshMinutes);
        Assert.Equal(300, normalized.RotationSeconds);
    }

    [Fact]
    public void NormalizesTypographyValues()
    {
        var normalized = new BandTypographySettings
        {
            FontFamily = "  ",
            FontSizePercent = 100,
            TextColourArgb = 0x00123456
        }.Normalized();

        Assert.Equal(BandTypographySettings.DefaultFontFamily, normalized.FontFamily);
        Assert.Equal(60, normalized.FontSizePercent);
        Assert.Equal(unchecked((int)0xFF123456), normalized.TextColourArgb);
    }

    [Fact]
    public void SavesAndLoadsTypographySettings()
    {
        var store = Store();
        var expected = new JkBarSettings
        {
            Typography = new BandTypographySettings
            {
                FontFamily = "Arial",
                FontSizePercent = 42,
                Bold = false,
                Italic = true,
                TextShadow = true,
                TextColourArgb = unchecked((int)0xFF102030)
            }
        };

        store.Save(expected);

        Assert.Equal(expected.Typography, store.Load().Typography);
    }

    [Fact]
    public void NormalizesBandItemOrderAndVisibility()
    {
        var normalized = new BandItemsSettings
        {
            Order = [BandItemKind.Clock, BandItemKind.Cpu, BandItemKind.Clock, BandItemKind.Custom],
            Hidden = [BandItemKind.Gpu, BandItemKind.Gpu, BandItemKind.Custom]
        }.Normalized();

        Assert.Equal(
            [
                BandItemKind.Clock,
                BandItemKind.Cpu,
                BandItemKind.Gpu,
                BandItemKind.Memory,
                BandItemKind.Disk,
                BandItemKind.Network,
                BandItemKind.GlobalSecureAccess,
                BandItemKind.OneDrive,
                BandItemKind.Syncthing
            ],
            normalized.Order);
        Assert.Equal([BandItemKind.Gpu], normalized.Hidden);
        Assert.False(normalized.IsVisible(BandItemKind.Gpu));
        Assert.True(normalized.IsVisible(BandItemKind.Clock));
    }

    [Fact]
    public void SavesAndLoadsBandItemSettings()
    {
        var store = Store();
        var expected = new BandItemsSettings
        {
            Order =
            [
                BandItemKind.Clock,
                BandItemKind.Syncthing,
                BandItemKind.OneDrive,
                BandItemKind.GlobalSecureAccess,
                BandItemKind.Network,
                BandItemKind.Disk,
                BandItemKind.Memory,
                BandItemKind.Gpu,
                BandItemKind.Cpu
            ],
            Hidden = [BandItemKind.Gpu, BandItemKind.Clock]
        };

        store.Save(new JkBarSettings { BandItems = expected });
        var actual = store.Load().BandItems;

        Assert.Equal(expected.Order, actual.Order);
        Assert.Equal(expected.Hidden, actual.Hidden);
    }

    [Fact]
    public void SavesAndLoadsThePercentageDisplayStyles()
    {
        var store = Store();
        var expected = new BandItemsSettings
        {
            PercentStyles =
            [
                new BandItemStyle { Kind = BandItemKind.Cpu, Style = BandPercentStyle.VerticalLabelValueGraph },
                new BandItemStyle { Kind = BandItemKind.Memory, Style = BandPercentStyle.VerticalLabelGraph }
            ]
        };

        store.Save(new JkBarSettings { BandItems = expected });
        var actual = store.Load().BandItems;

        Assert.Equal(BandPercentStyle.VerticalLabelValueGraph, actual.StyleFor(BandItemKind.Cpu));
        Assert.Equal(BandPercentStyle.VerticalLabelGraph, actual.StyleFor(BandItemKind.Memory));
        Assert.Equal(BandPercentStyle.LabelAndValue, actual.StyleFor(BandItemKind.Gpu));
        Assert.Contains("\"Style\": 2", File.ReadAllText(store.Path));
    }

    [Fact]
    public void SavesAndLoadsIdleNotchContent()
    {
        var store = Store();
        var expected = new NotchSettings { IdleContent = IdleNotchContent.DateTime };

        store.Save(new JkBarSettings { Notch = expected });

        Assert.Equal(expected, store.Load().Notch);
    }

    [Fact]
    public void RejectsUnknownIdleNotchContent()
    {
        var normalized = new NotchSettings { IdleContent = (IdleNotchContent)99 }.Normalized();

        Assert.Equal(IdleNotchContent.Empty, normalized.IdleContent);
    }

    [Fact]
    public void KeepsTheAlertTextSizeWithinReadableBounds()
    {
        Assert.Equal(45, new NotchSettings { AlertFontSizePercent = 400 }.Normalized().AlertFontSizePercent);
        Assert.Equal(10, new NotchSettings { AlertFontSizePercent = 0 }.Normalized().AlertFontSizePercent);
    }

    [Fact]
    public void SamplesTheReadoutsEveryTwoSecondsUnlessToldOtherwise()
    {
        Assert.Equal(2, new JkBarSettings().Normalized().Behaviour.MetricsRefreshSeconds);
    }

    [Fact]
    public void KeepsTheReadoutIntervalBetweenOneAndTenSeconds()
    {
        Assert.Equal(10, new BehaviourSettings { MetricsRefreshSeconds = 99 }.Normalized().MetricsRefreshSeconds);
        Assert.Equal(1, new BehaviourSettings { MetricsRefreshSeconds = 0 }.Normalized().MetricsRefreshSeconds);
        Assert.Equal(5, new BehaviourSettings { MetricsRefreshSeconds = 5 }.Normalized().MetricsRefreshSeconds);
    }

    [Fact]
    public void KeepsAnUnknownIdleContentFromResettingTheAlertTextSize()
    {
        var normalized = new NotchSettings
        {
            IdleContent = (IdleNotchContent)99,
            AlertFontSizePercent = 30
        }.Normalized();

        Assert.Equal(30, normalized.AlertFontSizePercent);
    }

    [Fact]
    public void NormalizesAppearanceValues()
    {
        var normalized = new AppearanceSettings
        {
            Overlap = (OverlapMode)99,
            BandColourArgb = 0x00123456,
            BandOpacityPercent = 140,
            ImagePath = "  ",
            ImageScalePercent = 5
        }.Normalized();

        Assert.Equal(OverlapMode.ReserveTopEdge, normalized.Overlap);
        Assert.Equal(unchecked((int)0xFF123456), normalized.BandColourArgb);
        Assert.Equal(100, normalized.BandOpacityPercent);
        Assert.Null(normalized.ImagePath);
        Assert.Equal(30, normalized.ImageScalePercent);
    }

    [Fact]
    public void SavesAndLoadsAppearanceSettings()
    {
        var store = Store();
        var expected = new AppearanceSettings
        {
            Overlap = OverlapMode.Floating,
            BandColourArgb = unchecked((int)0xFF203040),
            BandOpacityPercent = 60,
            ImagePath = @"C:\Images\logo.png",
            ImageScalePercent = 65
        };

        store.Save(new JkBarSettings { Appearance = expected });

        Assert.Equal(expected, store.Load().Appearance);
    }

    [Fact]
    public void FollowsWhicheverScreenUntilAMonitorIsChosen()
    {
        Assert.Equal(string.Empty, new AppearanceSettings().Normalized().MonitorDeviceName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TreatsABlankMonitorAsFollowingTheWindow(string? stored)
    {
        var normalized = new AppearanceSettings { MonitorDeviceName = stored! }.Normalized();

        Assert.Equal(string.Empty, normalized.MonitorDeviceName);
    }

    [Fact]
    public void SavesAndLoadsTheChosenMonitor()
    {
        var store = Store();

        store.Save(new JkBarSettings
        {
            Appearance = new AppearanceSettings { MonitorDeviceName = @"  \\.\DISPLAY2  " }
        });

        Assert.Equal(@"\\.\DISPLAY2", store.Load().Appearance.MonitorDeviceName);
    }

    [Fact]
    public void AppliesBandItemOrderAndVisibility()
    {
        var settings = new BandItemsSettings
        {
            Order = [BandItemKind.Clock, BandItemKind.Network, BandItemKind.Cpu],
            Hidden = [BandItemKind.Network]
        };
        BandItem[] items =
        [
            new BandItem("CPU", "1%", "100%") with { Kind = BandItemKind.Cpu },
            new BandItem("NET", "1 KB/s", "1023 MB/s") with { Kind = BandItemKind.Network },
            new BandItem("TIME", "10:00", "00:00") with { Kind = BandItemKind.Clock }
        ];

        var applied = settings.Apply(items);

        Assert.Equal([BandItemKind.Clock, BandItemKind.Cpu], applied.Select(item => item.Kind));
    }

    [Fact]
    public void LeavesTheGraphColourUnsetWhenItFollowsTheText()
    {
        Assert.Null(new BandItemsSettings().Normalized().GraphColourArgb);
    }

    [Fact]
    public void MakesAChosenGraphColourOpaque()
    {
        var settings = new BandItemsSettings { GraphColourArgb = 0x00_20_91_58 }.Normalized();

        Assert.Equal(unchecked((int)0xFF_20_91_58), settings.GraphColourArgb);
    }

    [Fact]
    public void KeepsTheGraphColourThroughASaveAndLoad()
    {
        var store = Store();

        store.Save(new JkBarSettings
        {
            BandItems = new BandItemsSettings { GraphColourArgb = unchecked((int)0xFF_20_91_58) }
        });

        Assert.Equal(unchecked((int)0xFF_20_91_58), store.Load().BandItems.GraphColourArgb);
    }

    [Fact]
    public void AdoptsAnEarlierFileWhenTheNewLocationIsEmpty()
    {
        Directory.CreateDirectory(_directory);
        var source = Path.Combine(_directory, "old.json");
        var destination = Path.Combine(_directory, "portable", "settings.json");
        File.WriteAllText(source, "{}");

        Assert.True(SettingsStore.Adopt(source, destination));
        Assert.True(File.Exists(destination));
    }

    [Fact]
    public void NeverOverwritesTheFileAlreadyInUse()
    {
        Directory.CreateDirectory(_directory);
        var source = Path.Combine(_directory, "old.json");
        var destination = Path.Combine(_directory, "settings.json");
        File.WriteAllText(source, "old");
        File.WriteAllText(destination, "current");

        Assert.False(SettingsStore.Adopt(source, destination));
        Assert.Equal("current", File.ReadAllText(destination));
    }

    [Fact]
    public void ReportsAFolderItCannotWriteTo()
    {
        Directory.CreateDirectory(_directory);
        var blocker = Path.Combine(_directory, "blocker");
        File.WriteAllText(blocker, "x");

        // A folder cannot live inside a file, so this stands in for a location the user may not write to.
        Assert.False(SettingsStore.CanWriteTo(Path.Combine(blocker, "nested")));
    }

    private SettingsStore Store() => new(Path.Combine(_directory, "settings.json"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }

        GC.SuppressFinalize(this);
    }
}