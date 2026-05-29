# 04 - C# 讲解：业务服务层

## 1. 三层服务分工

| 类 | 文件 | 职责 |
|----|------|------|
| `SpiritDeskService` | `Services/SpiritDeskService.cs` | 任务、聊天、精灵切换、签到投喂、猜拳、档案初始化 |
| `SpiritPersonaService` | `Services/SpiritPersonaService.cs` | 问候语、提示语、规则化精灵口吻 |
| `LlmReplyService` | `Services/LlmReplyService.cs` | 调用豆包/方舟等兼容 OpenAI 的 API |

`SpiritDeskService` 通过主构造函数注入：

```csharp
public class SpiritDeskService(
    SpiritDeskDbContext dbContext,
    SpiritPersonaService personaService,
    LlmReplyService llmReplyService)
```

## 2. SpiritDeskService 核心方法

### 2.1 视图模型构建

| 方法 | 返回类型 | 用途 |
|------|----------|------|
| `BuildViewModelAsync` | `SpiritDeskViewModel` | 首页：档案、精灵、任务、聊天、今日计数、问候 |
| `BuildSettingsViewModelAsync` | `SettingsViewModel` | 设置页 |
| `BuildChatHistoryViewModelAsync` | `ChatHistoryViewModel` | 聊天页全量记录 |

`SpiritDeskViewModel` 聚合 `Profile`、`CurrentSpirit`、`Tasks`、`ChatMessages`、`TaskSnapshot`（待办/完成/提醒子集）等，供 Razor 一次绑定。

### 2.2 初始化与精灵

- `EnsureInitializedAsync()`：建库、种子、演示数据
- `NeedsSpiritSelectionAsync()`：`CurrentSpiritId` 为空则需去选精灵
- `GetSpiritsAsync()`：五精灵列表
- `SelectSpiritAsync(string spiritId)`：写档案、追加系统消息

### 2.3 聊天 SendMessageAsync

流程概要：

1. 校验消息非空、已选精灵
2. 写入用户消息到 `ChatMessages`
3. 调用 `LlmReplyService` 尝试生成回复；失败则用 `SpiritPersonaService` 规则兜底
4. 写入精灵消息，可能更新 `Mood`、`Affinity`
5. `SaveChangesAsync`

未配置 API Key 时仍可演示，依赖规则脚本。

### 2.4 任务 CRUD

`AddTaskAsync`、`UpdateTaskAsync`、`DeleteTaskAsync`、`CompleteTaskAsync` 等，由 `IndexModel` 的 PageHandler 调用（`OnPostAddTask` 等命名）。

### 2.5 互动与游戏

- `InteractAsync(actionType)`：checkin、feed、study、rest、encourage 等，写 `DailyActionLog` 并改数值
- `PlayGameAsync(choice)`：猜拳，消耗每日次数，结算金币（受精灵 `GameCountBonus` 影响）

### 2.6 回归补偿

`ApplyWelcomeBackEffectAsync`：长时间未登录时，若当前精灵为慢慢壤（soil）等，触发额外安抚逻辑，结果通过 `ReturnNotice` 显示在首页。

## 3. SpiritPersonaService

不访问数据库，根据 `SpiritDefinition` 与 `UserProfile` 生成字符串：

- `BuildGreeting`：按时段问候
- `BuildHelperTip`：工作台顶部提示（浮球 API `/api/companion/current` 的 `statusText` 亦用此）
- `BuildWelcomeBack`：回归文案
- 规则回复模板：配合聊天兜底

## 4. LlmReplyService

- 读取配置 / 环境变量：`ARK_API_KEY`、`ARK_API_BASE`、`ARK_MODEL`（见 README 与 `.env.example`）
- 使用 `HttpClient` 发送 chat completions 请求
- API 不可用或 Key 为空时返回 null，由上层走规则回复

**安全**：密钥应放在 `.env` 或服务器环境变量，勿提交到 Git。

## 5. OperationFeedback 与页面提示

部分操作返回 `OperationFeedback` 或设置 `SpiritSelectionResult`，PageModel 转为 `TempData` 或 `NoticeMessage` 显示在页面横幅。

## 6. 与 PageModel 的调用关系

```text
IndexModel.OnGetAsync()
    → spiritDeskService.BuildViewModelAsync()
    → 绑定到 Index.cshtml

IndexModel.OnPostSendMessage()
    → spiritDeskService.SendMessageAsync(MessageText)
    → RedirectToPage() 刷新
```

Razor Page 的 **Handler** 命名约定：`OnPost{HandlerName}` 对应表单 `asp-page-handler`。

## 7. 调试建议

- 在 `SendMessageAsync` 打断点观察 LLM 与规则分支
- 查看 `DailyActionLogs` 表确认互动次数
- 修改 `BuildSpiritDefinitions()` 后注意 `EnsureSpiritDefinitionsAsync` 是否同步到库

下一步阅读：[05-CSharp-Razor-Pages与页面模型.md](./05-CSharp-Razor-Pages与页面模型.md)
