# 发布 SpiritDesk.Web 到本地目录，便于上传到宝塔 / scp
param(
    [string]$Configuration = "Release",
    [string]$OutputDirectory = "",
    # 结束本机占用 SpiritDesk.Web.dll 的进程（dotnet host / Shell）；编译失败 MSB3027 时可加此开关再跑一次
    [switch]$ReleaseLocks
)

$ErrorActionPreference = "Stop"
$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$WebProj = Join-Path $RepoRoot "src\SpiritDesk.Web\SpiritDesk.Web.csproj"

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $RepoRoot "artifacts\spiritdesk-web-publish"
}

function Stop-SpiritDeskWebLocks {
    $stopped = [System.Collections.Generic.List[string]]::new()

    foreach ($proc in Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.ProcessName -eq 'SpiritDesk.Shell' }) {
        Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
        $stopped.Add(("SpiritDesk.Shell PID={0}" -f $proc.Id))
    }

    foreach ($p in @(Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" -ErrorAction SilentlyContinue)) {
        $cmd = [string]$p.CommandLine
        if (-not [string]::IsNullOrEmpty($cmd) -and ($cmd -like '*SpiritDesk.Web*')) {
            Stop-Process -Id $p.ProcessId -Force -ErrorAction SilentlyContinue
            $stopped.Add(("dotnet SpiritDesk.Web PID={0}" -f $p.ProcessId))
        }
    }

    if ($stopped.Count -gt 0) {
        Write-Host ("ReleaseLocks: 已结束 — " + ($stopped -join "; "))
        Start-Sleep -Milliseconds 400
    } else {
        Write-Host "ReleaseLocks: 未发现 SpiritDesk.Shell / 托管 SpiritDesk.Web 的 dotnet 进程。"
    }
}

if ($ReleaseLocks) {
    Stop-SpiritDeskWebLocks
}

& dotnet publish $WebProj `
    -c $Configuration `
    -o $OutputDirectory `
    --no-self-contained

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish 失败（退出码 $LASTEXITCODE）。可先关掉 SpiritDesk 与托管 SpiritDesk.Web 的 dotnet，或对脚本加开关 ``-ReleaseLocks`` 再试。"
}

Write-Host ""
Write-Host "已发布到: $OutputDirectory"
Write-Host "宝塔操作：把上述文件夹内的「全部文件」上传到站点目录（例如 /opt/spiritdesk/web/），覆盖旧文件。"
Write-Host "然后仅在 SSH / 宝塔终端执行： sudo systemctl daemon-reload && sudo systemctl restart spiritdesk-web"
Write-Host ""
Write-Host "本机 scp 示例： scp -r `"$OutputDirectory\*`" user@服务器:/opt/spiritdesk/web/"
