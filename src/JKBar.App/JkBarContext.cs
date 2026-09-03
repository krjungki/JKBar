// Hosts the bar and its tray menu. The bar is click-through, so the tray is the only way to reach or quit the app.
using JKBar.Core;
using JKBar.Core.Layout;

namespace JKBar.App;

internal sealed class JkBarContext : ApplicationContext
{
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

        _bar.Show();
        _tray.ContextMenuStrip = BuildMenu();
    }

    /// <summary>
    /// The width candidates are here so the default can be judged on screen rather than argued from numbers.
    /// Nothing is persisted yet; the choice lasts for the session.
    /// </summary>
    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        var width = new ToolStripMenuItem("너비");

        foreach (var (label, value) in WidthChoices())
        {
            var item = new ToolStripMenuItem(label) { Tag = value };
            item.Click += (_, _) =>
            {
                _bar.SetLogicalWidth(value);
                RefreshChecks(width);
            };
            width.DropDownItems.Add(item);
        }

        menu.Items.Add(width);
        menu.Items.Add(BuildAlignmentMenu());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("현재 크기 보기", null, (_, _) => ShowMeasurements());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("종료", null, (_, _) => Quit());

        menu.Opening += (_, _) => RefreshChecks(width);

        return menu;
    }

    private List<(string Label, int Value)> WidthChoices()
    {
        var proportional = NotchMetrics.ProportionalWidth(_bar.LogicalScreenWidth());
        var choices = new List<(string, int)>
        {
            ($"macOS 14\" 값 그대로 ({NotchMetrics.MacBookPro14.Width})", NotchMetrics.MacBookPro14.Width),
            ($"화면 폭의 12.2% ({proportional})", proportional)
        };

        foreach (var step in new[] { 150, 220, 260, 300, 360 })
        {
            choices.Add(($"{step}", step));
        }

        return choices;
    }

    private ToolStripMenuItem BuildAlignmentMenu()
    {
        var menu = new ToolStripMenuItem("위치");

        foreach (var (label, value) in new[]
        {
            ("왼쪽", NotchAlignment.Left),
            ("가운데", NotchAlignment.Centre),
            ("오른쪽", NotchAlignment.Right)
        })
        {
            menu.DropDownItems.Add(new ToolStripMenuItem(label, null, (_, _) => _bar.Align(value)));
        }

        return menu;
    }

    private void RefreshChecks(ToolStripMenuItem width)
    {
        foreach (ToolStripMenuItem item in width.DropDownItems)
        {
            item.Checked = item.Tag is int value && value == _bar.LogicalWidth;
        }
    }

    private void ShowMeasurements()
    {
        var bounds = _bar.CurrentBounds();
        var screen = _bar.LogicalScreenWidth();
        var share = screen > 0 ? _bar.LogicalWidth / (double)screen : 0;

        MessageBox.Show(
            $"논리 크기: {_bar.LogicalWidth} x {NotchMetrics.MacBookPro14.Height}\n"
            + $"실제 픽셀: {bounds.Width} x {bounds.Height}\n"
            + $"화면 폭 대비: {share:P1}  (macOS 기준 12.2%)",
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
