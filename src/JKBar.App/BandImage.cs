// Loads the user's band image.
namespace JKBar.App;

internal static class BandImage
{
    /// <summary>
    /// Copied into memory so the file is not left open, which would stop the user replacing or deleting it.
    /// GDI+ reports unreadable image data as ArgumentException or OutOfMemoryException rather than anything
    /// descriptive, so both mean "not a picture we can show" here.
    /// </summary>
    internal static Image? Load(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read);
            using var decoded = Image.FromStream(stream);

            return new Bitmap(decoded);
        }
        catch (Exception e) when (e is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or NotSupportedException
            or OutOfMemoryException)
        {
            return null;
        }
    }
}
