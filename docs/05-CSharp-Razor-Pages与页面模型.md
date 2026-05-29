# 05 - C# 讲解：Razor Pages 与页面模型

> **`.cshtml` 是不是 SSR？** 是。服务器执行 Razor 生成 HTML，再返回浏览器。  
> **Web csproj / 每个 cshtml 干什么？** 见 [10-SpiritDesk.Web项目与cshtml详解.md](./10-SpiritDesk.Web项目与cshtml详解.md)。  
> **wwwroot？** 见 [basic/08-wwwroot与静态资源详解.md](./basic/08-wwwroot与静态资源详解.md)。

## 1. Razor Pages 路由规则

文件在 `Pages/` 下，路由由路径推导：

| 文件 | URL | PageModel |
|------|-----|-----------|
| `Pages/Index.cshtml` | `/` 或 `/Index` | `IndexModel` |
| `Pages/Chat.cshtml` | `/Chat` | `ChatModel` |
| `Pages/Assistant.cshtml` | `/Assistant` | `AssistantModel` |
| `Pages/Settings.cshtml` | `/Settings` | `SettingsModel` |
| `Pages/Login.cshtml` | `/Login` | `LoginModel` |
| `Pages/Register.cshtml` | `/Register` | `RegisterModel` |
| `Pages/Spirits/Select.cshtml` | `/Spirits/Select` | `SelectModel` |

页面顶部指令示例：

```cshtml
@page
@model SpiritDesk.Web.Pages.IndexModel
```

## 2. 布局与共享组件

- `_ViewStart.cshtml`：设置 `Layout = "_Layout"`
- `Shared/_Layout.cshtml`：HTML 骨架、引用 `site.css`、`site.js`
- `Shared/_SidebarCollapseToggle.cshtml`：侧栏折叠
- `Shared/_FloatingCompanion.cshtml`：网页内右下角悬浮精灵（非 WPF 浮球）

工作台页面常用结构：

```html
<div class="workbench-app" data-workbench-root style="--spirit-accent:@spirit.AccentColor">
  <aside class="sidebar">...</aside>
  <main class="main-workbench">...</main>
</div>
```

`--spirit-accent` 将当前精灵强调色注入 CSS，与全站薄荷主色并存。

## 3. PageModel 基本模式

```csharp
public class IndexModel(SpiritDeskService spiritDeskService) : PageModel
{
    public SpiritDeskViewModel Desk { get; private set; } = new();
    public string? NoticeMessage { get; set; }

    [BindProperty]
    public string MessageText { get; set; } = "";

    public async Task<IActionResult> OnGetAsync()
    {
        if (await spiritDeskService.NeedsSpiritSelectionAsync())
            return RedirectToPage("/Spirits/Select");

        Desk = await spiritDeskService.BuildViewModelAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostSendMessageAsync()
    {
        await spiritDeskService.SendMessageAsync(MessageText);
        return RedirectToPage();
    }
}
```

要点：

- **OnGetAsync**：显示页面
- **OnPostXxxAsync**：处理表单 POST，`asp-page-handler="SendMessage"` 对应 `OnPostSendMessageAsync`
- **[BindProperty]**：自动绑定表单字段
- **RedirectToPage**：POST-Redirect-GET，避免重复提交

## 4. 各 `.cshtml` 文件职责总表

| 文件 | SSR 页面 URL | 界面职责 | 逻辑文件 |
|------|--------------|----------|----------|
| `Index.cshtml` | `/` | 工作台、任务、猜拳 | `Index.cshtml.cs` |
| `Chat.cshtml` | `/Chat` | 聊天历史 | `Chat.cshtml.cs` |
| `Assistant.cshtml` | `/Assistant` | 签到投喂等 | `Assistant.cshtml.cs` |
| `Settings.cshtml` | `/Settings` | 设置 | `Settings.cshtml.cs` |
| `Spirits/Select.cshtml` | `/Spirits/Select` | 五选一 | `Select.cshtml.cs` |
| `Login.cshtml` | `/Login` | 登录表单 | `Login.cshtml.cs` |
| `Register.cshtml` | `/Register` | 注册表单 | `Register.cshtml.cs` |
| `Logout.cshtml` | `/Logout` | 登出 POST | `Logout.cshtml.cs` |
| `Error.cshtml` | `/Error` | 错误页 | `Error.cshtml.cs` |
| `Privacy.cshtml` | `/Privacy` | 隐私占位 | `Privacy.cshtml.cs` |
| `Shared/_Layout.cshtml` | — | 全站外壳 | 无 `.cs` |
| `Shared/_FloatingCompanion.cshtml` | — | 网页内悬浮精灵 | 无 `.cs` |
| `_ViewStart.cshtml` | — | 默认 Layout | 无 |
| `_ViewImports.cshtml` | — | 全局 using | 无 |

## 5. 各页面职责（展开）

### Index（工作台）

- 展示任务日历（`desk-calendar.js`）、待办列表、24h 提醒、聊天摘要
- Handler：添加/更新/删除/完成任务、发送消息、猜拳（`PlayGame`）
- 未选精灵时重定向到 `Spirits/Select`

### Spirits/Select

- 五选一精灵卡片，POST 选择后写入 `CurrentSpiritId`

### Chat

- 完整聊天记录 + 发送框

### Assistant

- 签到、投喂、学习/休息/鼓励等互动按钮

### Settings

- 改昵称、切换精灵、退出登录（启用认证时）

### Login / Register

- 见 [06-CSharp-认证与登录注册.md](./06-CSharp-认证与登录注册.md)

## 6. Models 与 Entities 区别

| 位置 | 类型 | 用途 |
|------|------|------|
| `SpiritDesk.Core.Entities` | EF 实体 | 数据库表 |
| `SpiritDesk.Web.Models` | ViewModel | 单页或聚合展示，如 `SpiritDeskViewModel`、`ChatHistoryViewModel` |

ViewModel 不直接映射表，由 Service 组装。

## 7. 表单与防伪

POST 表单需包含防伪令牌：

```cshtml
<form method="post" asp-page-handler="SendMessage">
    <input type="hidden" name="__RequestVerificationToken" value="..." />
</form>
```

或使用 Tag Helper 自动生成。首页部分 Handler 使用隐藏表单 + JS 提交日历任务。

## 8. 前端脚本（在 wwwroot，不是 SSR）

- `wwwroot/js/site.js`：侧栏折叠、猜拳弹层
- `wwwroot/js/desk-calendar.js`：首页日历视图（日/周/月）

脚本与 Razor 分离，通过 `data-*` 属性与 DOM 协作。

## 9. 学习路径建议

1. 从 `Index.cshtml` + `Index.cshtml.cs` 对照阅读
2. 跟踪一次 `OnPostCompleteTask` 到 `SpiritDeskService` 的调用链
3. 修改 `site.css` 中 `.workbench-app` 观察全局样式影响

下一步阅读：[06-CSharp-认证与登录注册.md](./06-CSharp-认证与登录注册.md)
