// Hosts the bar and its tray menu. The bar is click-through, so the tray is the only way to reach or quit the app.
using JKBar.Core;
using JKBar.Core.Layout;

namespace JKBar.App;

internal sealed class JkBarContext : ApplicationContext
{
    private const string ProportionalTag = "proportional";

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
        var width = BuildWidthMenu();

        menu.Items.Add(width);
        menu.Items.Add(BuildAlignmentMenu());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("알림 펴짐 미리보기", null, (_, _) => _bar.Announce(TimeSpan.FromSeconds(3)));
        menu.Items.Add("현재 크기 보기", null, (_, _) => ShowMeasurements());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("종료", null, (_, _) => Quit());

        menu.Opening += (_, _) => RefreshChecks(width);

        return menu;
    }

    private ToolStripMenuItem BuildWidthMenu()
    {
        var menu = new ToolStripMenuItem("너비");

        var proportional = new ToolStripMenuItem($"화면 폭의 {NotchMetrics.MacWidthShareOfScreen:P1} (macOS 비율)")
        {
            Tag = ProportionalTag
        };
        proportional.Click += (_, _) => _bar.UseProportionalWidth();
        menu.DropDownItems.Add(proportional);
        menu.DropDownItems.Add(new ToolStripSeparator());

        foreach (var step in new[] { NotchMetrics.MacBookPro14.Width, 150, 220, 260, 300, 360 })
        {
            var value = step;
            var item = new ToolStripMenuItem($"{value} 고정") { Tag = value };
            item.Click += (_, _) => _bar.SetLogicalWidth(value);
            menu.DropDownItems.Add(item);
        }

        return menu;
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
        foreach (var item in width.DropDownItems.OfType<ToolStripMenuItem>())
        {
            item.Checked = item.Tag switch
            {
                string tag when tag == ProportionalTag => _bar.WidthFollowsScreen,
                int value => !_bar.WidthFollowsScreen && value == _bar.LogicalWidth,
                _ => false
            };
        }
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
