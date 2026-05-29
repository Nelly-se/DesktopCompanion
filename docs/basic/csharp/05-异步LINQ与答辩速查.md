# 05 - 异步、LINQ 与答辩速查

> 本篇讲项目里比较常见、但初学者容易懵的写法：`async/await`、`Task<T>`、LINQ、空合并、模式匹配。  
> 这些写法在 `SpiritDeskService.cs` 和 PageModel 里很多。

---

## 1. 为什么需要异步？

项目里访问数据库、调用大模型、发 HTTP 请求都可能比较慢。如果同步等待，线程会被卡住。

异步写法：

```csharp
public async Task<IActionResult> OnGetAsync()
{
    Desk = await spiritDeskService.BuildViewModelAsync();
    return Page();
}
```

意思是：

> 等 `BuildViewModelAsync()` 完成，但等待期间不阻塞整个服务器线程。

---

## 2. `Task` 和 `Task<T>`

| 写法 | 含义 |
|------|------|
| `Task` | 异步任务，不返回具体数据 |
| `Task<string>` | 异步任务，完成后返回字符串 |
| `Task<IActionResult>` | 异步任务，完成后返回页面处理结果 |
| `Task<List<ChatMessage>>` | 异步任务，完成后返回消息列表 |

示例：

```csharp
public async Task SendMessageAsync(string message)
{
    await dbContext.SaveChangesAsync();
}
```

这个方法保存消息，但不返回数据。

```csharp
public async Task<SpiritDeskViewModel> BuildViewModelAsync()
{
    // 查询数据
    return new SpiritDeskViewModel { ... };
}
```

这个方法会返回给页面用的 ViewModel。

---

## 3. `await` 用在哪里？

`await` 后面通常是耗时操作：

```csharp
await dbContext.SaveChangesAsync();
await response.Content.ReadAsStringAsync();
await spiritDeskService.SendMessageAsync(MessageText);
```

判断规则：

> 方法名以 `Async` 结尾，返回 `Task` 或 `Task<T>`，通常就需要 `await`。

---

## 4. LINQ 是什么？

LINQ 是 C# 查询集合的语法。可以理解成“对列表做筛选、排序、转换”。

项目里常见：

```csharp
var pendingTasks = tasks.Where(x => !x.IsCompleted).Take(8).ToList();
```

拆开：

| 片段 | 含义 |
|------|------|
| `tasks` | 任务列表 |
| `Where(...)` | 筛选 |
| `x => !x.IsCompleted` | 只要没完成的任务 |
| `Take(8)` | 取前 8 条 |
| `ToList()` | 转成列表 |

---

## 5. 常见 LINQ 方法

| 方法 | 含义 | 示例 |
|------|------|------|
| `Where` | 筛选 | `tasks.Where(x => !x.IsCompleted)` |
| `OrderBy` | 升序排序 | `tasks.OrderBy(x => x.DueAt)` |
| `ThenBy` | 第二排序条件 | `ThenBy(x => x.CreatedAt)` |
| `Take` | 取前几条 | `Take(8)` |
| `TakeLast` | 取最后几条 | `TakeLast(12)` |
| `FirstOrDefault` | 找第一条，没有则默认值 | `FirstOrDefault(x => x.Id == id)` |
| `Any` | 是否存在 | `Any(x => x.IsCompleted)` |
| `Select` | 转换形状 | `Select(x => x.Name)` |
| `ToList` | 转成列表 | `ToList()` |

---

## 6. `x => ...` 是什么？

这是 Lambda 表达式，可以理解成“小函数”。

```csharp
tasks.Where(x => !x.IsCompleted)
```

读法：

> 对每个任务 `x`，判断 `x.IsCompleted` 是否为 false。

再比如：

```csharp
spirits.FirstOrDefault(x => x.Id == spiritId)
```

读法：

> 在精灵列表里找第一个 `Id` 等于 `spiritId` 的精灵。

---

## 7. 空合并 `??`

项目里有：

```csharp
var currentSpirit = selectedSpirit
    ?? spirits.FirstOrDefault(x => x.Id == SpiritIds.Light)
    ?? spirits.First();
```

`??` 表示：左边如果不是 `null` 就用左边，否则用右边。

这段意思是：

1. 优先用用户选择的精灵
2. 没有就用默认光系精灵
3. 还没有就用列表第一个

---

## 8. 空条件 `?.`

```csharp
task.DueAt?.ToString("yyyy-MM-dd HH:mm")
```

意思是：

> 如果 `DueAt` 有值，就格式化；如果是 `null`，结果也是 `null`，不报错。

---

## 9. 模式匹配：`is null` / `is not null`

```csharp
if (spirit is null)
{
    return;
}
```

意思是：如果没有找到精灵，就结束。

```csharp
if (matchedSpirit is not null)
{
    return matchedSpirit.Name;
}
```

意思是：如果找到了精灵，就返回它的名字。

---

## 10. 答辩常见问题

### Q1：为什么方法后面很多 `Async`？

答：

> 因为这些方法会访问数据库、网络或文件，使用异步可以避免阻塞服务器线程，提高响应能力。

### Q2：`await` 是什么意思？

答：

> `await` 表示等待一个异步任务完成，例如等数据库保存完成或等大模型返回结果。它不是让整个程序卡死，而是把线程让出来。

### Q3：LINQ 在项目里干什么？

答：

> LINQ 用来筛选、排序和转换集合，比如从任务列表里筛出未完成任务，从聊天记录里取最近几条消息。

### Q4：`List<T>` 和 `DbSet<T>` 区别是什么？

答：

> `List<T>` 是内存里的普通列表；`DbSet<T>` 代表数据库表，LINQ 查询会被 EF Core 翻译成 SQL 去数据库执行。

### Q5：为什么有些类型后面有 `?`？

答：

> `?` 表示这个值可以为空，例如任务可以没有截止时间，聊天消息可以没有旧版本的精灵 ID。

---

## 11. 最小答辩总结

> 本项目大量使用 `async/await` 处理数据库和大模型请求，用 LINQ 对任务、聊天、精灵集合进行筛选排序。`Task<T>` 表示异步返回值，`??` 和 `?.` 用来安全处理空值，`List<T>` 保存内存列表，`DbSet<T>` 对应数据库表。
