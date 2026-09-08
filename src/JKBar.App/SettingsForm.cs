// Presents every user-facing JKBar setting in one window.
using JKBar.App.Interop;
using JKBar.App.Stocks;
using JKBar.Core;
using JKBar.Core.Layout;
using JKBar.Core.Presentation;
using JKBar.Core.Settings;
using JKBar.Core.Stocks;
using JKBar.Core.Update;

namespace JKBar.App;

internal sealed class SettingsForm : Form
{
    private static readonly IReadOnlyDictionary<BandItemKind, string> ItemLabels =
        new Dictionary<BandItemKind, string>
        {
            [BandItemKind.Cpu] = "CPU",
            [BandItemKind.Gpu] = "GPU",
            [BandItemKind.Memory] = "RAM",
            [BandItemKind.Disk] = "DISK 읽기/쓰기",
            [BandItemKind.Network] = "NET 송수신",
            [BandItemKind.GlobalSecureAccess] = "Global Secure Access 상태",
            [BandItemKind.OneDrive] = "OneDrive 상태",
            [BandItemKind.Syncthing] = "Syncthing 상태",
            [BandItemKind.Clock] = "시계"
        };

    private readonly ComboBox _overlap = DropDown();
    private readonly ComboBox _idleNotch = DropDown();
    private readonly ComboBox _monitor = DropDown();
    private readonly ComboBox _updateCheck = DropDown();
    private readonly CheckBox _updateOnStartup = new() { Text = "시작할 때도 한 번 확인", AutoSize = true };
    private readonly Button _bandColour = new() { Text = "색 선택...", AutoSize = true };
    private readonly Panel _bandSwatch = new() { Width = 44, Height = 24, BorderStyle = BorderStyle.FixedSingle };

    // Ticks count 5% each, so the slider cannot land on a value the user did not ask for.
    private readonly TrackBar _bandOpacity = new()
    {
        Minimum = 0,
        Maximum = 20,
        TickFrequency = 1,
        SmallChange = 1,
        LargeChange = 2,
        AutoSize = false,
        Width = 280,
        Height = 45
    };

    private readonly Label _bandOpacityValue = new() { AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly TextBox _imagePath = new() { ReadOnly = true, Dock = DockStyle.Fill };
    private readonly Label _fontSummary = new() { AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly Panel _textSwatch = new() { Width = 44, Height = 24, BorderStyle = BorderStyle.FixedSingle };
    private readonly CheckBox _textShadow = new() { Text = "글자 그림자 표시", AutoSize = true };
    private readonly CheckBox _startWithWindows = new() { Text = "Windows에 로그인하면 JKBar를 시작", AutoSize = true };
    private readonly CheckBox _showNowPlaying = new() { Text = "재생 중인 곡을 노치에 계속 표시", AutoSize = true };
    private readonly CheckBox _hideWhenFullscreen = new() { Text = "전체화면 앱이 실행 중이면 숨기고 측정도 멈춤", AutoSize = true };
    private readonly NumericUpDown _metricsRefresh = new() { Minimum = 1, Maximum = 10, Width = 90 };
    // Both sliders count in fives so dragging never lands on a value the user cannot repeat.
    private readonly TrackBar _alertFontSize = new() { Minimum = 2, Maximum = 9, TickFrequency = 1, Width = 220 };
    private readonly Label _alertFontSizeValue = new() { AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly TrackBar _imageScale = new() { Minimum = 6, Maximum = 20, TickFrequency = 2, Width = 220 };
    private readonly Label _imageScaleValue = new() { AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly CheckedListBox _items = new()
    {
        Dock = DockStyle.Fill,
        CheckOnClick = true,
        IntegralHeight = false
    };

    private readonly ComboBox _percentStyle = DropDown();
    private readonly Dictionary<BandItemKind, BandPercentStyle> _percentStyles = [];
    private readonly CheckBox _newsEnabled = new() { Text = "뉴스 표시", AutoSize = true };
    private readonly TextBox _feedUrl = new() { Dock = DockStyle.Fill };
    private readonly NumericUpDown _refreshMinutes = new() { Minimum = 5, Maximum = 1440, Width = 90 };
    private readonly NumericUpDown _rotationSeconds = new() { Minimum = 5, Maximum = 300, Width = 90 };
    private readonly ListView _processes = new()
    {
        Dock = DockStyle.Fill,
        View = View.Details,
        FullRowSelect = true,
        HideSelection = false,
        MultiSelect = false
    };

    private readonly TextBox _processName = new() { Dock = DockStyle.Fill };
    private readonly TextBox _processPath = new() { Dock = DockStyle.Fill };
    private readonly CheckBox _processNameOnly = new()
    {
        Text = "경로 없이 프로세스 이름으로만 찾기",
        AutoSize = true
    };

    private readonly CheckBox _stocksEnabled = new() { Text = "주식 표시", AutoSize = true };
    private readonly TextBox _stockQuery = new() { Dock = DockStyle.Fill };
    private readonly ComboBox _stockMatches = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Dock = DockStyle.Fill,
        Enabled = false
    };

    private readonly ListView _stocks = new()
    {
        Dock = DockStyle.Fill,
        View = View.Details,
        FullRowSelect = true,
        HideSelection = false,
        MultiSelect = false
    };

    private readonly NumericUpDown _stockRefresh = new() { Minimum = 10, Maximum = 600, Increment = 10, Width = 90 };
    private readonly NumericUpDown _stockRotation = new() { Minimum = 3, Maximum = 60, Width = 90 };
    private readonly NaverStockClient _stockClient = new();
    private readonly Action _previewAlert;
    private readonly Func<string> _measurements;
    private readonly Func<string, string?> _importImage;
    private readonly JkBarSettings _original;

    private BandTypographySettings _typography;
    private Color _selectedBandColour;
    private bool _suspendPreview = true;

    /// <summary>Raised while editing so the bar shows the pending values before they are confirmed.</summary>
    internal event Action<JkBarSettings>? Preview;

    internal SettingsForm(
        JkBarSettings settings,
        Action previewAlert,
        Func<string> measurements,
        Func<string, string?> importImage)
    {
        _previewAlert = previewAlert;
        _importImage = importImage;
        _measurements = measurements;

        var normalized = settings.Normalized();
        _original = normalized;
        _typography = normalized.Typography;
        _selectedBandColour = Color.FromArgb(normalized.Appearance.BandColourArgb);

        Text = $"JKBar {BuildInfo.Version} 설정";
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimumSize = new Size(680, 800);
        ClientSize = new Size(760, 900);
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.Dpi;

        AddOption(_overlap, "항상 위", OverlapMode.Floating);
        AddOption(_overlap, "자리 예약 (창이 아래에서 시작)", OverlapMode.ReserveTopEdge);
        AddOption(_overlap, "바탕화면에 고정 (창 뒤로)", OverlapMode.PinnedToDesktop);
        SelectOption(_overlap, normalized.Appearance.Overlap);

        AddOption(_idleNotch, "비우기", IdleNotchContent.Empty);
        AddOption(_idleNotch, "날짜 및 시간", IdleNotchContent.DateTime);
        AddOption(_idleNotch, "픽셀 동물 - 개", IdleNotchContent.Dog);
        AddOption(_idleNotch, "픽셀 동물 - 고양이", IdleNotchContent.Cat);
        AddOption(_idleNotch, "픽셀 동물 - 팬더", IdleNotchContent.Panda);
        SelectOption(_idleNotch, normalized.Notch.IdleContent);

        LoadMonitors(normalized.Appearance.MonitorDeviceName);

        AddOption(_updateCheck, "확인하지 않음", UpdateCheckFrequency.Never);
        AddOption(_updateCheck, "매일", UpdateCheckFrequency.Daily);
        AddOption(_updateCheck, "매주", UpdateCheckFrequency.Weekly);
        SelectOption(_updateCheck, normalized.Update.Check);
        _updateOnStartup.Checked = normalized.Update.CheckOnStartup;

        _bandSwatch.BackColor = _selectedBandColour;
        _bandOpacity.Value = Math.Clamp((int)Math.Round(normalized.Appearance.BandOpacityPercent / 5d), 0, 20);
        _bandOpacity.ValueChanged += (_, _) =>
        {
            UpdateOpacitySummary();
            RaisePreview();
        };
        UpdateOpacitySummary();
        _alertFontSize.Value = Math.Clamp((int)Math.Round(normalized.Notch.AlertFontSizePercent / 5d), 2, 9);
        _alertFontSize.ValueChanged += (_, _) =>
        {
            UpdateAlertFontSummary();
            RaisePreview();
        };
        UpdateAlertFontSummary();
        _imageScale.Value = Math.Clamp((int)Math.Round(normalized.Appearance.ImageScalePercent / 5d), 6, 20);
        _imageScale.ValueChanged += (_, _) =>
        {
            UpdateImageScaleSummary();
            RaisePreview();
        };
        UpdateImageScaleSummary();
        _imagePath.Text = normalized.Appearance.ImagePath ?? string.Empty;
        _textShadow.Checked = normalized.Typography.TextShadow;
        _startWithWindows.Checked = normalized.Startup.StartWithWindows;
        _showNowPlaying.Checked = normalized.Notch.ShowNowPlaying;
        _hideWhenFullscreen.Checked = normalized.Behaviour.HideWhenFullscreen;
        _metricsRefresh.Value = normalized.Behaviour.MetricsRefreshSeconds;
        UpdateFontSummary();

        foreach (var kind in normalized.BandItems.Order)
        {
            _items.Items.Add(new ItemEntry(kind, ItemLabels[kind]), normalized.BandItems.IsVisible(kind));
        }
        if (_items.Items.Count > 0)
        {
            _items.SelectedIndex = 0;
        }

        foreach (var kind in BandItemsSettings.StyleableKinds)
        {
            _percentStyles[kind] = normalized.BandItems.StyleFor(kind);
        }

        AddOption(_percentStyle, "이름과 숫자", BandPercentStyle.LabelAndValue);
        AddOption(_percentStyle, "세로 이름과 그래프", BandPercentStyle.VerticalLabelGraph);
        AddOption(_percentStyle, "세로 이름, 숫자와 그래프", BandPercentStyle.VerticalLabelValueGraph);
        ShowPercentStyleOfSelection();

        _newsEnabled.Checked = normalized.News.Enabled;
        _feedUrl.Text = normalized.News.FeedUrl;
        _refreshMinutes.Value = normalized.News.RefreshMinutes;
        _rotationSeconds.Value = normalized.News.RotationSeconds;

        foreach (var watched in normalized.ProcessWatch.Items)
        {
            AddProcessRow(watched);
        }

        _stocksEnabled.Checked = normalized.Stocks.Enabled;
        _stockRefresh.Value = normalized.Stocks.RefreshSeconds;
        _stockRotation.Value = normalized.Stocks.RotationSeconds;
        foreach (var stock in normalized.Stocks.Items)
        {
            AddStockRow(stock);
        }

        _bandColour.Click += (_, _) => ChooseBandColour();
        Controls.Add(BuildRoot());
        FormClosing += ValidateBeforeClose;

        _overlap.SelectedIndexChanged += (_, _) => RaisePreview();
        _idleNotch.SelectedIndexChanged += (_, _) => RaisePreview();
        _monitor.SelectedIndexChanged += (_, _) => RaisePreview();
        _updateCheck.SelectedIndexChanged += (_, _) => RaisePreview();
        _updateOnStartup.CheckedChanged += (_, _) => RaisePreview();
        _startWithWindows.CheckedChanged += (_, _) => RaisePreview();
        _showNowPlaying.CheckedChanged += (_, _) => RaisePreview();
        _hideWhenFullscreen.CheckedChanged += (_, _) => RaisePreview();
        _metricsRefresh.ValueChanged += (_, _) => RaisePreview();
        _textShadow.CheckedChanged += (_, _) => RaisePreview();
        _newsEnabled.CheckedChanged += (_, _) => RaisePreview();
        _refreshMinutes.ValueChanged += (_, _) => RaisePreview();
        _rotationSeconds.ValueChanged += (_, _) => RaisePreview();
        _stocksEnabled.CheckedChanged += (_, _) => RaisePreview();
        _stockRefresh.ValueChanged += (_, _) => RaisePreview();
        _stockRotation.ValueChanged += (_, _) => RaisePreview();

        // Previewing on every keystroke would refetch the feed, so the address waits until focus leaves.
        _feedUrl.Validated += (_, _) => RaisePreview();

        // ItemCheck runs before the box records the new state, so the preview waits for the pending update.
        _items.ItemCheck += (_, _) => BeginInvoke(RaisePreview);
        _items.SelectedIndexChanged += (_, _) => ShowPercentStyleOfSelection();
        _percentStyle.SelectedIndexChanged += (_, _) => TakePercentStyleForSelection();

        _suspendPreview = false;
    }

    private void RaisePreview()
    {
        if (!_suspendPreview)
        {
            Preview?.Invoke(Settings);
        }
    }

    /// <summary>Built from the settings this window was opened with, so values it does not edit survive a save.</summary>
    internal JkBarSettings Settings => _original with
    {
        Appearance = new AppearanceSettings
        {
            Overlap = SelectedValue(_overlap, OverlapMode.ReserveTopEdge),
            BandColourArgb = _selectedBandColour.ToArgb(),
            BandOpacityPercent = _bandOpacity.Value * 5,
            ImagePath = string.IsNullOrWhiteSpace(_imagePath.Text) ? null : _imagePath.Text,
            ImageScalePercent = _imageScale.Value * 5,
            MonitorDeviceName = SelectedMonitor()
        },
        Notch = new NotchSettings
        {
            IdleContent = SelectedValue(_idleNotch, IdleNotchContent.Empty),
            AlertFontSizePercent = _alertFontSize.Value * 5,
            ShowNowPlaying = _showNowPlaying.Checked
        },
        Startup = new StartupSettings { StartWithWindows = _startWithWindows.Checked },
        Behaviour = new BehaviourSettings
        {
            HideWhenFullscreen = _hideWhenFullscreen.Checked,
            MetricsRefreshSeconds = (int)_metricsRefresh.Value
        },
        Typography = _typography with { TextShadow = _textShadow.Checked },
        BandItems = ReadBandItems(),
        ProcessWatch = ReadProcessWatch(),
        Stocks = ReadStocks(),
        Update = _original.Update with
        {
            Check = SelectedValue(_updateCheck, UpdateCheckFrequency.Never),
            CheckOnStartup = _updateOnStartup.Checked
        },
        News = new NewsSettings
        {
            Enabled = _newsEnabled.Checked,
            FeedUrl = _feedUrl.Text.Trim(),
            RefreshMinutes = (int)_refreshMinutes.Value,
            RotationSeconds = (int)_rotationSeconds.Value
        }
    };

    private Control BuildRoot()
    {
        var title = new Label
        {
            Text = "JKBar",
            Font = new Font("Segoe UI", 18, FontStyle.Bold, GraphicsUnit.Point),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 2)
        };
        var version = new Label
        {
            Text = $"버전 {BuildInfo.Version}",
            AutoSize = true,
            ForeColor = SystemColors.GrayText
        };
        var header = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            Padding = new Padding(4, 0, 0, 8)
        };
        header.Controls.Add(title);
        header.Controls.Add(version);

        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(Page("기본", BuildGeneralTab()));
        tabs.TabPages.Add(Page("Bar 및 항목", BuildBandTab()));
        tabs.TabPages.Add(Page("뉴스", BuildNewsTab()));
        tabs.TabPages.Add(Page("프로세스 리스트", BuildProcessTab()));
        tabs.TabPages.Add(Page("주식", BuildStockTab()));
        tabs.TabPages.Add(Page("진단", BuildDiagnosticsTab()));

        var save = new Button { Text = "확인", DialogResult = DialogResult.OK, AutoSize = true };
        var cancel = new Button { Text = "취소", DialogResult = DialogResult.Cancel, AutoSize = true };
        var quit = new Button { Text = "JKBar 종료", DialogResult = DialogResult.Abort, AutoSize = true };
        var actions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, AutoSize = true };
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        actions.Controls.Add(quit, 0, 0);
        actions.Controls.Add(cancel, 1, 0);
        actions.Controls.Add(save, 2, 0);

        AcceptButton = save;
        CancelButton = cancel;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            ColumnCount = 1,
            RowCount = 3
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.Controls.Add(header, 0, 0);
        root.Controls.Add(tabs, 0, 1);
        root.Controls.Add(actions, 0, 2);
        return root;
    }

    private Control BuildGeneralTab()
    {
        var layout = FormGrid();
        AddRow(layout, "모니터", _monitor);
        AddRow(layout, "겹침 방식", _overlap);
        AddRow(layout, "평상시 노치", _idleNotch);
        AddRow(layout, string.Empty, _showNowPlaying);
        AddRow(layout, "알림 글자 크기", SliderRow(_alertFontSize, _alertFontSizeValue));
        AddRow(layout, "자동 시작", _startWithWindows);
        AddRow(layout, "전체화면", _hideWhenFullscreen);
        AddRow(layout, "지표 갱신(초)", _metricsRefresh);
        AddRow(layout, "업데이트 확인", _updateCheck);
        AddRow(layout, string.Empty, _updateOnStartup);
        AddFiller(layout);
        return layout;
    }

    private Control BuildBandTab()
    {
        var colorRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        colorRow.Controls.Add(_bandSwatch);
        colorRow.Controls.Add(_bandColour);

        var imageButtons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        var chooseImage = new Button { Text = "이미지 선택...", AutoSize = true };
        var clearImage = new Button { Text = "제거", AutoSize = true };
        chooseImage.Click += (_, _) => ChooseImage();
        clearImage.Click += (_, _) => _imagePath.Clear();
        imageButtons.Controls.Add(chooseImage);
        imageButtons.Controls.Add(clearImage);

        var fontRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        var chooseFont = new Button { Text = "글꼴 및 글자색...", AutoSize = true };
        chooseFont.Click += (_, _) => ChooseTypography();
        _textSwatch.Margin = new Padding(3, 3, 10, 3);
        fontRow.Controls.Add(_fontSummary);
        fontRow.Controls.Add(_textSwatch);
        fontRow.Controls.Add(chooseFont);

        var moveUp = new Button { Text = "↑", Width = 40, Height = 36 };
        var moveDown = new Button { Text = "↓", Width = 40, Height = 36 };
        moveUp.Click += (_, _) => MoveSelected(-1);
        moveDown.Click += (_, _) => MoveSelected(1);
        var tools = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true
        };
        tools.Controls.Add(moveUp);
        tools.Controls.Add(moveDown);
        var tooltips = new ToolTip();
        tooltips.SetToolTip(moveUp, "위로 이동");
        tooltips.SetToolTip(moveDown, "아래로 이동");

        var itemLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        itemLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        itemLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        itemLayout.Controls.Add(_items, 0, 0);
        itemLayout.Controls.Add(tools, 1, 0);

        var opacityRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        _bandOpacityValue.Margin = new Padding(6, 14, 3, 3);
        opacityRow.Controls.Add(_bandOpacity);
        opacityRow.Controls.Add(_bandOpacityValue);

        var layout = FormGrid();
        AddRow(layout, "Bar 색", colorRow);
        AddRow(layout, "Bar 투명도", opacityRow);
        AddRow(layout, "사용자 이미지", _imagePath);
        AddRow(layout, string.Empty, imageButtons);
        AddRow(layout, "이미지 크기", SliderRow(_imageScale, _imageScaleValue));
        AddRow(layout, "글꼴", fontRow);
        AddRow(layout, string.Empty, _textShadow);
        AddRow(layout, "선택한 항목 표시 형식", _percentStyle);
        AddRow(layout, "오른쪽 표시 항목", itemLayout, fill: true);
        return layout;
    }

    private void UpdateOpacitySummary() => _bandOpacityValue.Text = $"{_bandOpacity.Value * 5}%";

    private void UpdateAlertFontSummary() => _alertFontSizeValue.Text = $"{_alertFontSize.Value * 5}%";

    private void UpdateImageScaleSummary() => _imageScaleValue.Text = $"{_imageScale.Value * 5}%";

    private Control BuildNewsTab()
    {
        var layout = FormGrid();
        AddRow(layout, string.Empty, _newsEnabled);
        AddRow(layout, "피드 주소", _feedUrl);
        AddRow(layout, "피드 갱신(분)", _refreshMinutes);
        AddRow(layout, "기사 전환(초)", _rotationSeconds);
        AddFiller(layout);
        return layout;
    }

    /// <summary>
    /// The watch list. A path is asked for as well as a name because the icon is read from the file: a running
    /// process cannot be asked for its own image without rights JKBar does not take.
    /// </summary>
    /// <summary>
    /// The watch list. An executable can be pointed at so its icon is read from the file, or the entry can go by
    /// process name alone, in which case whichever copy is running supplies the icon.
    /// </summary>
    private Control BuildProcessTab()
    {
        _processes.Columns.Add("이름", 150);
        _processes.Columns.Add("경로", 290);
        _processes.Columns.Add("찾는 방식", 80);
        _processes.SelectedIndexChanged += (_, _) =>
        {
            if (_processes.SelectedItems.Count == 0)
            {
                return;
            }

            _processName.Text = _processes.SelectedItems[0].Text;
            _processPath.Text = _processes.SelectedItems[0].SubItems[1].Text;
            _processNameOnly.Checked = _processPath.Text.Length == 0;
        };

        _processNameOnly.CheckedChanged += (_, _) =>
        {
            _processPath.Enabled = !_processNameOnly.Checked;
            if (_processNameOnly.Checked)
            {
                _processPath.Clear();
            }
        };

        var browse = new Button { Text = "실행 파일 선택...", AutoSize = true };
        var add = new Button { Text = "추가 / 수정", AutoSize = true };
        var remove = new Button { Text = "제거", AutoSize = true };
        browse.Click += (_, _) => BrowseForProcess();
        add.Click += (_, _) => AddOrUpdateProcess();
        remove.Click += (_, _) => RemoveSelectedProcess();

        var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        buttons.Controls.Add(browse);
        buttons.Controls.Add(add);
        buttons.Controls.Add(remove);

        var layout = FormGrid();
        AddRow(layout, "프로세스 이름", _processName);
        AddRow(layout, string.Empty, _processNameOnly);
        AddRow(layout, "실행 파일 경로", _processPath);
        AddRow(layout, string.Empty, buttons);
        AddRow(layout, "감시 목록", _processes, fill: true);
        return layout;
    }

    private void BrowseForProcess()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "실행 중인지 확인할 프로그램",
            Filter = "실행 파일|*.exe|모든 파일|*.*"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _processNameOnly.Checked = false;
        _processPath.Text = dialog.FileName;
        _processName.Text = System.IO.Path.GetFileName(dialog.FileName);
    }

    private void AddOrUpdateProcess()
    {
        var entry = new WatchedProcess { Name = _processName.Text, Path = _processPath.Text }.Normalized();
        if (entry.MatchKey.Length == 0)
        {
            MessageBox.Show(
                "프로세스 이름을 입력하거나 실행 파일을 선택해 주세요.",
                "JKBar",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        var existing = _processes.Items
            .Cast<ListViewItem>()
            .FirstOrDefault(item => Key(item).Equals(entry.MatchKey, StringComparison.OrdinalIgnoreCase));

        if (existing is null && _processes.Items.Count >= ProcessWatchSettings.MaximumItems)
        {
            MessageBox.Show(
                $"감시 목록은 최대 {ProcessWatchSettings.MaximumItems}개까지 둘 수 있습니다.",
                "JKBar",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        if (existing is null)
        {
            AddProcessRow(entry);
        }
        else
        {
            existing.Text = entry.Name;
            existing.SubItems[1].Text = entry.Path;
            existing.SubItems[2].Text = MatchLabel(entry);
        }

        _processName.Clear();
        _processPath.Clear();
        _processNameOnly.Checked = false;
        RaisePreview();
    }

    private void AddProcessRow(WatchedProcess entry)
    {
        var row = _processes.Items.Add(entry.Name);
        row.SubItems.Add(entry.Path);
        row.SubItems.Add(MatchLabel(entry));
    }

    private static string MatchLabel(WatchedProcess entry) => entry.ByNameOnly ? "이름만" : "경로";

    private void RemoveSelectedProcess()
    {
        if (_processes.SelectedItems.Count == 0)
        {
            return;
        }

        _processes.Items.Remove(_processes.SelectedItems[0]);
        RaisePreview();
    }

    private ProcessWatchSettings ReadProcessWatch() => new()
    {
        Items = [.. _processes.Items
            .Cast<ListViewItem>()
            .Select(item => new WatchedProcess { Name = item.Text, Path = item.SubItems[1].Text })]
    };

    private static string Key(ListViewItem item) =>
        new WatchedProcess { Name = item.Text }.Normalized().MatchKey;

    /// <summary>
    /// The watch list. A name is looked up at Naver before it is registered, because the band quotes by symbol
    /// and a name the site does not know would silently show nothing.
    /// </summary>
    private Control BuildStockTab()
    {
        _stocks.Columns.Add("종목", 240);
        _stocks.Columns.Add("코드", 120);
        _stocks.Columns.Add("구분", 120);

        var search = new Button { Text = "네이버에서 찾기", AutoSize = true };
        var add = new Button { Text = "추가", AutoSize = true, Enabled = false };
        var remove = new Button { Text = "제거", AutoSize = true };
        search.Click += async (_, _) => await SearchStocksAsync(search, add);
        add.Click += (_, _) => AddSelectedStock();
        remove.Click += (_, _) => RemoveSelectedStock();
        _stockQuery.TextChanged += (_, _) =>
        {
            _stockMatches.Items.Clear();
            _stockMatches.Enabled = false;
            add.Enabled = false;
        };

        var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        buttons.Controls.Add(search);
        buttons.Controls.Add(add);
        buttons.Controls.Add(remove);

        var layout = FormGrid();
        AddRow(layout, string.Empty, _stocksEnabled);
        AddRow(layout, "종목 이름", _stockQuery);
        AddRow(layout, "찾은 종목", _stockMatches);
        AddRow(layout, string.Empty, buttons);
        AddRow(layout, "시세 갱신(초)", _stockRefresh);
        AddRow(layout, "종목 전환(초)", _stockRotation);
        AddRow(layout, "표시 목록", _stocks, fill: true);
        return layout;
    }

    private async Task SearchStocksAsync(Button search, Button add)
    {
        var query = _stockQuery.Text.Trim();
        if (query.Length == 0)
        {
            return;
        }

        search.Enabled = false;
        _stockMatches.Items.Clear();
        try
        {
            var matches = await _stockClient.SearchAsync(query, CancellationToken.None);
            if (matches is null)
            {
                MessageBox.Show(
                    "네이버 금융에 연결하지 못했습니다. 잠시 후 다시 시도해 주세요.",
                    "JKBar",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            if (matches.Count == 0)
            {
                MessageBox.Show(
                    $"'{query}' 이름으로 찾은 종목이 없습니다.",
                    "JKBar",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            // Added in one go: filling a docked list one item at a time lets the window redraw half-populated.
            _stockMatches.Items.AddRange([.. matches.Take(20).Select(match => new StockOption(match))]);
            _stockMatches.SelectedIndex = 0;
            _stockMatches.Enabled = true;
            add.Enabled = true;
        }
        finally
        {
            search.Enabled = true;
        }
    }

    private void AddSelectedStock()
    {
        if (_stockMatches.SelectedItem is not StockOption option)
        {
            return;
        }

        var entry = WatchedStock.From(option.Match);
        if (_stocks.Items.Cast<ListViewItem>().Any(item =>
            item.SubItems[1].Text.Equals(entry.Code, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        if (_stocks.Items.Count >= StockWatchSettings.MaximumItems)
        {
            MessageBox.Show(
                $"표시 목록은 최대 {StockWatchSettings.MaximumItems}개까지 둘 수 있습니다.",
                "JKBar",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        AddStockRow(entry);
        _stockQuery.Clear();
        RaisePreview();
    }

    private void AddStockRow(WatchedStock entry)
    {
        var row = _stocks.Items.Add(entry.Name);
        row.SubItems.Add(entry.Code);
        row.SubItems.Add(entry.IsIndex ? "지수" : "종목");
    }

    private void RemoveSelectedStock()
    {
        if (_stocks.SelectedItems.Count == 0)
        {
            return;
        }

        _stocks.Items.Remove(_stocks.SelectedItems[0]);
        RaisePreview();
    }

    private StockWatchSettings ReadStocks() => new()
    {
        Enabled = _stocksEnabled.Checked,
        RefreshSeconds = (int)_stockRefresh.Value,
        RotationSeconds = (int)_stockRotation.Value,
        Items = [.. _stocks.Items.Cast<ListViewItem>().Select(item => new WatchedStock
        {
            Name = item.Text,
            Code = item.SubItems[1].Text,
            IsIndex = item.SubItems[2].Text == "지수"
        })]
    };

    private sealed record StockOption(StockMatch Match)
    {
        public override string ToString() =>
            $"{Match.Name} ({Match.Code}{(Match.Kind.Length == 0 ? string.Empty : " · " + Match.Kind)})";
    }

    private Control BuildDiagnosticsTab()
    {
        var measurements = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            Dock = DockStyle.Top,
            Height = 110,
            Text = _measurements()
        };
        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            Dock = DockStyle.Top
        };
        var preview = new Button { Text = "알림 펼침 미리보기", AutoSize = true };
        var refresh = new Button { Text = "크기 다시 읽기", AutoSize = true };
        preview.Click += (_, _) => _previewAlert();
        refresh.Click += (_, _) => measurements.Text = _measurements();
        buttons.Controls.Add(preview);
        buttons.Controls.Add(refresh);

        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16) };
        panel.Controls.Add(buttons);
        panel.Controls.Add(measurements);
        buttons.Top = measurements.Bottom + 12;
        return panel;
    }

    private void ChooseBandColour()
    {
        using var dialog = new ColorDialog { Color = _selectedBandColour, FullOpen = true, AnyColor = true };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _selectedBandColour = dialog.Color;
            _bandSwatch.BackColor = dialog.Color;
            RaisePreview();
        }
    }

    private void ChooseImage()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Bar 왼쪽에 표시할 이미지",
            Filter = "이미지|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.ico|모든 파일|*.*"
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            // The chosen file is copied into JKBar's own folder, so the setting never depends on the original.
            _imagePath.Text = _importImage(dialog.FileName) ?? dialog.FileName;
            RaisePreview();
        }
    }

    private void ChooseTypography()
    {
        using var initial = CreateFont(_typography);
        using var dialog = new FontDialog
        {
            Font = initial,
            Color = Color.FromArgb(_typography.TextColourArgb),
            ShowColor = true,
            ShowEffects = true,
            FontMustExist = true,
            MinSize = 6,
            MaxSize = 15
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _typography = new BandTypographySettings
        {
            FontFamily = dialog.Font.FontFamily.Name,
            FontSizePercent = (int)Math.Round(dialog.Font.SizeInPoints / 24f * 100),
            Bold = dialog.Font.Bold,
            Italic = dialog.Font.Italic,
            TextColourArgb = dialog.Color.ToArgb()
        }.Normalized();
        UpdateFontSummary();
        RaisePreview();
    }

    private void UpdateFontSummary()
    {
        var styles = new List<string>();
        if (_typography.Bold)
        {
            styles.Add("Bold");
        }
        if (_typography.Italic)
        {
            styles.Add("Italic");
        }
        _fontSummary.Text = $"{_typography.FontFamily} {string.Join(' ', styles)}".Trim();

        // The name stays readable ink; the chosen colour gets its own bordered swatch because white on white vanishes.
        _textSwatch.BackColor = Color.FromArgb(_typography.TextColourArgb);
    }

    /// <summary>A display the user unplugged is still offered, so choosing it again does not need it plugged in.</summary>
    private void LoadMonitors(string chosen)
    {
        _monitor.Items.Add(new MonitorEntry(string.Empty, "자동 (창이 놓인 화면)"));
        foreach (var monitor in MonitorCatalog.All())
        {
            _monitor.Items.Add(monitor);
        }

        if (chosen.Length > 0 && !_monitor.Items.Cast<MonitorEntry>().Any(entry => entry.DeviceName == chosen))
        {
            _monitor.Items.Add(new MonitorEntry(chosen, $"{chosen} · 연결되지 않음"));
        }

        _monitor.SelectedIndex = Math.Max(
            0,
            _monitor.Items.Cast<MonitorEntry>().ToList().FindIndex(entry => entry.DeviceName == chosen));
    }

    private string SelectedMonitor() =>
        _monitor.SelectedItem is MonitorEntry entry ? entry.DeviceName : string.Empty;

    private BandItemsSettings ReadBandItems()
    {
        var entries = _items.Items.Cast<ItemEntry>().ToArray();
        return new BandItemsSettings
        {
            Order = entries.Select(entry => entry.Kind).ToArray(),
            Hidden = entries.Where((_, index) => !_items.GetItemChecked(index)).Select(entry => entry.Kind).ToArray(),
            PercentStyles = _percentStyles
                .Select(pair => new BandItemStyle { Kind = pair.Key, Style = pair.Value })
                .ToArray()
        };
    }

    /// <summary>Only a percentage can be drawn as a graph, so the choice is offered for those readouts alone.</summary>
    private void ShowPercentStyleOfSelection()
    {
        var kind = SelectedKind();
        var styleable = kind is not null && BandItemsSettings.StyleableKinds.Contains(kind.Value);
        _percentStyle.Enabled = styleable;

        var wasSuspended = _suspendPreview;
        _suspendPreview = true;
        SelectOption(
            _percentStyle,
            styleable ? _percentStyles[kind!.Value] : BandPercentStyle.LabelAndValue);
        _suspendPreview = wasSuspended;
    }

    private void TakePercentStyleForSelection()
    {
        if (SelectedKind() is not { } kind || !BandItemsSettings.StyleableKinds.Contains(kind))
        {
            return;
        }

        _percentStyles[kind] = SelectedValue(_percentStyle, BandPercentStyle.LabelAndValue);
        RaisePreview();
    }

    private BandItemKind? SelectedKind() =>
        _items.SelectedItem is ItemEntry entry ? entry.Kind : null;

    private void MoveSelected(int offset)
    {
        var from = _items.SelectedIndex;
        var to = from + offset;
        if (from < 0 || to < 0 || to >= _items.Items.Count)
        {
            return;
        }

        var entry = (ItemEntry)_items.Items[from];
        var isChecked = _items.GetItemChecked(from);
        _items.Items.RemoveAt(from);
        _items.Items.Insert(to, entry);
        _items.SetItemChecked(to, isChecked);
        _items.SelectedIndex = to;
        RaisePreview();
    }

    private void ValidateBeforeClose(object? sender, FormClosingEventArgs e)
    {
        if (DialogResult != DialogResult.OK || Settings.News.TryGetFeedUri(out _))
        {
            return;
        }

        e.Cancel = true;
        MessageBox.Show(
            this,
            "피드 주소는 http:// 또는 https://로 시작하는 올바른 주소여야 합니다.",
            "JKBar",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
        _feedUrl.Focus();
        _feedUrl.SelectAll();
    }

    private static Font CreateFont(BandTypographySettings typography)
    {
        var style = (typography.Bold ? FontStyle.Bold : FontStyle.Regular)
            | (typography.Italic ? FontStyle.Italic : FontStyle.Regular);
        var sizeInPoints = typography.FontSizePercent * 24f / 100f;
        try
        {
            return new Font(typography.FontFamily, sizeInPoints, style, GraphicsUnit.Point);
        }
        catch (ArgumentException)
        {
            return new Font(BandTypographySettings.DefaultFontFamily, sizeInPoints, style, GraphicsUnit.Point);
        }
    }

    private static FlowLayoutPanel SliderRow(TrackBar slider, Label value)
    {
        var row = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        value.Margin = new Padding(6, 14, 3, 3);
        row.Controls.Add(slider);
        row.Controls.Add(value);
        return row;
    }

    private static TableLayoutPanel FormGrid()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            ColumnCount = 2,
            RowCount = 0
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        return layout;
    }

    private static void AddRow(TableLayoutPanel layout, string label, Control control, bool fill = false)
    {
        var row = layout.RowCount++;
        layout.RowStyles.Add(new RowStyle(fill ? SizeType.Percent : SizeType.AutoSize, fill ? 100 : 0));
        layout.Controls.Add(new Label
        {
            Text = label,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(3, 8, 14, 8)
        }, 0, row);
        control.Anchor = fill
            ? AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top | AnchorStyles.Bottom
            : AnchorStyles.Left | AnchorStyles.Right;
        control.Margin = new Padding(3, 5, 3, 5);
        layout.Controls.Add(control, 1, row);
    }

    private static void AddFiller(TableLayoutPanel layout)
    {
        var row = layout.RowCount++;
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var filler = new Panel { Dock = DockStyle.Fill };
        layout.Controls.Add(filler, 0, row);
        layout.SetColumnSpan(filler, 2);
    }

    private static TabPage Page(string title, Control content)
    {
        var page = new TabPage(title) { Padding = new Padding(0) };
        page.Controls.Add(content);
        return page;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _stockClient.Dispose();
        }

        base.Dispose(disposing);
    }

    private static ComboBox DropDown() => new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 300 };

    private static void AddOption<T>(ComboBox combo, string label, T value) where T : struct =>
        combo.Items.Add(new Option<T>(label, value));

    private static void SelectOption<T>(ComboBox combo, T value) where T : struct
    {
        combo.SelectedItem = combo.Items.Cast<Option<T>>().First(option => EqualityComparer<T>.Default.Equals(option.Value, value));
    }

    private static T SelectedValue<T>(ComboBox combo, T fallback) where T : struct =>
        combo.SelectedItem is Option<T> option ? option.Value : fallback;

    private sealed record Option<T>(string Label, T Value) where T : struct
    {
        public override string ToString() => Label;
    }

    private sealed record ItemEntry(BandItemKind Kind, string Label)
    {
        public override string ToString() => Label;
    }
}
