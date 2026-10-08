@echo off
setlocal enabledelayedexpansion

echo =========================================================================
echo       COSMOS SPIKE: BENCHMARK DO VE GODOT 4.7.2 VS BEVY 0.19.1
echo       GOI AN TOAN 5 DIEM - BAO VE TOAN DIEN PHAN CUNG MAY YANG
echo =========================================================================
echo May chuan: Yang Core i5-10300H (4C/8T), NVIDIA GTX 1650 dGPU (4GB VRAM)
echo Co che:
echo   [1] Kiem tra dGPU: Bat buoc NVIDIA GTX 1650; Chan hoan toan Intel UHD.
echo   [2] Watchdog OS: 25s moi scene qua PowerShell; taskkill cuong che neu treo.
echo   [3] Fail-Stop: Chay tang dan (A to D1 to D2 to D3 to B to C); Dung ngay neu loi.
echo   [4] 2 Luot do: Capped 60 FPS (thuc te game) va Uncapped (stress throughput).
echo   [5] Canh vat ly s15: Hat GPU (100k/300k/1M) + 1.000 bodies Keplerian that.
echo =========================================================================
echo.

cd /d "%~dp0"

:: -------------------------------------------------------------------------
:: [YEU CAU 5] KIEM TRA GPU HE THONG - CHAN TRIET DE INTEL UHD
:: -------------------------------------------------------------------------
echo [Kiem tra GPU] Dang truy van danh sach card do hoa he thong...
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0check_gpu.ps1"
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [LOI] Kiem tra GPU that bai hoac khong an toan! Chuong trinh dung lai.
    pause
    exit /b 1
)
echo.

if not exist results mkdir results
del /q results\*.json 2>nul

set GODOT_EXE=E:\Godot\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe
set BEVY_EXE=%~dp0bevy\target\release\bevy_bench.exe
set WATCHDOG_PS=%~dp0watchdog_runner.ps1

if not exist "%GODOT_EXE%" (
    echo [LOI] Khong tim thay Godot console executable tai: %GODOT_EXE%
    pause
    exit /b 1
)

set HAS_BEVY=1
if not exist "%BEVY_EXE%" (
    echo [CANH BAO] Chua tim thay Bevy release binary tai: %BEVY_EXE%
    echo Se chi chay benchmark tren Godot 4.7.2.
    set HAS_BEVY=0
)

echo Bat dau chuoi benchmark tang dan tu nhe toi nang.
echo =========================================================================
echo.

:: -------------------------------------------------------------------------
:: 1. SCENE A (100k points thun)
:: -------------------------------------------------------------------------
call :RUN_STEP "godot" "A" "shader" 60 "results\godot_A_shader_cap60.json" "Godot Scene A (100k) - Shader - Capped 60 FPS"
if !STEP_ERR! NEQ 0 goto :FAIL_STOP

call :RUN_STEP "godot" "A" "shader" 0 "results\godot_A_shader_uncapped.json" "Godot Scene A (100k) - Shader - Uncapped"
if !STEP_ERR! NEQ 0 goto :FAIL_STOP

if "%HAS_BEVY%"=="1" (
    call :RUN_STEP "bevy" "A" "shader" 60 "results\bevy_A_shader_cap60.json" "Bevy Scene A (100k) - Shader - Capped 60 FPS"
    if !STEP_ERR! NEQ 0 goto :FAIL_STOP

    call :RUN_STEP "bevy" "A" "shader" 0 "results\bevy_A_shader_uncapped.json" "Bevy Scene A (100k) - Shader - Uncapped"
    if !STEP_ERR! NEQ 0 goto :FAIL_STOP
)

:: -------------------------------------------------------------------------
:: 2. SCENE D1 (15: 100k ht GPU + 1.000 bodies tht)
:: -------------------------------------------------------------------------
call :RUN_STEP "godot" "D1" "shader" 60 "results\godot_D1_shader_cap60.json" "Godot Scene D1 (100k + 1k bodies) - Shader - Capped 60 FPS"
if !STEP_ERR! NEQ 0 goto :FAIL_STOP

call :RUN_STEP "godot" "D1" "shader" 0 "results\godot_D1_shader_uncapped.json" "Godot Scene D1 (100k + 1k bodies) - Shader - Uncapped"
if !STEP_ERR! NEQ 0 goto :FAIL_STOP

if "%HAS_BEVY%"=="1" (
    call :RUN_STEP "bevy" "D1" "shader" 60 "results\bevy_D1_shader_cap60.json" "Bevy Scene D1 (100k + 1k bodies) - Shader - Capped 60 FPS"
    if !STEP_ERR! NEQ 0 goto :FAIL_STOP

    call :RUN_STEP "bevy" "D1" "shader" 0 "results\bevy_D1_shader_uncapped.json" "Bevy Scene D1 (100k + 1k bodies) - Shader - Uncapped"
    if !STEP_ERR! NEQ 0 goto :FAIL_STOP
)

:: -------------------------------------------------------------------------
:: 3. SCENE D2 (15: 300k ht GPU + 1.000 bodies tht)
:: -------------------------------------------------------------------------
call :RUN_STEP "godot" "D2" "shader" 60 "results\godot_D2_shader_cap60.json" "Godot Scene D2 (300k + 1k bodies) - Shader - Capped 60 FPS"
if !STEP_ERR! NEQ 0 goto :FAIL_STOP

call :RUN_STEP "godot" "D2" "shader" 0 "results\godot_D2_shader_uncapped.json" "Godot Scene D2 (300k + 1k bodies) - Shader - Uncapped"
if !STEP_ERR! NEQ 0 goto :FAIL_STOP

if "%HAS_BEVY%"=="1" (
    call :RUN_STEP "bevy" "D2" "shader" 60 "results\bevy_D2_shader_cap60.json" "Bevy Scene D2 (300k + 1k bodies) - Shader - Capped 60 FPS"
    if !STEP_ERR! NEQ 0 goto :FAIL_STOP

    call :RUN_STEP "bevy" "D2" "shader" 0 "results\bevy_D2_shader_uncapped.json" "Bevy Scene D2 (300k + 1k bodies) - Shader - Uncapped"
    if !STEP_ERR! NEQ 0 goto :FAIL_STOP
)

:: -------------------------------------------------------------------------
:: 4. SCENE D3 (15: 1M ht GPU + 1.000 bodies tht)
:: -------------------------------------------------------------------------
call :RUN_STEP "godot" "D3" "shader" 60 "results\godot_D3_shader_cap60.json" "Godot Scene D3 (1M + 1k bodies) - Shader - Capped 60 FPS"
if !STEP_ERR! NEQ 0 goto :FAIL_STOP

call :RUN_STEP "godot" "D3" "shader" 0 "results\godot_D3_shader_uncapped.json" "Godot Scene D3 (1M + 1k bodies) - Shader - Uncapped"
if !STEP_ERR! NEQ 0 goto :FAIL_STOP

if "%HAS_BEVY%"=="1" (
    call :RUN_STEP "bevy" "D3" "shader" 60 "results\bevy_D3_shader_cap60.json" "Bevy Scene D3 (1M + 1k bodies) - Shader - Capped 60 FPS"
    if !STEP_ERR! NEQ 0 goto :FAIL_STOP

    call :RUN_STEP "bevy" "D3" "shader" 0 "results\bevy_D3_shader_uncapped.json" "Bevy Scene D3 (1M + 1k bodies) - Shader - Uncapped"
    if !STEP_ERR! NEQ 0 goto :FAIL_STOP
)

:: -------------------------------------------------------------------------
:: 5. SCENE B (1M points thun)
:: -------------------------------------------------------------------------
call :RUN_STEP "godot" "B" "shader" 60 "results\godot_B_shader_cap60.json" "Godot Scene B (1M) - Shader - Capped 60 FPS"
if !STEP_ERR! NEQ 0 goto :FAIL_STOP

call :RUN_STEP "godot" "B" "shader" 0 "results\godot_B_shader_uncapped.json" "Godot Scene B (1M) - Shader - Uncapped"
if !STEP_ERR! NEQ 0 goto :FAIL_STOP

if "%HAS_BEVY%"=="1" (
    call :RUN_STEP "bevy" "B" "shader" 60 "results\bevy_B_shader_cap60.json" "Bevy Scene B (1M) - Shader - Capped 60 FPS"
    if !STEP_ERR! NEQ 0 goto :FAIL_STOP

    call :RUN_STEP "bevy" "B" "shader" 0 "results\bevy_B_shader_uncapped.json" "Bevy Scene B (1M) - Shader - Uncapped"
    if !STEP_ERR! NEQ 0 goto :FAIL_STOP
)

:: -------------------------------------------------------------------------
:: 6. SCENE C (1M points + Bloom + 50k Explosion Particles)
:: -------------------------------------------------------------------------
call :RUN_STEP "godot" "C" "shader" 60 "results\godot_C_shader_cap60.json" "Godot Scene C (1M+Bloom+Part) - Shader - Capped 60 FPS"
if !STEP_ERR! NEQ 0 goto :FAIL_STOP

call :RUN_STEP "godot" "C" "shader" 0 "results\godot_C_shader_uncapped.json" "Godot Scene C (1M+Bloom+Part) - Shader - Uncapped"
if !STEP_ERR! NEQ 0 goto :FAIL_STOP

if "%HAS_BEVY%"=="1" (
    call :RUN_STEP "bevy" "C" "shader" 60 "results\bevy_C_shader_cap60.json" "Bevy Scene C (1M+Bloom+Part) - Shader - Capped 60 FPS"
    if !STEP_ERR! NEQ 0 goto :FAIL_STOP

    call :RUN_STEP "bevy" "C" "shader" 0 "results\bevy_C_shader_uncapped.json" "Bevy Scene C (1M+Bloom+Part) - Shader - Uncapped"
    if !STEP_ERR! NEQ 0 goto :FAIL_STOP
)

goto :SUMMARY_EXIT

:: -------------------------------------------------------------------------
:: FAIL-STOP HANDLER: DUNG TOAN BO NEU CO SCENE LOI HOAC TIMEOUT
:: -------------------------------------------------------------------------
:FAIL_STOP
echo.
echo =========================================================================
echo  [FAIL-STOP KICH HOAT]
echo  Mot phep do da that bai hoac bi watchdog OS 25s cat ngang!
echo  DUNG NGAY TOAN BO BENCHMARK DE BAO VE PHAN CUNG MAY YANG.
echo  Khong chay bat ky canh nao nang hon nua!
echo =========================================================================
echo.

:SUMMARY_EXIT
echo.
echo =========================================================================
echo                         TONG HOP KET QUA BENCHMARK
echo =========================================================================
C:\Python314\python.exe aggregate_results.py

echo.
echo Hoan tat phien benchmark! Nhan phim bat ky de dong cua so.
pause
exit /b 0

:: =========================================================================
:: SUBROUTINE: CHAY 1 BUOC BENCHMARK DUOC BOC BOI OS WATCHDOG 25S
:: Param 1: Engine (godot / bevy)
:: Param 2: Scene (A / B / C / D1 / D2 / D3)
:: Param 3: Mode (shader / cpu)
:: Param 4: FpsCap (60 / 0)
:: Param 5: OutJson (results\xxx.json)
:: Param 6: Title (Mo ta)
:: =========================================================================
:RUN_STEP
set ENGINE=%~1
set SCENE=%~2
set MODE=%~3
set FPS_CAP=%~4
set OUT_JSON=%~dp0%~5
set TITLE=%~6

echo.
echo [CHAY] %TITLE%
if "%ENGINE%"=="godot" (
    set TARGET_EXE=%GODOT_EXE%
    set ARGS=--gpu-index 1 --path godot --scene=%SCENE% --mode=%MODE% --fps-cap=%FPS_CAP% --duration=10.0 --warmup=2.0 --out="%OUT_JSON%"
) else (
    set TARGET_EXE=%BEVY_EXE%
    set ARGS=--scene=%SCENE% --mode=%MODE% --fps-cap=%FPS_CAP% --duration=10.0 --warmup=2.0 --out="%OUT_JSON%"
)

powershell -NoProfile -ExecutionPolicy Bypass -File "%WATCHDOG_PS%" -ExePath "%TARGET_EXE%" -ArgList "%ARGS%" -OutJson "%OUT_JSON%" -Scene "%SCENE%" -Mode "%MODE%" -Engine "%ENGINE%" -FpsCap %FPS_CAP% -TimeoutSec 25
set STEP_ERR=%ERRORLEVEL%

if !STEP_ERR! NEQ 0 (
    echo [THAT BAI] Phep do exit code: !STEP_ERR!
) else (
    echo [THANH CONG] Da ghi nhan du lieu vao %~5
)
timeout /t 2 >nul
exit /b 0
