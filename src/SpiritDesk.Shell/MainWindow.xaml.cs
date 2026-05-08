using System.ComponentModel;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Windows;
using Microsoft.AspNetCore.Builder;
using SpiritDesk.Web;

namespace SpiritDesk.Shell;

public partial class MainWindow : Window
{
    private WebApplication? _webApplication;

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
            var port = GetFreePort();
            var baseUrl = $"http://127.0.0.1:{port}";
            var contentRoot = ResolveWebProjectRoot();
            var webRoot = Path.Combine(contentRoot, "wwwroot");

            _webApplication = SpiritDeskWebHost.Build(
                Array.Empty<string>(),
                contentRoot,
                webRoot,
                [baseUrl]);

            await _webApplication.StartAsync();
            StatusText.Text = $"本地服务已启动：{baseUrl}";

            await Browser.EnsureCoreWebView2Async();
            Browser.Source = new Uri(baseUrl);
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
        if (_webApplication is null)
        {
            return;
        }

        try
        {
            await _webApplication.StopAsync();
            await _webApplication.DisposeAsync();
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

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
