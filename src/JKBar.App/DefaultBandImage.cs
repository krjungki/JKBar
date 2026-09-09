// Loads the embedded band image used when the user has not selected one of their own.
using System.Reflection;

namespace JKBar.App;

internal static class DefaultBandImage
{
    private const string ResourceName = "JKBar.App.Assets.default-band-image.png";

    internal static Image? Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName);
        if (stream is null)
        {
            return null;
        }

        using var decoded = Image.FromStream(stream);
        return new Bitmap(decoded);
    }
}