// Shapes text with DirectWrite and composites CPU-analyzed grayscale glyph masks onto the existing GDI+ surface.
using System.Drawing.Imaging;
using System.Globalization;
using System.Runtime.InteropServices;

namespace JKBar.App.Rendering;

internal static class DirectWriteText
{
    private static readonly Lazy<DirectWriteEngine?> Engine = new(CreateEngine);

    internal static bool IsAvailable => Engine.Value is not null;

    internal static float Measure(string text, Font font)
    {
        if (text.Length == 0)
        {
            return 0;
        }

        try
        {
            return Engine.Value?.Measure(text, font) ?? GdiMeasure(text, font);
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            return GdiMeasure(text, font);
        }
    }

    internal static void Draw(
        Graphics graphics,
        string text,
        Font font,
        Color colour,
        RectangleF bounds,
        StringAlignment alignment,
        StringAlignment lineAlignment,
        bool ellipsis)
    {
        if (text.Length == 0 || bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        try
        {
            if (Engine.Value?.Draw(graphics, text, font, colour, bounds, alignment, lineAlignment, ellipsis) == true)
            {
                return;
            }
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
        }

        using var brush = new SolidBrush(colour);
        using var format = StringFormat.GenericTypographic;
        format.Alignment = alignment;
        format.LineAlignment = lineAlignment;
        format.FormatFlags |= StringFormatFlags.NoWrap;
        format.Trimming = ellipsis ? StringTrimming.EllipsisCharacter : StringTrimming.None;
        graphics.DrawString(text, font, brush, bounds, format);
    }

    private static DirectWriteEngine? CreateEngine()
    {
        try
        {
            return new DirectWriteEngine();
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            return null;
        }
    }

    private static float GdiMeasure(string text, Font font)
    {
        using var bitmap = new Bitmap(1, 1);
        using var graphics = Graphics.FromImage(bitmap);
        using var format = StringFormat.GenericTypographic;
        format.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces | StringFormatFlags.NoWrap;
        return graphics.MeasureString(text, font, PointF.Empty, format).Width;
    }

    private static bool IsRecoverable(Exception exception) =>
        exception is COMException or DllNotFoundException or EntryPointNotFoundException or ArgumentException;
}

internal sealed class DirectWriteEngine : IDisposable
{
    private const int NaturalMeasuringMode = 0;
    private const int NaturalSymmetricRenderingMode = 5;
    private const int ClearTypeTexture = 1;
    private const float MaximumLayout = 100_000f;

    private static readonly Guid FactoryId = new("b859ee5a-d838-4b5b-a2e8-1adc7d93db48");
    private IntPtr _factory;
    private bool _disposed;

    internal DirectWriteEngine()
    {
        var factoryId = FactoryId;
        DirectWriteInterop.Check(DirectWriteInterop.DWriteCreateFactory(0, ref factoryId, out _factory));
    }

    internal float Measure(string text, Font font)
    {
        ThrowIfDisposed();
        IntPtr format = IntPtr.Zero;
        IntPtr layout = IntPtr.Zero;

        try
        {
            format = CreateFormat(font, StringAlignment.Near, StringAlignment.Near, ellipsis: false, out _);
            layout = CreateLayout(text, format, MaximumLayout, MaximumLayout);
            DirectWriteInterop.Check(DirectWriteInterop.Method<DirectWriteInterop.GetMetricsDelegate>(layout, 60)(layout, out var metrics));
            return metrics.WidthIncludingTrailingWhitespace;
        }
        finally
        {
            DirectWriteInterop.Release(ref layout);
            DirectWriteInterop.Release(ref format);
        }
    }

    internal bool Draw(
        Graphics graphics,
        string text,
        Font font,
        Color colour,
        RectangleF bounds,
        StringAlignment alignment,
        StringAlignment lineAlignment,
        bool ellipsis)
    {
        ThrowIfDisposed();
        var width = Math.Max(1, (int)Math.Ceiling(bounds.Width));
        var height = Math.Max(1, (int)Math.Ceiling(bounds.Height));
        IntPtr format = IntPtr.Zero;
        IntPtr trimmingSign = IntPtr.Zero;
        IntPtr layout = IntPtr.Zero;

        try
        {
            format = CreateFormat(font, alignment, lineAlignment, ellipsis, out trimmingSign);
            layout = CreateLayout(text, format, width, height);
            var canvas = new AlphaCanvas(this, width, height);
            var context = GCHandle.Alloc(canvas);

            try
            {
                DirectWriteInterop.Check(DirectWriteInterop.Method<DirectWriteInterop.DrawLayoutDelegate>(layout, 58)(
                    layout,
                    GCHandle.ToIntPtr(context),
                    DirectWriteTextCallback.Instance,
                    0,
                    0));
            }
            finally
            {
                context.Free();
            }

            if (!canvas.HasInk)
            {
                return true;
            }

            using var bitmap = canvas.ToBitmap(colour);
            graphics.DrawImageUnscaled(bitmap, (int)Math.Round(bounds.X), (int)Math.Round(bounds.Y));
            return true;
        }
        finally
        {
            DirectWriteInterop.Release(ref layout);
            DirectWriteInterop.Release(ref trimmingSign);
            DirectWriteInterop.Release(ref format);
        }
    }

    internal void AddGlyphRun(AlphaCanvas canvas, float baselineX, float baselineY, IntPtr glyphRun)
    {
        IntPtr analysis = IntPtr.Zero;

        try
        {
            DirectWriteInterop.Check(DirectWriteInterop.Method<DirectWriteInterop.CreateGlyphRunAnalysisDelegate>(_factory, 23)(
                _factory,
                glyphRun,
                1f,
                IntPtr.Zero,
                NaturalSymmetricRenderingMode,
                NaturalMeasuringMode,
                baselineX,
                baselineY,
                out analysis));

            DirectWriteInterop.Check(DirectWriteInterop.Method<DirectWriteInterop.GetAlphaTextureBoundsDelegate>(analysis, 3)(
                analysis,
                ClearTypeTexture,
                out var bounds));

            var width = bounds.Right - bounds.Left;
            var height = bounds.Bottom - bounds.Top;
            if (width <= 0 || height <= 0)
            {
                return;
            }

            var texture = new byte[checked(width * height * 3)];
            DirectWriteInterop.Check(DirectWriteInterop.Method<DirectWriteInterop.CreateAlphaTextureDelegate>(analysis, 4)(
                analysis,
                ClearTypeTexture,
                ref bounds,
                texture,
                (uint)texture.Length));
            canvas.Blend(bounds, texture);
        }
        finally
        {
            DirectWriteInterop.Release(ref analysis);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        DirectWriteInterop.Release(ref _factory);
        _disposed = true;
    }

    private IntPtr CreateFormat(
        Font font,
        StringAlignment alignment,
        StringAlignment lineAlignment,
        bool ellipsis,
        out IntPtr trimmingSign)
    {
        trimmingSign = IntPtr.Zero;
        var weight = font.Bold ? 700 : 400;
        var style = font.Italic ? 2 : 0;
        var locale = CultureInfo.CurrentUICulture.Name;
        DirectWriteInterop.Check(DirectWriteInterop.Method<DirectWriteInterop.CreateTextFormatDelegate>(_factory, 15)(
            _factory,
            font.FontFamily.Name,
            IntPtr.Zero,
            weight,
            style,
            5,
            font.Size,
            locale,
            out var format));

        try
        {
            DirectWriteInterop.Check(DirectWriteInterop.Method<DirectWriteInterop.SetTextFormatPropertyDelegate>(format, 3)(
                format,
                HorizontalAlignment(alignment)));
            DirectWriteInterop.Check(DirectWriteInterop.Method<DirectWriteInterop.SetTextFormatPropertyDelegate>(format, 4)(
                format,
                ParagraphAlignment(lineAlignment)));
            DirectWriteInterop.Check(DirectWriteInterop.Method<DirectWriteInterop.SetTextFormatPropertyDelegate>(format, 5)(format, 1));

            if (ellipsis)
            {
                DirectWriteInterop.Check(DirectWriteInterop.Method<DirectWriteInterop.CreateEllipsisDelegate>(_factory, 20)(
                    _factory,
                    format,
                    out trimmingSign));
                var trimming = new DirectWriteInterop.Trimming { Granularity = 1 };
                DirectWriteInterop.Check(DirectWriteInterop.Method<DirectWriteInterop.SetTrimmingDelegate>(format, 9)(
                    format,
                    ref trimming,
                    trimmingSign));
            }

            return format;
        }
        catch
        {
            DirectWriteInterop.Release(ref trimmingSign);
            DirectWriteInterop.Release(ref format);
            throw;
        }
    }

    private IntPtr CreateLayout(string text, IntPtr format, float width, float height)
    {
        DirectWriteInterop.Check(DirectWriteInterop.Method<DirectWriteInterop.CreateTextLayoutDelegate>(_factory, 18)(
            _factory,
            text,
            (uint)text.Length,
            format,
            width,
            height,
            out var layout));
        return layout;
    }

    private static int HorizontalAlignment(StringAlignment alignment) => alignment switch
    {
        StringAlignment.Far => 1,
        StringAlignment.Center => 2,
        _ => 0
    };

    private static int ParagraphAlignment(StringAlignment alignment) => alignment switch
    {
        StringAlignment.Far => 1,
        StringAlignment.Center => 2,
        _ => 0
    };

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}

internal sealed class AlphaCanvas(DirectWriteEngine engine, int width, int height)
{
    private readonly byte[] _alpha = new byte[checked(width * height)];

    internal bool HasInk { get; private set; }

    internal void AddGlyphRun(float baselineX, float baselineY, IntPtr glyphRun) =>
        engine.AddGlyphRun(this, baselineX, baselineY, glyphRun);

    internal void Blend(DirectWriteInterop.NativeRect bounds, byte[] texture)
    {
        var textureWidth = bounds.Right - bounds.Left;
        var textureHeight = bounds.Bottom - bounds.Top;

        for (var sourceY = 0; sourceY < textureHeight; sourceY++)
        {
            var targetY = bounds.Top + sourceY;
            if ((uint)targetY >= (uint)height)
            {
                continue;
            }

            for (var sourceX = 0; sourceX < textureWidth; sourceX++)
            {
                var targetX = bounds.Left + sourceX;
                if ((uint)targetX >= (uint)width)
                {
                    continue;
                }

                var source = (sourceY * textureWidth + sourceX) * 3;
                var coverage = (texture[source] + texture[source + 1] + texture[source + 2] + 1) / 3;
                if (coverage == 0)
                {
                    continue;
                }

                var target = targetY * width + targetX;
                var existing = _alpha[target];
                _alpha[target] = (byte)(coverage + (existing * (255 - coverage) + 127) / 255);
                HasInk = true;
            }
        }
    }

    internal Bitmap ToBitmap(Color colour)
    {
        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
        var data = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);

        try
        {
            var row = new byte[data.Stride];
            for (var y = 0; y < height; y++)
            {
                Array.Clear(row);
                for (var x = 0; x < width; x++)
                {
                    var coverage = _alpha[y * width + x];
                    var alpha = coverage * colour.A / 255;
                    var offset = x * 4;
                    row[offset] = (byte)(colour.B * alpha / 255);
                    row[offset + 1] = (byte)(colour.G * alpha / 255);
                    row[offset + 2] = (byte)(colour.R * alpha / 255);
                    row[offset + 3] = (byte)alpha;
                }

                Marshal.Copy(row, 0, data.Scan0 + y * data.Stride, row.Length);
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        return bitmap;
    }
}

internal static class DirectWriteTextCallback
{
    private static readonly Guid UnknownId = new("00000000-0000-0000-c000-000000000046");
    private static readonly Guid PixelSnappingId = new("eaf3a2da-ecf4-4d24-b644-b34f6842024b");
    private static readonly Guid TextRendererId = new("ef8a8135-5cc6-45fe-8825-c5a0724eb819");

    private static readonly DirectWriteInterop.QueryInterfaceDelegate QueryInterfaceCallback = QueryInterface;
    private static readonly DirectWriteInterop.ReferenceDelegate AddRefCallback = AddRef;
    private static readonly DirectWriteInterop.ReferenceDelegate ReleaseCallback = Release;
    private static readonly DirectWriteInterop.IsPixelSnappingDisabledDelegate PixelSnappingCallback = IsPixelSnappingDisabled;
    private static readonly DirectWriteInterop.GetCurrentTransformDelegate TransformCallback = GetCurrentTransform;
    private static readonly DirectWriteInterop.GetPixelsPerDipDelegate PixelsPerDipCallback = GetPixelsPerDip;
    private static readonly DirectWriteInterop.DrawGlyphRunCallbackDelegate GlyphRunCallback = DrawGlyphRun;
    private static readonly DirectWriteInterop.DrawDecorationCallbackDelegate UnderlineCallback = DrawDecoration;
    private static readonly DirectWriteInterop.DrawDecorationCallbackDelegate StrikethroughCallback = DrawDecoration;
    private static readonly DirectWriteInterop.DrawInlineObjectCallbackDelegate InlineObjectCallback = DrawInlineObject;

    // DirectWrite may call this shared renderer for the process lifetime. Keeping its tiny unmanaged vtable and
    // instance alive avoids a shutdown race with finalization and keeps every delegate rooted for native calls.
    internal static readonly IntPtr Instance = CreateInstance();

    private static IntPtr CreateInstance()
    {
        var table = Marshal.AllocHGlobal(IntPtr.Size * 10);
        Marshal.WriteIntPtr(table, 0 * IntPtr.Size, Marshal.GetFunctionPointerForDelegate(QueryInterfaceCallback));
        Marshal.WriteIntPtr(table, 1 * IntPtr.Size, Marshal.GetFunctionPointerForDelegate(AddRefCallback));
        Marshal.WriteIntPtr(table, 2 * IntPtr.Size, Marshal.GetFunctionPointerForDelegate(ReleaseCallback));
        Marshal.WriteIntPtr(table, 3 * IntPtr.Size, Marshal.GetFunctionPointerForDelegate(PixelSnappingCallback));
        Marshal.WriteIntPtr(table, 4 * IntPtr.Size, Marshal.GetFunctionPointerForDelegate(TransformCallback));
        Marshal.WriteIntPtr(table, 5 * IntPtr.Size, Marshal.GetFunctionPointerForDelegate(PixelsPerDipCallback));
        Marshal.WriteIntPtr(table, 6 * IntPtr.Size, Marshal.GetFunctionPointerForDelegate(GlyphRunCallback));
        Marshal.WriteIntPtr(table, 7 * IntPtr.Size, Marshal.GetFunctionPointerForDelegate(UnderlineCallback));
        Marshal.WriteIntPtr(table, 8 * IntPtr.Size, Marshal.GetFunctionPointerForDelegate(StrikethroughCallback));
        Marshal.WriteIntPtr(table, 9 * IntPtr.Size, Marshal.GetFunctionPointerForDelegate(InlineObjectCallback));

        var instance = Marshal.AllocHGlobal(IntPtr.Size);
        Marshal.WriteIntPtr(instance, table);
        return instance;
    }

    private static int QueryInterface(IntPtr self, ref Guid interfaceId, out IntPtr result)
    {
        if (interfaceId == UnknownId || interfaceId == PixelSnappingId || interfaceId == TextRendererId)
        {
            result = self;
            return 0;
        }

        result = IntPtr.Zero;
        return unchecked((int)0x80004002);
    }

    private static uint AddRef(IntPtr self) => 2;

    private static uint Release(IntPtr self) => 1;

    private static int IsPixelSnappingDisabled(IntPtr self, IntPtr context, out int disabled)
    {
        disabled = 0;
        return 0;
    }

    private static int GetCurrentTransform(IntPtr self, IntPtr context, out DirectWriteInterop.Matrix transform)
    {
        transform = new DirectWriteInterop.Matrix { M11 = 1, M22 = 1 };
        return 0;
    }

    private static int GetPixelsPerDip(IntPtr self, IntPtr context, out float pixelsPerDip)
    {
        pixelsPerDip = 1;
        return 0;
    }

    private static int DrawGlyphRun(
        IntPtr self,
        IntPtr context,
        float baselineX,
        float baselineY,
        int measuringMode,
        IntPtr glyphRun,
        IntPtr description,
        IntPtr effect)
    {
        try
        {
            if (GCHandle.FromIntPtr(context).Target is AlphaCanvas canvas)
            {
                canvas.AddGlyphRun(baselineX, baselineY, glyphRun);
            }

            return 0;
        }
        catch (Exception exception)
        {
            return Marshal.GetHRForException(exception);
        }
    }

    private static int DrawDecoration(
        IntPtr self,
        IntPtr context,
        float baselineX,
        float baselineY,
        IntPtr decoration,
        IntPtr effect) => 0;

    private static int DrawInlineObject(
        IntPtr self,
        IntPtr context,
        float originX,
        float originY,
        IntPtr inlineObject,
        int isSideways,
        int isRightToLeft,
        IntPtr effect) => 0;
}

internal static class DirectWriteInterop
{
    [DllImport("dwrite.dll", ExactSpelling = true)]
    internal static extern int DWriteCreateFactory(int factoryType, ref Guid interfaceId, out IntPtr factory);

    internal static T Method<T>(IntPtr instance, int index) where T : Delegate
    {
        var table = Marshal.ReadIntPtr(instance);
        return Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(table, index * IntPtr.Size));
    }

    internal static void Check(int result)
    {
        if (result < 0)
        {
            Marshal.ThrowExceptionForHR(result);
        }
    }

    internal static void Release(ref IntPtr instance)
    {
        if (instance == IntPtr.Zero)
        {
            return;
        }

        Marshal.Release(instance);
        instance = IntPtr.Zero;
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    internal delegate int CreateTextFormatDelegate(
        IntPtr self,
        [MarshalAs(UnmanagedType.LPWStr)] string family,
        IntPtr collection,
        int weight,
        int style,
        int stretch,
        float size,
        [MarshalAs(UnmanagedType.LPWStr)] string locale,
        out IntPtr format);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    internal delegate int SetTextFormatPropertyDelegate(IntPtr self, int value);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    internal delegate int CreateEllipsisDelegate(IntPtr self, IntPtr format, out IntPtr trimmingSign);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    internal delegate int SetTrimmingDelegate(IntPtr self, ref Trimming trimming, IntPtr trimmingSign);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    internal delegate int CreateTextLayoutDelegate(
        IntPtr self,
        [MarshalAs(UnmanagedType.LPWStr)] string text,
        uint length,
        IntPtr format,
        float width,
        float height,
        out IntPtr layout);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    internal delegate int GetMetricsDelegate(IntPtr self, out TextMetrics metrics);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    internal delegate int DrawLayoutDelegate(IntPtr self, IntPtr context, IntPtr renderer, float originX, float originY);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    internal delegate int CreateGlyphRunAnalysisDelegate(
        IntPtr self,
        IntPtr glyphRun,
        float pixelsPerDip,
        IntPtr transform,
        int renderingMode,
        int measuringMode,
        float baselineX,
        float baselineY,
        out IntPtr analysis);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    internal delegate int GetAlphaTextureBoundsDelegate(IntPtr self, int textureType, out NativeRect bounds);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    internal delegate int CreateAlphaTextureDelegate(
        IntPtr self,
        int textureType,
        ref NativeRect bounds,
        [Out] byte[] texture,
        uint textureSize);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    internal delegate int QueryInterfaceDelegate(IntPtr self, ref Guid interfaceId, out IntPtr result);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    internal delegate uint ReferenceDelegate(IntPtr self);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    internal delegate int IsPixelSnappingDisabledDelegate(IntPtr self, IntPtr context, out int disabled);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    internal delegate int GetCurrentTransformDelegate(IntPtr self, IntPtr context, out Matrix transform);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    internal delegate int GetPixelsPerDipDelegate(IntPtr self, IntPtr context, out float pixelsPerDip);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    internal delegate int DrawGlyphRunCallbackDelegate(
        IntPtr self,
        IntPtr context,
        float baselineX,
        float baselineY,
        int measuringMode,
        IntPtr glyphRun,
        IntPtr description,
        IntPtr effect);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    internal delegate int DrawDecorationCallbackDelegate(
        IntPtr self,
        IntPtr context,
        float baselineX,
        float baselineY,
        IntPtr decoration,
        IntPtr effect);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    internal delegate int DrawInlineObjectCallbackDelegate(
        IntPtr self,
        IntPtr context,
        float originX,
        float originY,
        IntPtr inlineObject,
        int isSideways,
        int isRightToLeft,
        IntPtr effect);

    [StructLayout(LayoutKind.Sequential)]
    internal struct Trimming
    {
        internal int Granularity;
        internal uint Delimiter;
        internal uint DelimiterCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct TextMetrics
    {
        internal float Left;
        internal float Top;
        internal float Width;
        internal float WidthIncludingTrailingWhitespace;
        internal float Height;
        internal float LayoutWidth;
        internal float LayoutHeight;
        internal uint MaximumBidiReorderingDepth;
        internal uint LineCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeRect
    {
        internal int Left;
        internal int Top;
        internal int Right;
        internal int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Matrix
    {
        internal float M11;
        internal float M12;
        internal float M21;
        internal float M22;
        internal float Dx;
        internal float Dy;
    }
}