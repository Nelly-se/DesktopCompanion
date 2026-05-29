# WebView2 与桌面 + Web 专题（`docs/basic/webview2/`）

> 本文件夹专门讲：**WebView2 是什么**、**前端网页和桌面程序怎么分工**、**SpiritDesk 里 WPF + WebView2 + 本地 Web 服务怎么配合**。  
> 适合零基础，也适合答辩前把「桌面壳 + 网页界面」讲清楚。

## 建议阅读顺序

| 顺序 | 文档 | 内容 |
|------|------|------|
| 1 | [01-WebView2是什么.md](./01-WebView2是什么.md) | WebView2 定义、和浏览器/Edge 的关系、能做什么不能做什么 |
| 2 | [02-前端Web与桌面端分工.md](./02-前端Web与桌面端分工.md) | 网页端 vs 桌面端、SpiritDesk 三层结构 |
| 3 | [03-本项目WebView2怎么用.md](./03-本项目WebView2怎么用.md) | MainWindow、XAML 控件、启动流程、浮球与 Cookie |
| 4 | [04-端口Cookie与本地Web子进程.md](./04-端口Cookie与本地Web子进程.md) | 谁设端口、Web 子进程、Cookie 存哪 |
| 5 | [05-常见方案对比与答辩话术.md](./05-常见方案对比与答辩话术.md) | Electron / 纯 WPF / 纯 Web、老师可能怎么问 |

## 和其他文档的关系

| 文档 | 区别 |
|------|------|
| **本文件夹** | 从零讲 WebView2 + 桌面/Web 分工 |
| [07-CSharp-Shell桌面宿主.md](../../07-CSharp-Shell桌面宿主.md) | Shell 代码级说明 |
| [13-Program入口与桌面Web运行原理.md](../../13-Program入口与桌面Web运行原理.md) | Program / 启动时序 |
| [14-WPF-App.xaml与App.xaml.cs详解.md](../../14-WPF-App.xaml与App.xaml.cs详解.md) | WPF 入口与 XAML |
| [basic/auth/](../auth/README.md) | 登录注册、Cookie 认证细节 |

## 一句话定位

SpiritDesk 的桌面窗口是 **WPF 壳 + WebView2 浏览器控件**；窗口里看到的页面来自 **本地或远程的 SpiritDesk.Web**；桌面浮球是 **纯 WPF 窗口**，通过 HTTP API 和网页共用同一套后端。
