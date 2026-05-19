Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root

$shellProject = "src/SpiritDesk.Shell/SpiritDesk.Shell.csproj"

# 本地桌面模式优先使用内置本地 Web，不依赖已启动的网页端
Remove-Item Env:SPIRITDESK_REMOTE_BASEURL -ErrorAction SilentlyContinue

dotnet build $shellProject -c Release -v minimal
if ($LASTEXITCODE -ne 0) {
    throw "Build failed: $shellProject"
}

dotnet run --project $shellProject -c Release --no-build
