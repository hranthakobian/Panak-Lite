@echo off
setlocal enabledelayedexpansion
title Panak MSIX Builder

pushd "%~dp0\.."

echo ===================================================
echo             Building Panak Store MSIX
echo ===================================================

powershell -NoProfile -ExecutionPolicy Bypass -File "scripts\BuildMsix.ps1" %*
if %ERRORLEVEL% neq 0 (
    echo.
    echo [ERROR] MSIX build failed with exit code %ERRORLEVEL%.
    popd
    pause
    exit /b %ERRORLEVEL%
)

popd
pause
