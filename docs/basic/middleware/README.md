# 中间件专题（`docs/basic/middleware/`）

> 本文件夹专门讲：**中间件是什么**、**ASP.NET Core 请求管道怎么工作**、**SpiritDesk 项目里用了哪些中间件、顺序为什么这样排**。  
> 适合零基础，也适合答辩前把「请求从浏览器到页面/API」讲清楚。

## 建议阅读顺序

| 顺序 | 文档 | 内容 |
|------|------|------|
| 1 | [01-中间件基础知识.md](./01-中间件基础知识.md) | 什么是中间件、管道、Use/Map、顺序、和 Filter 的区别 |
| 2 | [02-本项目中间件管道详解.md](./02-本项目中间件管道详解.md) | `SpiritDeskWebHost.cs` 里每一段 `Use*` / `Map*` 干什么 |
| 3 | [03-一次请求如何穿过中间件.md](./03-一次请求如何穿过中间件.md) | 打开首页、访问 CSS、调 API、未登录跳转 四条路径 |

## 核心文件

| 文件 | 作用 |
|------|------|
| `src/SpiritDesk.Web/Program.cs` | 入口：`Build` → `Run()` 启动 Kestrel |
| `src/SpiritDesk.Web/SpiritDeskWebHost.cs` | **中间件管道全部在这里组装** |
| `src/SpiritDesk.Web/Pages/*.cshtml.cs` | 管道末端：Razor Page 业务逻辑 |
| `src/SpiritDesk.Shell/MainWindow.xaml.cs` | 启动 Web 进程，WebView2 发 HTTP 请求走同一管道 |

## 一句话定位

SpiritDesk 的 Web 请求先经过 **异常处理 → 路由 → 静态文件 → 认证 → 授权** 等中间件，再落到 **Razor 页面** 或 **`/api/companion/*` 最小 API**；配置在 `SpiritDeskWebHost.Build()` 里，`Program.cs` 只负责启动。

## 答辩速背

> 中间件是 ASP.NET Core 里按顺序处理 HTTP 请求的组件。本项目在 `SpiritDeskWebHost.cs` 中配置管道：先 `UseExceptionHandler` 和 `UseRouting`，再 `UseStaticFiles` 提供 wwwroot，开启登录门禁时加 `UseAuthentication` 和 `UseAuthorization`，最后 `MapRazorPages` 和 `MapGet`/`MapPost` 映射页面与浮球 API。顺序很重要，例如认证必须在授权之前。

## 和其他文档的关系

| 文档 | 区别 |
|------|------|
| **本文件夹** | 从零讲中间件 + 本项目管道 |
| [auth/](../auth/README.md) | Cookie 认证、`HttpOnly`、登录注册细节 |
| [webview2/](../webview2/README.md) | WebView2、Shell 如何连本地 Web |
| [11-cshtml与wwwroot一次请求如何生成页面.md](../11-cshtml与wwwroot一次请求如何生成页面.md) | SSR 与静态资源分工 |
| [13-Program入口与桌面Web运行原理.md](../../13-Program入口与桌面Web运行原理.md) | Program / 启动时序 |
