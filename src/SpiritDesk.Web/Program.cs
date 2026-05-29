// =============================================================================
// Program.cs — Web 服务入口（顶级语句，无 Main 方法显式写法）
// =============================================================================
// 流程：SpiritDeskWebHost.Build(args) 组装 DI/EF/路由 → app.Run() 启动 Kestrel
// C# 语法：
//   - 顶级语句：编译器生成隐式 Program 类与 Main，适合极简入口
//   - var app：类型推断为 WebApplication
// 说明：本项目最终可以作为桌面程序运行；桌面 Shell 会启动/连接这个 Web 服务，
//       再用 WebView2 把 Razor Pages 页面显示在 WPF 窗口里。
// =============================================================================

var app = SpiritDesk.Web.SpiritDeskWebHost.Build(args);
app.Run();
