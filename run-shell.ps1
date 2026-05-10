Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root

$shellProject = "src/SpiritDesk.Shell/SpiritDesk.Shell.csproj"

dotnet build $shellProject -v minimal
if ($LASTEXITCODE -ne 0) {
    throw "Build failed: $shellProject"
}

dotnet run --project $shellProject --no-build
