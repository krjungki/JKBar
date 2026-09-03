// Hosts the bar and its tray menu. The bar is click-through, so the tray is the only way to reach or quit the app.
using System.Reflection;
using JKBar.Core;
using JKBar.Core.Layout;

namespace JKBar.App;

internal sealed class JkBarContext : ApplicationContext
{
    /// <summary>
    /// The icon opens this itself on right-click but exposes no public way to do the same from the left button.
    /// Borrowing its own method keeps both buttons identical, including how the menu dismisses; a test pins the
    /// name so a future runtime cannot drop it silently.
    /// </summary>
    private static readonly MethodInfo? IconMenuOpener =
        typeof(NotifyIcon).GetMethod("ShowContextMenu", BindingFlags.Instance | BindingFlags.NonPublic);

    private readonly NotchForm _bar = new();
    private readonly NotifyIcon _tray = new();
    private readonly IntPtr _iconHandle;

    internal JkBarContext()
    {
        var (icon, handle) = TrayIconFactory.Create();
        _iconHandle = handle;

        _tray.Icon = icon;
        _tray.Text = $"JKBar {BuildInfo.Version}";
        _tray.Visible = true;
        _tray.MouseUp += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                OpenMenu();
            }
        };

        _bar.Show();
        _tray.ContextMenuStrip = BuildMenu();
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        var overlap = BuildOverlapMenu();
        var band = BuildBandMenu();

        menu.Items.Add(overlap);
        menu.Items.Add(band);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("알림 펴짐 미리보기", null, (_, _) => _bar.Announce(TimeSpan.FromSeconds(3)));
        menu.Items.Add("현재 크기 보기", null, (_, _) => ShowMeasurements());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("종료", null, (_, _) => Quit());

        menu.Opening += (_, _) =>
        {
            RefreshOverlapChecks(overlap);
            RefreshBandChecks(band);
        };

        return menu;
    }

    private ToolStripMenuItem BuildOverlapMenu()
    {
        var menu = new ToolStripMenuItem("겹침");

        foreach (var (label, mode) in new[]
        {
            ("항상 위", OverlapMode.Floating),
            ("자리 예약 (창이 아래에서 시작)", OverlapMode.ReserveTopEdge),
            ("바탕화면에 고정 (창 뒤로)", OverlapMode.PinnedToDesktop)
        })
        {
            var value = mode;
            var item = new ToolStripMenuItem(label) { Tag = value };
            item.Click += (_, _) => _bar.SetOverlap(value);
            menu.DropDownItems.Add(item);
        }

        return menu;
    }

    private ToolStripMenuItem BuildBandMenu()
    {
        var menu = new ToolStripMenuItem("예약 띠");
        var colours = new ToolStripMenuItem("색");

        foreach (var (label, colour) in new[]
        {
            ("검정", Color.Black),
            ("진회색", Color.FromArgb(28, 28, 30)),
            ("흰색", Color.White)
        })
        {
            var value = colour;
            var item = new ToolStripMenuItem(label) { Tag = value };
            item.Click += (_, _) => _bar.SetBand(_bar.Band with { Colour = value });
            colours.DropDownItems.Add(item);
        }

        colours.DropDownItems.Add(new ToolStripSeparator());
        colours.DropDownItems.Add("직접 선택...", null, (_, _) => PickColour());

        var opacity = new ToolStripMenuItem("투명도");
        foreach (var step in new[] { 100, 75, 50, 25, 0 })
        {
            var value = step;
            var item = new ToolStripMenuItem($"{value}%") { Tag = value };
            item.Click += (_, _) => _bar.SetBand(_bar.Band with { OpacityPercent = value });
            opacity.DropDownItems.Add(item);
        }

        menu.DropDownItems.Add(colours);
        menu.DropDownItems.Add(opacity);
        menu.DropDownItems.Add(new ToolStripSeparator());
        menu.DropDownItems.Add("이미지 선택...", null, (_, _) => PickImage());
        menu.DropDownItems.Add("이미지 제거", null, (_, _) => _bar.ClearImage());

        return menu;
    }

    private void PickImage()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "띠 왼쪽에 표시할 이미지",
            Filter = "이미지|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.ico|모든 파일|*.*"
        };

        if (dialog.ShowDialog() != DialogResult.OK)
        {
            return;
        }

        if (!_bar.SetImage(dialog.FileName))
        {
            MessageBox.Show(
                "이 파일은 이미지로 읽을 수 없습니다. 다른 파일을 골라 주세요.",
                "JKBar",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private void PickColour()
    {
        using var dialog = new ColorDialog { Color = _bar.Band.Colour, FullOpen = true, AnyColor = true };
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            _bar.SetBand(_bar.Band with { Colour = dialog.Color });
        }
    }

    private void RefreshOverlapChecks(ToolStripMenuItem overlap)
    {
        foreach (var item in overlap.DropDownItems.OfType<ToolStripMenuItem>())
        {
            item.Checked = item.Tag is OverlapMode mode && mode == _bar.Overlap;
        }
    }

    /// <summary>The band only exists while the edge is reserved, so the menu says so rather than doing nothing.</summary>
    private void RefreshBandChecks(ToolStripMenuItem band)
    {
        band.Enabled = _bar.Overlap == OverlapMode.ReserveTopEdge;

        foreach (var item in band.DropDownItems.OfType<ToolStripMenuItem>().SelectMany(g => g.DropDownItems.OfType<ToolStripMenuItem>()))
        {
            item.Checked = item.Tag switch
            {
                Color colour => colour.ToArgb() == _bar.Band.Colour.ToArgb(),
                int percent => percent == _bar.Band.Opacity,
                _ => false
            };
        }
    }

    private void OpenMenu()
    {
        if (IconMenuOpener is not null)
        {
            IconMenuOpener.Invoke(_tray, null);
            return;
        }

        // Windows needs a foreground window of ours for the menu to dismiss on an outside click; the menu itself
        // is the only one this app has, since the bar refuses activation.
        _tray.ContextMenuStrip?.Show(Cursor.Position);
    }

    private void ShowMeasurements()
    {
        var actual = _bar.ActualBounds();
        var screen = _bar.LogicalScreenWidth();
        var share = screen > 0 ? _bar.LogicalWidth / (double)screen : 0;

        MessageBox.Show(
            $"논리 너비: {_bar.LogicalWidth}\n"
            + $"실제 픽셀: {actual.Width} x {actual.Height}\n"
            + $"창 상단 y: {actual.Top}  (0이면 패널 끝에 붙음)\n"
            + $"화면 폭 대비: {share:P1}  (macOS 기준 {NotchMetrics.MacWidthShareOfScreen:P1})",
            "JKBar",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void Quit()
    {
        _tray.Visible = false;
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _tray.Dispose();
            TrayIconFactory.Destroy(_iconHandle);
            _bar.Dispose();
        }

        base.Dispose(disposing);
    }
}
