@echo off
setlocal enabledelayedexpansion

echo =========================================================================
echo       COSMOS SPIKE: BENCHMARK DO VE GODOT 4.7.2 VS BEVY 0.19.1
echo =========================================================================
echo May chuan: Yang Core i5-10300H (4C/8T), NVIDIA GTX 1650 dGPU
echo Dieu kien: 1920x1080, VSync OFF, 10s moi canh (bo 2s dau), tu thoat.
echo Ep dung: NVIDIA dGPU (Godot --gpu-index 1, Bevy HighPerformance adapter)
echo =========================================================================
echo.

cd /d "%~dp0"

if not exist results mkdir results
del /q results\*.json 2>nul

set GODOT_EXE=E:\Godot\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe
set BEVY_EXE=%~dp0bevy\target\release\bevy_bench.exe

if not exist "%GODOT_EXE%" (
    echo [LOI] Khong tim thay Godot tai: %GODOT_EXE%
    pause
    exit /b 1
)

if not exist "%BEVY_EXE%" (
    echo [LOI] Khong tim thay Bevy release binary tai: %BEVY_EXE%
    echo Vui long chay build release truoc!
    pause
    exit /b 1
)

echo [1/12] Running Godot Scene A (100k) - CPU Push...
"%GODOT_EXE%" --gpu-index 1 --path godot --scene=A --mode=cpu --duration=10.0 --warmup=2.0 --out="%~dp0results\godot_A_cpu.json"
timeout /t 2 >nul

echo [2/12] Running Godot Scene A (100k) - Vertex Shader RAIL...
"%GODOT_EXE%" --gpu-index 1 --path godot --scene=A --mode=shader --duration=10.0 --warmup=2.0 --out="%~dp0results\godot_A_shader.json"
timeout /t 2 >nul

echo [3/12] Running Bevy Scene A (100k) - CPU Push...
"%BEVY_EXE%" --scene=A --mode=cpu --duration=10.0 --warmup=2.0 --out="%~dp0results\bevy_A_cpu.json"
timeout /t 2 >nul

echo [4/12] Running Bevy Scene A (100k) - Vertex Shader RAIL...
"%BEVY_EXE%" --scene=A --mode=shader --duration=10.0 --warmup=2.0 --out="%~dp0results\bevy_A_shader.json"
timeout /t 2 >nul

echo [5/12] Running Godot Scene B (1M) - CPU Push...
"%GODOT_EXE%" --gpu-index 1 --path godot --scene=B --mode=cpu --duration=10.0 --warmup=2.0 --out="%~dp0results\godot_B_cpu.json"
timeout /t 2 >nul

echo [6/12] Running Godot Scene B (1M) - Vertex Shader RAIL...
"%GODOT_EXE%" --gpu-index 1 --path godot --scene=B --mode=shader --duration=10.0 --warmup=2.0 --out="%~dp0results\godot_B_shader.json"
timeout /t 2 >nul

echo [7/12] Running Bevy Scene B (1M) - CPU Push...
"%BEVY_EXE%" --scene=B --mode=cpu --duration=10.0 --warmup=2.0 --out="%~dp0results\bevy_B_cpu.json"
timeout /t 2 >nul

echo [8/12] Running Bevy Scene B (1M) - Vertex Shader RAIL...
"%BEVY_EXE%" --scene=B --mode=shader --duration=10.0 --warmup=2.0 --out="%~dp0results\bevy_B_shader.json"
timeout /t 2 >nul

echo [9/12] Running Godot Scene C (1M + Bloom + 50k Particles) - CPU Push...
"%GODOT_EXE%" --gpu-index 1 --path godot --scene=C --mode=cpu --duration=10.0 --warmup=2.0 --out="%~dp0results\godot_C_cpu.json"
timeout /t 2 >nul

echo [10/12] Running Godot Scene C (1M + Bloom + 50k Particles) - Vertex Shader RAIL...
"%GODOT_EXE%" --gpu-index 1 --path godot --scene=C --mode=shader --duration=10.0 --warmup=2.0 --out="%~dp0results\godot_C_shader.json"
timeout /t 2 >nul

echo [11/12] Running Bevy Scene C (1M + Bloom + 50k Particles) - CPU Push...
"%BEVY_EXE%" --scene=C --mode=cpu --duration=10.0 --warmup=2.0 --out="%~dp0results\bevy_C_cpu.json"
timeout /t 2 >nul

echo [12/12] Running Bevy Scene C (1M + Bloom + 50k Particles) - Vertex Shader RAIL...
"%BEVY_EXE%" --scene=C --mode=shader --duration=10.0 --warmup=2.0 --out="%~dp0results\bevy_C_shader.json"
timeout /t 2 >nul

echo.
echo =========================================================================
echo                         TONG HOP KET QUA
echo =========================================================================
C:\Python314\python.exe aggregate_results.py

echo.
echo Hoan tat toan bo 12 phep do! Nhan phim bat ky de thoat.
pause
