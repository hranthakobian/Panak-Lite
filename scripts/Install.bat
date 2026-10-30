@echo off
title Panak Installer
echo Stopping existing Panak instances...
taskkill /F /IM Panak.exe 2>nul
taskkill /F /IM Setup.exe 2>nul

echo Building and launching Standalone Setup.exe...
call "%~dp0BuildSetup.bat"

pushd "%~dp0\.."
if exist "publish\Setup.exe" (
    start "" "publish\Setup.exe"
) else if exist "Setup\bin\Release\net8.0-windows10.0.19041.0\win-x64\Setup.exe" (
    start "" "Setup\bin\Release\net8.0-windows10.0.19041.0\win-x64\Setup.exe"
) else if exist "Setup\bin\Debug\net8.0-windows10.0.19041.0\Setup.exe" (
    start "" "Setup\bin\Debug\net8.0-windows10.0.19041.0\Setup.exe"
) else (
    echo Error: Setup.exe not found.
    pause
)
popd
