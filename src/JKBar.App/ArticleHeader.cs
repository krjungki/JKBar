// A small header bar with the headline, its source, and the two actions the panel offers.
using System.Runtime.Versioning;

namespace JKBar.App;

[SupportedOSPlatform("windows")]
internal sealed class ArticleHeader : Panel
{
    private static readonly Color Separator = Color.FromArgb(230, 230, 235);
    private static readonly Color PrimaryFill = Color.FromArgb(38, 103, 196);
    private static readonly Color PrimaryInk = Color.FromArgb(255, 255, 255);
    private static readonly Color PrimaryEdge = Color.FromArgb(30, 84, 160);
    private static readonly Color QuietFill = Color.FromArgb(240, 240, 244);
    private static readonly Color QuietEdge = Color.FromArgb(206, 206, 213);

    private readonly Label _title;
    private readonly Label _source;
    private readonly ArticleHeaderButton _browser;
    private readonly ArticleHeaderButton _close;

    private double _scale = 1d;

    internal ArticleHeader(Color surface, Color ink, Color muted)
    {
        Dock = DockStyle.Top;
        BackColor = surface;

        _title = new Label { AutoSize = false, ForeColor = ink, AutoEllipsis = true, TextAlign = ContentAlignment.BottomLeft };
        _source = new Label { AutoSize = false, ForeColor = muted, AutoEllipsis = true, TextAlign = ContentAlignment.TopLeft };
        _browser = new ArticleHeaderButton("브라우저로 열기", QuietFill, ink, QuietEdge);
        _close = new ArticleHeaderButton("닫기", PrimaryFill, PrimaryInk, PrimaryEdge);

        Controls.Add(_title);
        Controls.Add(_source);
        Controls.Add(_browser);
        Controls.Add(_close);

        MouseDown += StartDrag;
        _title.MouseDown += StartDrag;
        _source.MouseDown += StartDrag;
    }

    /// <summary>Set by the window: the header is its title bar, so pressing it starts a move.</summary>
    internal Action? Drag;

    private void StartDrag(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            Drag?.Invoke();
        }
    }

    internal event Action? BrowserRequested
    {
        add => _browser.Pressed += value;
        remove => _browser.Pressed -= value;
    }

    internal event Action? CloseRequested
    {
        add => _close.Pressed += value;
        remove => _close.Pressed -= value;
    }

    internal void Describe(string title, string source)
    {
        _title.Text = title;
        _source.Text = source;
    }

    /// <summary>The window is laid out in real pixels, so the chrome has to be scaled by hand.</summary>
    internal void ApplyScale(double scale)
    {
        _scale = scale;
        Height = Scaled(58);
        _title.Font = new Font("Segoe UI Semibold", 10.5f);
        _source.Font = new Font("Segoe UI", 8.5f);
        _browser.ApplyScale(scale);
        _close.ApplyScale(scale);
        PerformLayout();
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);

        var pad = Scaled(16);
        var gap = Scaled(8);

        _close.Location = new Point(Width - pad - _close.Width, (Height - _close.Height) / 2);
        _browser.Location = new Point(_close.Left - gap - _browser.Width, _close.Top);

        var textWidth = Math.Max(1, _browser.Left - gap - pad);
        var half = Height / 2;
        _title.SetBounds(pad, Scaled(6), textWidth, half - Scaled(4));
        _source.SetBounds(pad, half - Scaled(2), textWidth, half - Scaled(6));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var pen = new Pen(Separator);
        e.Graphics.DrawLine(pen, 0, Height - 1, Width, Height - 1);
    }

    private int Scaled(int value) => (int)Math.Round(value * _scale);
}
