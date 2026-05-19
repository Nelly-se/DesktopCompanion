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

        // 本地桌面模式仍使用 Development，便于加载 .env / 本地配置；
        // 但桌面壳自带的是单机本地 Web，不再要求额外登录。
        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        startInfo.Environment["SpiritDesk__Auth__Enabled"] = "false";

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
