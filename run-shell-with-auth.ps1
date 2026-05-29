Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# 桌面 Shell 启动，但保留登录/注册门禁（用于答辩演示账号功能）
$env:SPIRITDESK_REQUIRE_AUTH = "1"
Remove-Item Env:SPIRITDESK_REMOTE_BASEURL -ErrorAction SilentlyContinue

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root

$shellProject = "src/SpiritDesk.Shell/SpiritDesk.Shell.csproj"

dotnet build $shellProject -c Release -v minimal
if ($LASTEXITCODE -ne 0) {
    throw "Build failed: $shellProject"
}

Write-Host "SPIRITDESK_REQUIRE_AUTH=1：WebView2 将先进入 /Login，演示账号 spiritdesk / spiritdesk" -ForegroundColor Cyan
dotnet run --project $shellProject -c Release --no-build
