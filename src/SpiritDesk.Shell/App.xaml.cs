// =============================================================================
// App.xaml.cs — WPF 应用程序入口（对应 App.xaml 的 Application 定义）
// =============================================================================
// C# 语法：
//   - partial class：另一部分由 XAML 编译器生成（InitializeComponent 等）
//   - override OnStartup：替换默认启动逻辑，手动 new MainWindow().Show()
//   - StartupEventArgs e：命令行参数入口（本项目未使用 args）
// =============================================================================

using System.Windows;

namespace SpiritDesk.Shell;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var window = new MainWindow();
        window.Show();
    }
}
