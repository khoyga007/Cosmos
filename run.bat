@echo off
rem Spike: opens the Godot window. Optional first argument = number of small objects in the belts (default 5000).
set N=%1
if "%N%"=="" set N=5000
rem -c Release makes the CORE build in Release, which is ~3x cheaper per Advance step.
rem The game assembly itself still builds Debug on purpose: Cosmos.Game.sln maps the game
rem project Release|Any CPU -> Debug|Any CPU while the core maps Release -> Release.
rem Godot only ever loads .godot\mono\temp\bin\Debug\, so this mismatch is what puts the
rem Release core where Godot looks. Do NOT "fix" that mapping, or the game silently
rem falls back to a Debug core (8.1 ms -> 2.3 ms per step at 5250 objects).
dotnet build "%~dp0game\Cosmos.Game.sln" -c Release -v q -nologo || exit /b 1
"E:\Godot\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe" --path "%~dp0game" -- --rocks=%N%
