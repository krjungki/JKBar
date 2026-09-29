// Covers wallpaper placement, strip measurement and the adaptive text/opacity choice without touching the desktop.
using System.Drawing;
using JKBar.Core.Layout;
using JKBar.Core.Settings;

namespace JKBar.Core.Tests;

public class AdaptiveAppearanceTests
{
    private static readonly RectangleF Monitor = new(0, 0, 1920, 1080);
    private static readonly RectangleF Desktop = new(0, 0, 3840, 1080);

    [Theory]
    [InlineData(WallpaperFit.Fill, 2000, 750, -480, 0, 2880, 1080)]
    [InlineData(WallpaperFit.Fit, 2000, 1000, 0, 60, 1920, 960)]
    [InlineData(WallpaperFit.Stretch, 600, 900, 0, 0, 1920, 1080)]
    [InlineData(WallpaperFit.Center, 600, 900, 660, 90, 600, 900)]
    [InlineData(WallpaperFit.Tile, 600, 900, 0, 0, 600, 900)]
    public void PlacesTheImageTheWayWindowsFitsIt(
        WallpaperFit fit, float imageWidth, float imageHeight, float x, float y, float width, float height)
    {
        var destination = WallpaperPlacement.Destination(new SizeF(imageWidth, imageHeight), Monitor, Desktop, fit);

        Assert.Equal(new RectangleF(x, y, width, height), destination);
    }

    [Fact]
    public void SpanFillsTheWholeVirtualDesktop()
    {
        var destination = WallpaperPlacement.Destination(new SizeF(3840, 1080), Monitor, Desktop, WallpaperFit.Span);

        Assert.Equal(Desktop, destination);
    }

    [Fact]
    public void LuminanceAndContrastFollowWcag()
    {
        Assert.Equal(1d, ColourContrast.RelativeLuminance(Color.White), 6);
        Assert.Equal(0d, ColourContrast.RelativeLuminance(Color.Black), 6);
        Assert.Equal(21d, ColourContrast.Ratio(1, 0), 6);
        // Mid sRGB grey is far darker than half in linear light; applying the weights to encoded values would say 0.5.
        Assert.InRange(ColourContrast.RelativeLuminance(Color.FromArgb(128, 128, 128)), 0.21, 0.22);
        Assert.Equal(Color.FromArgb(128, 128, 128).ToArgb(), ColourContrast.Mix(Color.White, Color.Black, 0.5).ToArgb());
    }

    [Fact]
    public void FlatStripIsCalmAndDetailedStripIsBusy()
    {
        var flat = Fill(64, 8, Color.FromArgb(40, 90, 160));
        var checker = Checker(64, 8, Color.White, Color.Black);

        var calm = BackdropAnalysis.Analyze(flat, 64, 8);
        var busy = BackdropAnalysis.Analyze(checker, 64, 8);

        Assert.Equal(Color.FromArgb(40, 90, 160).ToArgb(), calm.Mean.ToArgb());
        Assert.Equal(0d, calm.Complexity, 6);
        Assert.Equal(1d, busy.Complexity, 6);
        Assert.Equal(0d, busy.DarkLuminance, 6);
        Assert.Equal(1d, busy.LightLuminance, 6);
    }

    [Fact]
    public void BlurCalmsDetailWithoutChangingTheAverageOrAlpha()
    {
        var pixels = Checker(64, 16, Color.White, Color.Black);
        var before = BackdropAnalysis.Analyze(pixels, 64, 16);

        BackdropAnalysis.Blur(pixels, 64, 16, radius: 3);
        var after = BackdropAnalysis.Analyze(pixels, 64, 16);

        Assert.True(after.Complexity < before.Complexity / 4);
        Assert.InRange(after.Mean.R, 120, 135);
        Assert.All(Enumerable.Range(0, 64 * 16), index => Assert.Equal(255, pixels[index * 4 + 3]));
    }

    [Fact]
    public void SaturationLeavesGreyAloneAndLiftsColour()
    {
        var grey = Fill(1, 1, Color.FromArgb(100, 100, 100));
        var colour = Fill(1, 1, Color.FromArgb(120, 100, 80));

        BackdropAnalysis.Saturate(grey, 1.5);
        BackdropAnalysis.Saturate(colour, 1.5);

        Assert.Equal(new byte[] { 100, 100, 100 }, grey[..3]);
        Assert.True(colour[2] > 120 && colour[0] < 80);
    }

    [Fact]
    public void BrightWallpaperGetsDarkTextAndDarkWallpaperGetsLightText()
    {
        var bright = Analysis(Color.FromArgb(235, 235, 230), complexity: 0);
        var dark = Analysis(Color.FromArgb(20, 30, 50), complexity: 0);

        Assert.Equal(AdaptiveAppearance.DarkText, AdaptiveAppearance.Resolve(bright, Color.White, 25, false, null).Text);
        Assert.Equal(AdaptiveAppearance.LightText, AdaptiveAppearance.Resolve(dark, Color.White, 0, false, null).Text);
    }

    [Fact]
    public void BorderlineWallpaperKeepsTheTextColourAlreadyShown()
    {
        // Close to the luminance where dark and light text read equally well.
        var borderline = Analysis(Color.FromArgb(118, 118, 118), complexity: 0);

        var fromDark = AdaptiveAppearance.Resolve(borderline, Color.Black, 0, false, Showing(AdaptiveAppearance.DarkText));
        var fromLight = AdaptiveAppearance.Resolve(borderline, Color.Black, 0, false, Showing(AdaptiveAppearance.LightText));

        Assert.Equal(AdaptiveAppearance.DarkText, fromDark.Text);
        Assert.Equal(AdaptiveAppearance.LightText, fromLight.Text);
    }

    [Fact]
    public void BusyWallpaperRaisesOpacityFromTheUsersFloor()
    {
        var calm = Analysis(Color.Gray, complexity: 0);
        var busy = Analysis(Color.Gray, complexity: 1);

        Assert.Equal(25, AdaptiveAppearance.Resolve(calm, Color.White, 25, false, null).OpacityPercent);
        Assert.Equal(AdaptiveAppearance.BusiestOpacityPercent,
            AdaptiveAppearance.Resolve(busy, Color.White, 25, false, null).OpacityPercent);
        Assert.Equal(90, AdaptiveAppearance.Resolve(busy, Color.White, 90, false, null).OpacityPercent);
        Assert.Equal(100, AdaptiveAppearance.Resolve(calm, Color.White, 25, opaque: true, null).OpacityPercent);
    }

    [Fact]
    public void AutomaticLookPicksALightFrostOverLightWallpaperAndADarkOneOverDark()
    {
        var bright = AdaptiveAppearance.ResolveAutomatic(Analysis(Color.FromArgb(235, 235, 230), complexity: 0), false, null);
        var dark = AdaptiveAppearance.ResolveAutomatic(Analysis(Color.FromArgb(20, 30, 50), complexity: 0), false, null);

        Assert.Equal(AdaptiveAppearance.LightTint, bright.Tint);
        Assert.Equal(AdaptiveAppearance.DarkText, bright.Text);
        Assert.Equal(AdaptiveAppearance.DarkTint, dark.Tint);
        Assert.Equal(AdaptiveAppearance.LightText, dark.Text);
    }

    [Fact]
    public void AutomaticOpacityIgnoresTheUsersSettingAndFollowsTheWallpaper()
    {
        var calm = Analysis(Color.Gray, complexity: 0);
        var busy = Analysis(Color.Gray, complexity: 1);

        Assert.Equal(AdaptiveAppearance.AutomaticCalmOpacityPercent, AdaptiveAppearance.ResolveAutomatic(calm, false, null).OpacityPercent);
        Assert.Equal(AdaptiveAppearance.AutomaticBusiestOpacityPercent, AdaptiveAppearance.ResolveAutomatic(busy, false, null).OpacityPercent);
        Assert.Equal(100, AdaptiveAppearance.ResolveAutomatic(calm, opaque: true, null).OpacityPercent);
    }

    [Fact]
    public void AutomaticTintKeepsItsSideOnABorderlineWallpaper()
    {
        var borderline = Analysis(Color.FromArgb(118, 118, 118), complexity: 0);
        var fromLight = AdaptiveAppearance.ResolveAutomatic(borderline, false, Showing(AdaptiveAppearance.DarkText) with { Tint = AdaptiveAppearance.LightTint });
        var fromDark = AdaptiveAppearance.ResolveAutomatic(borderline, false, Showing(AdaptiveAppearance.LightText) with { Tint = AdaptiveAppearance.DarkTint });

        Assert.Equal(AdaptiveAppearance.LightTint, fromLight.Tint);
        Assert.Equal(AdaptiveAppearance.DarkTint, fromDark.Tint);
    }

    [Fact]
    public void TransitionEasesTheTint()
    {
        var start = new DateTimeOffset(2026, 9, 29, 12, 0, 0, TimeSpan.Zero);
        var transition = new AppearanceTransition();
        transition.Retarget(new AdaptiveLook(20, AdaptiveAppearance.DarkText, false) { Tint = AdaptiveAppearance.LightTint }, start);
        transition.Retarget(new AdaptiveLook(20, AdaptiveAppearance.LightText, false) { Tint = AdaptiveAppearance.DarkTint }, start);

        var middle = transition.Current(start + AppearanceTransition.Duration / 2)!;
        Assert.InRange(middle.Tint!.Value.R, 100, 160);
    }

    [Fact]
    public void LowWorstCaseContrastTurnsOnTheShadow()
    {
        var even = Analysis(Color.White, complexity: 0);
        var mixed = new BackdropAnalysis(Color.FromArgb(128, 128, 128), 0, 1, 0);

        Assert.False(AdaptiveAppearance.Resolve(even, Color.White, 0, false, null).Shadow);
        Assert.True(AdaptiveAppearance.Resolve(mixed, Color.White, 0, false, null).Shadow);
    }

    [Fact]
    public void TransitionEasesAndClearingIsImmediate()
    {
        var start = new DateTimeOffset(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);
        var dark = new AdaptiveLook(25, AdaptiveAppearance.DarkText, false);
        var light = new AdaptiveLook(65, AdaptiveAppearance.LightText, true);
        var transition = new AppearanceTransition();

        transition.Retarget(dark, start);
        Assert.Equal(dark, transition.Current(start));
        Assert.False(transition.IsRunning(start));

        transition.Retarget(light, start);
        var middle = transition.Current(start + AppearanceTransition.Duration / 2)!;
        Assert.InRange(middle.OpacityPercent, 40, 50);
        Assert.InRange(middle.Text.R, 100, 160);
        Assert.True(transition.IsRunning(start + AppearanceTransition.Duration / 2));
        Assert.Equal(light, transition.Current(start + AppearanceTransition.Duration));

        transition.Retarget(null, start + AppearanceTransition.Duration);
        Assert.Null(transition.Current(start + AppearanceTransition.Duration));
    }

    [Fact]
    public void AdaptiveOptionsDefaultOffAndRoundTrip()
    {
        var defaults = new AppearanceSettings().Normalized();
        Assert.False(defaults.AdaptiveAppearance);
        Assert.False(defaults.BlurredBackdrop);

        var path = Path.Combine(Path.GetTempPath(), $"jkbar-adaptive-{Guid.NewGuid():N}", "settings.json");
        try
        {
            var store = new SettingsStore(path);
            store.Save(new JkBarSettings { Appearance = new AppearanceSettings { AdaptiveAppearance = true, BlurredBackdrop = true } });
            var loaded = store.Load().Appearance;
            Assert.True(loaded.AdaptiveAppearance);
            Assert.True(loaded.BlurredBackdrop);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [Fact]
    public void RemovedFloatingModeReadsAsReserving()
    {
        var path = Path.Combine(Path.GetTempPath(), $"jkbar-overlap-{Guid.NewGuid():N}", "settings.json");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, """{ "Appearance": { "Overlap": 0 } }""");

            Assert.Equal(OverlapMode.ReserveTopEdge, new SettingsStore(path).Load().Appearance.Overlap);
            Assert.False(Enum.IsDefined((OverlapMode)0));
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [Fact]
    public void SplitWallpaperGivesEachSideItsOwnReadableText()
    {
        // Bright stone on the left, deep shade on the right, under the grey 20% tint that exposed the problem.
        var columns = Enumerable.Range(0, 64)
            .Select(x => x < 32 ? Color.FromArgb(230, 225, 215) : Color.FromArgb(40, 34, 30))
            .ToArray();
        var backdrop = new BackdropAnalysis(Color.FromArgb(135, 130, 122), 0.02, 0.75, 0) { Columns = columns };
        var tint = Color.FromArgb(128, 128, 128);

        var look = AdaptiveAppearance.Resolve(backdrop, tint, 20, false, null);

        Assert.Equal(AdaptiveAppearance.RegionCount, look.Regions.Length);
        Assert.All(look.Regions[..16], region => Assert.Equal(AdaptiveAppearance.DarkText, region.Text));
        Assert.All(look.Regions[16..], region => Assert.Equal(AdaptiveAppearance.LightText, region.Text));
        for (var index = 0; index < look.Regions.Length; index++)
        {
            var surface = ColourContrast.Mix(tint, columns[index * 2], look.OpacityPercent / 100d);
            var contrast = ColourContrast.Ratio(
                ColourContrast.RelativeLuminance(surface),
                ColourContrast.RelativeLuminance(look.Regions[index].Text));
            Assert.True(contrast >= AdaptiveAppearance.MinimumContrast, $"region {index}: {contrast:0.00}");
            Assert.False(look.Regions[index].Shadow);
        }
    }

    [Fact]
    public void EachRegionKeepsItsOwnBorderlineChoice()
    {
        var columns = Enumerable.Repeat(Color.FromArgb(118, 118, 118), 4).ToArray();
        var backdrop = new BackdropAnalysis(columns[0], 0.18, 0.18, 0) { Columns = columns };
        var showing = new AdaptiveLook(0, AdaptiveAppearance.DarkText, false)
        {
            Regions =
            [
                new RegionLook(AdaptiveAppearance.DarkText, false),
                new RegionLook(AdaptiveAppearance.LightText, false),
                new RegionLook(AdaptiveAppearance.DarkText, false),
                new RegionLook(AdaptiveAppearance.LightText, false)
            ]
        };

        var look = AdaptiveAppearance.Resolve(backdrop, Color.Black, 0, false, showing);

        Assert.Equal(showing.Regions.Select(region => region.Text), look.Regions.Select(region => region.Text));
        // Neither colour reaches AA on this grey, so every slice also asks for a shadow.
        Assert.All(look.Regions, region => Assert.True(region.Shadow));
    }

    [Fact]
    public void ColumnsAverageEachPixelColumn()
    {
        var pixels = Fill(2, 2, Color.FromArgb(200, 100, 0));
        pixels[4] = 255; pixels[5] = 255; pixels[6] = 255;
        pixels[12] = 55; pixels[13] = 55; pixels[14] = 55;

        var columns = BackdropAnalysis.Analyze(pixels, 2, 2).Columns;

        Assert.Equal(Color.FromArgb(200, 100, 0).ToArgb(), columns[0].ToArgb());
        Assert.Equal(Color.FromArgb(155, 155, 155).ToArgb(), columns[1].ToArgb());
    }

    [Fact]
    public void TransitionEasesEveryRegionAndEqualLooksDoNotAnimate()
    {
        var start = new DateTimeOffset(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);
        AdaptiveLook Look(Color text) => new(25, text, false) { Regions = [new RegionLook(text, false)] };
        var transition = new AppearanceTransition();

        transition.Retarget(Look(AdaptiveAppearance.DarkText), start);
        transition.Retarget(Look(AdaptiveAppearance.DarkText), start);
        Assert.False(transition.IsRunning(start));

        transition.Retarget(Look(AdaptiveAppearance.LightText), start);
        var middle = transition.Current(start + AppearanceTransition.Duration / 2)!;
        Assert.InRange(middle.Regions[0].Text.R, 100, 160);
        Assert.Equal(Look(AdaptiveAppearance.LightText), transition.Current(start + AppearanceTransition.Duration));
    }

    private static AdaptiveLook Showing(Color text) => new(0, text, false);

    private static BackdropAnalysis Analysis(Color mean, double complexity)
    {
        var luminance = ColourContrast.RelativeLuminance(mean);
        return new BackdropAnalysis(mean, luminance, luminance, complexity);
    }

    private static byte[] Fill(int width, int height, Color colour)
    {
        var pixels = new byte[width * height * 4];
        for (var index = 0; index < width * height; index++)
        {
            pixels[index * 4] = colour.B;
            pixels[index * 4 + 1] = colour.G;
            pixels[index * 4 + 2] = colour.R;
            pixels[index * 4 + 3] = 255;
        }

        return pixels;
    }

    private static byte[] Checker(int width, int height, Color first, Color second)
    {
        var pixels = Fill(width, height, first);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if ((x + y) % 2 == 1)
                {
                    var at = (y * width + x) * 4;
                    pixels[at] = second.B;
                    pixels[at + 1] = second.G;
                    pixels[at + 2] = second.R;
                }
            }
        }

        return pixels;
    }
}
