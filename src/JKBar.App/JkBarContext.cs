// Hosts the bar, the minimal tray menu, and the unified settings window.
using System.Diagnostics;
using System.Reflection;
using JKBar.App.Diagnostics;
using JKBar.App.Interop;
using JKBar.App.Update;
using JKBar.Core;
using JKBar.Core.Alerts;
using JKBar.Core.Layout;
using JKBar.Core.Settings;
using JKBar.Core.Update;
using Microsoft.Web.WebView2.Core;

namespace JKBar.App;

internal sealed class JkBarContext : ApplicationContext
{
    private static readonly MethodInfo? IconMenuOpener =
        typeof(NotifyIcon).GetMethod("ShowContextMenu", BindingFlags.Instance | BindingFlags.NonPublic);

    private readonly SettingsStore _settingsStore = SettingsStore.ForApp();
    private readonly NotchForm _bar = new();
    private readonly NewsArticleWindow _article = new();
    private readonly NotifyIcon _tray = new();
    private readonly ContextMenuStrip _computerMenu;
    private readonly IntPtr _iconHandle;
    private readonly UpdateService _updates = new();

    // Hourly is often enough for a daily or weekly schedule and costs nothing while the frequency is Never.
    private readonly System.Windows.Forms.Timer _updateClock = new() { Interval = 60 * 60 * 1000 };
    private readonly CancellationTokenSource _shutdown = new();
    private readonly DateTimeOffset _startedUtc = DateTimeOffset.UtcNow;
    private bool _updateRunning;
    private bool _checkedThisRun;
    private JkBarSettings _settings;
    private JkBarSettings? _applied;

    internal JkBarContext()
    {
        _settings = _settingsStore.Load();
        var (icon, handle) = TrayIconFactory.Create();
        _iconHandle = handle;

        _tray.Icon = icon;
        _tray.Text = $"JKBar {BuildInfo.Version}";
        _tray.ContextMenuStrip = BuildTrayMenu();
        _computerMenu = BuildComputerMenu();
        _tray.MouseUp += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                OpenTrayMenu();
            }
        };
        _tray.Visible = true;

        _bar.Show();
        _bar.ImageClicked += OpenComputerMenu;
        _bar.NewsClicked += ShowArticle;
        _bar.ProcessClicked += ProcessActivator.Activate;
        _bar.MenuRequested += OpenNotchMenu;
        _bar.MonitorFellBackToPrimary += RememberPrimaryMonitorFallback;
        _bar.NewsRoomExhausted += TurnNewsOffForRoom;
        _article.OpenInBrowser += OpenUrl;
        _article.LoadBounds = () => _settings.ArticleWindow;
        _article.SaveBounds = RememberArticleWindow;
        AdoptExternalBandImage();
        ApplySettings(_settings, showImageError: false);

        _updateClock.Tick += async (_, _) => await CheckIfDueAsync();
        _updateClock.Start();
        _ = CheckIfDueAsync();
    }

    /// <summary>Nothing is contacted unless the user turned automatic checks on; the default is off.</summary>
    private async Task CheckIfDueAsync()
    {
        var update = _settings.Update.Normalized();
        if (!UpdateSchedule.IsDue(
            update.Check,
            update.LastCheckUtc,
            _startedUtc,
            DateTimeOffset.UtcNow,
            update.CheckOnStartup,
            _checkedThisRun))
        {
            return;
        }

        await CheckForUpdatesAsync(announce: false);
    }

    private async Task CheckForUpdatesAsync(bool announce)
    {
        if (_updateRunning)
        {
            return;
        }

        _updateRunning = true;
        try
        {
            _checkedThisRun = true;
            var outcome = await _updates.RunAsync(announce, _shutdown.Token);
            if (outcome != UpdateOutcome.CheckFailed)
            {
                RememberUpdateCheck();
            }

            if (outcome == UpdateOutcome.Applying)
            {
                Quit();
            }
        }
        catch (OperationCanceledException)
        {
            // The app is closing.
        }
        finally
        {
            _updateRunning = false;
        }
    }

    /// <summary>Only the timestamp is written back, so a settings window open at the same time keeps its edits.</summary>
    private void RememberUpdateCheck()
    {
        var stamped = _settings with
        {
            Update = _settings.Update with { LastCheckUtc = DateTimeOffset.UtcNow }
        };

        if (TrySaveSettings(stamped))
        {
            _applied = stamped.Normalized();
        }
    }

    /// <summary>
    /// The left slot could not hold a headline beside the name and the quote. The news is switched off rather
    /// than left as an unreadable stub, and the user is told so the choice does not look like a fault.
    /// </summary>
    private void TurnNewsOffForRoom()
    {
        if (!_settings.News.Enabled)
        {
            return;
        }

        var without = _settings with { News = _settings.News with { Enabled = false } };
        if (!TrySaveSettings(without))
        {
            return;
        }

        ApplySettings(without, showImageError: false);
        _bar.Notify(new NotchAlert(
            AlertCategory.JkBar,
            "jkbar.news.no.room",
            "뉴스를 껐습니다",
            "제목 자리가 부족합니다. 글꼴을 줄인 뒤 다시 켜세요.",
            AlertSeverity.Warning));
    }

    /// <summary>
    /// Only the panel's own rectangle is written back, so a settings window open at the same time cannot have its
    /// edits overwritten by this.
    /// </summary>
    private void RememberArticleWindow(ArticleWindowSettings bounds)
    {
        if (_settings.ArticleWindow == bounds)
        {
            return;
        }

        _settings = _settings with { ArticleWindow = bounds };

        try
        {
            _settingsStore.Save(_settings);
        }
        catch (IOException)
        {
            // Losing the remembered position is not worth interrupting the user over.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private async void ShowArticle(Rectangle anchor)
    {
        if (_article.Visible)
        {
            _article.Hide();
            return;
        }

        if (_bar.CurrentNews is not { } article)
        {
            return;
        }

        try
        {
            await _article.ShowArticleAsync(article, anchor);
        }
        catch (Exception error) when (error is WebView2RuntimeNotFoundException or InvalidOperationException or IOException)
        {
            // Without the WebView2 runtime there is nothing to read the article in, so hand it to the browser.
            _article.Hide();
            OpenUrl(article.Link);
        }
    }

    private ContextMenuStrip BuildTrayMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add(new ToolStripMenuItem($"JKBar {BuildInfo.Version}") { Enabled = false });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("설정 열기...", null, (_, _) => OpenSettings());
        menu.Items.Add("업데이트 확인...", null, async (_, _) => await CheckForUpdatesAsync(announce: true));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("앱 종료", null, (_, _) => Quit());
        return menu;
    }

    private ContextMenuStrip BuildComputerMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("이 컴퓨터에 대해서", null, (_, _) => OpenSystemInformation());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("컴퓨터 재시작", null, (_, _) => ConfirmPowerAction(ComputerPowerAction.Restart));
        menu.Items.Add("컴퓨터 종료", null, (_, _) => ConfirmPowerAction(ComputerPowerAction.ShutDown));
        return menu;
    }

    private void OpenSettings()
    {
        using var dialog = new SettingsForm(
            _settings,
            _bar.SyncSnapshots,
            PreviewAlert,
            MeasurementText,
            ImportBandImage);
        dialog.Preview += pending => ApplySettings(pending, showImageError: false);

        var result = dialog.ShowDialog();
        if (result == DialogResult.Abort)
        {
            Quit();
            return;
        }

        if (result != DialogResult.OK)
        {
            ApplySettings(_settings, showImageError: false);
            return;
        }

        var updated = dialog.Settings.Normalized();

        // A check that finished while the window was open must not be undone by the snapshot the window started from.
        if (_settings.Update.LastCheckUtc > updated.Update.LastCheckUtc)
        {
            updated = updated with { Update = updated.Update with { LastCheckUtc = _settings.Update.LastCheckUtc } };
        }

        if (TrySaveSettings(updated))
        {
            ApplySettings(updated, showImageError: true);
            _bar.Notify(new NotchAlert(
                AlertCategory.JkBar,
                "jkbar.settings.saved",
                "설정을 적용했습니다",
                Severity: AlertSeverity.Done));
        }
        else
        {
            ApplySettings(_settings, showImageError: false);
        }
    }

    // A unique key each press, so repeated previews are not collapsed as duplicates.
    private void PreviewAlert() => _bar.Notify(new NotchAlert(
        AlertCategory.JkBar,
        $"jkbar.preview.{DateTime.UtcNow.Ticks}",
        "알림 미리보기",
        "활성 알림은 이렇게 표시됩니다",
        AlertSeverity.Done));

    /// <summary>Only the parts that actually changed are pushed, so live previews cannot restart the appbar or the feed.</summary>
    private void ApplySettings(JkBarSettings settings, bool showImageError)
    {
        var normalized = settings.Normalized();
        var appearance = normalized.Appearance;
        var previous = _applied;

        if (previous is null || previous.Appearance.BandColourArgb != appearance.BandColourArgb
            || previous.Appearance.BandOpacityPercent != appearance.BandOpacityPercent
            || previous.BandItems.GraphColourArgb != normalized.BandItems.GraphColourArgb)
        {
            _bar.SetBand(new BandStyle(
                Color.FromArgb(appearance.BandColourArgb),
                appearance.BandOpacityPercent)
            {
                GraphColour = normalized.BandItems.GraphColourArgb is { } graph
                    ? Color.FromArgb(graph)
                    : null
            });
        }

        if (previous is null || previous.Typography != normalized.Typography)
        {
            _bar.SetTypography(normalized.Typography);
        }

        if (previous is null || !BandItemsMatch(previous.BandItems, normalized.BandItems))
        {
            _bar.SetBandItems(normalized.BandItems);
        }

        if (previous is null || !SyncAlertsMatch(previous.SyncAlerts, normalized.SyncAlerts))
        {
            _bar.SetSyncAlerts(normalized.SyncAlerts);
        }

        if (previous is null || previous.Notch != normalized.Notch)
        {
            _bar.SetNotchSettings(normalized.Notch);
        }

        if (previous is null || previous.News != normalized.News)
        {
            _bar.SetNewsSettings(normalized.News);
        }

        if (previous is null || previous.Appearance.ImagePath != appearance.ImagePath)
        {
            ApplyImage(appearance.ImagePath, showImageError);
        }

        if (previous is null || previous.Appearance.ImageScalePercent != appearance.ImageScalePercent)
        {
            _bar.SetImageScale(appearance.ImageScalePercent);
        }

        if (previous is null || previous.Behaviour != normalized.Behaviour)
        {
            _bar.SetBehaviour(normalized.Behaviour);
        }

        if (previous is null || !ProcessWatchMatches(previous.ProcessWatch, normalized.ProcessWatch))
        {
            _bar.SetProcessWatch(normalized.ProcessWatch);
        }

        if (previous is null || !StocksMatch(previous.Stocks, normalized.Stocks))
        {
            _bar.SetStocks(normalized.Stocks);
        }

        if (previous is null || previous.Appearance.Overlap != appearance.Overlap)
        {
            _bar.SetOverlap(appearance.Overlap);
        }

        if (previous is null || previous.Appearance.MonitorDeviceName != appearance.MonitorDeviceName)
        {
            _bar.SetMonitor(appearance.MonitorDeviceName);
        }

        ApplyAutoStart(normalized.Startup.StartWithWindows, previous);

        _applied = normalized;
    }

    /// <summary>The registry can drift while JKBar is closed, so the first apply reconciles it either way.</summary>
    private void ApplyAutoStart(bool wanted, JkBarSettings? previous)
    {
        if (previous is not null && previous.Startup.StartWithWindows == wanted && AutoStart.IsEnabled() == wanted)
        {
            return;
        }

        if (!AutoStart.Set(wanted) && wanted)
        {
            _bar.Notify(new NotchAlert(
                AlertCategory.JkBar,
                "jkbar.autostart.failed",
                "자동 시작을 켜지 못했습니다",
                "레지스트리 쓰기가 거부되었습니다",
                AlertSeverity.Warning));
        }
    }

    /// <summary>
    /// Copies the picture into JKBar's own folder at the size the band draws it. The user is free to move or
    /// delete whatever they picked afterwards, and the setting keeps working.
    /// </summary>
    private string? ImportBandImage(string sourcePath) =>
        BandImageStore.Import(sourcePath, _settingsStore.Folder);

    /// <summary>Settings written before JKBar kept its own copy still point at the user's file; bring it in once.</summary>
    private void AdoptExternalBandImage()
    {
        var path = _settings.Appearance.ImagePath;
        if (string.IsNullOrWhiteSpace(path)
            || BandImageImport.IsImportedCopy(path, _settingsStore.Folder)
            || ImportBandImage(path) is not { } copied)
        {
            return;
        }

        _settings = _settings with { Appearance = _settings.Appearance with { ImagePath = copied } };

        try
        {
            _settingsStore.Save(_settings);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The copy is already in place; it just has to be made again next time.
        }
    }

    private void ApplyImage(string? path, bool showImageError)
    {
        if (path is null)
        {
            _bar.SetDefaultImage();
            return;
        }

        if (_bar.SetImage(path))
        {
            return;
        }

        _bar.SetDefaultImage();
        if (showImageError)
        {
            MessageBox.Show(
                "선택한 이미지를 읽지 못했습니다. 파일 위치와 형식을 확인해 주세요.",
                "JKBar",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private static bool BandItemsMatch(BandItemsSettings first, BandItemsSettings second) =>
        first.Order.SequenceEqual(second.Order)
        && first.Hidden.SequenceEqual(second.Hidden)
        && first.PercentStyles.SequenceEqual(second.PercentStyles)
        && first.GraphColourArgb == second.GraphColourArgb;

    private static bool SyncAlertsMatch(SyncAlertSettings first, SyncAlertSettings second) =>
        first.MutedGoodProviders.SequenceEqual(second.MutedGoodProviders)
        && first.MutedAttentionProviders.SequenceEqual(second.MutedAttentionProviders);

    private static bool ProcessWatchMatches(ProcessWatchSettings first, ProcessWatchSettings second) =>
        first.Items.SequenceEqual(second.Items);

    private static bool StocksMatch(StockWatchSettings first, StockWatchSettings second) =>
        first.Enabled == second.Enabled
        && first.RefreshSeconds == second.RefreshSeconds
        && first.RotationSeconds == second.RotationSeconds
        && first.Items.SequenceEqual(second.Items);

    private bool TrySaveSettings(JkBarSettings settings)
    {
        try
        {
            _settingsStore.Save(settings);
            _settings = settings;
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(
                $"설정을 저장하지 못했습니다.\n\n{exception.Message}",
                "JKBar",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return false;
        }
    }

    private string MeasurementText()
    {
        var actual = _bar.ActualBounds();
        var screen = _bar.LogicalScreenWidth();
        var share = screen > 0 ? _bar.LogicalWidth / (double)screen : 0;

        return $"논리 너비: {_bar.LogicalWidth}\r\n"
            + $"실제 픽셀: {actual.Width} x {actual.Height}\r\n"
            + $"창 상단 y: {actual.Top}  (0이면 패널 끝에 붙음)\r\n"
            + $"화면 폭 대비: {share:P1}  (macOS 기준 {NotchMetrics.MacWidthShareOfScreen:P1})\r\n"
            + $"설정 파일: {_settingsStore.Path}\r\n"
            + $"오류 로그: {CrashLog.Path}";
    }

    private static void OpenUrl(Uri uri)
    {
        try
        {
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            MessageBox.Show(
                $"브라우저에서 링크를 열지 못했습니다.\n\n{exception.Message}",
                "JKBar",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private void OpenTrayMenu()
    {
        if (IconMenuOpener is not null)
        {
            IconMenuOpener.Invoke(_tray, null);
            return;
        }

        _tray.ContextMenuStrip?.Show(Cursor.Position);
    }

    private void OpenNotchMenu() => _tray.ContextMenuStrip?.Show(Cursor.Position);

    private void OpenComputerMenu(Rectangle anchor) =>
        _computerMenu.Show(new Point(anchor.Left, anchor.Bottom));

    private static void OpenSystemInformation()
    {
        try
        {
            using var started = Process.Start(new ProcessStartInfo("msinfo32.exe") { UseShellExecute = true });
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            MessageBox.Show(
                $"시스템 정보를 열지 못했습니다.\n\n{exception.Message}",
                "JKBar",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private static void ConfirmPowerAction(ComputerPowerAction action)
    {
        var restart = action == ComputerPowerAction.Restart;
        var verb = restart ? "다시 시작" : "종료";
        var result = MessageBox.Show(
            $"컴퓨터를 지금 {verb}할까요?\n\n저장하지 않은 작업이 손실될 수 있습니다.",
            "JKBar",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (result != DialogResult.Yes)
        {
            return;
        }

        var command = ComputerPowerCommand.For(action);
        try
        {
            using var started = Process.Start(new ProcessStartInfo(command.FileName, command.Arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true
            });
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            MessageBox.Show(
                $"컴퓨터를 {verb}하지 못했습니다.\n\n{exception.Message}",
                "JKBar",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private void RememberPrimaryMonitorFallback()
    {
        if (_settings.Appearance.MonitorDeviceName.Length == 0)
        {
            return;
        }

        var updated = _settings with
        {
            Appearance = _settings.Appearance with { MonitorDeviceName = string.Empty }
        };
        if (TrySaveSettings(updated))
        {
            ApplySettings(updated, showImageError: false);
        }
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
            _shutdown.Cancel();
            _updateClock.Dispose();
            _updates.Dispose();
            _shutdown.Dispose();
            _computerMenu.Dispose();
            _tray.Dispose();
            TrayIconFactory.Destroy(_iconHandle);
            _article.Dispose();
            _bar.Dispose();
        }

        base.Dispose(disposing);
    }
}
