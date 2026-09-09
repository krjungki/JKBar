// Draws scenery behind a pixel pet with each measured horizon centred vertically in the notch.
using System.Collections.Frozen;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
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

            // Fitting the height maps the crop's centred horizon directly onto the notch's middle row.
            var scale = metrics.Height / (double)picture.Height;
            var drawn = picture.Width * scale;
            if (drawn >= metrics.Width)
            {
                graphics.DrawImage(
                    picture,
                    new RectangleF((float)((metrics.Width - drawn) / 2), 0, (float)drawn, metrics.Height),
                    new RectangleF(0, 0, picture.Width, picture.Height),
                    GraphicsUnit.Pixel);
            }
            else
            {
                using var brush = new TextureBrush(picture, WrapMode.TileFlipX);
                brush.Transform = new Matrix((float)scale, 0, 0, (float)scale, (float)((metrics.Width - drawn) / 2), 0);
                graphics.FillRectangle(brush, 0, 0, metrics.Width, metrics.Height);
            }
        }
        finally
        {
            graphics.Restore(state);
        }
    }

    private static FrozenDictionary<NotchScene, Bitmap> Load()
    {
        var scenes = new Dictionary<NotchScene, Bitmap>();
        foreach (var (scene, resource, horizonY) in new[]
                 {
                     (NotchScene.SummerField, "JKBar.App.Assets.scene-summer-field.png", 68),
                     (NotchScene.SunsetSky, "JKBar.App.Assets.scene-sunset-sky.png", 64),
                     (NotchScene.CloudHill, "JKBar.App.Assets.scene-cloud-hill.png", 70),
                     (NotchScene.WillowLake, "JKBar.App.Assets.scene-willow-lake.png", 72),
                     (NotchScene.TropicalCoast, "JKBar.App.Assets.scene-tropical-coast.png", 70)
                 })
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource);
            if (stream is null) continue;

            // A bitmap built straight from a stream keeps reading from it, so it is copied into one of its own.
            using var loaded = new Bitmap(stream);
            var crop = HorizonCrop.Centered(loaded.Height, horizonY);
            var picture = new Bitmap(loaded.Width, crop.Height, PixelFormat.Format32bppArgb);
            using (var canvas = Graphics.FromImage(picture))
            {
                canvas.DrawImage(
                    loaded,
                    new Rectangle(0, 0, picture.Width, picture.Height),
                    new Rectangle(0, crop.Top, loaded.Width, crop.Height),
                    GraphicsUnit.Pixel);
            }
            scenes[scene] = picture;
        }

        return scenes.ToFrozenDictionary();
    }
}
