Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root

$projects = @(
    "src/SpiritDesk.Core/SpiritDesk.Core.csproj",
    "src/SpiritDesk.Web/SpiritDesk.Web.csproj",
    "src/SpiritDesk.Shell/SpiritDesk.Shell.csproj"
)

foreach ($project in $projects) {
    Write-Host "Building $project" -ForegroundColor Cyan
    dotnet build $project -v minimal
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed: $project"
    }
}

Write-Host "Build completed." -ForegroundColor Green
