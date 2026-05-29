// =============================================================================
// MainWindow.xaml.cs — WPF 主窗口代码后置（内嵌 WebView2 + 启动 Web 进程）
// =============================================================================
// 数据结构：
//   - Process? _webProcess：本地 dotnet run Web 的子进程，可空（连远程时为 null）
//   - HttpClient：浮球与 Web API 共用，Dispose 在 OnClosing
//   - Uri baseUrl：站点根地址
// C# 语法：
//   - partial class MainWindow : Window：XAML 与 C# 分部类
//   - async void OnLoaded：事件处理器允许 async（异常需自行处理）
//   - ?. 与 ??：可空引用、空合并
// =============================================================================

using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Windows;

namespace SpiritDesk.Shell;

public partial class MainWindow : Window
{
    private Process? _webProcess;
    private readonly HttpClient _httpClient = new();
    private CompanionBubbleWindow? _companionBubble;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var baseUrl = ResolveBaseUrl(out var useRemoteEndpoint);
            if (!useRemoteEndpoint)
            {
                var webProjectRoot = ResolveWebProjectRoot();
                var webEntryAssembly = ResolveWebEntryAssembly(webProjectRoot);
                _webProcess = StartWebProcess(webProjectRoot, webEntryAssembly, baseUrl);
            }

            await WaitForServerAsync(baseUrl);

            StatusText.Text = useRemoteEndpoint
                ? $"已连接云端服务：{baseUrl}"
                : $"本地服务已启动：{baseUrl}";
            await Browser.EnsureCoreWebView2Async();
            Browser.Source = new Uri(baseUrl);

            _companionBubble = new CompanionBubbleWindow(
                new Uri(baseUrl),
                _httpClient,
                this,
                () => BuildAuthCookieHeaderAsync(baseUrl));
            Activated += OnMainWindowActivated;
            _companionBubble.Show();
        }
        catch (Exception ex)
        {
            StatusText.Text = "启动失败";
            MessageBox.Show(
                $"SpiritDesk 启动失败：{ex.Message}",
                "SpiritDesk",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        _companionBubble?.Close();
        _companionBubble = null;

        Activated -= OnMainWindowActivated;

        _httpClient.Dispose();

        if (_webProcess is null)
        {
            return;
        }

        try
        {
            if (!_webProcess.HasExited)
            {
                _webProcess.Kill(entireProcessTree: true);
                await _webProcess.WaitForExitAsync();
            }

            _webProcess.Dispose();
        }
        catch
        {
        }
    }

    private void OnMainWindowActivated(object? sender, EventArgs e)
    {
        _companionBubble?.NotifyHostActivated();
    }

    private static string ResolveWebProjectRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "src", "SpiritDesk.Web");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("未找到 SpiritDesk.Web 项目目录。");
    }

    private static string ResolveWebEntryAssembly(string webProjectRoot)
    {
        var candidates = new[] { "Release", "Debug" }
            .Select(configuration => Path.Combine(webProjectRoot, "bin", configuration, "net9.0", "SpiritDesk.Web.dll"))
            .Where(File.Exists)
            .Select(path => new FileInfo(path))
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .ToList();

        if (candidates.Count > 0)
        {
            return candidates[0].FullName;
        }

        throw new FileNotFoundException(
            $"未找到 Web 程序输出文件 SpiritDesk.Web.dll（已在 Release/Debug net9.0 路径查找）。请先构建 SpiritDesk.Web 项目。");
    }

    private static Process StartWebProcess(string webProjectRoot, string webEntryAssembly, string baseUrl)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{webEntryAssembly}\" --urls {baseUrl}",
            WorkingDirectory = webProjectRoot,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        // 本地子进程：Development 便于 .env / appsettings.Development.json。
        // 默认 Shell 免登录（SpiritDesk__Auth__Enabled=false）；答辩演示登录注册时设环境变量 SPIRITDESK_REQUIRE_AUTH=1。
        // 连 SPIRITDESK_REMOTE_BASEURL 云端时不走此处，门禁由远端站点配置决定。
        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        if (!IsShellAuthRequired())
        {
            startInfo.Environment["SpiritDesk__Auth__Enabled"] = "false";
        }

        var process = Process.Start(startInfo);
        if (process is null)
        {
            throw new InvalidOperationException("无法启动本地 Web 服务进程。");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return process;
    }

    private static string ResolveBaseUrl(out bool useRemoteEndpoint)
    {
        var remoteBaseUrl = Environment.GetEnvironmentVariable("SPIRITDESK_REMOTE_BASEURL");
        if (!string.IsNullOrWhiteSpace(remoteBaseUrl))
        {
            useRemoteEndpoint = true;
            return remoteBaseUrl.Trim().TrimEnd('/');
        }

        useRemoteEndpoint = false;
        var port = GetFreePort();
        return $"http://127.0.0.1:{port}";
    }

    private async Task WaitForServerAsync(string baseUrl)
    {
        const int maxAttempts = 40;

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            if (_webProcess?.HasExited == true)
            {
                throw new InvalidOperationException("本地 Web 服务进程已提前退出。");
            }

            try
            {
                using var response = await _httpClient.GetAsync(baseUrl);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch
            {
            }

            await Task.Delay(250);
        }

        throw new TimeoutException("等待本地 Web 服务启动超时。");
    }

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    /// <summary>
    /// 环境变量 SPIRITDESK_REQUIRE_AUTH=1 时，本地子进程不覆盖 Auth.Enabled，沿用 Development 配置（显示登录/注册）。
    /// </summary>
    private static bool IsShellAuthRequired()
    {
        var value = Environment.GetEnvironmentVariable("SPIRITDESK_REQUIRE_AUTH");
        return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<string?> BuildAuthCookieHeaderAsync(string baseUrl)
    {
        if (Browser.CoreWebView2 is null)
        {
            return null;
        }

        try
        {
            var cookies = await Browser.CoreWebView2.CookieManager.GetCookiesAsync(baseUrl);
            var authPairs = cookies
                .Where(static cookie => string.Equals(cookie.Name, "SpiritDesk.Auth", StringComparison.Ordinal))
                .Select(static cookie => $"{cookie.Name}={cookie.Value}")
                .ToList();

            return authPairs.Count == 0 ? null : string.Join("; ", authPairs);
        }
        catch
        {
            return null;
        }
    }
}
