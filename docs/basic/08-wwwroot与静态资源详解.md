# 08 - wwwroot 与静态资源详解（单篇专讲）

> `wwwroot` 是 **SpiritDesk.Web** 里放「浏览器直接下载的文件」的文件夹。  
> 和 **`Pages/*.cshtml`（SSR 动态页）** 是两套机制，答辩时要说清楚分工。  
> **一次请求怎么生成整页？** 见专篇 [11-cshtml与wwwroot一次请求如何生成页面.md](./11-cshtml与wwwroot一次请求如何生成页面.md)。

---

## 0. wwwroot 是怎么来的？是不是我们自己写的？

分两层说：**文件夹从哪来** vs **里面的文件谁写的**。

### 0.1 文件夹 `wwwroot` 本身 —— 不是项目发明的，是 ASP.NET Core 约定

| 问题 | 答案 |
|------|------|
| 谁规定叫 `wwwroot`？ | **微软 ASP.NET Core**（用 `Microsoft.NET.Sdk.Web` 建网站项目时的默认约定） |
| 必须叫这名吗？ | 默认就是 `wwwroot`；可在宿主里改 `WebRootPath`，本项目**没改**，沿用默认 |
| 怎么生效？ | `SpiritDesk.Web.csproj` 用了 `Sdk.Web` → 编译时把 `wwwroot/**` 当静态 Web 资源；运行时在 `SpiritDeskWebHost.cs` 里调用 **`app.UseStaticFiles()`**，浏览器才能访问 `/css/...`、`/js/...` |
| 和 `Pages/` 一样吗？ | **不一样**。`Pages` 是 Razor SSR；`wwwroot` 是磁盘上的原样文件，**不编译成 C#** |

创建 Web 项目时（例如 `dotnet new webapp` / Visual Studio「ASP.NET Core Web App」模板），模板就会在 `SpiritDesk.Web` 下生成一个空的或带示例的 **`wwwroot` 目录**。本仓库最早提交（`Initial commit`）里已经有 `wwwroot`，说明**从立项起就按标准网站结构建的**，不是后来随便起的文件夹名。

**答辩一句话**：`wwwroot` 这个**名字和机制**来自 **ASP.NET Core 框架**；我们做的是往里面**放、改**自己的 CSS/JS/图片。

### 0.2 里面的文件 —— 三类来源（不要混成「全是自己写的」）

```
wwwroot/
├── lib/                 ← ① 脚手架/模板带来的第三方库（Bootstrap、jQuery 等）
├── css/site.css         ← ② 项目组编写并持续修改（薄荷主题、布局）
├── js/site.js 等        ← ② 项目组编写（侧栏、猜拳弹窗等少量交互）
└── assets/images/*.png  ← ③ 设计导出（Figma → PNG），放进约定路径
```

| 来源 | 路径 | 是否「自己写」 | 说明 |
|------|------|----------------|------|
| **① 模板/工具** | `wwwroot/lib/**` | **否**（第三方） | 创建 Web 项目时常用 LibMan / 模板自带，体积大；本项目**主视觉不依赖**它，登录校验等可能引用其中 jQuery Validation |
| **② 项目组** | `css/site.css`、`js/site.js`、`js/desk-calendar.js` | **是**（团队维护） | 在模板可能有的 `site.css`/`site.js` 基础上**按 SpiritDesk 需求改写、扩充**（配色、工作台、登录页等）；答辩可强调「样式与交互脚本是我们维护的」 |
| **③ 设计资源** | `assets/images/` | **是**（导出物，不是手写代码） | 精灵立绘等从 **Figma 导出 PNG**，按约定路径放置；代码里只写 URL（如 `/assets/images/spirit-light.png`） |

**老师问「wwwroot 是你写的吗？」推荐答法：**

> 文件夹是 ASP.NET Core 规定的静态资源目录；**框架负责提供目录和访问方式**（`UseStaticFiles`）。  
> **里面内容分三种**：`lib` 是模板带的第三方库；**`site.css` / `site.js` 是我们项目自己写和维护的**；图片是 Figma 导出后放进 `assets/images`。页面通过 `_Layout.cshtml` 里的 `~/css/site.css` 引用这些文件。

### 0.3 和代码里哪一行对应？

```csharp
// SpiritDeskWebHost.cs — 开启「把 wwwroot 当网站根目录下的静态文件」
app.UseStaticFiles();
```

```html
<!-- _Layout.cshtml — ~/ 在运行时指向 wwwroot -->
<link rel="stylesheet" href="~/css/site.css" asp-append-version="true" />
<script src="~/js/site.js" asp-append-version="true"></script>
```

`dotnet publish` 后，输出目录里会有 **`wwwroot` 整夹**（与 `SpiritDesk.Web.dll` 同级），部署上传服务器时一起带上（见 `artifacts/spiritdesk-web-publish/wwwroot/`）。

---

## 1. wwwroot 是什么？

ASP.NET Core 约定：项目下的 **`wwwroot/`** 目录 = **Web 根目录（Web Root）**。

- 编译/发布后，整夹复制到输出目录（如 `bin/Debug/net9.0/wwwroot/`）。
- 中间件 `app.UseStaticFiles()`（在 `SpiritDeskWebHost.cs`）开启后，浏览器可通过 URL **直接访问** 这些文件。
- **不经过** Razor、不执行 C#；请求 `/css/site.css` 就是读磁盘上的 CSS 文件。

**答辩一句话**：`wwwroot` = 网站的 **静态资源仓库**（样式、脚本、图片）；页面长什么样除了 SSR 的 HTML，还靠这里引用的 CSS/JS。

---

## 2. 浏览器怎么引用？`~/` 是什么意思？

在 `.cshtml` 里常见：

```html
<link rel="stylesheet" href="~/css/site.css" asp-append-version="true" />
<script src="~/js/site.js" asp-append-version="true"></script>
<img src="~/assets/images/spirit-light.png" alt="" />
```

| 符号 | 含义 |
|------|------|
| `~` | 网站根路径，运行时映射到 `wwwroot` |
| `~/css/site.css` | 实际文件 `wwwroot/css/site.css` |
| `asp-append-version` | 发布时加文件哈希，改 CSS 后浏览器会拉新版本 |

数据库里存的精灵图路径也常是 **`/assets/images/spirit-light.png`**（以 `/` 开头），浏览器会向 **同一站点** 请求静态文件。

---

## 3. 目录结构（本项目实际 + 约定）

```
wwwroot/
├── css/
│   └── site.css              ★ 全站主样式（薄荷主题、侧栏、登录页、工作台）
├── js/
│   ├── site.js               ★ 侧栏折叠、猜拳弹窗
│   └── desk-calendar.js      ★ 首页日历（日/周/月，若有引用）
├── assets/
│   └── images/               ★ Figma 导出的精灵 PNG、背景图（答辩展示用）
│       ├── spirit-light.png
│       ├── spirit-water.png
│       ├── spirit-air.png
│       ├── spirit-soil.png
│       └── spirit-nutrition.png
└── lib/                      ○ 第三方库（脚手架自带，一般不改）
    ├── bootstrap/            栅格、组件样式（部分页面可能间接用到）
    ├── jquery/
    └── jquery-validation/    登录/注册表单校验（通过 Partial 引入）
```

图例：**★** = 团队主要维护；**○** = 模板依赖，知道即可。

> 若仓库里暂时没有 `assets/images/`（未提交大图），运行前需按 `需求文档` / Figma 导出放到该路径，否则页面 `<img>` 会 404。

---

## 4. 和 `.cshtml`（SSR）怎么分工？

| 需求 | 放哪里 | 原因 |
|------|--------|------|
| 显示用户昵称、任务列表 | `Pages/Index.cshtml` + `Index.cshtml.cs` | 数据来自 SQLite，每次请求不同 → **SSR** |
| 全站颜色、圆角、侧栏样式 | `wwwroot/css/site.css` | 所有页面共用，内容固定 → **静态** |
| 侧栏折叠、弹窗开关 | `wwwroot/js/site.js` | 浏览器里跑一点交互，不改页面路由 → **静态 JS** |
| 精灵头像 PNG | `wwwroot/assets/images/*.png` | 纯文件，URL 固定 → **静态** |
| 浮球要 JSON 数据 | 不走 wwwroot，走 `/api/companion/*` | API 在 `SpiritDeskWebHost.cs` |

```text
用户打开 /Index
    ├─ 动态：Razor 把 Model 填进 Index.cshtml → HTML
    └─ 静态：HTML 里 <link href="~/css/site.css"> → 浏览器再请求 wwwroot/css/site.css
```

**不是**「整个站都是静态 HTML」；**是**「动态 HTML + 静态资源叠加」。

---

## 5. 各子目录详细说明

### 5.1 `css/site.css`

- 全站视觉：**薄荷绿**主题、`--spirit-accent` 随当前精灵变色。
- 改登录页、侧栏、卡片、按钮，主要改这个文件。
- 与 SSR 配合：Razor 可在容器上写 `style="--spirit-accent:@spirit.AccentColor"`，CSS 用变量接精灵色。

### 5.2 `js/site.js`

- 不打包、不 npm：页面底部 `<script src="~/js/site.js">` 直接加载。
- 负责：猜拳模态框显隐、侧栏折叠状态写 `localStorage`。
- **不负责**：保存任务到数据库（那是表单 POST 回服务器）。

### 5.3 `js/desk-calendar.js`

- 首页日历视图交互（若 `Index.cshtml` 引用了该脚本）。
- 与 `site.js` 一样，是 **浏览器端增强**，不是第二套前端框架。

### 5.4 `assets/images/`

- Figma 导出精灵立绘、可选背景。
- `SpiritDefinition.ImagePath` 存 `/assets/images/spirit-xxx.png`。
- Shell 桌面浮球显示头像时，会把相对路径拼到 `http://127.0.0.1:端口` 后下载。

### 5.5 `lib/`（Bootstrap、jQuery）

- ASP.NET 项目模板自带，体积大。
- 用途：表单校验（`jquery.validate`）、部分 Bootstrap 工具类。
- **答辩**：我们主视觉在 `site.css` 自研，`lib` 是脚手架依赖，不是业务核心。

---

## 6. 和 `SpiritDesk.Web.csproj` 的关系

`Microsoft.NET.Sdk.Web` 会自动：

- 把 `wwwroot/**` 标记为静态 Web 资源；
- `dotnet publish` 时复制到发布目录；
- 无需在 csproj 里逐个 `<Content Include=...>`（除非特殊文件）。

**Web 项目** = Razor 页面（动态） + wwwroot（静态） + 后端 Service/API，三者都在同一个 csproj 里。

---

## 7. 和部署的关系

- 发布到服务器：`artifacts/spiritdesk-web-publish/wwwroot/` 会一起上传。
- Nginx 反代时，一般由 Kestrel 自己提供静态文件；也可由 Nginx 直接缓存 `/css`、`/js`（进阶，答辩可不提）。

---

## 8. 常见问题

**问：wwwroot 这个文件夹是谁创建的？**  
答：**名字和位置是 ASP.NET Core 约定**；用 Web 项目模板新建站点时就会有。SpiritDesk 在 `SpiritDeskWebHost` 里用 `UseStaticFiles()` 打开访问；**里面的 `site.css`/`site.js` 是团队写的**，`lib` 是模板第三方库，图片是 Figma 导出。

**问：wwwroot 里的 JS 算前端框架吗？**  
答：不算。只是少量原生 JS 增强；页面主体是 **Razor SSR**。

**问：为什么图片不放在 `Pages` 里？**  
答：`Pages` 是模板；图片是静态二进制文件，放 `wwwroot` URL 稳定、可缓存。

**问：改样式要改 cshtml 吗？**  
答：改布局结构才改 cshtml；改颜色间距优先改 **`site.css`**。

---

## 9. 上一篇 / 下一篇

- 上一篇：[07-老师提问怎么答.md](./07-老师提问怎么答.md)
- **推荐续读**：[11-cshtml与wwwroot一次请求如何生成页面.md](./11-cshtml与wwwroot一次请求如何生成页面.md)（SSR 与 wwwroot 两条线、时序图、浏览器验证）
- 专题：[10-SpiritDesk.Web项目与cshtml详解.md](../10-SpiritDesk.Web项目与cshtml详解.md)（Web 项目 + SSR）
- 技术目录：[08-API与静态资源.md](../08-API与静态资源.md)（API + wwwroot 速查）
