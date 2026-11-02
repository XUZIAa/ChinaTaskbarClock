@echo off
REM ============================================================
REM  ChinaClock build script
REM  Output: single-file ChinaClock.exe (lunar.dll embedded, no external deps)
REM  Requires: Windows + .NET Framework 4.8 + Roslyn csc (from VS / Build Tools)
REM ============================================================
setlocal enabledelayedexpansion
cd /d "%~dp0"

set "ROSLYN="
if not defined ROSLYN if exist "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe" set "ROSLYN=C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe"
if not defined ROSLYN if exist "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\Roslyn\csc.exe" set "ROSLYN=C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\Roslyn\csc.exe"
if not defined ROSLYN if exist "C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\Roslyn\csc.exe" set "ROSLYN=C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\Roslyn\csc.exe"
if not defined ROSLYN if exist "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\Roslyn\csc.exe" set "ROSLYN=C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\Roslyn\csc.exe"
if not defined ROSLYN if defined CSC if exist "%CSC%" set "ROSLYN=%CSC%"

if defined ROSLYN goto :build
echo [ERROR] Roslyn csc.exe not found. Install Visual Studio 2022 / Build Tools,
echo         or set env var CSC to the full path of csc.exe, then retry.
exit /b 1

:build
echo Using compiler: %ROSLYN%
echo.

echo [1/3] Building single-file ChinaClock.exe ...
"%ROSLYN%" /nologo /target:winexe /platform:anycpu /optimize+ /out:ChinaClock.exe /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:lunar.dll /r:netstandard.dll /res:lunar.dll,ChinaClock.embedded.lunar.dll ChinaClock.cs
if errorlevel 1 (echo [FAIL] ChinaClock.cs compile error & exit /b 1)

echo [2/3] Building ChinaClock.Tests.exe ...
"%ROSLYN%" /nologo /target:exe /out:ChinaClock.Tests.exe /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ChinaClock.Tests.cs
if errorlevel 1 (echo [FAIL] test compile error & exit /b 1)

echo [3/3] Running tests ...
ChinaClock.Tests.exe ChinaClock.exe
if errorlevel 1 (echo [FAIL] tests did not pass & exit /b 1)

echo.
echo Done: single-file ChinaClock.exe generated (lunar.dll embedded, distributable alone).
endlocal
