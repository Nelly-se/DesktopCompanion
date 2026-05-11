using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace SpiritDesk.Shell;

public partial class CompanionBubbleWindow : Window
{
    private const double CollapsedSize = 88;
    private const double DefaultExpandedWidth = 320;
    private const double DefaultExpandedHeight = 220;
    private const double MinExpandedWidth = 320;
    private const double MinExpandedHeight = 220;
    private const double MaxExpandedWidth = 420;
    private const double MaxExpandedHeight = 260;

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
    private readonly ShellSettingsService _settingsService = new();
    private readonly DispatcherTimer _singleClickTimer;
    private readonly string _logFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SpiritDesk",
        "companion-bubble.log");
    private Point _pressMouseRelative;
    private bool _dragArm;
    private bool _isExiting;
    private bool _isExpanded;
    private bool _isInitializing;
    private double _panelWidth = DefaultExpandedWidth;
    private double _panelHeight = DefaultExpandedHeight;
    private string _theme = "mint";
    private string _lastImageUrl = string.Empty;
    private CompanionStatusDto _status = DefaultStatus;

    public CompanionBubbleWindow(Uri baseUri, HttpClient httpClient, Window mainWindow)
    {
        InitializeComponent();
        _baseUri = baseUri;
        _httpClient = httpClient;
        _mainWindow = mainWindow;
        ToolTip = "SpiritDesk 桌面浮球：单击展开，双击打开主窗口";

        _singleClickTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(230) };
        _singleClickTimer.Tick += (_, _) =>
        {
            _singleClickTimer.Stop();
            ToggleExpanded();
        };

        Log("CompanionBubbleWindow constructed.");

        Loaded += async (_, _) =>
        {
            Log("CompanionBubbleWindow Loaded.");
            _isInitializing = true;
            RestoreWindowState();
            ApplyStatus(_status);
            await RefreshStatusAsync();
            _isInitializing = false;
        };
        Closing += (_, _) => SaveWindowState();
    }

    private void PositionAtPrimaryWorkAreaBottomRight()
    {
        var workArea = SystemParameters.WorkArea;
        const double margin = 18;
        Left = workArea.Right - Width - margin;
        Top = workArea.Bottom - Height - margin;
    }

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

    private void OpenMainWindow()
    {
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Show();
        _mainWindow.Activate();
    }

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
        _ = RefreshStatusAsync();
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

    private async Task RefreshStatusAsync()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(4));
            var endpoint = new Uri(_baseUri, "/api/companion/current");
            Log($"Companion API URL: {endpoint}");
            var dto = await _httpClient.GetFromJsonAsync<CompanionStatusDto>(endpoint, cts.Token);
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

    private async Task ApplySpiritImageAsync(string imageUrl)
    {
        var stage = "init";
        Log($"ApplySpiritImageAsync called. rawImageUrl={imageUrl}");

        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            Log("Image URL empty, showing fallback glyph.");
            ShowFallbackGlyph();
            return;
        }

        if (imageUrl == _lastImageUrl)
        {
            Log("Image URL unchanged, skip reloading.");
            return;
        }

        try
        {
            stage = "resolve-url";
            var imageUri = BuildImageUri(imageUrl);
            Log($"Resolved fullImageUrl={imageUri}");
            stage = "download-start";
            Log("Start downloading image bytes.");
            var bytes = await _httpClient.GetByteArrayAsync(imageUri);
            stage = "download-success";
            Log($"Image bytes downloaded. size={bytes.Length}");

            BitmapImage bitmap;
            stage = "bitmap-create-start";
            Log("Stage 2: bitmap create start.");
            using (var ms = new MemoryStream(bytes))
            {
                bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = ms;
                bitmap.EndInit();
                bitmap.Freeze();
            }
            stage = "bitmap-create-success";
            Log("Stage 2: bitmap create success.");

            stage = "ui-apply-start";
            Log("Stage 3: apply UI start.");
            BubbleSpiritImage.Source = bitmap;
            PanelSpiritImage.Source = bitmap;
            stage = "ui-source-assigned";
            Log("Stage 3: image source assigned.");
            BubbleSpiritImage.Visibility = Visibility.Visible;
            PanelSpiritImage.Visibility = Visibility.Visible;
            BubbleFallbackView.Visibility = Visibility.Collapsed;
            AvatarFallbackView.Visibility = Visibility.Collapsed;
            BubbleFallbackText.Visibility = Visibility.Collapsed;
            PanelFallbackText.Visibility = Visibility.Collapsed;
            stage = "ui-fallback-collapsed";
            Log("Stage 3: fallback collapsed.");
            _lastImageUrl = imageUrl;
            stage = "done";
            Log("Image load success. Image Visible; fallback text Collapsed.");
        }
        catch (Exception ex)
        {
            Log($"Image load failed at stage '{stage}': {ex.GetType().Name}: {ex.Message}");
            ShowFallbackGlyph();
        }
    }

    private Uri BuildImageUri(string imageUrl)
    {
        if (Uri.TryCreate(imageUrl, UriKind.Absolute, out var absolute))
        {
            return absolute;
        }

        var normalizedPath = imageUrl.StartsWith("/") ? imageUrl : "/" + imageUrl;
        return new Uri(_baseUri, normalizedPath);
    }

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
        ToggleTopmostMenuItem.Header = Topmost ? "取消置顶" : "置顶";
    }

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
            ToggleThemeMenuItem.Header = "切换到薄荷主题";
            return;
        }

        BubbleBorder.Background = ToBrush("#F6FBF7");
        BubbleBorder.BorderBrush = ToBrush("#BFE5CF");
        PanelView.Background = ToBrush("#F2FAF5");
        PanelView.BorderBrush = ToBrush("#BFE5CF");
        AvatarBorder.Background = ToBrush("#E6F4EC");
        AvatarBorder.BorderBrush = ToBrush("#B8DFC9");
        ToggleThemeMenuItem.Header = "切换到暖色主题";
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


