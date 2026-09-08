// Paints the reserved band: the fill, the user's image on the left, and the readouts on the right.
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using JKBar.App.Sync;
using JKBar.Core.Layout;
using JKBar.Core.News;
using JKBar.Core.Presentation;
using JKBar.Core.Settings;
using JKBar.Core.Stocks;

namespace JKBar.App.Rendering;

internal static class BandRenderer
{
    private const float PaddingShareOfHeight = 0.22f;
    private const float LabelRowOverlap = 0.5f;
    private const float SameFontRowOverlap = 0.12f;
    private static readonly Marker DiskRead = new("R", Color.FromArgb(32, 145, 88));
    private static readonly Marker DiskWrite = new("W", Color.FromArgb(218, 48, 57));
    private static readonly Marker NetworkUp = new("U", Color.FromArgb(218, 48, 57));
    private static readonly Marker NetworkDown = new("D", Color.FromArgb(38, 103, 196));

    // Korean exchanges colour a gain red and a loss blue, which is the opposite of the American convention.
    private static readonly Color StockRising = Color.FromArgb(218, 48, 57);
    private static readonly Color StockFalling = Color.FromArgb(38, 103, 196);
    private static readonly Color StatusIconInk = Color.FromArgb(250, 250, 252);
    private static readonly Color BadgeAttention = Color.FromArgb(218, 48, 57);
    private static readonly Color BadgeGood = Color.FromArgb(32, 145, 88);
    private const float StatusIconShareOfHeight = 0.66f;
    private const float BadgeShareOfIcon = 0.62f;
    private const float BadgeOverhangShareOfBadge = 0.4f;
    private const int LeftGroupGap = 2;

    /// <summary>
    /// The widest reading a quote box has to hold. Korean names are wider per character than digits, so the
    /// yardstick is written in them.
    /// </summary>
    private const string QuoteYardstick = "종목이름다섯 0,000,000 ▼00.00%";

    /// <summary>
    /// The stretch of the active application's name that keeps its room before the headline gets any. Written in
    /// Korean because it has to hold on a screen scaled up far enough to make the font wide.
    /// </summary>
    private const string NameYardstick = "앱이름여섯자";

    /// <summary>The shortest headline worth showing. Narrower than this the news is turned off, not left as a stub.</summary>
    private const string NewsYardstick = "NEWS 00:00 [뉴스제목여덟자]";

    /// <summary>How much of the left slot the headline box takes. The rest is the logo and the active application.</summary>
    private const float NewsShareOfSlot = 0.55f;

    /// <summary>The graph is about as wide as the band is tall, which reads as a chart without crowding the rest.</summary>
    private const float GraphShareOfHeight = 0.95f;

    /// <summary>How tall the reading above a chart is set, as a share of the band. The chart takes what is left.</summary>
    private const float GraphValueShareOfHeight = 0.32f;

    /// <summary>How far down the next stacked letter starts, in line heights.</summary>
    private const float LetterStep = 0.72f;

    /// <summary>Multiples of the padding between the watched-process icons and the readouts they sit beside.</summary>
    private const int ProcessIconGroupGap = 3;

    /// <summary>Share of an icon's own size left between two of them.</summary>
    private const float ProcessIconGap = 0.42f;

    /// <summary>How tall the running count is drawn, as a share of the icon it sits on.</summary>
    private const float CountShareOfIcon = 0.62f;

    /// <summary>The cutout is black on the band too, so a z-order change can never tint the bar with band colour.</summary>
    private static readonly Color NotchFill = Color.FromArgb(255, 0, 0, 0);

    /// <param name="notch">Where the bar sits, in surface coordinates, so content can keep clear of it.</param>
    /// <param name="notchCornerRadius">The bar's bottom radius, so the band stamps exactly the same shape.</param>
    /// <param name="quote">The symbol showing right now; the caller cycles through the registered ones.</param>
    /// <param name="items">In display order, left to right.</param>
    /// <param name="runningProcesses">Watched executables that are running; their icons sit left of the readouts.</param>
    internal static BandHitAreas Paint(
        Graphics g,
        Size surface,
        NotchGeometry.Rect notch,
        int notchCornerRadius,
        BandStyle style,
        BandTypographySettings typography,
        Image? image,
        int imageScalePercent,
        string? activeApp,
        StockQuote? quote,
        NewsItem? news,
        IReadOnlyList<BandItem> items,
        IReadOnlyList<RunningProcess> runningProcesses)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.Half;

        // ClearType leaves black fringes on a layered surface because it assumes an opaque background.
        g.TextRenderingHint = TextRenderingHint.AntiAlias;
        g.Clear(style.ForLayeredSurface());
        StampNotch(g, notch, notchCornerRadius);

        var padding = (int)Math.Round(surface.Height * PaddingShareOfHeight);
        var band = new NotchGeometry.Rect(0, 0, surface.Width, surface.Height);
        var slots = BandLayout.Divide(band, notch, padding);

        var imageRight = DrawImage(g, slots.Left, image, imageScalePercent, padding);

        var foregroundColour = Color.FromArgb(typography.TextColourArgb);
        using var valueFont = CreateFont(typography, surface.Height, 1f);
        using var labelFont = CreateFont(typography, surface.Height, 0.58f);
        using var rateFont = CreateFont(typography, surface.Height, 0.68f);
        using var foreground = new SolidBrush(foregroundColour);
        using SolidBrush? shadow = typography.TextShadow
            ? new SolidBrush(ShadowColourFor(foregroundColour))
            : null;
        var ink = new Ink(foreground, shadow, foregroundColour, style.GraphColour ?? foregroundColour);
        var contentLeft = LeftContentStart(slots.Left, imageRight, padding);
        var (newsBox, quoteBox, newsCramped) = LeftBoxes(g, slots.Left, activeApp, valueFont, padding, contentLeft);
        DrawActiveApp(g, slots.Left, activeApp, valueFont, ink, padding, contentLeft, newsBox.Left);
        DrawQuote(g, quoteBox, quote, valueFont, ink);
        var newsBounds = DrawNews(g, newsBox, news, valueFont, ink);
        var itemsLeft = DrawItems(
            g,
            slots.Right,
            items,
            valueFont,
            labelFont,
            rateFont,
            ink,
            padding);
        var processIcons = DrawProcessIcons(g, slots.Right, runningProcesses, itemsLeft, padding, ink);

        return new BandHitAreas(newsBounds, processIcons, news is not null && newsCramped);
    }

    /// <summary>
    /// The bar is its own layered window on top of this one, but the shell reorders topmost windows and the band
    /// can end up above it. Painting the cutout opaque black here means the result is black either way.
    /// </summary>
    private static void StampNotch(Graphics g, NotchGeometry.Rect notch, int cornerRadius)
    {
        if (notch.Width <= 0 || notch.Height <= 0)
        {
            return;
        }

        using var path = NotchRenderer.Silhouette(notch.Width, notch.Height, cornerRadius);
        using var matrix = new Matrix();
        matrix.Translate(notch.Left, notch.Top);
        path.Transform(matrix);

        using var brush = new SolidBrush(NotchFill);
        g.FillPath(brush, path);
    }
    private static int DrawImage(Graphics g, NotchGeometry.Rect slot, Image? image, int scalePercent, int padding)
    {
        if (image is null || slot.Width <= 0)
        {
            return slot.Left;
        }

        var available = (int)Math.Round((slot.Height - padding) * (Math.Clamp(scalePercent, 1, 100) / 100d));
        if (available <= 0)
        {
            return slot.Left;
        }

        var factor = Math.Min(available / (double)image.Height, slot.Width / (double)image.Width);
        var width = (int)Math.Round(image.Width * factor);
        var height = (int)Math.Round(image.Height * factor);
        if (width <= 0 || height <= 0)
        {
            return slot.Left;
        }

        g.DrawImage(image, slot.Left, slot.Top + ((slot.Height - height) / 2), width, height);
        return slot.Left + width;
    }

    /// <param name="limit">Where the headline's box starts; the name is trimmed before it reaches that.</param>
    private static void DrawActiveApp(
        Graphics g,
        NotchGeometry.Rect slot,
        string? name,
        Font font,
        Ink ink,
        int padding,
        int left,
        int limit)
    {
        if (string.IsNullOrWhiteSpace(name) || slot.Width <= 0)
        {
            return;
        }

        var available = limit - (padding * LeftGroupGap) - left;
        if (available <= 0)
        {
            return;
        }

        using var format = LeftTextFormat();
        var measured = (int)Math.Ceiling(g.MeasureString(name, font, available, format).Width) + 2;
        Write(g, name, font, format, ink, new RectangleF(left, slot.Top, Math.Min(available, measured), slot.Height));
    }

    /// <summary>
    /// The headline and the quote get boxes measured leftwards from the bar, so neither shifts when its own text
    /// changes length. The quote's box is as wide as the longest reading it can hold; the headline's follows the
    /// screen, because that is what decides how much of a title fits. Neither the active application's name nor
    /// the quote gives up room, so on a narrow slot the headline is the one that runs out.
    /// </summary>
    /// <returns>Cramped is true when the screen cannot hold a readable headline at all, whatever is running.</returns>
    private static (Rectangle News, Rectangle Quote, bool Cramped) LeftBoxes(
        Graphics g,
        NotchGeometry.Rect slot,
        string? activeApp,
        Font font,
        int padding,
        int contentLeft)
    {
        if (slot.Width <= 0)
        {
            return (Rectangle.Empty, Rectangle.Empty, false);
        }

        using var format = LeftTextFormat();
        var gap = padding * LeftGroupGap;

        // Anchored at the bar so an empty box still tells the name how far right it may run.
        var closed = new Rectangle(slot.Right - padding, slot.Top, 0, slot.Height);
        var room = slot.Right - padding - contentLeft;
        if (room <= 0)
        {
            return (closed, closed, true);
        }

        var quoteWidth = Math.Min(room, Measure(g, QuoteYardstick, font, format) + 2);
        var nameWidth = string.IsNullOrWhiteSpace(activeApp)
            ? 0
            : Measure(g, activeApp, font, format) + 2 + gap;

        // Judged against a yardstick rather than the name in the foreground, so switching windows cannot keep
        // turning the news off and on.
        var cramped = room - quoteWidth - gap - Measure(g, NameYardstick, font, format) - gap
            < Measure(g, NewsYardstick, font, format);

        var newsWidth = Math.Clamp(
            (int)Math.Round(slot.Width * NewsShareOfSlot),
            0,
            Math.Max(0, room - quoteWidth - gap - nameWidth));
        if (newsWidth <= 0)
        {
            return (closed, new Rectangle(slot.Right - padding - quoteWidth, slot.Top, quoteWidth, slot.Height), cramped);
        }

        // A gap off the bar as well, so the quote does not look stuck to the cutout.
        var quoteLeft = slot.Right - padding - quoteWidth;

        return (
            new Rectangle(quoteLeft - gap - newsWidth, slot.Top, newsWidth, slot.Height),
            new Rectangle(quoteLeft, slot.Top, quoteWidth, slot.Height),
            cramped);
    }

    /// <summary>
    /// One registered symbol, centred in its own fixed box. The move is coloured on its own so a rise or a fall
    /// can be read without the numbers. The price and the move keep their room and a long name is cut short,
    /// because a fund with a twenty-letter name would otherwise push the reading out of the box.
    /// </summary>
    private static void DrawQuote(Graphics g, Rectangle box, StockQuote? quote, Font font, Ink ink)
    {
        if (quote is null || box.Width <= 0)
        {
            return;
        }

        using var format = LeftTextFormat();
        var change = quote.ChangePercent.Trim();
        // The separators lead rather than trail, because a trailing space is measured but not drawn, which
        // shifted the centred group by a dozen pixels as the reading changed.
        var price = $" {quote.Price}";
        var move = change.Length == 0 ? string.Empty : $" {StockPresentation.Marker(quote.Direction)}{change}%";

        var moveWidth = move.Length == 0 ? 0 : Math.Min(box.Width, Measure(g, move, font, format) + 2);
        var priceWidth = Math.Min(box.Width - moveWidth, Measure(g, price, font, format) + 2);
        var nameWidth = Math.Min(
            Measure(g, quote.Name, font, format) + 2,
            Math.Max(0, box.Width - priceWidth - moveWidth));
        var left = box.Left + Math.Max(0, (box.Width - nameWidth - priceWidth - moveWidth) / 2);

        Write(g, quote.Name, font, format, ink, new RectangleF(left, box.Top, nameWidth, box.Height));
        Write(g, price, font, format, ink, new RectangleF(left + nameWidth, box.Top, priceWidth, box.Height));
        if (moveWidth <= 0)
        {
            return;
        }

        using var brush = new SolidBrush(quote.Direction switch
        {
            StockDirection.Rising => StockRising,
            StockDirection.Falling => StockFalling,
            _ => ink.Colour
        });
        Write(
            g,
            move,
            font,
            format,
            ink with { Foreground = brush },
            new RectangleF(left + nameWidth + priceWidth, box.Top, moveWidth, box.Height));
    }

    /// <returns>Where the text itself landed, so a click beside a short headline does not open it.</returns>
    private static Rectangle DrawNews(Graphics g, Rectangle box, NewsItem? news, Font font, Ink ink)
    {
        if (news is null || box.Width <= 0)
        {
            return Rectangle.Empty;
        }

        var text = NewsPresentation.Headline(news, TimeZoneInfo.Local, CultureInfo.CurrentCulture);
        using var format = LeftTextFormat();

        var measured = (int)Math.Ceiling(g.MeasureString(text, font, box.Width, format).Width) + 2;
        var width = Math.Min(box.Width, measured);

        // Held against the right edge so the headline reads as one group with the quote beside it.
        var bounds = new Rectangle(box.Right - width, box.Top, width, box.Height);
        Write(g, text, font, format, ink, new RectangleF(bounds.Left, bounds.Top, bounds.Width, bounds.Height));

        return bounds;
    }

    /// <summary>Each piece on the left is separated by a gap wide enough to read as its own group.</summary>
    private static int LeftContentStart(NotchGeometry.Rect slot, int previousRight, int padding) =>
        previousRight > slot.Left ? previousRight + (padding * LeftGroupGap) : slot.Left;

    private static StringFormat LeftTextFormat()
    {
        var format = StringFormat.GenericTypographic;
        format.FormatFlags |= StringFormatFlags.NoWrap;
        format.Trimming = StringTrimming.EllipsisCharacter;
        format.LineAlignment = StringAlignment.Center;
        return format;
    }

    /// <returns>The left edge the readouts ended up occupying, which is where anything before them has to stop.</returns>
    private static int DrawItems(
        Graphics g,
        NotchGeometry.Rect slot,
        IReadOnlyList<BandItem> items,
        Font valueFont,
        Font labelFont,
        Font rateFont,
        Ink ink,
        int padding)
    {
        if (slot.Width <= 0 || items.Count == 0)
        {
            return slot.Right;
        }

        using var format = StringFormat.GenericTypographic;
        format.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;
        format.LineAlignment = StringAlignment.Center;

        var gap = padding;
        var inner = Math.Max(2, padding / 2);
        var right = slot.Right;
        var leftmost = slot.Right;

        // Right-aligned, so the list is walked backwards and callers can supply plain left-to-right order.
        for (var i = items.Count - 1; i >= 0; i--)
        {
            var item = items[i];
            var width = MeasureItem(g, item, valueFont, labelFont, rateFont, format, inner, slot.Height);

            var start = right - width;
            if (start < slot.Left)
            {
                return leftmost;
            }

            DrawItem(
                g,
                item,
                new RectangleF(start, slot.Top, width, slot.Height),
                valueFont,
                labelFont,
                rateFont,
                format,
                ink,
                inner);

            var nextKind = i > 0 ? items[i - 1].Kind : BandItemKind.Custom;
            var separatesActivityGroups = item.Kind is BandItemKind.Disk or BandItemKind.Network
                && nextKind is BandItemKind.Disk or BandItemKind.Network
                && item.Kind != nextKind;
            // The direction letters already tell the two apart, so the seam only needs to be a touch wider.
            var itemGap = separatesActivityGroups ? (int)Math.Round(gap * 1.5) : gap;
            leftmost = start;
            right = start - itemGap;
        }

        return leftmost;
    }

    /// <summary>
    /// The icons that say a watched application is running. They sit left of the readouts with a wider seam than
    /// the readouts use between themselves, so an icon reads as its own thing rather than as the CPU's picture.
    /// </summary>
    /// <returns>Where each icon landed, so a click on one can be traced back to the application it stands for.</returns>
    private static IReadOnlyList<ProcessIcon> DrawProcessIcons(
        Graphics g,
        NotchGeometry.Rect slot,
        IReadOnlyList<RunningProcess> processes,
        int readoutsLeft,
        int padding,
        Ink ink)
    {
        if (slot.Width <= 0 || processes.Count == 0)
        {
            return [];
        }

        var diameter = StatusIconDiameter(slot.Height);
        // Wide enough that two icons read as two applications rather than one strip of artwork.
        var gap = Math.Max(6, (int)Math.Round(diameter * ProcessIconGap));
        var top = slot.Top + ((slot.Height - diameter) / 2);
        var right = readoutsLeft - (padding * ProcessIconGroupGap);
        var drawn = new List<ProcessIcon>(processes.Count);

        for (var i = processes.Count - 1; i >= 0; i--)
        {
            var watched = processes[i].Watched;
            // An entry registered by name alone has no file to read, so the running copy supplies its own icon.
            var icon = watched.ByNameOnly
                ? ProviderIconResolver.ResolveProcess(watched.MatchKey, diameter)
                : ProviderIconResolver.ResolveFile(watched.Path, diameter);
            if (icon is null)
            {
                continue;
            }

            var start = right - diameter;
            if (start < slot.Left)
            {
                break;
            }

            var bounds = new Rectangle(start, top, diameter, diameter);
            g.DrawImage(icon, bounds);
            DrawCountBadge(g, bounds, diameter, processes[i].Count, ink);
            drawn.Add(new ProcessIcon(watched, bounds));
            right = start - gap;
        }

        return drawn;
    }

    /// <summary>
    /// How many copies are running, as a bare number over the icon's top corner. A single copy carries none: the
    /// icon already says it is running.
    /// </summary>
    private static void DrawCountBadge(Graphics g, Rectangle icon, int diameter, int count, Ink ink)
    {
        if (count <= 1)
        {
            return;
        }

        var text = count > 9 ? "9+" : count.ToString(CultureInfo.InvariantCulture);
        using var font = new Font(
            BandTypographySettings.DefaultFontFamily,
            Math.Max(8f, diameter * CountShareOfIcon),
            FontStyle.Bold,
            GraphicsUnit.Pixel);
        using var format = new StringFormat(StringFormat.GenericTypographic)
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            FormatFlags = StringFormatFlags.NoWrap
        };

        var height = font.GetHeight(g);
        var width = Math.Max(height, g.MeasureString(text, font, PointF.Empty, format).Width);
        // Anchored by its right edge, so `9+` grows back over the icon instead of into the next one.
        var bounds = new RectangleF(
            icon.Right + (height * 0.5f) - width,
            icon.Top - (height * 0.18f),
            width,
            height);

        // No plate behind it, so the number needs its own outline to stay readable over any artwork.
        using var outline = new GraphicsPath();
        outline.AddString(
            text,
            font.FontFamily,
            (int)FontStyle.Bold,
            font.Size,
            bounds,
            format);
        using var edge = new Pen(ShadowColourFor(ink.Colour), Math.Max(2f, diameter * 0.09f))
        {
            LineJoin = LineJoin.Round
        };
        g.DrawPath(edge, outline);
        g.FillPath(ink.Foreground, outline);
    }

    private static int MeasureItem(
        Graphics g,
        BandItem item,
        Font valueFont,
        Font labelFont,
        Font rateFont,
        StringFormat format,
        int inner,
        int height) => item.Layout switch
    {
        BandItemLayout.StackedPercent => Math.Max(
            Measure(g, item.LabelYardstick, labelFont, format),
            Measure(g, item.Values[0].Template, valueFont, format)),
        BandItemLayout.VerticalLabelGraph or BandItemLayout.VerticalLabelValueGraph =>
            VerticalLabelWidth(g, item, labelFont, format, height) + inner + GraphWidth(height),
        BandItemLayout.RateRows => item.Values.Max(value => Measure(g, value.Template, rateFont, format))
            + inner
            + MarkerWidth(g, NetworkUp, NetworkDown, rateFont, format),
        BandItemLayout.IndicatorRows => item.Values.Max(value => Measure(g, value.Template, rateFont, format))
            + inner
            + MarkerWidth(g, DiskRead, DiskWrite, rateFont, format),        BandItemLayout.StatusIcon => StatusIconWidth(height),
        _ => MeasureInline(g, item, valueFont, format, inner)
    };

    private static int MeasureInline(Graphics g, BandItem item, Font font, StringFormat format, int inner)
    {
        var width = item.Label.Length == 0 ? 0 : Measure(g, item.LabelYardstick, font, format);
        return item.Values.Aggregate(width, (current, value) =>
            current + inner + Measure(g, value.Template, font, format));
    }

    private static void DrawItem(
        Graphics g,
        BandItem item,
        RectangleF bounds,
        Font valueFont,
        Font labelFont,
        Font rateFont,
        StringFormat format,
        Ink ink,
        int inner)
    {
        switch (item.Layout)
        {
            case BandItemLayout.StackedPercent:
                DrawStacked(g, item, bounds, valueFont, labelFont, format, ink);
                break;
            case BandItemLayout.VerticalLabelGraph:
                DrawVerticalLabelGraph(g, item, bounds, labelFont, format, ink, inner, withValue: false);
                break;
            case BandItemLayout.VerticalLabelValueGraph:
                DrawVerticalLabelGraph(g, item, bounds, labelFont, format, ink, inner, withValue: true);
                break;
            case BandItemLayout.RateRows:
                DrawMarkedRows(g, item, bounds, NetworkUp, NetworkDown, rateFont, format, ink, inner);
                break;
            case BandItemLayout.IndicatorRows:
                DrawMarkedRows(g, item, bounds, DiskRead, DiskWrite, rateFont, format, ink, inner);
                break;
            case BandItemLayout.StatusIcon:
                DrawStatusIcon(g, item, bounds, format);
                break;
            default:
                DrawInline(g, item, bounds, valueFont, format, ink, inner);
                break;
        }
    }

    private static void DrawStacked(
        Graphics g,
        BandItem item,
        RectangleF bounds,
        Font valueFont,
        Font labelFont,
        StringFormat format,
        Ink ink)
    {
        var value = item.Values[0].Text;
        var rows = CompactRows(g, bounds, labelFont, valueFont, LabelRowOverlap);
        WriteInBox(g, item.Label, labelFont, format, ink, rows.Top);
        WriteInBox(g, value, valueFont, format, ink, rows.Bottom);
    }

    /// <summary>
    /// The label's letters stand in a column beside a chart of recent load, optionally with the current reading
    /// above the chart. Both take the same width so switching between them does not shift the neighbours.
    /// </summary>
    private static void DrawVerticalLabelGraph(
        Graphics g,
        BandItem item,
        RectangleF bounds,
        Font labelFont,
        StringFormat format,
        Ink ink,
        int inner,
        bool withValue)
    {
        var height = (int)Math.Round(bounds.Height);
        var graphWidth = GraphWidth(height);
        var labelWidth = Math.Max(0f, bounds.Width - inner - graphWidth);

        using var stacked = VerticalFont(labelFont, height, item.Label.Length);
        DrawVerticalLabel(
            g,
            item.Label,
            new RectangleF(bounds.Left, bounds.Top, labelWidth, bounds.Height),
            stacked,
            format,
            ink);

        var right = new RectangleF(bounds.Left + labelWidth + inner, bounds.Top, graphWidth, bounds.Height);
        if (!withValue)
        {
            DrawTrail(g, item.Trail, Plot(right), ink.Graph);
            return;
        }

        using var reading = SizedLike(labelFont, height * GraphValueShareOfHeight);

        var textHeight = Math.Min(bounds.Height * 0.5f, (float)Math.Ceiling(reading.GetHeight(g)));
        WriteInBox(
            g,
            item.Values[0].Text,
            reading,
            format,
            ink,
            new RectangleF(right.Left, right.Top, right.Width, textHeight),
            StringAlignment.Far);
        DrawTrail(
            g,
            item.Trail,
            Plot(new RectangleF(right.Left, right.Top + textHeight, right.Width, right.Height - textHeight)),
            ink.Graph);
    }

    private static void DrawVerticalLabel(
        Graphics g,
        string label,
        RectangleF bounds,
        Font font,
        StringFormat format,
        Ink ink)
    {
        if (label.Length == 0 || bounds.Width <= 0)
        {
            return;
        }

        var lineHeight = (float)Math.Ceiling(font.GetHeight(g));
        var step = Math.Max(1f, lineHeight * LetterStep);
        var top = bounds.Top + ((bounds.Height - LetterStackHeight(lineHeight, label.Length)) / 2f);

        for (var i = 0; i < label.Length; i++)
        {
            WriteInBox(
                g,
                label[i].ToString(),
                font,
                format,
                ink,
                new RectangleF(bounds.Left, top + (step * i), bounds.Width, lineHeight),
                StringAlignment.Center);
        }
    }

    /// <summary>
    /// A filled area under the readings with a brighter line on top, over a faint plate that says where the
    /// chart's floor and ceiling are even when the load has been flat.
    /// </summary>
    private static void DrawTrail(Graphics g, IReadOnlyList<double>? trail, RectangleF plot, Color colour)
    {
        if (plot.Width < 2 || plot.Height < 2)
        {
            return;
        }

        using (var plate = new SolidBrush(Color.FromArgb(38, colour)))
        {
            g.FillRectangle(plate, plot);
        }

        if (trail is null || trail.Count == 0)
        {
            return;
        }

        var points = Curve(trail, plot);
        using var area = new GraphicsPath();
        area.AddLines(points);
        area.AddLine(points[^1].X, plot.Bottom, points[0].X, plot.Bottom);
        area.CloseFigure();

        using (var fill = new SolidBrush(Color.FromArgb(110, colour)))
        {
            g.FillPath(fill, area);
        }

        using var pen = new Pen(colour, Math.Max(1f, plot.Height * 0.06f));
        g.DrawLines(pen, points);
    }

    /// <summary>A single reading is a level rather than a slope, so it is drawn straight across.</summary>
    private static PointF[] Curve(IReadOnlyList<double> trail, RectangleF plot)
    {
        var step = trail.Count == 1 ? 0f : plot.Width / (trail.Count - 1);
        var points = new PointF[trail.Count];
        for (var i = 0; i < trail.Count; i++)
        {
            var share = (float)Math.Clamp(trail[i], 0, 100) / 100f;
            points[i] = new PointF(plot.Left + (step * i), plot.Bottom - (plot.Height * share));
        }

        return trail.Count == 1
            ? [new PointF(plot.Left, points[0].Y), new PointF(plot.Right, points[0].Y)]
            : points;
    }

    /// <summary>The chart keeps a hair of clearance so its outline never touches the band's edges.</summary>
    private static RectangleF Plot(RectangleF area)
    {
        var inset = Math.Max(1f, area.Height * 0.13f);
        return RectangleF.Inflate(area, 0, -inset);
    }

    private static int GraphWidth(int height) => Math.Max(12, (int)Math.Round(height * GraphShareOfHeight));

    private static int VerticalLabelWidth(
        Graphics g,
        BandItem item,
        Font labelFont,
        StringFormat format,
        int height)
    {
        var label = item.LabelYardstick;
        if (label.Length == 0)
        {
            return 0;
        }

        using var stacked = VerticalFont(labelFont, height, label.Length);
        return label.Max(letter => Measure(g, letter.ToString(), stacked, format));
    }

    /// <summary>Letters set one per row have to shrink until the whole word fits the band's height.</summary>
    private static Font VerticalFont(Font model, int height, int letters)
    {
        // A line box is roughly a third taller than the size it was asked for, which is what has to fit.
        var lines = LetterStackHeight(1.32f, letters);
        return SizedLike(model, Math.Min(model.Size, height * 0.94f / lines));
    }

    private static Font SizedLike(Font model, float size)
    {
        var wanted = Math.Max(5f, size);

        try
        {
            return new Font(model.FontFamily, wanted, model.Style, GraphicsUnit.Pixel);
        }
        catch (ArgumentException)
        {
            return new Font(BandTypographySettings.DefaultFontFamily, wanted, model.Style, GraphicsUnit.Pixel);
        }
    }

    /// <summary>Rows overlap so the stack reads as one word rather than three separate letters.</summary>
    private static float LetterStackHeight(float lineHeight, int letters) =>
        lineHeight * (1f + (LetterStep * Math.Max(0, letters - 1)));

    /// <param name="first">Marks the top row; <paramref name="second"/> marks the bottom one.</param>
    private static void DrawMarkedRows(
        Graphics g,
        BandItem item,
        RectangleF bounds,
        Marker first,
        Marker second,
        Font font,
        StringFormat format,
        Ink ink,
        int inner)
    {
        var valueWidth = item.Values.Max(value => Measure(g, value.Template, font, format));
        var markerWidth = MarkerWidth(g, first, second, font, format);
        var rows = CompactRows(g, bounds, font, font, SameFontRowOverlap);

        DrawMarkedRow(g, item.Values[0].Text, first, rows.Top, markerWidth, valueWidth, font, format, ink, inner);
        DrawMarkedRow(g, item.Values[1].Text, second, rows.Bottom, markerWidth, valueWidth, font, format, ink, inner);
    }

    /// <summary>Both markers share a column so the two values line up under each other.</summary>
    private static int MarkerWidth(Graphics g, Marker first, Marker second, Font font, StringFormat format) =>
        Math.Max(Measure(g, first.Text, font, format), Measure(g, second.Text, font, format));

    private static void DrawMarkedRow(
        Graphics g,
        string value,
        Marker marker,
        RectangleF row,
        int markerWidth,
        int valueWidth,
        Font font,
        StringFormat format,
        Ink ink,
        int inner)
    {
        using var brush = new SolidBrush(marker.Colour);
        WriteInBox(
            g,
            marker.Text,
            font,
            format,
            ink with { Foreground = brush },
            new RectangleF(row.Left, row.Top, markerWidth, row.Height));
        WriteInBox(
            g,
            value,
            font,
            format,
            ink,
            new RectangleF(row.Left + markerWidth + inner, row.Top, valueWidth, row.Height));
    }

    /// <summary>The installed application's own icon, or a lettered disc when the icon cannot be read.</summary>
    private static void DrawStatusIcon(Graphics g, BandItem item, RectangleF bounds, StringFormat format)
    {
        var diameter = StatusIconDiameter((int)bounds.Height);
        var circle = new RectangleF(
            bounds.Left,
            bounds.Top + ((bounds.Height - diameter) / 2f),
            diameter,
            diameter);

        var provider = SyncStatusSource.ProviderIdFor(item.Kind);
        var icon = provider is null ? null : ProviderIconResolver.Resolve(provider, diameter);
        if (icon is not null)
        {
            g.DrawImage(icon, circle);
        }
        else
        {
            DrawLetteredDisc(g, item, circle, diameter, format);
        }

        if (item.Badge != BandItemBadge.None)
        {
            DrawStatusBadge(g, circle, diameter, item.Badge);
        }
    }

    private static void DrawLetteredDisc(
        Graphics g,
        BandItem item,
        RectangleF circle,
        int diameter,
        StringFormat format)
    {
        using var fill = new SolidBrush(item.Accent ?? StatusIconInk);
        g.FillEllipse(fill, circle);

        using var letterFont = new Font("Segoe UI", diameter * 0.56f, FontStyle.Bold, GraphicsUnit.Pixel);
        using var letter = new SolidBrush(StatusIconInk);
        using var centred = (StringFormat)format.Clone();
        centred.Alignment = StringAlignment.Center;
        centred.LineAlignment = StringAlignment.Center;
        g.DrawString(item.Label, letterFont, letter, circle, centred);
    }

    /// <summary>The marks are drawn as shapes: a glyph this small turns to mush at band sizes.</summary>
    private static void DrawStatusBadge(Graphics g, RectangleF circle, int iconDiameter, BandItemBadge badge)
    {
        var size = BadgeDiameter(iconDiameter);
        // Perched on the corner so the artwork underneath stays recognisable.
        var bounds = new RectangleF(
            circle.Right - (size * (1f - BadgeOverhangShareOfBadge)),
            circle.Top - (size * 0.24f),
            size,
            size);
        var ring = Math.Max(1f, size * 0.12f);

        using var mark = new SolidBrush(StatusIconInk);
        // The ring keeps the badge readable against both the artwork and the colour it sits on.
        g.FillEllipse(mark, bounds);

        using var background = new SolidBrush(badge == BandItemBadge.Good ? BadgeGood : BadgeAttention);
        g.FillEllipse(background, RectangleF.Inflate(bounds, -ring, -ring));

        var inner = size - (2f * ring);
        var origin = new PointF(bounds.Left + ring, bounds.Top + ring);
        if (badge == BandItemBadge.Good)
        {
            DrawCheckMark(g, origin, inner);
        }
        else
        {
            DrawExclamationMark(g, mark, bounds, origin, size, inner);
        }
    }

    private static void DrawCheckMark(Graphics g, PointF origin, float inner)
    {
        using var pen = new Pen(StatusIconInk, Math.Max(1.5f, inner * 0.17f))
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };

        g.DrawLines(pen,
        [
            new PointF(origin.X + (inner * 0.24f), origin.Y + (inner * 0.52f)),
            new PointF(origin.X + (inner * 0.43f), origin.Y + (inner * 0.71f)),
            new PointF(origin.X + (inner * 0.78f), origin.Y + (inner * 0.30f))
        ]);
    }

    private static void DrawExclamationMark(
        Graphics g,
        Brush mark,
        RectangleF bounds,
        PointF origin,
        float size,
        float inner)
    {
        var stroke = Math.Max(1.5f, inner * 0.2f);
        var left = bounds.Left + ((size - stroke) / 2f);
        g.FillRectangle(mark, left, origin.Y + (inner * 0.17f), stroke, inner * 0.44f);
        g.FillRectangle(mark, left, origin.Y + (inner * 0.72f), stroke, stroke);
    }

    private static float BadgeDiameter(int iconDiameter) => Math.Max(10f, iconDiameter * BadgeShareOfIcon);

    private static int StatusIconDiameter(int height) =>
        Math.Max(10, (int)Math.Round(height * StatusIconShareOfHeight));

    /// <summary>The badge overhang is always reserved, so a state change never shifts the icons beside it.</summary>
    private static int StatusIconWidth(int height)
    {
        var diameter = StatusIconDiameter(height);
        return diameter + (int)Math.Ceiling(BadgeDiameter(diameter) * BadgeOverhangShareOfBadge);
    }

    private static void DrawInline(
        Graphics g,
        BandItem item,
        RectangleF bounds,
        Font font,
        StringFormat format,
        Ink ink,
        int inner)
    {
        var labelWidth = item.Label.Length == 0 ? 0 : Measure(g, item.LabelYardstick, font, format);
        if (labelWidth > 0)
        {
            WriteInBox(
                g,
                item.Label,
                font,
                format,
                ink,
                new RectangleF(bounds.Left, bounds.Top, labelWidth, bounds.Height));
        }

        var x = bounds.Left + labelWidth;
        foreach (var value in item.Values)
        {
            var box = Measure(g, value.Template, font, format);
            x += inner;
            WriteInBox(
                g,
                value.Text,
                font,
                format,
                ink,
                new RectangleF(x, bounds.Top, box, bounds.Height),
                StringAlignment.Far);
            x += box;
        }
    }

    private static void WriteInBox(
        Graphics g,
        string text,
        Font font,
        StringFormat format,
        Ink ink,
        RectangleF bounds,
        StringAlignment alignment = StringAlignment.Near)
    {
        var previous = format.Alignment;
        format.Alignment = alignment;
        Write(g, text, font, format, ink, bounds);
        format.Alignment = previous;
    }

    private static void Write(Graphics g, string text, Font font, StringFormat format, Ink ink, RectangleF bounds)
    {
        if (ink.Shadow is not null)
        {
            g.DrawString(
                text,
                font,
                ink.Shadow,
                new RectangleF(bounds.X + 1, bounds.Y + 1, bounds.Width, bounds.Height),
                format);
        }

        g.DrawString(text, font, ink.Foreground, bounds, format);
    }

    /// <summary>A shadow only helps when it contrasts with the glyphs, so it flips with the chosen text colour.</summary>
    internal static Color ShadowColourFor(Color text) =>
        (0.2126 * text.R) + (0.7152 * text.G) + (0.0722 * text.B) > 140
            ? Color.FromArgb(150, 0, 0, 0)
            : Color.FromArgb(150, 255, 255, 255);

    /// <param name="Colour">The same colour the foreground brush paints, for the shades a graph needs.</param>
    /// <param name="Graph">What the load charts are drawn in, which the user can set apart from the text.</param>
    private readonly record struct Ink(Brush Foreground, Brush? Shadow, Color Colour, Color Graph);

    /// <summary>The letter in front of a rate row, and the colour that tells the two rows apart at a glance.</summary>
    private readonly record struct Marker(string Text, Color Colour);

    private static (RectangleF Top, RectangleF Bottom) CompactRows(
        Graphics g,
        RectangleF bounds,
        Font topFont,
        Font bottomFont,
        float overlapShareOfRow)
    {
        var topHeight = (float)Math.Ceiling(topFont.GetHeight(g));
        var bottomHeight = (float)Math.Ceiling(bottomFont.GetHeight(g));
        // DrawString reserves ascent and descent padding around each glyph run. Overlapping the line boxes removes
        // that padding, but rows sharing one font must overlap less or their glyphs run into each other.
        var overlap = Math.Max(1f, (float)Math.Round(Math.Min(topHeight, bottomHeight) * overlapShareOfRow));
        var groupHeight = topHeight + bottomHeight - overlap;
        var top = bounds.Top + ((bounds.Height - groupHeight) / 2f);

        return (
            new RectangleF(bounds.Left, top, bounds.Width, topHeight),
            new RectangleF(bounds.Left, top + topHeight - overlap, bounds.Width, bottomHeight));
    }

    private static Font CreateFont(BandTypographySettings settings, int surfaceHeight, float sizeFactor)
    {
        var style = (settings.Bold ? FontStyle.Bold : FontStyle.Regular)
            | (settings.Italic ? FontStyle.Italic : FontStyle.Regular);
        var size = Math.Max(5f, surfaceHeight * settings.FontSizePercent / 100f * sizeFactor);

        try
        {
            return new Font(settings.FontFamily, size, style, GraphicsUnit.Pixel);
        }
        catch (ArgumentException)
        {
            return new Font(BandTypographySettings.DefaultFontFamily, size, style, GraphicsUnit.Pixel);
        }
    }

    private static int Measure(Graphics g, string s, Font font, StringFormat format) =>
        (int)Math.Ceiling(g.MeasureString(s, font, PointF.Empty, format).Width);
}
