Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root

$shellProject = "src/SpiritDesk.Shell/SpiritDesk.Shell.csproj"

# 本地桌面模式优先使用内置本地 Web，不依赖已启动的网页端（默认免登录）
# 若要演示登录/注册，请改用 run-shell-with-auth.ps1 或 run-web.ps1
Remove-Item Env:SPIRITDESK_REMOTE_BASEURL -ErrorAction SilentlyContinue
Remove-Item Env:SPIRITDESK_REQUIRE_AUTH -ErrorAction SilentlyContinue

dotnet build $shellProject -c Release -v minimal
if ($LASTEXITCODE -ne 0) {
    throw "Build failed: $shellProject"
}

dotnet run --project $shellProject -c Release --no-build
