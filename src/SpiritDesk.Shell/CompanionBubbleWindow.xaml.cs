using System.Net.Http;
using System.Net.Http.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace SpiritDesk.Shell;

public partial class CompanionBubbleWindow : Window
{
    private readonly Uri _baseUri;
    private readonly HttpClient _http;
    private readonly Window _mainWindow;
    private readonly System.Windows.Threading.DispatcherTimer _syncTimer;

    private IReadOnlyList<SpiritItem> _spirits = [];
    private string? _currentSpiritId;

    private Point _pressMouseRelative;
    private bool _dragArm;

    public CompanionBubbleWindow(Uri baseUri, HttpClient http, Window mainWindow)
    {
        InitializeComponent();
        _baseUri = baseUri;
        _http = http;
        _mainWindow = mainWindow;

        ToolTip = "SpiritDesk 桌面精灵 · 按住左键拖动 · 右键切换 · 双击打开主窗口";

        Loaded += async (_, _) =>
        {
            PositionAtPrimaryWorkAreaBottomRight();
            await RefreshFromServerAsync();
        };

        _syncTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _syncTimer.Tick += async (_, _) => await RefreshFromServerAsync();
        _syncTimer.Start();
    }

    protected override void OnClosed(EventArgs e)
    {
        _syncTimer.Stop();
        base.OnClosed(e);
    }

    private void PositionAtPrimaryWorkAreaBottomRight()
    {
        var wa = SystemParameters.WorkArea;
        const double margin = 18;
        Left = wa.Right - Width - margin;
        Top = wa.Bottom - Height - margin;
    }

    private void Bubble_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
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
        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            // 某些手势下 DragMove 可能无效，忽略即可
        }
    }

    private void Bubble_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _dragArm = false;
    }

    private void Bubble_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Show();
        _mainWindow.Activate();
    }

    private void Bubble_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        ShowSpiritContextMenu();
    }

    private void ShowSpiritContextMenu()
    {
        var menu = new ContextMenu();

        if (_spirits.Count == 0)
        {
            menu.Items.Add(new MenuItem { Header = "正在加载精灵列表…", IsEnabled = false });
        }
        else
        {
            foreach (var spirit in _spirits)
            {
                var id = spirit.Id;
                var item = new MenuItem
                {
                    Header = spirit.Name,
                    FontWeight = spirit.Id == _currentSpiritId ? FontWeights.SemiBold : FontWeights.Normal
                };
                item.Click += async (_, _) => await PostSelectSpiritAsync(id);
                menu.Items.Add(item);
            }

            menu.Items.Add(new Separator());
            var refreshItem = new MenuItem { Header = "同步当前形象" };
            refreshItem.Click += async (_, _) => await RefreshFromServerAsync();
            menu.Items.Add(refreshItem);

            menu.Items.Add(new Separator());
            var resetPosItem = new MenuItem { Header = "回到屏幕右下角" };
            resetPosItem.Click += (_, _) => PositionAtPrimaryWorkAreaBottomRight();
            menu.Items.Add(resetPosItem);
        }

        menu.PlacementTarget = this;
        menu.IsOpen = true;
    }

    private async Task PostSelectSpiritAsync(string spiritId)
    {
        try
        {
            var url = new Uri(_baseUri, "/api/companion/select-spirit");
            using var response = await _http.PostAsJsonAsync(url, new { spiritId });
            if (!response.IsSuccessStatusCode)
            {
                var detail = await response.Content.ReadAsStringAsync();
                MessageBox.Show(
                    $"切换精灵失败：{detail}",
                    "SpiritDesk",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            await RefreshFromServerAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"切换精灵失败：{ex.Message}",
                "SpiritDesk",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private async Task RefreshFromServerAsync()
    {
        try
        {
            var url = new Uri(_baseUri, "/api/companion/state");
            var dto = await _http.GetFromJsonAsync<CompanionStateDto>(url);
            if (dto is null)
            {
                return;
            }

            _currentSpiritId = dto.CurrentSpiritId;
            _spirits = dto.Spirits?.Select(static x => new SpiritItem(x.Id, x.Name, x.ImagePath)).ToList()
                       ?? [];

            var path = dto.NeedsSelection || string.IsNullOrWhiteSpace(_currentSpiritId)
                ? _spirits.FirstOrDefault()?.ImagePath
                : _spirits.FirstOrDefault(s => s.Id == _currentSpiritId)?.ImagePath;

            var displayName = dto.NeedsSelection
                ? "请先在主窗口选择精灵"
                : _spirits.FirstOrDefault(s => s.Id == _currentSpiritId)?.Name ?? "精灵";

            Dispatcher.Invoke(() =>
            {
                Title = $"SpiritDesk — {displayName}";
                if (!string.IsNullOrWhiteSpace(path))
                {
                    var absolute = new Uri(_baseUri, path.TrimStart('/'));
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = absolute;
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    bmp.Freeze();
                    SpiritImage.Source = bmp;
                }
            });
        }
        catch
        {
            // 启动早期 Web 尚未就绪时忽略，定时器会重试
        }
    }

    private sealed record SpiritItem(string Id, string Name, string ImagePath);

    private sealed class CompanionStateDto
    {
        public bool NeedsSelection { get; set; }
        public string? CurrentSpiritId { get; set; }
        public List<CompanionSpiritJson>? Spirits { get; set; }
    }

    private sealed class CompanionSpiritJson
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string ImagePath { get; set; } = "";
    }
}
