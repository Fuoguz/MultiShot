@echo off
setlocal
cd /d "%~dp0"
if not exist "MultiShot.exe" call build.bat
if exist "MultiShot.exe" start "" "MultiShot.exe"
