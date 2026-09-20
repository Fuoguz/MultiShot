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

echo [MultiShot] Building...
"%CSC%" /nologo /target:winexe /optimize+ /platform:anycpu /codepage:65001 /out:MultiShot.exe /reference:System.Windows.Forms.dll /reference:System.Drawing.dll MultiShot.cs
if errorlevel 1 (
  echo.
  echo Build failed.
  pause
  exit /b 1
)

echo [MultiShot] Build succeeded: MultiShot.exe
