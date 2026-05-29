// =============================================================================
// CompanionBubbleWindow.xaml.cs — 桌面浮球 WPF 窗口代码后置
// =============================================================================
// 职责：
//   - 屏幕角落常驻小圆球；单击展开面板，双击唤起 MainWindow（WebView2 主站）
//   - 定时/聚焦时 GET /api/companion/current 拉取当前精灵与养成数值
//   - 右键菜单切换五精灵 → POST /api/companion/select-spirit
//   - 云端门禁开启时，经 _cookieHeaderProvider 把 MainWindow WebView2 的 SpiritDesk.Auth 带给 API
//
// 依赖 Web 项目接口（SpiritDeskWebHost 映射）：
//   GET  /api/companion/current
//   POST /api/companion/select-spirit  Body: { "spiritId": "light" | ... }
//
// 数据结构：
//   - CompanionStatusDto：与 current API 的 camelCase JSON 对应（私有嵌套类）
//   - Uri _baseUri：站点根（本地 127.0.0.1 或 SPIRITDESK_REMOTE_BASEURL）
//   - ShellSettings：位置、展开尺寸、主题、置顶 持久化到 %AppData%/SpiritDesk
//   - DispatcherTimer：单击防抖 230ms；状态轮询 12s
//
// C# 语法：
//   - partial class：控件在 CompanionBubbleWindow.xaml，逻辑在本文件
//   - Func&lt;Task&lt;string?&gt;&gt;? _cookieHeaderProvider：可选，MainWindow.BuildAuthCookieHeaderAsync
//   - ConfigureAwait(false) + Dispatcher.InvokeAsync：HTTP 在后台线程，改 UI 回 UI 线程
// =============================================================================

using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SpiritDesk.Core.Constants;

namespace SpiritDesk.Shell;

public partial class CompanionBubbleWindow : Window
{
    // --- 窗口尺寸：收起为圆球，展开为可拖拽调整的面板 ---
    private const double CollapsedSize = 88;
    private const double DefaultExpandedWidth = 320;
    private const double DefaultExpandedHeight = 360;
    private const double MinExpandedWidth = 320;
    private const double MinExpandedHeight = 320;
    private const double MaxExpandedWidth = 420;
    private const double MaxExpandedHeight = 480;

    /// <summary>API 失败或首次加载前的占位展示（与默认精灵卷卷晴一致）。</summary>
    private static readonly CompanionStatusDto DefaultStatus = new()
    {
        Success = false,
        Name = "卷卷晴",
        Title = "工作学习发动机",
        StatusText = "先完成最重要的一件事吧。",
        ImageUrl = "/assets/images/spirit-light.png",
        Mood = 88,
        Affinity = 120,
        Level = 3,
        Coins = 20
    };

    private readonly Uri _baseUri;
    private readonly HttpClient _httpClient;
    private readonly Window _mainWindow;
    /// <summary>连云端且 Web 需登录时，从主窗口 WebView2 读取 Cookie 请求头。</summary>
    private readonly Func<Task<string?>>? _cookieHeaderProvider;
    private readonly ShellSettingsService _settingsService = new();
    /// <summary>区分单击（延迟后展开）与拖拽：按下后移动超过阈值则取消单击计时。</summary>
    private readonly DispatcherTimer _singleClickTimer;
    /// <summary>后台轮询 companion 状态，无需打开主窗口也能更新球上文案。</summary>
    private readonly DispatcherTimer _pollTimer;
    private readonly string _logFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SpiritDesk",
        "companion-bubble.log");
    private Point _pressMouseRelative;
    private bool _dragArm;
    private bool _isExiting;
    private bool _isExpanded;
    /// <summary>RestoreWindowState 期间为 true，避免 SizeChanged 误写设置。</summary>
    private bool _isInitializing;
    private double _panelWidth = DefaultExpandedWidth;
    private double _panelHeight = DefaultExpandedHeight;
    private string _theme = "mint";
    private CompanionStatusDto _status = DefaultStatus;

    // 内置五种精灵（与 SpiritDesk.Web 一致），右键菜单不请求 /api/companion/state。
    private static readonly (string Id, string DisplayName)[] BuiltInSpiritPickers =
    [
        (SpiritIds.Light, "卷卷晴"),
        (SpiritIds.Water, "嘻嘻滴"),
        (SpiritIds.Air, "贴贴朵"),
        (SpiritIds.Soil, "慢慢壤"),
        (SpiritIds.Nutrition, "新新星"),
    ];

    private static readonly JsonSerializerOptions ApiJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <param name="baseUri">与 MainWindow WebView2 同源，用于拼 API 与精灵图片 URL。</param>
    /// <param name="cookieHeaderProvider">通常为 MainWindow 传入；本地 Auth 关闭时可 null。</param>
    public CompanionBubbleWindow(Uri baseUri, HttpClient httpClient, Window mainWindow, Func<Task<string?>>? cookieHeaderProvider = null)
    {
        InitializeComponent();
        _baseUri = baseUri;
        _httpClient = httpClient;
        _mainWindow = mainWindow;
        _cookieHeaderProvider = cookieHeaderProvider;
        ToolTip = "SpiritDesk 桌面浮球：单击展开，双击打开主窗口，右键菜单直接选择精灵";

        // 单击：MouseUp 启动计时，若 230ms 内未发生拖拽/双击则 ToggleExpanded
        _singleClickTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(230) };
        _singleClickTimer.Tick += (_, _) =>
        {
            _singleClickTimer.Stop();
            ToggleExpanded();
        };

        _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(12) };
        _pollTimer.Tick += (_, _) => _ = PollTickAsync();

        Log("CompanionBubbleWindow constructed.");

        Loaded += async (_, _) =>
        {
            Log("CompanionBubbleWindow Loaded.");
            _isInitializing = true;
            RestoreWindowState();       // 读 ShellSettings：位置、展开、主题
            ApplyStatus(_status);       // 先用 DefaultStatus，避免首帧空白
            await RefreshStatusAsync(); // 再与 Web/API 对齐真实精灵
            _pollTimer.Start();
            _isInitializing = false;
        };
        Closing += (_, _) =>
        {
            _pollTimer.Stop();
            SaveWindowState();
        };
    }

    // --- 位置：工作区右下角默认落点、拖拽后钳制在屏幕内 ---

    private void PositionAtPrimaryWorkAreaBottomRight()
    {
        var workArea = SystemParameters.WorkArea;
        const double margin = 18;
        Left = workArea.Right - Width - margin;
        Top = workArea.Bottom - Height - margin;
    }

    // --- 鼠标：按下记录起点；移动超 6px 视为拖拽；抬起触发单击计时；双击打开主窗 ---

    private void Bubble_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (IsInteractionSource(e.OriginalSource))
        {
            _dragArm = false;
            return;
        }

        _pressMouseRelative = e.GetPosition(this);
        _dragArm = true;
    }

    private void Bubble_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragArm || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var pos = e.GetPosition(this);
        var dx = pos.X - _pressMouseRelative.X;
        var dy = pos.Y - _pressMouseRelative.Y;
        // 36 = 6² 像素，避免手抖被当成拖拽
        if (dx * dx + dy * dy < 36)
        {
            return;
        }

        _dragArm = false;
        _singleClickTimer.Stop();
        try
        {
            DragMove();
            EnsureInsideWorkArea();
            SaveWindowState();
        }
        catch (InvalidOperationException)
        {
        }
    }

    private void Bubble_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragArm)
        {
            return;
        }

        _dragArm = false;
        if (IsInteractionSource(e.OriginalSource))
        {
            return;
        }

        if (e.ClickCount == 1)
        {
            _singleClickTimer.Stop();
            _singleClickTimer.Start();
        }
    }

    private void Bubble_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        _singleClickTimer.Stop();
        e.Handled = true;
        OpenMainWindow();
    }

    private void OpenSpiritDeskMenuItem_Click(object sender, RoutedEventArgs e) => OpenMainWindow();

    // --- 右键菜单：动态生成五精灵项 + 打开主站 / 主题 / 置顶 / 退出 ---

    /// <summary>每次打开菜单时重建 Items；当前精灵名与 API 返回的 Name 比对打勾。</summary>
    private void CompanionContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        if (sender is not ContextMenu cm)
        {
            return;
        }

        cm.Items.Clear();
        cm.Items.Add(CreateSectionLabel("切换精灵"));
        var current = (_status?.Name ?? string.Empty).Trim();
        foreach (var (id, displayName) in BuiltInSpiritPickers)
        {
            var sid = id.Trim();
            var isCurrent = !string.IsNullOrEmpty(current)
                && string.Equals(current, displayName, StringComparison.OrdinalIgnoreCase);
            cm.Items.Add(CreateSpiritMenuItem(displayName, sid, isCurrent));
        }

        cm.Items.Add(new Separator());
        cm.Items.Add(CreateSectionLabel("桌面控制"));
        AppendStaticCompanionMenuItems(cm);
    }

    private void AppendStaticCompanionMenuItems(ContextMenu cm)
    {
        cm.Items.Add(MenuLink("打开 SpiritDesk", "↗", OpenSpiritDeskMenuItem_Click));
        cm.Items.Add(MenuLink("重置位置", "⌖", ResetPositionMenuItem_Click));
        cm.Items.Add(MenuLink(_theme == "warm" ? "切换到薄荷主题" : "切换到暖色主题", "◐", ToggleThemeMenuItem_Click));
        cm.Items.Add(MenuLink(Topmost ? "取消置顶" : "置顶", "▣", ToggleTopmostMenuItem_Click));
        cm.Items.Add(new Separator());
        cm.Items.Add(MenuLink("退出", "×", ExitMenuItem_Click));
    }

    private static MenuItem MenuLink(string header, string iconGlyph, RoutedEventHandler handler)
    {
        var mi = new MenuItem
        {
            Header = header,
            Icon = CreateGlyphIcon(iconGlyph)
        };
        mi.Click += handler;
        return mi;
    }

    private MenuItem CreateSpiritMenuItem(string displayName, string spiritId, bool isCurrent)
    {
        var item = new MenuItem
        {
            Header = isCurrent ? $"{displayName}  当前" : displayName,
            Tag = spiritId,
            Icon = CreateSpiritIcon(isCurrent)
        };
        item.Click += SpiritSwitchPickMenuItem_Click;
        return item;
    }

    private static TextBlock CreateSectionLabel(string text)
    {
        return new TextBlock
        {
            Text = text,
            Margin = new Thickness(12, 4, 12, 2),
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            Foreground = ToBrush("#7A9288")
        };
    }

    private static Border CreateSpiritIcon(bool isCurrent)
    {
        return new Border
        {
            Width = 12,
            Height = 12,
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(1),
            BorderBrush = ToBrush(isCurrent ? "#5AA17A" : "#BFD4C9"),
            Background = isCurrent ? ToBrush("#5AA17A") : Brushes.Transparent
        };
    }

    private static TextBlock CreateGlyphIcon(string glyph)
    {
        return new TextBlock
        {
            Text = glyph,
            FontSize = 13,
            FontWeight = FontWeights.Bold,
            Foreground = ToBrush("#6D877C"),
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    /// <summary>MenuItem.Tag 为 SpiritIds（如 light）；POST 成功后 RefreshStatus。</summary>
    private async void SpiritSwitchPickMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem mi || mi.Tag is not string sid || string.IsNullOrWhiteSpace(sid))
        {
            return;
        }

        await SelectSpiritAsync(sid).ConfigureAwait(false);
        if (Application.Current.Dispatcher.CheckAccess())
        {
            TryCloseContextMenu(mi);
        }
        else
        {
            await Application.Current.Dispatcher.InvokeAsync(() => TryCloseContextMenu(mi));
        }
    }

    private static void TryCloseContextMenu(MenuItem mi)
    {
        var parent = mi.Parent;
        while (parent is MenuItem parentMi)
        {
            parent = parentMi.Parent;
        }

        if (parent is ContextMenu ctx)
        {
            ctx.IsOpen = false;
        }
    }

    private void ResetPositionMenuItem_Click(object sender, RoutedEventArgs e)
    {
        PositionAtPrimaryWorkAreaBottomRight();
        EnsureInsideWorkArea();
        SaveWindowState();
    }

    private void ToggleThemeMenuItem_Click(object sender, RoutedEventArgs e)
    {
        _theme = _theme == "warm" ? "mint" : "warm";
        ApplyTheme();
        SaveWindowState();
    }

    private void ToggleTopmostMenuItem_Click(object sender, RoutedEventArgs e)
    {
        Topmost = !Topmost;
        SyncTopmostMenuHeader();
        SaveWindowState();
    }

    private void ExitMenuItem_Click(object sender, RoutedEventArgs e) => ExitApplication();

    private void OpenSpiritDeskButton_Click(object sender, RoutedEventArgs e) => OpenMainWindow();

    private void CollapseButton_Click(object sender, RoutedEventArgs e) => Collapse();

    /// <summary>面板内按钮/输入框按下时不触发浮球拖拽与单击展开。</summary>
    private void Control_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragArm = false;
        _singleClickTimer.Stop();
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_isInitializing || !_isExpanded)
        {
            return;
        }

        _panelWidth = Clamp(Width, MinExpandedWidth, MaxExpandedWidth);
        _panelHeight = Clamp(Height, MinExpandedHeight, MaxExpandedHeight);
        SaveWindowState();
    }

    // --- 与 MainWindow 联动：激活主窗；退出时关闭主窗或结束应用 ---

    private void OpenMainWindow()
    {
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Show();
        _mainWindow.Activate();
    }

    /// <summary>优先关闭主窗（会连带结束 Shell）；主窗未加载则直接 Shutdown。</summary>
    private void ExitApplication()
    {
        if (_isExiting)
        {
            return;
        }

        _isExiting = true;
        try
        {
            if (_mainWindow.IsLoaded)
            {
                _mainWindow.Close();
                return;
            }

            Close();
            Application.Current.Shutdown();
        }
        finally
        {
            _isExiting = false;
        }
    }

    // --- 收起圆球 ↔ 展开面板（BubbleView / PanelView 切换，锚定右下角缩放）---

    private void ToggleExpanded()
    {
        if (_isExpanded) Collapse(); else Expand();
    }

    private void Expand()
    {
        if (_isExpanded)
        {
            return;
        }

        _isExpanded = true;
        BubbleView.Visibility = Visibility.Collapsed;
        PanelView.Visibility = Visibility.Visible;
        ResizeMode = ResizeMode.CanResizeWithGrip;
        MinWidth = MinExpandedWidth;
        MinHeight = MinExpandedHeight;
        MaxWidth = MaxExpandedWidth;
        MaxHeight = MaxExpandedHeight;
        SetWindowSizeKeepBottomRight(_panelWidth, _panelHeight);
        EnsureInsideWorkArea();
        SaveWindowState();
        _ = ExpandRefreshAsync();
    }

    private void Collapse()
    {
        if (!_isExpanded)
        {
            return;
        }

        _isExpanded = false;
        _panelWidth = Clamp(Width, MinExpandedWidth, MaxExpandedWidth);
        _panelHeight = Clamp(Height, MinExpandedHeight, MaxExpandedHeight);
        PanelView.Visibility = Visibility.Collapsed;
        BubbleView.Visibility = Visibility.Visible;
        ResizeMode = ResizeMode.NoResize;
        MinWidth = CollapsedSize;
        MinHeight = CollapsedSize;
        MaxWidth = CollapsedSize;
        MaxHeight = CollapsedSize;
        SetWindowSizeKeepBottomRight(CollapsedSize, CollapsedSize);
        EnsureInsideWorkArea();
        SaveWindowState();
    }

    /// <summary>改大小时固定窗口右下角不动，避免展开时「跳」到别的位置。</summary>
    private void SetWindowSizeKeepBottomRight(double width, double height)
    {
        var right = Left + Width;
        var bottom = Top + Height;

        Width = width;
        Height = height;

        Left = right - Width;
        Top = bottom - Height;
    }

    private void EnsureInsideWorkArea()
    {
        var workArea = SystemParameters.WorkArea;
        if (Left < workArea.Left) Left = workArea.Left;
        if (Top < workArea.Top) Top = workArea.Top;
        if (Left + Width > workArea.Right) Left = workArea.Right - Width;
        if (Top + Height > workArea.Bottom) Top = workArea.Bottom - Height;
    }

    private static bool IsInteractionSource(object? source) => source is Button || source is TextBox || source is PasswordBox;

    // --- HTTP：拉取/切换精灵状态；CreateRequestAsync 附带 Cookie（云端登录）---

    /// <summary>GET /api/companion/current，更新 _status 并刷新球与面板 UI。</summary>
    private async Task RefreshStatusAsync()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(4));
            var endpoint = new Uri(_baseUri, "/api/companion/current");
            Log($"Companion API URL: {endpoint}");
            using var request = await CreateRequestAsync(HttpMethod.Get, endpoint).ConfigureAwait(false);
            using var response = await _httpClient.SendAsync(request, cts.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                Log($"Companion API HTTP {(int)response.StatusCode}");
                return;
            }

            var dto = await response.Content.ReadFromJsonAsync<CompanionStatusDto>(ApiJsonOptions, cts.Token).ConfigureAwait(false);
            if (dto is null)
            {
                Log("Companion API returned null payload.");
                return;
            }

            Log($"Companion API result: name={dto.Name}, title={dto.Title}, imageUrl={dto.ImageUrl}");
            _status = NormalizeStatus(dto);
            await Dispatcher.InvokeAsync(() => ApplyStatus(_status));
        }
        catch (Exception ex)
        {
            Log($"Companion API failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>MainWindow 被激活时调用，立即同步一次 Web 侧精灵状态。</summary>
    public void NotifyHostActivated()
    {
        _ = HostFocusRefreshAsync();
    }

    private async Task HostFocusRefreshAsync()
    {
        await RefreshStatusAsync();
    }

    private async Task ExpandRefreshAsync()
    {
        await RefreshStatusAsync();
    }

    private async Task PollTickAsync()
    {
        try
        {
            await RefreshStatusAsync();
        }
        catch (Exception ex)
        {
            Log($"Poll tick failed: {ex.Message}");
        }
    }

    /// <summary>POST select-spirit；服务端写 UserProfile 并可能追加聊天，浮球再 Refresh。</summary>
    private async Task SelectSpiritAsync(string spiritId)
    {
        if (string.IsNullOrWhiteSpace(spiritId))
        {
            return;
        }

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(12));
            var uri = new Uri(_baseUri, "/api/companion/select-spirit");
            var payloadJson = JsonSerializer.Serialize(new { spiritId = spiritId.Trim() });
            using var request = await CreateRequestAsync(HttpMethod.Post, uri, payloadJson).ConfigureAwait(false);
            using var response = await _httpClient.SendAsync(request, cts.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                Log($"Select spirit HTTP {(int)response.StatusCode}");
                return;
            }

            await RefreshStatusAsync();
        }
        catch (Exception ex)
        {
            Log($"Select spirit error: {ex.Message}");
        }
    }

    /// <summary>API 缺字段时用 DefaultStatus 兜底，数值钳制为非负。</summary>
    private static CompanionStatusDto NormalizeStatus(CompanionStatusDto input)
    {
        return new CompanionStatusDto
        {
            Success = input.Success,
            Name = string.IsNullOrWhiteSpace(input.Name) ? DefaultStatus.Name : input.Name,
            Title = string.IsNullOrWhiteSpace(input.Title) ? DefaultStatus.Title : input.Title,
            StatusText = string.IsNullOrWhiteSpace(input.StatusText) ? DefaultStatus.StatusText : input.StatusText,
            ImageUrl = string.IsNullOrWhiteSpace(input.ImageUrl) ? DefaultStatus.ImageUrl : input.ImageUrl,
            Mood = Math.Max(0, input.Mood),
            Affinity = Math.Max(0, input.Affinity),
            Level = Math.Max(1, input.Level),
            Coins = Math.Max(0, input.Coins)
        };
    }

    /// <summary>组装 HttpRequestMessage；有 cookieHeaderProvider 时附加 Cookie 头（与浏览器登录态一致）。</summary>
    private async Task<HttpRequestMessage> CreateRequestAsync(HttpMethod method, Uri uri, string? payloadJson = null)
    {
        var request = new HttpRequestMessage(method, uri);
        if (!string.IsNullOrWhiteSpace(payloadJson))
        {
            request.Content = new StringContent(payloadJson, Encoding.UTF8, "application/json");
        }

        if (_cookieHeaderProvider is null)
        {
            return request;
        }

        var cookieHeader = await _cookieHeaderProvider().ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(cookieHeader))
        {
            request.Headers.TryAddWithoutValidation("Cookie", cookieHeader);
        }

        return request;
    }

    // --- UI 绑定：文案指标 + 异步加载 wwwroot 精灵 PNG ---

    private void ApplyStatus(CompanionStatusDto dto)
    {
        NameText.Text = dto.Name;
        TitleText.Text = dto.Title;
        StatusText.Text = dto.StatusText;
        MetricsLine1Text.Text = $"心情 {dto.Mood} · 亲密 {dto.Affinity} · Lv.{dto.Level}";
        MetricsLine2Text.Text = $"金币 {dto.Coins}";
        Log($"ApplyStatus called. imageUrl={dto.ImageUrl}");
        _ = ApplySpiritImageAsync(dto.ImageUrl);
    }

    private async Task<BitmapImage?> LoadSpiritBitmapAsync(string imageUrl)
    {
        try
        {
            var imageUri = BuildImageUri(imageUrl);
            var bytes = await _httpClient.GetByteArrayAsync(imageUri).ConfigureAwait(false);
            using var ms = new MemoryStream(bytes);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = ms;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch (Exception ex)
        {
            Log($"LoadSpiritBitmapAsync failed: {ex.Message}");
            return null;
        }
    }

    private async Task ApplySpiritImageAsync(string imageUrl)
    {
        Log($"ApplySpiritImageAsync called. rawImageUrl={imageUrl}");

        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            Log("Image URL empty, showing fallback glyph.");
            await Dispatcher.InvokeAsync(ShowFallbackGlyph);
            return;
        }

        try
        {
            var bitmap = await LoadSpiritBitmapAsync(imageUrl).ConfigureAwait(false);
            if (bitmap is null)
            {
                await Dispatcher.InvokeAsync(ShowFallbackGlyph);
                return;
            }

            await Dispatcher.InvokeAsync(() =>
            {
                BubbleSpiritImage.Source = bitmap;
                PanelSpiritImage.Source = bitmap;
                BubbleSpiritImage.Visibility = Visibility.Visible;
                PanelSpiritImage.Visibility = Visibility.Visible;
                BubbleFallbackView.Visibility = Visibility.Collapsed;
                AvatarFallbackView.Visibility = Visibility.Collapsed;
                BubbleFallbackText.Visibility = Visibility.Collapsed;
                PanelFallbackText.Visibility = Visibility.Collapsed;
                Log("ApplySpiritImageAsync UI applied.");
            });
        }
        catch (Exception ex)
        {
            Log($"ApplySpiritImageAsync failed: {ex.GetType().Name}: {ex.Message}");
            await Dispatcher.InvokeAsync(ShowFallbackGlyph);
        }
    }

    /// <summary>相对路径如 /assets/images/spirit-light.png 拼到 _baseUri；绝对 URL 直接用。</summary>
    private Uri BuildImageUri(string imageUrl)
    {
        if (Uri.TryCreate(imageUrl, UriKind.Absolute, out var absolute))
        {
            return absolute;
        }

        var normalizedPath = imageUrl.StartsWith("/") ? imageUrl : "/" + imageUrl;
        return new Uri(_baseUri, normalizedPath);
    }

    /// <summary>图片下载失败时显示 XAML 里的 Fallback 字形，不崩溃。</summary>
    private void ShowFallbackGlyph()
    {
        BubbleSpiritImage.Source = null;
        PanelSpiritImage.Source = null;
        BubbleSpiritImage.Visibility = Visibility.Collapsed;
        PanelSpiritImage.Visibility = Visibility.Collapsed;
        BubbleFallbackView.Visibility = Visibility.Visible;
        AvatarFallbackView.Visibility = Visibility.Visible;
        BubbleFallbackText.Visibility = Visibility.Visible;
        PanelFallbackText.Visibility = Visibility.Visible;
        Log("Fallback glyph visible. Image Collapsed.");
    }

    /// <summary>API 返回 JSON 的反序列化模型；字段名与 camelCase JSON 一致。</summary>
    private sealed class CompanionStatusDto
    {
        public bool Success { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string StatusText { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public int Mood { get; set; }
        public int Affinity { get; set; }
        public int Level { get; set; }
        public int Coins { get; set; }
    }

    // --- 持久化：ShellSettings 读写位置、展开、主题 mint/warm、置顶 ---

    private void RestoreWindowState()
    {
        var settings = _settingsService.Load();

        var theme = string.IsNullOrWhiteSpace(settings.CompanionTheme) ? "mint" : settings.CompanionTheme.Trim().ToLowerInvariant();
        _theme = theme == "warm" ? "warm" : "mint";
        ApplyTheme();

        Topmost = settings.CompanionBubbleTopmost;
        SyncTopmostMenuHeader();

        _panelWidth = Clamp(settings.CompanionPanelWidth <= 0 ? DefaultExpandedWidth : settings.CompanionPanelWidth, MinExpandedWidth, MaxExpandedWidth);
        _panelHeight = Clamp(settings.CompanionPanelHeight <= 0 ? DefaultExpandedHeight : settings.CompanionPanelHeight, MinExpandedHeight, MaxExpandedHeight);

        if (settings.CompanionBubbleExpanded) ExpandWithoutSaving(); else CollapseWithoutSaving();

        if (IsSavedPositionVisible(settings.CompanionBubbleLeft, settings.CompanionBubbleTop))
        {
            Left = settings.CompanionBubbleLeft;
            Top = settings.CompanionBubbleTop;
            EnsureInsideWorkArea();
            return;
        }

        PositionAtPrimaryWorkAreaBottomRight();
        EnsureInsideWorkArea();
    }

    private void SaveWindowState()
    {
        if (_isInitializing) return;

        var settings = new ShellSettings
        {
            CompanionBubbleLeft = Left,
            CompanionBubbleTop = Top,
            CompanionBubbleExpanded = _isExpanded,
            CompanionBubbleTopmost = Topmost,
            CompanionPanelWidth = _panelWidth,
            CompanionPanelHeight = _panelHeight,
            CompanionTheme = _theme
        };

        _settingsService.Save(settings);
    }

    /// <summary>启动恢复布局时不触发 SaveWindowState（_isInitializing 已为 true）。</summary>
    private void ExpandWithoutSaving()
    {
        _isExpanded = true;
        BubbleView.Visibility = Visibility.Collapsed;
        PanelView.Visibility = Visibility.Visible;
        ResizeMode = ResizeMode.CanResizeWithGrip;
        Width = _panelWidth;
        Height = _panelHeight;
        MinWidth = MinExpandedWidth;
        MinHeight = MinExpandedHeight;
        MaxWidth = MaxExpandedWidth;
        MaxHeight = MaxExpandedHeight;
    }

    private void CollapseWithoutSaving()
    {
        _isExpanded = false;
        PanelView.Visibility = Visibility.Collapsed;
        BubbleView.Visibility = Visibility.Visible;
        ResizeMode = ResizeMode.NoResize;
        MinWidth = CollapsedSize;
        MinHeight = CollapsedSize;
        MaxWidth = CollapsedSize;
        MaxHeight = CollapsedSize;
        Width = CollapsedSize;
        Height = CollapsedSize;
    }

    private bool IsSavedPositionVisible(double left, double top)
    {
        if (!double.IsFinite(left) || !double.IsFinite(top)) return false;

        var workArea = SystemParameters.WorkArea;
        var probeWidth = _isExpanded ? _panelWidth : CollapsedSize;
        var probeHeight = _isExpanded ? _panelHeight : CollapsedSize;
        var right = left + probeWidth;
        var bottom = top + probeHeight;
        return right > workArea.Left + 24 && bottom > workArea.Top + 24 && left < workArea.Right - 24 && top < workArea.Bottom - 24;
    }

    private void SyncTopmostMenuHeader()
    {
        /* 置顶文案在每次打开右键菜单时根据 Topmost 动态生成 */
    }

    /// <summary>切换 BubbleBorder / PanelView 背景色；与 site.css 薄荷/暖色两套视觉对应。</summary>
    private void ApplyTheme()
    {
        if (_theme == "warm")
        {
            BubbleBorder.Background = ToBrush("#FFF7EF");
            BubbleBorder.BorderBrush = ToBrush("#F2C8A1");
            PanelView.Background = ToBrush("#FFF7EF");
            PanelView.BorderBrush = ToBrush("#F2C8A1");
            AvatarBorder.Background = ToBrush("#FDE7D3");
            AvatarBorder.BorderBrush = ToBrush("#EDBE91");
            return;
        }

        BubbleBorder.Background = ToBrush("#F6FBF7");
        BubbleBorder.BorderBrush = ToBrush("#BFE5CF");
        PanelView.Background = ToBrush("#F2FAF5");
        PanelView.BorderBrush = ToBrush("#BFE5CF");
        AvatarBorder.Background = ToBrush("#E6F4EC");
        AvatarBorder.BorderBrush = ToBrush("#B8DFC9");
    }

    private static Brush ToBrush(string hex)
    {
        return (Brush)new BrushConverter().ConvertFromString(hex)!;
    }

    private static double Clamp(double value, double min, double max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }
    /// <summary>调试日志：%AppData%/SpiritDesk/companion-bubble.log 与 Debug 输出。</summary>
    private void Log(string message)
    {
        try
        {
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}";
            var dir = Path.GetDirectoryName(_logFilePath);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.AppendAllText(_logFilePath, line + Environment.NewLine);
            Debug.WriteLine(line);
        }
        catch
        {
        }
    }
}


