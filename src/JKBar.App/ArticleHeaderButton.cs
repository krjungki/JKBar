// A labelled header action. Glyph-only buttons read as decoration, so both actions carry their own words.
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.Versioning;

namespace JKBar.App;

[SupportedOSPlatform("windows")]
internal sealed class ArticleHeaderButton : Control
{
    private const int Rounding = 6;
    private const int LogicalHeight = 30;
    private const int LogicalPadding = 14;

    private readonly Palette _palette;

    private bool _hovered;
    private bool _pressed;
    private double _scale = 1d;

    /// <param name="fill">Resting fill; the hover and pressed shades are derived from it.</param>
    internal ArticleHeaderButton(string caption, Color fill, Color ink, Color edge)
    {
        _palette = new Palette(fill, Shade(fill, 0.94f), Shade(fill, 0.87f), ink, edge);
        Text = caption;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        Cursor = Cursors.Hand;
    }

    internal event Action? Pressed;

    /// <summary>The window is laid out in real pixels, so the button sizes itself around its own caption.</summary>
    internal void ApplyScale(double scale)
    {
        _scale = scale;
        Font = new Font("Segoe UI Semibold", 9f);

        using var graphics = CreateGraphics();
        var caption = (int)Math.Ceiling(graphics.MeasureString(Text, Font).Width);
        Size = new Size(caption + (Scaled(LogicalPadding) * 2), Scaled(LogicalHeight));
        Invalidate();
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _hovered = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hovered = false;
        _pressed = false;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        _pressed = true;
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        var inside = _pressed && ClientRectangle.Contains(e.Location);
        _pressed = false;
        Invalidate();

        if (inside && e.Button == MouseButtons.Left)
        {
            Pressed?.Invoke();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        var body = new Rectangle(0, 0, Width - 1, Height - 1);
        using var shape = Rounded(body, Scaled(Rounding));
        using var background = new SolidBrush(_pressed ? _palette.Down : _hovered ? _palette.Hover : _palette.Fill);
        e.Graphics.FillPath(background, shape);

        using var border = new Pen(_palette.Edge);
        e.Graphics.DrawPath(border, shape);

        using var ink = new SolidBrush(_palette.Ink);
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            FormatFlags = StringFormatFlags.NoWrap
        };
        e.Graphics.DrawString(Text, Font, ink, ClientRectangle, format);
    }

    private int Scaled(int value) => (int)Math.Round(value * _scale);

    private static GraphicsPath Rounded(Rectangle bounds, int radius)
    {
        var diameter = Math.Max(2, radius * 2);
        var path = new GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();

        return path;
    }

    private static Color Shade(Color colour, float factor) => Color.FromArgb(
        colour.A,
        (int)Math.Clamp(colour.R * factor, 0, 255),
        (int)Math.Clamp(colour.G * factor, 0, 255),
        (int)Math.Clamp(colour.B * factor, 0, 255));

    private readonly record struct Palette(Color Fill, Color Hover, Color Down, Color Ink, Color Edge);
}
