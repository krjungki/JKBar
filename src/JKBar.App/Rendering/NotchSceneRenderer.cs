// Draws scenery behind a pixel pet. Each picture was cut around its horizon so centring it lines the horizon up
// with the middle of the notch.
using System.Collections.Frozen;
using System.Drawing.Drawing2D;
using System.Reflection;
using JKBar.Core.Layout;
using JKBar.Core.Settings;

namespace JKBar.App.Rendering;

internal static class NotchSceneRenderer
{
    private static readonly FrozenDictionary<NotchScene, Bitmap> Scenes = Load();

    internal static bool Has(NotchScene scene) => Scenes.ContainsKey(scene);

    internal static void Paint(Graphics graphics, NotchMetrics metrics, NotchScene scene)
    {
        if (!Scenes.TryGetValue(scene, out var picture) || metrics.Width <= 0 || metrics.Height <= 0) return;

        var state = graphics.Save();
        try
        {
            using var silhouette = NotchRenderer.Silhouette(metrics.Width, metrics.Height, metrics.BottomCornerRadius);
            graphics.SetClip(silhouette, CombineMode.Intersect);
            graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            graphics.PixelOffsetMode = PixelOffsetMode.Half;

            // Scaled to cover rather than to fit, so a notch of any shape is filled and the crop stays centred.
            var scale = Math.Max(metrics.Width / (double)picture.Width, metrics.Height / (double)picture.Height);
            var width = (int)Math.Ceiling(picture.Width * scale);
            var height = (int)Math.Ceiling(picture.Height * scale);
            graphics.DrawImage(picture, new Rectangle((metrics.Width - width) / 2, (metrics.Height - height) / 2, width, height));
        }
        finally
        {
            graphics.Restore(state);
        }
    }

    private static FrozenDictionary<NotchScene, Bitmap> Load()
    {
        var scenes = new Dictionary<NotchScene, Bitmap>();
        foreach (var (scene, resource) in new[]
                 {
                     (NotchScene.SummerField, "JKBar.App.Assets.scene-summer-field.png"),
                     (NotchScene.SunsetSky, "JKBar.App.Assets.scene-sunset-sky.png"),
                     (NotchScene.CloudHill, "JKBar.App.Assets.scene-cloud-hill.png")
                 })
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource);
            if (stream is null) continue;
            scenes[scene] = new Bitmap(stream);
        }

        return scenes.ToFrozenDictionary();
    }
}
