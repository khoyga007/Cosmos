param(
    [Parameter(Mandatory=$true)][string]$ExePath,
    [Parameter(Mandatory=$true)][string]$ArgList,
    [Parameter(Mandatory=$true)][string]$OutJson,
    [string]$Scene = "A",
    [string]$Mode = "shader",
    [string]$Engine = "godot",
    [int]$FpsCap = 0,
    [int]$TimeoutSec = 25
)

$ErrorActionPreference = "Continue"

Write-Host "[Watchdog] Khoi dong: $ExePath" -ForegroundColor Cyan
Write-Host "           Tham so: $ArgList" -ForegroundColor DarkGray
Write-Host "           Han muc thoi gian: $TimeoutSec giay (OS Watchdog Bao Ve May Yang)" -ForegroundColor Yellow

$psi = New-Object System.Diagnostics.ProcessStartInfo
$psi.FileName = $ExePath
$psi.Arguments = $ArgList
$psi.UseShellExecute = $false

$proc = [System.Diagnostics.Process]::Start($psi)
if ($null -eq $proc) {
    Write-Host "[Watchdog Loi] Khong the khoi dong tien trinh!" -ForegroundColor Red
    exit 1
}

$finished = $proc.WaitForExit($TimeoutSec * 1000)

if (-not $finished) {
    Write-Host ""
    Write-Host "=========================================================================" -ForegroundColor Red
    Write-Host " [WATCHDOG TIMEOUT 25s] TIEN TRINH VUOT QUA $TimeoutSec GIAY!" -ForegroundColor Red
    Write-Host " Cuong che taskkill ngay lap tuc de bao ve phan cung / tranh TDR treo may!" -ForegroundColor Red
    Write-Host "=========================================================================" -ForegroundColor Red

    try {
        & taskkill.exe /F /T /PID $proc.Id *>$null
    } catch {}

    try {
        if (-not $proc.HasExited) {
            $proc.Kill()
        }
    } catch {}

    # Ghi log kt qu TIMEOUT  aggregate_results.py ghi nhn
    $timeoutResult = @{
        engine = $Engine
        scene = $Scene
        mode = $Mode
        fps_cap = $FpsCap
        status = "TIMEOUT"
        error = "Process terminated by 25s OS watchdog to prevent TDR hang"
        duration_sec = $TimeoutSec
    }
    $parentDir = [System.IO.Path]::GetDirectoryName($OutJson)
    if (-not [string]::IsNullOrEmpty($parentDir) -and -not (Test-Path $parentDir)) {
        New-Item -ItemType Directory -Path $parentDir -Force | Out-Null
    }
    $timeoutResult | ConvertTo-Json -Depth 3 | Set-Content -Path $OutJson -Encoding UTF8

    exit 124
}

$exitCode = $proc.ExitCode
if ($exitCode -ne 0) {
    Write-Host "[Watchdog] Tien trinh ket thuc voi ma loi: $exitCode" -ForegroundColor Red
    exit $exitCode
}

Write-Host "[Watchdog OK] Hoan tat thanh cong trong han muc thoi gian." -ForegroundColor Green
exit 0
