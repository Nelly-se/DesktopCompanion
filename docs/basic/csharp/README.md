# C# 基本语法与数据结构速成

> 目标：看懂本项目里的 `.cs` 文件，能向老师解释「变量、类、方法、List、Dictionary、async/await」这些基础概念。  
> 不追求一次学完 C#，先把 SpiritDesk 里最常见的写法讲清楚。

## 建议阅读顺序

| 顺序 | 文档 | 重点 |
|------|------|------|
| 1 | [01-基本语法.md](./01-基本语法.md) | 变量、类型、属性、字符串、注释 |
| 2 | [02-方法与流程控制.md](./02-方法与流程控制.md) | `if`、`switch`、循环、方法、返回值 |
| 3 | [03-类对象与项目常见写法.md](./03-类对象与项目常见写法.md) | class、对象、构造函数、依赖注入 |
| 4 | [04-常用数据结构.md](./04-常用数据结构.md) | `List<T>`、`Dictionary<TKey,TValue>`、数组、可空 |
| 5 | [05-异步LINQ与答辩速查.md](./05-异步LINQ与答辩速查.md) | `async/await`、`Task<T>`、LINQ、答辩问答 |

## 本项目最常见的 C# 关键词

| 关键词 | 一句话解释 | 项目里哪里常见 |
|--------|------------|----------------|
| `class` | 定义一个类，也就是一种对象模板 | `ChatMessage.cs`、`SpiritDeskService.cs` |
| `public` | 公开成员，别的文件可以访问 | 实体属性、服务方法 |
| `private` | 私有成员，只能在当前类内部用 | 辅助方法、字段 |
| `async` / `await` | 异步等待数据库或 HTTP 请求 | PageModel、Service |
| `Task<T>` | 异步方法最终会返回一个 `T` | `Task<IActionResult>` |
| `List<T>` | 一组同类型数据 | 聊天消息、任务列表 |
| `Dictionary<TKey,TValue>` | 键值对表 | 每日互动次数统计 |
| `string?` | 这个字符串可以是 `null` | 可选参数、可选字段 |
| `required` | 创建对象时必须填写 | ViewModel 属性 |

## 怎么和项目对应起来

```text
src/SpiritDesk.Core/Entities/
  ChatMessage.cs        ← 类、属性、DateTime、string?

src/SpiritDesk.Web/Pages/
  Index.cshtml.cs       ← PageModel、BindProperty、OnPost 方法

src/SpiritDesk.Web/Services/
  SpiritDeskService.cs  ← async/await、List、Dictionary、LINQ

src/SpiritDesk.Web/Data/
  SpiritDeskDbContext.cs ← DbSet<T>、EF Core 数据表映射
```

答辩时可以这样说：

> C# 代码主要分三类：实体类负责描述数据结构，PageModel 负责接收页面请求，Service 负责业务逻辑和数据库操作。
