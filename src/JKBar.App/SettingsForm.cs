// Presents every user-facing JKBar setting in one window.
using JKBar.App.Diagnostics;
using JKBar.App.Interop;
using JKBar.App.Stocks;
using JKBar.Core;
using JKBar.Core.Layout;
using JKBar.Core.Presentation;
using JKBar.Core.Settings;
using JKBar.Core.Stocks;
using JKBar.Core.Sync;
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
    private readonly ComboBox _notchScene = DropDown();
    private readonly ComboBox _monitor = DropDown();
    private readonly ComboBox _updateCheck = DropDown();
    private readonly CheckBox _updateOnStartup = new() { Text = "시작할 때도 한 번 확인", AutoSize = true };
    private readonly Button _bandColour = new() { Text = "색 선택...", AutoSize = true };
    private readonly Panel _bandSwatch = new() { Width = 44, Height = 24, BorderStyle = BorderStyle.FixedSingle };
    private readonly Button _graphColour = new() { Text = "색 선택...", AutoSize = true };
    private readonly Panel _graphSwatch = new() { Width = 44, Height = 24, BorderStyle = BorderStyle.FixedSingle };
    private readonly CheckBox _graphFollowsText = new() { Text = "글자색 사용", AutoSize = true };

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
    private readonly ListView _items = new()
    {
        Dock = DockStyle.Fill,
        View = View.Details,
        CheckBoxes = true,
        HeaderStyle = ColumnHeaderStyle.None,
        FullRowSelect = true,
        HideSelection = false,
        MultiSelect = false,
        ShowItemToolTips = true
    };

    private readonly ComboBox _percentStyle = DropDown();
    private readonly Dictionary<BandItemKind, BandPercentStyle> _percentStyles = [];
    private readonly Dictionary<BandItemKind, bool> _itemVisibility = [];
    private readonly Dictionary<string, CheckBox> _syncGoodAlerts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CheckBox> _syncAttentionAlerts = new(StringComparer.Ordinal);
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
    private readonly IReadOnlySet<BandItemKind> _unavailableItems;

    private BandTypographySettings _typography;
    private Color _selectedBandColour;
    private Color _selectedGraphColour;
    private bool _suspendPreview = true;

    /// <summary>Raised while editing so the bar shows the pending values before they are confirmed.</summary>
    internal event Action<JkBarSettings>? Preview;

    internal SettingsForm(
        JkBarSettings settings,
        IReadOnlyList<SyncProviderSnapshot> syncSnapshots,
        Action previewAlert,
        Func<string> measurements,
        Func<string, string?> importImage)
    {
        _previewAlert = previewAlert;
        _importImage = importImage;
        _measurements = measurements;

        var normalized = settings.Normalized();
        _original = normalized;
        _unavailableItems = SyncStatusSource.UnavailableKinds(syncSnapshots);
        _typography = normalized.Typography;
        _selectedBandColour = Color.FromArgb(normalized.Appearance.BandColourArgb);
        _selectedGraphColour = Color.FromArgb(
            normalized.BandItems.GraphColourArgb ?? normalized.Typography.TextColourArgb);

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
        AddOption(_idleNotch, "픽셀 동물 - 오리", IdleNotchContent.Duck);
        AddOption(_idleNotch, "픽셀 동물 - 햄스터", IdleNotchContent.Hamster);
        SelectOption(_idleNotch, normalized.Notch.IdleContent);

        AddOption(_notchScene, "없음", NotchScene.None);
        AddOption(_notchScene, "푸른 들판", NotchScene.SummerField);
        AddOption(_notchScene, "노을 들판", NotchScene.SunsetSky);
        AddOption(_notchScene, "고목 초원", NotchScene.CloudHill);
        AddOption(_notchScene, "버드나무 호수", NotchScene.WillowLake);
        AddOption(_notchScene, "열대 해변", NotchScene.TropicalCoast);
        SelectOption(_notchScene, normalized.Notch.Scene);
        UpdateSceneAvailability();

        LoadMonitors(normalized.Appearance.MonitorDeviceName);

        AddOption(_updateCheck, "확인하지 않음", UpdateCheckFrequency.Never);
        AddOption(_updateCheck, "매일", UpdateCheckFrequency.Daily);
        AddOption(_updateCheck, "매주", UpdateCheckFrequency.Weekly);
        SelectOption(_updateCheck, normalized.Update.Check);
        _updateOnStartup.Checked = normalized.Update.CheckOnStartup;

        _bandSwatch.BackColor = _selectedBandColour;
        _graphSwatch.BackColor = _selectedGraphColour;
        _graphFollowsText.Checked = normalized.BandItems.GraphColourArgb is null;
        _graphFollowsText.CheckedChanged += (_, _) =>
        {
            UpdateGraphColourState();
            RaisePreview();
        };
        UpdateGraphColourState();
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

        _items.Columns.Add(string.Empty);
        _items.Resize += (_, _) => ResizeBandItemColumn();
        foreach (var kind in normalized.BandItems.Order)
        {
            var unavailable = _unavailableItems.Contains(kind);
            var visible = normalized.BandItems.IsVisible(kind);
            _itemVisibility[kind] = visible;
            _items.Items.Add(new ListViewItem(ItemLabels[kind])
            {
                Tag = kind,
                Checked = visible && !unavailable,
                ForeColor = unavailable ? SystemColors.GrayText : _items.ForeColor,
                ToolTipText = unavailable ? "앱이 감지되지 않았습니다." : string.Empty
            });
        }
        if (_items.Items.Count > 0)
        {
            _items.Items[0].Selected = true;
        }
        ResizeBandItemColumn();

        foreach (var kind in BandItemsSettings.StyleableKinds)
        {
            _percentStyles[kind] = normalized.BandItems.StyleFor(kind);
        }

        foreach (var providerId in SyncProviderCatalog.BuiltIn)
        {
            _syncGoodAlerts[providerId] = SyncAlertCheckBox(normalized.SyncAlerts.Allows(providerId, BandItemBadge.Good));
            _syncAttentionAlerts[providerId] = SyncAlertCheckBox(normalized.SyncAlerts.Allows(providerId, BandItemBadge.Attention));
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
        _graphColour.Click += (_, _) => ChooseGraphColour();
        Controls.Add(BuildRoot());
        FormClosing += ValidateBeforeClose;

        _overlap.SelectedIndexChanged += (_, _) => RaisePreview();
        _idleNotch.SelectedIndexChanged += (_, _) =>
        {
            UpdateSceneAvailability();
            RaisePreview();
        };
        _notchScene.SelectedIndexChanged += (_, _) => RaisePreview();
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
        _items.ItemCheck += (_, e) =>
        {
            if (IsUnavailable(e.Index))
            {
                e.NewValue = e.CurrentValue;
                return;
            }

            _itemVisibility[ItemKind(_items.Items[e.Index])] = e.NewValue == CheckState.Checked;
            BeginInvoke(RaisePreview);
        };
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

    private bool IsUnavailable(int index) =>
        index >= 0
        && index < _items.Items.Count
        && _unavailableItems.Contains(ItemKind(_items.Items[index]));

    private static BandItemKind ItemKind(ListViewItem item) => (BandItemKind)item.Tag!;

    private void ResizeBandItemColumn()
    {
        if (_items.Columns.Count > 0)
        {
            _items.Columns[0].Width = Math.Max(1, _items.ClientSize.Width - 1);
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
            Scene = SelectedValue(_notchScene, NotchScene.None),
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
        SyncAlerts = ReadSyncAlerts(),
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

        var tabs = Tabs(Color.FromArgb(38, 92, 168), Color.FromArgb(233, 236, 242));
        tabs.TabPages.Add(Page("기본", BuildGeneralTab()));
        tabs.TabPages.Add(Page("Bar 설정", BuildBandTab()));
        tabs.TabPages.Add(Page("컨텐츠 설정", BuildContentTab(), Color.FromArgb(238, 241, 247)));
        tabs.TabPages.Add(Page("진단 설정", BuildDiagnosticsTab()));

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
        AddRow(layout, "표시 모니터", _monitor);
        AddRow(layout, "Bar 표시 방식", _overlap);
        AddRow(layout, "노치 표시", _idleNotch);
        AddRow(layout, "노치 배경", _notchScene);
        AddRow(layout, string.Empty, _showNowPlaying);
        AddRow(layout, "확장 알림 글자 크기", SliderRow(_alertFontSize, _alertFontSizeValue));
        AddRow(layout, "Windows 자동 시작", _startWithWindows);
        AddRow(layout, "전체화면 자동 숨김", _hideWhenFullscreen);
        AddRow(layout, "업데이트 자동 확인", _updateCheck);
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
        var imageRow = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        imageRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        imageRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        imageRow.Controls.Add(_imagePath, 0, 0);
        imageRow.Controls.Add(imageButtons, 1, 0);

        var fontRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        var chooseFont = new Button { Text = "글꼴 및 글자색...", AutoSize = true };
        chooseFont.Click += (_, _) => ChooseTypography();
        _textSwatch.Margin = new Padding(3, 3, 10, 3);
        fontRow.Controls.Add(_fontSummary);
        fontRow.Controls.Add(_textSwatch);
        fontRow.Controls.Add(chooseFont);
        _textShadow.Margin = new Padding(12, 7, 3, 3);
        fontRow.Controls.Add(_textShadow);

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
        itemLayout.MinimumSize = new Size(0, 96);
        itemLayout.Controls.Add(_items, 0, 0);
        itemLayout.Controls.Add(tools, 1, 0);

        var opacityRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        _bandOpacityValue.Margin = new Padding(6, 14, 3, 3);
        opacityRow.Controls.Add(_bandOpacity);
        opacityRow.Controls.Add(_bandOpacityValue);

        var graphColourRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        _graphFollowsText.Margin = new Padding(3, 7, 10, 3);
        graphColourRow.Controls.Add(_graphFollowsText);
        graphColourRow.Controls.Add(_graphSwatch);
        graphColourRow.Controls.Add(_graphColour);

        var appearance = FormGrid();
        AddRow(appearance, "Bar 색깔", colorRow);
        AddRow(appearance, "Bar 투명도", opacityRow);
        AddRow(appearance, "사용자 로고", imageRow);
        AddRow(appearance, "사용자 로고 크기", SliderRow(_imageScale, _imageScaleValue));
        AddRow(appearance, "Bar 폰트", fontRow);
        AddFiller(appearance);

        var items = FormGrid();
        AddRow(items, "성능 카운터 갱신(초)", _metricsRefresh);
        AddRow(items, "성능 카운터 표시 방식", _percentStyle);
        AddRow(items, "성능 카운터 그래프 색", graphColourRow);
        AddRow(items, "내장 앱 확장 알림", BuildSyncAlertOptions());
        AddRow(items, "오른쪽 표시 항목", itemLayout, fill: true);

        var tabs = Tabs(Color.FromArgb(23, 132, 130), Color.FromArgb(222, 228, 236));
        tabs.Margin = new Padding(10);
        tabs.TabPages.Add(Page("Bar 모양", appearance));
        tabs.TabPages.Add(Page("표시항목", items));
        var frame = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10), BackColor = Color.FromArgb(238, 241, 247) };
        frame.Controls.Add(tabs);
        return frame;
    }

    private static CheckBox SyncAlertCheckBox(bool isChecked) => new()
    {
        Checked = isChecked,
        AutoSize = true,
        Anchor = AnchorStyles.None
    };

    private Control BuildSyncAlertOptions()
    {
        var layout = new TableLayoutPanel { AutoSize = true, ColumnCount = 3, RowCount = 4 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.Controls.Add(new Label { AutoSize = true }, 0, 0);
        layout.Controls.Add(new Label { Text = "정상", AutoSize = true, Anchor = AnchorStyles.None }, 1, 0);
        layout.Controls.Add(new Label { Text = "주의 필요", AutoSize = true, Anchor = AnchorStyles.None }, 2, 0);

        for (var index = 0; index < SyncProviderCatalog.BuiltIn.Count; index++)
        {
            var providerId = SyncProviderCatalog.BuiltIn[index];
            layout.Controls.Add(new Label
            {
                Text = SyncProviderCatalog.DisplayName(providerId),
                AutoSize = true,
                Anchor = AnchorStyles.Left
            }, 0, index + 1);
            layout.Controls.Add(_syncGoodAlerts[providerId], 1, index + 1);
            layout.Controls.Add(_syncAttentionAlerts[providerId], 2, index + 1);
        }

        return layout;
    }

    private void UpdateOpacitySummary() => _bandOpacityValue.Text = $"{_bandOpacity.Value * 5}%";

    private void UpdateAlertFontSummary() => _alertFontSizeValue.Text = $"{_alertFontSize.Value * 5}%";

    private void UpdateImageScaleSummary() => _imageScaleValue.Text = $"{_imageScale.Value * 5}%";

    private Control BuildContentTab()
    {
        var tabs = Tabs(Color.FromArgb(23, 132, 130), Color.FromArgb(222, 228, 236));
        tabs.Margin = new Padding(10);
        tabs.TabPages.Add(Page("뉴스", BuildNewsTab()));
        tabs.TabPages.Add(Page("주식 리스트", BuildStockTab()));
        tabs.TabPages.Add(Page("실행 앱 리스트", BuildProcessTab()));

        // An inset panel puts a visible edge between the two tab strips.
        var frame = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10), BackColor = Color.FromArgb(238, 241, 247) };
        frame.Controls.Add(tabs);
        return frame;
    }

    private Control BuildNewsTab()
    {
        var layout = FormGrid();
        AddRow(layout, string.Empty, _newsEnabled);
        AddRow(layout, "뉴스 RSS URL", _feedUrl);
        AddRow(layout, "뉴스 갱신(분)", _refreshMinutes);
        AddRow(layout, "뉴스 전환(초)", _rotationSeconds);
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
        _processes.Columns.Add("이름", 120);
        _processes.Columns.Add("경로", 230);
        _processes.Columns.Add("찾는 방식", 130);
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
        AddRow(layout, "실행 앱 목록", _processes, fill: true);
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
        AddRow(layout, "주가 갱신(초)", _stockRefresh);
        AddRow(layout, "주가 전환(초)", _stockRotation);
        AddRow(layout, "주식 리스트", _stocks, fill: true);
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
            ScrollBars = ScrollBars.Vertical,
            Height = 150,
            Text = _measurements()
        };
        var preview = new Button { Text = "알림 펼침 미리보기", AutoSize = true };
        var refresh = new Button { Text = "크기 다시 읽기", AutoSize = true };
        preview.Click += (_, _) => _previewAlert();
        refresh.Click += (_, _) => measurements.Text = _measurements();
        var diagnosticButtons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        diagnosticButtons.Controls.Add(preview);
        diagnosticButtons.Controls.Add(refresh);

        var logPath = new TextBox { ReadOnly = true, Dock = DockStyle.Fill, Text = CrashLog.Path };
        var logStatus = new Label { AutoSize = true, Anchor = AnchorStyles.Left };
        var openLogFolder = new Button { Text = "로그 폴더 열기", AutoSize = true };
        var clearLog = new Button { Text = "로그 비우기", AutoSize = true };
        void UpdateLogStatus() => logStatus.Text = File.Exists(CrashLog.Path)
            ? $"{new FileInfo(CrashLog.Path).Length:N0} bytes"
            : "기록 없음";
        openLogFolder.Click += (_, _) => OpenLogFolder();
        clearLog.Click += (_, _) =>
        {
            if (!CrashLog.Clear())
            {
                MessageBox.Show(this, "오류 로그를 비우지 못했습니다.", "JKBar", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            UpdateLogStatus();
        };
        var logButtons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        logButtons.Controls.Add(openLogFolder);
        logButtons.Controls.Add(clearLog);
        logStatus.Margin = new Padding(12, 8, 3, 3);
        logButtons.Controls.Add(logStatus);
        UpdateLogStatus();

        var layout = FormGrid();
        AddRow(layout, "화면 및 저장 위치", measurements);
        AddRow(layout, string.Empty, diagnosticButtons);
        AddRow(layout, "오류 로그", logPath);
        AddRow(layout, "로그 설정", logButtons);
        AddFiller(layout);
        return layout;
    }

    private void OpenLogFolder()
    {
        var folder = System.IO.Path.GetDirectoryName(CrashLog.Path);
        if (folder is null)
        {
            return;
        }

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(folder) { UseShellExecute = true });
        }
        catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            MessageBox.Show(this, $"로그 폴더를 열지 못했습니다.\n\n{error.Message}", "JKBar", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
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

    private void ChooseGraphColour()
    {
        using var dialog = new ColorDialog { Color = _selectedGraphColour, FullOpen = true, AnyColor = true };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _selectedGraphColour = dialog.Color;
        _graphSwatch.BackColor = dialog.Color;
        _graphFollowsText.Checked = false;
        UpdateGraphColourState();
        RaisePreview();
    }

    private void UpdateGraphColourState()
    {
        var own = !_graphFollowsText.Checked;
        _graphColour.Enabled = own;
        _graphSwatch.BackColor = own
            ? _selectedGraphColour
            : Color.FromArgb(_typography.TextColourArgb);
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
        UpdateGraphColourState();
    }

    private void LoadMonitors(string chosen)
    {
        _monitor.Items.Add(new MonitorEntry(string.Empty, "자동 (Windows 주 모니터)"));
        foreach (var monitor in MonitorCatalog.All())
        {
            _monitor.Items.Add(monitor);
        }

        _monitor.SelectedIndex = Math.Max(
            0,
            _monitor.Items.Cast<MonitorEntry>().ToList().FindIndex(entry => entry.DeviceName == chosen));
    }

    private string SelectedMonitor() =>
        _monitor.SelectedItem is MonitorEntry entry ? entry.DeviceName : string.Empty;

    private BandItemsSettings ReadBandItems()
    {
        var entries = _items.Items.Cast<ListViewItem>().Select(ItemKind).ToArray();
        return new BandItemsSettings
        {
            Order = entries,
            Hidden = entries.Where(kind => !_itemVisibility[kind]).ToArray(),
            PercentStyles = _percentStyles
                .Select(pair => new BandItemStyle { Kind = pair.Key, Style = pair.Value })
                .ToArray(),
            GraphColourArgb = _graphFollowsText.Checked ? null : _selectedGraphColour.ToArgb()
        };
    }

    private SyncAlertSettings ReadSyncAlerts() => new()
    {
        MutedGoodProviders = [.. _syncGoodAlerts.Where(pair => !pair.Value.Checked).Select(pair => pair.Key)],
        MutedAttentionProviders = [.. _syncAttentionAlerts.Where(pair => !pair.Value.Checked).Select(pair => pair.Key)]
    };

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
        _items.SelectedItems.Count == 1 ? ItemKind(_items.SelectedItems[0]) : null;

    private void MoveSelected(int offset)
    {
        var from = _items.SelectedIndices.Count == 1 ? _items.SelectedIndices[0] : -1;
        var to = from + offset;
        if (from < 0 || to < 0 || to >= _items.Items.Count)
        {
            return;
        }

        var entry = _items.Items[from];
        _items.Items.RemoveAt(from);
        _items.Items.Insert(to, entry);
        entry.Selected = true;
        entry.Focused = true;
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

    private static TabPage Page(string title, Control content) => Page(title, content, Color.White);

    private static TabPage Page(string title, Control content, Color background)
    {
        var page = new TabPage(title)
        {
            Padding = new Padding(0),
            BackColor = background,
            UseVisualStyleBackColor = false
        };
        page.Controls.Add(content);
        return page;
    }

    /// <summary>
    /// The stock tab strip draws every tab the same, so a nested one looked like a second row of the first.
    /// Painting the selected tab in the accent colour is what tells the two levels apart.
    /// </summary>
    private static TabControl Tabs(Color accent, Color idle)
    {
        var tabs = new TabControl
        {
            Dock = DockStyle.Fill,
            DrawMode = TabDrawMode.OwnerDrawFixed,
            Padding = new Point(16, 5)
        };

        tabs.DrawItem += (sender, e) =>
        {
            var owner = (TabControl)sender!;
            var selected = owner.SelectedIndex == e.Index;
            var bounds = e.Bounds with { Y = e.Bounds.Y - 1, Height = e.Bounds.Height + 2 };

            using var background = new SolidBrush(selected ? accent : idle);
            e.Graphics.FillRectangle(background, bounds);
            using var edge = new Pen(Color.FromArgb(198, 202, 210));
            e.Graphics.DrawRectangle(edge, bounds);

            using var font = new Font(owner.Font, selected ? FontStyle.Bold : FontStyle.Regular);
            TextRenderer.DrawText(
                e.Graphics,
                owner.TabPages[e.Index].Text,
                font,
                bounds,
                selected ? Color.White : Color.FromArgb(72, 76, 84),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        };

        return tabs;
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        GrowToFitRows();
    }

    // The starting size is in pixels but the fonts follow the display's scaling, so on a high-scale display a long
    // row can want more room than the window it was sized for.
    private void GrowToFitRows()
    {
        var tabs = Descendants(this).OfType<TabControl>().ToList();
        var selected = tabs.Select(tab => tab.SelectedIndex).ToList();

        // A page only lays out once it has been shown, and the nested tabs only exist after their page has.
        for (var pass = 0; pass < 2; pass++)
            foreach (var tab in tabs)
                for (var page = 0; page < tab.TabPages.Count; page++)
                {
                    tab.SelectedIndex = page;
                    tab.PerformLayout();
                }

        var shortfall = 0;
        foreach (var control in Descendants(this))
        {
            if (control is not (CheckBox or RadioButton or Label or Button) || control.Width <= 0) continue;
            shortfall = Math.Max(shortfall, control.PreferredSize.Width - control.Width);
        }

        for (var index = 0; index < tabs.Count; index++) tabs[index].SelectedIndex = selected[index];
        if (shortfall <= 0) return;

        ClientSize = new Size(ClientSize.Width + Math.Min(shortfall, 400), ClientSize.Height);

        // Narrower than this and the same row is cut off again, so the window is not allowed to go there.
        MinimumSize = new Size(Math.Max(MinimumSize.Width, Size.Width), MinimumSize.Height);
    }

    // Scenery is only ever drawn behind a pet, so the choice is greyed out when the notch shows anything else.
    private void UpdateSceneAvailability() =>
        _notchScene.Enabled = PixelPetAnimation.IsPet(SelectedValue(_idleNotch, IdleNotchContent.Empty));

    private static IEnumerable<Control> Descendants(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            yield return child;
            foreach (var deeper in Descendants(child)) yield return deeper;
        }
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
}
