@echo off
setlocal
cd /d "%~dp0"

set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"

if not exist "%CSC%" (
  echo [MultiShot] Windows .NET Framework C# compiler was not found.
  echo Install/enable .NET Framework 4.x or build MultiShot.cs with Visual Studio.
  pause
  exit /b 1
)

echo [MultiShot] Generating icon...
"%CSC%" /nologo /target:exe /optimize+ /platform:anycpu /out:IconBuilder.exe /reference:System.Drawing.dll IconBuilder.cs
if errorlevel 1 (
  echo.
  echo Icon generation build failed.
  pause
  exit /b 1
)

IconBuilder.exe
if errorlevel 1 (
  echo.
  echo Icon generation failed.
  pause
  exit /b 1
)

echo [MultiShot] Building...
"%CSC%" /nologo /target:winexe /optimize+ /platform:anycpu /codepage:65001 /win32icon:MultiShot.ico /out:MultiShot.exe /reference:System.Windows.Forms.dll /reference:System.Drawing.dll MultiShot.cs
if errorlevel 1 (
  echo.
  echo Build failed.
  pause
  exit /b 1
)

del /q IconBuilder.exe >nul 2>nul

echo [MultiShot] Build succeeded: MultiShot.exe
