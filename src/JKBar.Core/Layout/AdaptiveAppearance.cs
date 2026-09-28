// Chooses the band's text colour, opacity and shadow from the wallpaper behind it, and eases between choices.
using System.Drawing;

namespace JKBar.Core.Layout;

public sealed record AdaptiveLook(int OpacityPercent, Color Text, bool Shadow);

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

    /// <param name="opaque">Windows transparency effects are off, so the tint is drawn solid.</param>
    /// <param name="current">The text colour on screen now, which the switch margin protects.</param>
    public static AdaptiveLook Resolve(BackdropAnalysis backdrop, Color tint, int baseOpacity, bool opaque, Color? current)
    {
        baseOpacity = Math.Clamp(baseOpacity, 0, 100);
        var opacity = opaque
            ? 100
            : baseOpacity >= BusiestOpacityPercent
                ? baseOpacity
                : (int)Math.Round(baseOpacity + (BusiestOpacityPercent - baseOpacity) * Math.Clamp(backdrop.Complexity, 0d, 1d));
        var alpha = opacity / 100d;

        var surface = ColourContrast.RelativeLuminance(ColourContrast.Mix(tint, backdrop.Mean, alpha));
        var darkContrast = ColourContrast.Ratio(surface, ColourContrast.RelativeLuminance(DarkText));
        var lightContrast = ColourContrast.Ratio(surface, ColourContrast.RelativeLuminance(LightText));

        var text = Same(current, DarkText)
            ? lightContrast > darkContrast * SwitchMargin ? LightText : DarkText
            : Same(current, LightText)
                ? darkContrast > lightContrast * SwitchMargin ? DarkText : LightText
                : darkContrast >= lightContrast ? DarkText : LightText;

        var textLuminance = ColourContrast.RelativeLuminance(text);
        var darkest = ColourContrast.RelativeLuminance(
            ColourContrast.Mix(tint, ColourContrast.GreyOf(backdrop.DarkLuminance), alpha));
        var lightest = ColourContrast.RelativeLuminance(
            ColourContrast.Mix(tint, ColourContrast.GreyOf(backdrop.LightLuminance), alpha));
        var worst = Math.Min(
            ColourContrast.Ratio(darkest, textLuminance),
            ColourContrast.Ratio(lightest, textLuminance));

        return new AdaptiveLook(opacity, text, worst < MinimumContrast);
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
        return new AdaptiveLook(
            (int)Math.Round(_from.OpacityPercent + (_to.OpacityPercent - _from.OpacityPercent) * eased),
            ColourContrast.Mix(_to.Text, _from.Text, eased),
            eased < 0.5 ? _from.Shadow : _to.Shadow);
    }
}
