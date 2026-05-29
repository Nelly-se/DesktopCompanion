// =============================================================================
// TempCheckDb — 临时 SQLite 诊断小工具（不参与 SpiritDesk 主解决方案发布）
// =============================================================================
//
// 【这个文件是干什么的】
//   独立于 SpiritDesk.Web / Shell 的一个最小控制台程序，用来在开发排错时
//   「直接打开磁盘上的 spiritdesk.db」，不经过 EF、不启动网站：
//     1) 判断数据库文件是否存在；
//     2) 用原生 SQL 执行 PRAGMA table_info(Spirits)，打印 Spirits 表每一列的
//        列名与 SQLite 类型，便于对照 SpiritDesk.Core 实体或 DatabaseBootstrapper
//        里声明的必需列是否一致。
//
// 【和 SpiritDeskWebHost 的分工】
//   - SpiritDeskWebHost：正式运行时组装 Web、注册 EF、EnsureCreated、处理 HTTP。
//   - TempCheckDb：只读诊断，不修改库、不建表；适合「Shell 已跑过但页面报错」时
//     快速确认是不是旧库路径错了、表根本没生成、或列名/类型与代码不一致。
//
// 【数据库文件通常在哪】
//   Web/Shell 启动时由 SpiritDeskWebHost 决定路径，默认：
//     {启动进程的输出目录}/data/spiritdesk.db
//   本仓库常见路径示例（Debug 构建后，按你本机仓库根目录改盘符）：
//     - 只跑 Web：  src/SpiritDesk.Web/bin/Debug/net9.0/data/spiritdesk.db
//     - Shell 起 Web 子进程：同上（子进程是 Web 项目，库在 Web 的 bin 下）
//     - 若配置了 SpiritDesk:DataDirectory，则以配置为准。
//
// 【如何运行】
//   cd temp/TempCheckDb
//   dotnet run
//   运行前务必修改下方 dbPath 为本机实际 .db 文件的完整路径。
//
// 【输出含义】
//   EXISTS / NO_DB     — 文件是否存在
//   列名|类型          — PRAGMA 每一行：name|type（例如 Id|TEXT）
//
// 【注意】
//   - 不在 CI、不在 Docker、不随主程序打包；temp 目录仅供本地工具。
//   - 硬编码 dbPath 是故意的：避免误扫错盘；换机器或换构建配置后需手改路径。
//   - 若需查其它表，可把 CommandText 改为 PRAGMA table_info(表名);
// =============================================================================

using System;
using System.Data;
using Microsoft.Data.Sqlite;

// -----------------------------------------------------------------------------
// 配置：指向你要检查的 spiritdesk.db 绝对路径（运行前必须改成你本机路径）
// -----------------------------------------------------------------------------
// 下方为示例路径（另一台机器上的 Shell 输出目录）；请改成本仓库 Debug 输出，例如：
//   @"d:\Users\zyx\Desktop\DesktopCompanion\src\SpiritDesk.Web\bin\Debug\net9.0\data\spiritdesk.db"
var dbPath = @"d:\software_construction\program\src\SpiritDesk.Shell\bin\Debug\net9.0-windows\data\spiritdesk.db";

// -----------------------------------------------------------------------------
// 步骤 1：检查文件是否存在（不连库，避免路径错时抛异常）
// -----------------------------------------------------------------------------
Console.WriteLine(System.IO.File.Exists(dbPath) ? "EXISTS" : "NO_DB");

if (!System.IO.File.Exists(dbPath))
{
    // 常见原因：还没运行过 Web/Shell（EF 尚未 EnsureCreated）、路径写错、看了 Shell 目录但库在 Web 目录。
    Console.WriteLine("提示：先 dotnet run Web 或启动 Shell 生成 data/spiritdesk.db，再改 dbPath 重试。");
    return;
}

// -----------------------------------------------------------------------------
// 步骤 2：打开 SQLite 连接（Microsoft.Data.Sqlite，与 EF 底层驱动一致）
// -----------------------------------------------------------------------------
using var conn = new SqliteConnection($"Data Source={dbPath}");
conn.Open();

// -----------------------------------------------------------------------------
// 步骤 3：查询 Spirits 表结构元数据
// -----------------------------------------------------------------------------
// PRAGMA table_info('表名') 返回列：
//   cid(序号) | name(列名) | type(声明类型) | notnull | dflt_value | pk(是否主键)
// 这里只打印 name 和 type，用来和 SpiritDefinition / HasData 种子或 EF 映射对照。
using var cmd = conn.CreateCommand();
cmd.CommandText = "PRAGMA table_info(Spirits);";

using var reader = cmd.ExecuteReader();
Console.WriteLine("--- Spirits columns (name|type) ---");
while (reader.Read())
{
    // GetValue(1)=name, GetValue(2)=type（索引 0 是 cid）
    Console.WriteLine($"{reader.GetValue(1)}|{reader.GetValue(2)}");
}

// 扩展：若要列出所有用户表，可另开命令：SELECT name FROM sqlite_master WHERE type='table';
