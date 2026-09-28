// Chooses the band's text colour, opacity and shadow from the wallpaper behind it, and eases between choices.
using System.Drawing;

namespace JKBar.Core.Layout;

public readonly record struct RegionLook(Color Text, bool Shadow);

/// <param name="Text">The choice for the band as a whole; <see cref="Regions"/> refine it left to right.</param>
public sealed record AdaptiveLook(int OpacityPercent, Color Text, bool Shadow)
{
    /// <summary>Equal-width slices of the band, left to right. Empty means <see cref="Text"/> applies everywhere.</summary>
    public RegionLook[] Regions { get; init; } = [];

    public bool Equals(AdaptiveLook? other) =>
        other is not null
        && OpacityPercent == other.OpacityPercent
        && Text == other.Text
        && Shadow == other.Shadow
        && Regions.AsSpan().SequenceEqual(other.Regions);

    public override int GetHashCode() => HashCode.Combine(OpacityPercent, Text, Shadow, Regions.Length);
}

public static class AdaptiveAppearance
{
    public static readonly Color DarkText = Color.FromArgb(0x18, 0x18, 0x1B);
    public static readonly Color LightText = Color.FromArgb(0xF5, 0xF5, 0xF7);

    /// <summary>The most a busy wallpaper can raise the tint to; the user's own opacity is the floor.</summary>
    public const int BusiestOpacityPercent = 85;

    /// <summary>WCAG 2 AA for body text; below it the text gets a shadow.</summary>
    public const double MinimumContrast = 4.5;

    /// <summary>The other text colour has to read this much better before a choice flips, so a borderline strip cannot flicker.</summary>
    public const double SwitchMargin = 1.2;

    /// <summary>About the width of one readout on a wide screen, so each item is judged on what is behind it.</summary>
    public const int RegionCount = 32;

    /// <param name="opaque">Windows transparency effects are off, so the tint is drawn solid.</param>
    /// <param name="current">The look on screen now, whose choices the switch margin protects.</param>
    public static AdaptiveLook Resolve(BackdropAnalysis backdrop, Color tint, int baseOpacity, bool opaque, AdaptiveLook? current)
    {
        baseOpacity = Math.Clamp(baseOpacity, 0, 100);
        var opacity = opaque
            ? 100
            : baseOpacity >= BusiestOpacityPercent
                ? baseOpacity
                : (int)Math.Round(baseOpacity + (BusiestOpacityPercent - baseOpacity) * Math.Clamp(backdrop.Complexity, 0d, 1d));
        var alpha = opacity / 100d;

        var text = Choose(ColourContrast.Mix(tint, backdrop.Mean, alpha), current?.Text);
        var shadow = Worst(
            text,
            ColourContrast.Mix(tint, ColourContrast.GreyOf(backdrop.DarkLuminance), alpha),
            ColourContrast.Mix(tint, ColourContrast.GreyOf(backdrop.LightLuminance), alpha)) < MinimumContrast;

        return new AdaptiveLook(opacity, text, shadow)
        {
            Regions = ResolveRegions(backdrop.Columns, tint, alpha, current?.Regions)
        };
    }

    private static RegionLook[] ResolveRegions(Color[] columns, Color tint, double alpha, RegionLook[]? current)
    {
        var count = Math.Min(RegionCount, columns.Length);
        var regions = new RegionLook[count];
        for (var index = 0; index < count; index++)
        {
            var first = index * columns.Length / count;
            var last = Math.Max(first + 1, (index + 1) * columns.Length / count);
            long red = 0, green = 0, blue = 0;
            var darkest = columns[first];
            var lightest = columns[first];
            for (var column = first; column < last; column++)
            {
                var colour = columns[column];
                red += colour.R;
                green += colour.G;
                blue += colour.B;
                if (ColourContrast.RelativeLuminance(colour) < ColourContrast.RelativeLuminance(darkest))
                {
                    darkest = colour;
                }

                if (ColourContrast.RelativeLuminance(colour) > ColourContrast.RelativeLuminance(lightest))
                {
                    lightest = colour;
                }
            }

            var width = last - first;
            var mean = Color.FromArgb(
                (int)Math.Round(red / (double)width),
                (int)Math.Round(green / (double)width),
                (int)Math.Round(blue / (double)width));
            var previous = current is { Length: > 0 } && current.Length == count ? current[index].Text : (Color?)null;
            var text = Choose(ColourContrast.Mix(tint, mean, alpha), previous);
            var worst = Worst(text, ColourContrast.Mix(tint, darkest, alpha), ColourContrast.Mix(tint, lightest, alpha));
            regions[index] = new RegionLook(text, worst < MinimumContrast);
        }

        return regions;
    }

    private static Color Choose(Color surfaceColour, Color? current)
    {
        var surface = ColourContrast.RelativeLuminance(surfaceColour);
        var darkContrast = ColourContrast.Ratio(surface, ColourContrast.RelativeLuminance(DarkText));
        var lightContrast = ColourContrast.Ratio(surface, ColourContrast.RelativeLuminance(LightText));

        return Same(current, DarkText)
            ? lightContrast > darkContrast * SwitchMargin ? LightText : DarkText
            : Same(current, LightText)
                ? darkContrast > lightContrast * SwitchMargin ? DarkText : LightText
                : darkContrast >= lightContrast ? DarkText : LightText;
    }

    private static double Worst(Color text, Color darkest, Color lightest)
    {
        var textLuminance = ColourContrast.RelativeLuminance(text);
        return Math.Min(
            ColourContrast.Ratio(ColourContrast.RelativeLuminance(darkest), textLuminance),
            ColourContrast.Ratio(ColourContrast.RelativeLuminance(lightest), textLuminance));
    }

    private static bool Same(Color? first, Color second) =>
        first is { } colour && colour.R == second.R && colour.G == second.G && colour.B == second.B;
}

/// <summary>Eases from the look on screen to a new one so a wallpaper change does not snap the text colour.</summary>
public sealed class AppearanceTransition
{
    public static readonly TimeSpan Duration = TimeSpan.FromMilliseconds(250);

    private AdaptiveLook? _from;
    private AdaptiveLook? _to;
    private DateTimeOffset _started;

    public AdaptiveLook? Target => _to;

    /// <summary>Clearing the target takes effect at once: turning the feature off should not fade.</summary>
    public void Retarget(AdaptiveLook? target, DateTimeOffset now)
    {
        if (target == _to)
        {
            return;
        }

        _from = target is null ? null : Current(now) ?? target;
        _to = target;
        _started = now;
    }

    public bool IsRunning(DateTimeOffset now) =>
        _from is not null && _to is not null && _from != _to && now - _started < Duration;

    public AdaptiveLook? Current(DateTimeOffset now)
    {
        if (_from is null || _to is null)
        {
            return _to;
        }

        var progress = Math.Clamp((now - _started) / Duration, 0d, 1d);
        if (progress >= 1)
        {
            return _to;
        }

        var eased = progress * progress * (3 - 2 * progress);
        var from = _from;
        var to = _to;
        return new AdaptiveLook(
            (int)Math.Round(from.OpacityPercent + (to.OpacityPercent - from.OpacityPercent) * eased),
            ColourContrast.Mix(to.Text, from.Text, eased),
            eased < 0.5 ? from.Shadow : to.Shadow)
        {
            // A different slice count (a resized band) has nothing to ease from, so it takes the new slices at once.
            Regions = from.Regions.Length != to.Regions.Length
                ? to.Regions
                : [.. to.Regions.Select((region, index) => new RegionLook(
                    ColourContrast.Mix(region.Text, from.Regions[index].Text, eased),
                    eased < 0.5 ? from.Regions[index].Shadow : region.Shadow))]
        };
    }
}
