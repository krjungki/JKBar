// A small card under a sync provider's icon that spells out what the coloured badge means.
using System.Runtime.Versioning;
using JKBar.App.Interop;
using JKBar.Core.Presentation;
using JKBar.Core.Sync;

namespace JKBar.App;

[SupportedOSPlatform("windows")]
internal sealed class SyncStatusPopup : Form
{
    private const int CsDropShadow = 0x00020000;
    private const int WsExTopmost = 0x00000008;
    private const int LogicalWidthLimit = 420;

    private static readonly TimeSpan HoverDelay = TimeSpan.FromMilliseconds(350);
    private static readonly TimeSpan LeaveDelay = TimeSpan.FromMilliseconds(300);
    private static readonly Color Surface = Color.FromArgb(40, 40, 44);
    private static readonly Color Edge = Color.FromArgb(78, 78, 84);
    private static readonly Color Title = Color.FromArgb(246, 246, 248);
    private static readonly Color Body = Color.FromArgb(196, 196, 204);

    private readonly Func<string, SyncProviderSnapshot?> _snapshotFor;
    private readonly System.Windows.Forms.Timer _tracker = new() { Interval = 100 };

    private string? _providerId;
    private Rectangle _anchor;
    private DateTimeOffset _pointedAt;
    private DateTimeOffset? _leftAt;
    private string _signature = string.Empty;
    private SyncProviderSnapshot _shown;
    private double _scale = 1;
    private Font? _titleFont;
    private Font? _bodyFont;

    internal SyncStatusPopup(Func<string, SyncProviderSnapshot?> snapshotFor)
    {
        _snapshotFor = snapshotFor;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = Surface;
        DoubleBuffered = true;
        Text = "JKBar sync status";
        _tracker.Tick += (_, _) => Track();
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.ClassStyle |= CsDropShadow;
            parameters.ExStyle |= NotchWindowInterop.WsExToolWindow | NotchWindowInterop.WsExNoActivate | WsExTopmost;
            return parameters;
        }
    }

    protected override bool ShowWithoutActivation => true;

    /// <param name="anchor">The icon, in screen coordinates.</param>
    /// <param name="immediate">A click shows the card at once; hovering waits a moment so passing over does not.</param>
    internal void PointAt(BandItemKind kind, Rectangle anchor, bool immediate)
    {
        if (SyncStatusSource.ProviderIdFor(kind) is not { } providerId)
        {
            return;
        }

        _leftAt = null;
        if (providerId == _providerId && anchor == _anchor)
        {
            if (immediate && !Visible)
            {
                Present();
            }

            return;
        }

        _providerId = providerId;
        _anchor = anchor;
        _pointedAt = DateTimeOffset.UtcNow;
        _signature = string.Empty;
        _tracker.Start();

        if (immediate || Visible)
        {
            Present();
        }
    }

    internal void Dismiss()
    {
        _tracker.Stop();
        _providerId = null;
        _anchor = Rectangle.Empty;
        _leftAt = null;
        if (Visible)
        {
            Hide();
        }
    }

    private void Track()
    {
        if (_providerId is null)
        {
            Dismiss();
            return;
        }

        var cursor = Cursor.Position;
        var inside = Rectangle.Inflate(_anchor, 2, 2).Contains(cursor) || (Visible && Bounds.Contains(cursor));
        var now = DateTimeOffset.UtcNow;
        if (!inside)
        {
            _leftAt ??= now;
            if (!Visible || now - _leftAt >= LeaveDelay)
            {
                Dismiss();
            }

            return;
        }

        _leftAt = null;
        if (!Visible)
        {
            if (now - _pointedAt >= HoverDelay)
            {
                Present();
            }

            return;
        }

        Present();
    }

    /// <summary>Re-reads the provider every tick while open, so the card follows a change of state.</summary>
    private void Present()
    {
        if (_providerId is null || _snapshotFor(_providerId) is not { IsVisible: true } snapshot)
        {
            Dismiss();
            return;
        }

        var scale = MonitorScale.At(_anchor.Left + (_anchor.Width / 2), _anchor.Bottom);
        var signature = $"{scale}|{snapshot.State}|{snapshot.Detail}";
        if (signature == _signature && Visible)
        {
            return;
        }

        _signature = signature;
        _shown = snapshot;
        if (scale != _scale || _titleFont is null)
        {
            _scale = scale;
            _titleFont?.Dispose();
            _bodyFont?.Dispose();
            _titleFont = new Font("Segoe UI", (float)(13 * scale), FontStyle.Bold, GraphicsUnit.Pixel);
            _bodyFont = new Font("Segoe UI", (float)(12 * scale), FontStyle.Regular, GraphicsUnit.Pixel);
        }

        var size = Measure();
        var screen = Screen.FromRectangle(_anchor).Bounds;
        var x = _anchor.Left + (_anchor.Width / 2) - (size.Width / 2);
        x = Math.Clamp(x, screen.Left, Math.Max(screen.Left, screen.Right - size.Width));
        var y = _anchor.Bottom + Scaled(4);
        Bounds = new Rectangle(x, y, size.Width, size.Height);
        Invalidate();

        if (!Visible)
        {
            Show();
            NotchWindowInterop.RaiseToTop(Handle);
        }
    }

    private int Scaled(int logical) => (int)Math.Round(logical * _scale);

    private string Heading => $"{SyncProviderCatalog.DisplayName(_shown.ProviderId)}  {SyncStatusSource.StateLabel(_shown.State)}";

    private const TextFormatFlags Flags = TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak | TextFormatFlags.Left;

    private Size Measure()
    {
        var padding = Scaled(12);
        var dot = Scaled(10);
        var limit = Scaled(LogicalWidthLimit);
        var heading = TextRenderer.MeasureText(Heading, _titleFont, new Size(limit, int.MaxValue), Flags);
        var width = dot + Scaled(8) + heading.Width;
        var height = heading.Height + Scaled(6);

        foreach (var line in SyncStatusSource.DetailLines(_shown))
        {
            var measured = TextRenderer.MeasureText(line, _bodyFont, new Size(limit, int.MaxValue), Flags);
            width = Math.Max(width, measured.Width);
            height += measured.Height + Scaled(2);
        }

        return new Size(Math.Min(width, limit) + (padding * 2), height + (padding * 2));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (_titleFont is null || _bodyFont is null)
        {
            return;
        }

        var g = e.Graphics;
        using (var border = new Pen(Edge))
        {
            g.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
        }

        var padding = Scaled(12);
        var limit = Width - (padding * 2);
        var dot = Scaled(10);
        var heading = TextRenderer.MeasureText(Heading, _titleFont, new Size(limit, int.MaxValue), Flags);

        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using (var fill = new SolidBrush(SyncStatusSource.ColourFor(_shown.State)))
        {
            g.FillEllipse(fill, padding, padding + ((heading.Height - dot) / 2), dot, dot);
        }

        var textLeft = padding + dot + Scaled(8);
        TextRenderer.DrawText(
            g, Heading, _titleFont, new Rectangle(textLeft, padding, Width - textLeft - padding, heading.Height), Title, Flags);

        var top = padding + heading.Height + Scaled(6);
        foreach (var line in SyncStatusSource.DetailLines(_shown))
        {
            var measured = TextRenderer.MeasureText(line, _bodyFont, new Size(limit, int.MaxValue), Flags);
            TextRenderer.DrawText(g, line, _bodyFont, new Rectangle(padding, top, limit, measured.Height), Body, Flags);
            top += measured.Height + Scaled(2);
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        Dismiss();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _tracker.Dispose();
            _titleFont?.Dispose();
            _bodyFont?.Dispose();
        }

        base.Dispose(disposing);
    }
}
