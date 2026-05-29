# 08 - API 与静态资源

## 1. Minimal API 一览

在 `SpiritDeskWebHost.Build` 末尾注册，供 **Shell 浮球** 或外部工具调用。

| 方法 | 路径 | 作用 |
|------|------|------|
| GET | `/api/companion/state` | 是否需要选精灵、当前精灵 ID、五精灵列表 |
| POST | `/api/companion/select-spirit` | 切换精灵，Body: `{ "spiritId": "light" }` |
| GET | `/api/companion/current` | 当前精灵展示信息（名称、头像 URL、心情等） |

另：`GET /healthz` 健康检查（含 DbContext 检查）。

### 认证

当 `SpiritDesk:Auth:Enabled` 为 true 时，上述 companion 接口会 `RequireAuthorization()`，需携带登录 Cookie。

### select-spirit 请求体

`Models/CompanionApiModels.cs`：

```csharp
public sealed class CompanionSelectBody
{
    public string SpiritId { get; set; } = string.Empty;
}
```

### current 响应示例字段

- `success`、`name`、`title`、`statusText`、`imageUrl`
- `mood`、`affinity`、`level`、`coins`

未选精灵时返回默认卷卷晴占位文案，`success: false`。

## 2. 静态资源 wwwroot

> **详细专篇（推荐答辩前读）**：[basic/08-wwwroot与静态资源详解.md](./basic/08-wwwroot与静态资源详解.md)  
> **与 .cshtml / SSR 的关系**：[10-SpiritDesk.Web项目与cshtml详解.md](./10-SpiritDesk.Web项目与cshtml详解.md)

```
wwwroot/
├── css/
│   └── site.css              # 全站主样式，薄荷主题变量
├── js/
│   ├── site.js               # 侧栏、猜拳弹窗
│   └── desk-calendar.js      # 首页日历
├── assets/images/
│   ├── spirit-light.png
│   ├── spirit-water.png
│   ├── spirit-air.png
│   ├── spirit-soil.png
│   └── spirit-nutrition.png
└── lib/                      # Bootstrap、jQuery（脚手架自带）
```

Razor 引用：

```cshtml
<link rel="stylesheet" href="~/css/site.css" asp-append-version="true" />
<script src="~/js/site.js" asp-append-version="true"></script>
```

`asp-append-version` 在发布时附加文件哈希，利于缓存刷新。

## 3. CSS 主题变量（节选）

```css
:root {
  --bg: #eef6f1;
  --brand-accent: #4a9d72;
  --spirit-accent: var(--brand-accent);
  --line: #dce8e2;
  ...
}
```

页面可覆盖 `--spirit-accent` 为当前精灵 `AccentColor`。

主按钮：`.action-button.mint`（薄荷渐变）。

## 4. 网页内悬浮精灵

`Pages/Shared/_FloatingCompanion.cshtml`：

- 右下角固定定位 `#floating-companion`
- 点击展开面板，链接到 Index / Chat / Assistant
- 与 WPF `CompanionBubbleWindow` 是两套实现

## 5. 图片 URL 约定

- 数据库存相对路径：`/assets/images/spirit-light.png`
- Shell 浮球加载时拼接到 `baseUrl` 绝对地址

## 6. 调用示例（curl）

本地 Web 默认端口见 `launchSettings.json`（如 5160），且认证关闭时：

```bash
curl http://127.0.0.1:5160/api/companion/current
curl -X POST http://127.0.0.1:5160/api/companion/select-spirit \
  -H "Content-Type: application/json" \
  -d "{\"spiritId\":\"water\"}"
```

下一步阅读：[09-构建运行与部署.md](./09-构建运行与部署.md)
