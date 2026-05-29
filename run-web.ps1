Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root

$webProject = "src/SpiritDesk.Web/SpiritDesk.Web.csproj"

# 仅启动网页（Development + appsettings.Development.json → Auth.Enabled=true）
dotnet run --project $webProject
