// Defines persisted user choices and their safe defaults.
using JKBar.Core.Layout;
using JKBar.Core.Presentation;

namespace JKBar.Core.Settings;

public sealed record AppearanceSettings
{
    public const int DefaultBandColourArgb = unchecked((int)0xFFFFFFFF);

    public OverlapMode Overlap { get; init; } = OverlapMode.ReserveTopEdge;
    public int BandColourArgb { get; init; } = DefaultBandColourArgb;
    public int BandOpacityPercent { get; init; } = 25;
    public string? ImagePath { get; init; }

    /// <summary>Share of the height the image may fill; 100 is as tall as the band allows.</summary>
    public int ImageScalePercent { get; init; } = 100;

    /// <summary>Which display the bar sits on. Empty follows whichever one it is already on.</summary>
    public string MonitorDeviceName { get; init; } = string.Empty;

    public AppearanceSettings Normalized() => this with
    {
        Overlap = Enum.IsDefined(Overlap) ? Overlap : OverlapMode.ReserveTopEdge,
        BandColourArgb = BandColourArgb | unchecked((int)0xFF000000),
        BandOpacityPercent = Math.Clamp(BandOpacityPercent, 0, 100),
        ImagePath = string.IsNullOrWhiteSpace(ImagePath) ? null : ImagePath.Trim(),
        ImageScalePercent = Math.Clamp(ImageScalePercent, 30, 100),
        MonitorDeviceName = string.IsNullOrWhiteSpace(MonitorDeviceName) ? string.Empty : MonitorDeviceName.Trim()
    };
}

public enum IdleNotchContent
{
    Empty,
    DateTime,
    Dog,
    Cat,
    Panda,
    Duck,
    Hamster
}

public sealed record NotchSettings
{
    public const int DefaultAlertFontSizePercent = 25;

    public IdleNotchContent IdleContent { get; init; } = IdleNotchContent.Empty;

    /// <summary>Keeps the playing track in the notch until playback stops, ahead of the resting content.</summary>
    public bool ShowNowPlaying { get; init; } = true;

    /// <summary>Share of the expanded notch height the alert title takes; the detail line follows it.</summary>
    public int AlertFontSizePercent { get; init; } = DefaultAlertFontSizePercent;

    public NotchSettings Normalized() => this with
    {
        IdleContent = Enum.IsDefined(IdleContent) ? IdleContent : IdleNotchContent.Empty,
        AlertFontSizePercent = Math.Clamp(AlertFontSizePercent, 10, 45)
    };
}

/// <summary>The registry Run entry is the real switch; this only records what the user asked for.</summary>
public sealed record StartupSettings
{
    public bool StartWithWindows { get; init; }
}

public sealed record BehaviourSettings
{
    public const int DefaultMetricsRefreshSeconds = 2;

    /// <summary>A game or a video is exactly when the bar is least welcome, so it hides and stops sampling.</summary>
    public bool HideWhenFullscreen { get; init; } = true;

    /// <summary>How often the readouts on the right are resampled and redrawn.</summary>
    public int MetricsRefreshSeconds { get; init; } = DefaultMetricsRefreshSeconds;

    public BehaviourSettings Normalized() => this with
    {
        MetricsRefreshSeconds = Math.Clamp(MetricsRefreshSeconds, 1, 10)
    };
}

public sealed record BandItemsSettings
{
    public static readonly BandItemKind[] DefaultOrder =
    [
        BandItemKind.Cpu,
        BandItemKind.Gpu,
        BandItemKind.Memory,
        BandItemKind.Disk,
        BandItemKind.Network,
        BandItemKind.GlobalSecureAccess,
        BandItemKind.OneDrive,
        BandItemKind.Syncthing,
        BandItemKind.Clock
    ];

    /// <summary>The readouts that are a percentage, and so can be drawn as a graph instead of a number.</summary>
    public static readonly BandItemKind[] StyleableKinds =
    [
        BandItemKind.Cpu,
        BandItemKind.Gpu,
        BandItemKind.Memory
    ];

    public BandItemKind[] Order { get; init; } = [.. DefaultOrder];
    public BandItemKind[] Hidden { get; init; } = [];
    public BandItemStyle[] PercentStyles { get; init; } = [];

    /// <summary>Null leaves the graphs the colour of the text beside them.</summary>
    public int? GraphColourArgb { get; init; }

    public BandItemsSettings Normalized()
    {
        var order = (Order ?? [])
            .Where(IsConfigurable)
            .Distinct()
            .Concat(DefaultOrder.Where(kind => !(Order ?? []).Contains(kind)))
            .ToArray();
        var hidden = (Hidden ?? []).Where(IsConfigurable).Distinct().ToArray();
        // Only the styles that change something are kept, so an untouched setup writes nothing.
        var styles = (PercentStyles ?? [])
            .Where(style => style is not null
                && StyleableKinds.Contains(style.Kind)
                && Enum.IsDefined(style.Style)
                && style.Style != BandPercentStyle.LabelAndValue)
            .DistinctBy(style => style.Kind)
            .ToArray();

        return this with
        {
            Order = order,
            Hidden = hidden,
            PercentStyles = styles,
            GraphColourArgb = GraphColourArgb is { } argb ? argb | unchecked((int)0xFF000000) : null
        };
    }

    public bool IsVisible(BandItemKind kind) => !Hidden.Contains(kind);

    public BandPercentStyle StyleFor(BandItemKind kind) =>
        (PercentStyles ?? []).FirstOrDefault(style => style?.Kind == kind)?.Style
        ?? BandPercentStyle.LabelAndValue;

    public IReadOnlyList<BandItem> Apply(IEnumerable<BandItem> items)
    {
        var normalized = Normalized();
        var available = items
            .Where(item => IsConfigurable(item.Kind))
            .GroupBy(item => item.Kind)
            .ToDictionary(group => group.Key, group => group.First());

        return normalized.Order
            .Where(normalized.IsVisible)
            .Where(available.ContainsKey)
            .Select(kind => normalized.Styled(available[kind]))
            .ToArray();
    }

    /// <summary>A percentage readout keeps its reading and only changes the way the renderer lays it out.</summary>
    private BandItem Styled(BandItem item) =>
        item.Layout != BandItemLayout.StackedPercent
            ? item
            : StyleFor(item.Kind) switch
            {
                BandPercentStyle.VerticalLabelGraph =>
                    item with { Layout = BandItemLayout.VerticalLabelGraph },
                BandPercentStyle.VerticalLabelValueGraph =>
                    item with { Layout = BandItemLayout.VerticalLabelValueGraph },
                _ => item
            };

    private static bool IsConfigurable(BandItemKind kind) => DefaultOrder.Contains(kind);
}

public sealed record BandItemStyle
{
    public BandItemKind Kind { get; init; }
    public BandPercentStyle Style { get; init; }
}

public sealed record BandTypographySettings
{
    public const string DefaultFontFamily = "Segoe UI";
    public const int DefaultFontSizePercent = 38;
    public const int DefaultTextColourArgb = unchecked((int)0xFF18181B);

    public string FontFamily { get; init; } = DefaultFontFamily;
    public int FontSizePercent { get; init; } = DefaultFontSizePercent;
    public bool Bold { get; init; } = true;
    public bool Italic { get; init; }
    public bool TextShadow { get; init; }
    public int TextColourArgb { get; init; } = DefaultTextColourArgb;

    public BandTypographySettings Normalized() => this with
    {
        FontFamily = string.IsNullOrWhiteSpace(FontFamily) ? DefaultFontFamily : FontFamily.Trim(),
        FontSizePercent = Math.Clamp(FontSizePercent, 25, 60),
        TextColourArgb = TextColourArgb | unchecked((int)0xFF000000)
    };
}

public sealed record NewsSettings
{
    public const string DefaultFeedUrl = "https://www.yna.co.kr/rss/news.xml";

    public bool Enabled { get; init; } = true;
    public string FeedUrl { get; init; } = DefaultFeedUrl;
    public int RefreshMinutes { get; init; } = 15;
    public int RotationSeconds { get; init; } = 15;

    public NewsSettings Normalized() => this with
    {
        FeedUrl = TryGetFeedUri(out _) ? FeedUrl.Trim() : DefaultFeedUrl,
        RefreshMinutes = Math.Clamp(RefreshMinutes, 5, 1440),
        RotationSeconds = Math.Clamp(RotationSeconds, 5, 300)
    };

    public bool TryGetFeedUri(out Uri uri)
    {
        if (Uri.TryCreate(FeedUrl?.Trim(), UriKind.Absolute, out var candidate)
            && (candidate.Scheme == Uri.UriSchemeHttp || candidate.Scheme == Uri.UriSchemeHttps))
        {
            uri = candidate;
            return true;
        }

        uri = null!;
        return false;
    }
}

/// <summary>
/// Where the article panel was last left, in real screen pixels. Zero width means it has never been placed, so
/// the panel falls back to hanging from the headline it was opened from.
/// </summary>
public sealed record ArticleWindowSettings
{
    public const int MinimumWidth = 360;
    public const int MinimumHeight = 260;

    public int X { get; init; }
    public int Y { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }

    public bool HasBounds => Width >= MinimumWidth && Height >= MinimumHeight;

    public ArticleWindowSettings Normalized() =>
        Width == 0 && Height == 0
            ? this
            : this with
            {
                Width = Math.Max(MinimumWidth, Width),
                Height = Math.Max(MinimumHeight, Height)
            };
}

public sealed record JkBarSettings
{
    public AppearanceSettings Appearance { get; init; } = new();
    public NewsSettings News { get; init; } = new();
    public BandTypographySettings Typography { get; init; } = new();
    public BandItemsSettings BandItems { get; init; } = new();
    public NotchSettings Notch { get; init; } = new();
    public StartupSettings Startup { get; init; } = new();
    public BehaviourSettings Behaviour { get; init; } = new();
    public ProcessWatchSettings ProcessWatch { get; init; } = new();
    public StockWatchSettings Stocks { get; init; } = new();
    public UpdateSettings Update { get; init; } = new();
    public ArticleWindowSettings ArticleWindow { get; init; } = new();

    public JkBarSettings Normalized() => this with
    {
        Appearance = Appearance?.Normalized() ?? new AppearanceSettings(),
        News = News?.Normalized() ?? new NewsSettings(),
        Typography = Typography?.Normalized() ?? new BandTypographySettings(),
        BandItems = BandItems?.Normalized() ?? new BandItemsSettings(),
        Notch = Notch?.Normalized() ?? new NotchSettings(),
        Startup = Startup ?? new StartupSettings(),
        Behaviour = Behaviour?.Normalized() ?? new BehaviourSettings(),
        ProcessWatch = ProcessWatch?.Normalized() ?? new ProcessWatchSettings(),
        Stocks = Stocks?.Normalized() ?? new StockWatchSettings(),
        Update = Update?.Normalized() ?? new UpdateSettings(),
        ArticleWindow = ArticleWindow?.Normalized() ?? new ArticleWindowSettings()
    };
}