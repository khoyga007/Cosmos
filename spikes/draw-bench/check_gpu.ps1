# Kiem tra dGPU NVIDIA tren may Yang truoc khi chay benchmark do ve
# YEU CAU 5: Bat buoc dGPU NVIDIA (GTX 1650); Chan tuyet doi neu chi co Intel UHD Graphics.

$ErrorActionPreference = "Continue"

Write-Host "================ Danh sach GPU he thong ================" -ForegroundColor Cyan
$gpus = Get-CimInstance Win32_VideoController
$hasNvidia = $false

foreach ($g in $gpus) {
    Write-Host ("  Card:   " + $g.Name) -ForegroundColor White
    Write-Host ("  Driver: " + $g.DriverVersion + " | Status: " + $g.Status) -ForegroundColor DarkGray
    if ($g.Name -match "NVIDIA|GeForce|GTX 1650") {
        $hasNvidia = $true
    }
}
Write-Host "=========================================================" -ForegroundColor Cyan

if (-not $hasNvidia) {
    Write-Host ""
    Write-Host "=========================================================================" -ForegroundColor Red
    Write-Host " [NGUY HIEM] Khong phat hien GPU NVIDIA GeForce GTX 1650!" -ForegroundColor Red
    Write-Host " He thong chi nhan card do hoa tich hop Intel UHD Graphics hoac card khac." -ForegroundColor Red
    Write-Host " DUNG BENCHMARK NGAY LAP TUC DE TRANH TDR / TREO CUNG MAY YANG!" -ForegroundColor Red
    Write-Host "=========================================================================" -ForegroundColor Red
    exit 1
}

Write-Host "[XAC NHAN] Da tim thay dGPU NVIDIA hop le tren may Yang. Du dieu kien tiep tuc!" -ForegroundColor Green
Write-Host ""
exit 0
