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

            _companionBubble = new CompanionBubbleWindow(new Uri(baseUrl), _httpClient, this);
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
        var configurations = new[] { "Release", "Debug" };
        foreach (var configuration in configurations)
        {
            var outputDirectory = Path.Combine(webProjectRoot, "bin", configuration, "net9.0");
            var entryAssemblyPath = Path.Combine(outputDirectory, "SpiritDesk.Web.dll");
            if (File.Exists(entryAssemblyPath))
            {
                return entryAssemblyPath;
            }
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
}
