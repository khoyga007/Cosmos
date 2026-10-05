@echo off
rem Spike: opens the Godot window. Optional first argument = grain count (default 100000).
set N=%1
if "%N%"=="" set N=100000
dotnet build "%~dp0game\Cosmos.Game.sln" -c Debug -v q -nologo || exit /b 1
"E:\Godot\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe" --path "%~dp0game" -- --grains=%N%
