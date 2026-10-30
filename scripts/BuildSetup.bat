@echo off
setlocal enabledelayedexpansion
title Panak Setup Builder

pushd "%~dp0\.."

echo ===================================================
echo             Building Panak Setup.exe
echo ===================================================

echo [1/5] Stopping any running instances...
taskkill /F /IM Panak.exe 2>nul
taskkill /F /IM Setup.exe 2>nul

echo [2/5] Publishing Panak application and Shell Extension...
if exist "staging_panak" rmdir /S /Q "staging_panak"
dotnet publish -c Release -r win-x64 --self-contained true -o staging_panak Panak.csproj
if %ERRORLEVEL% neq 0 (
    echo Error publishing Panak.
    popd
    exit /b %ERRORLEVEL%
)
dotnet publish -c Release -r win-x64 --self-contained false -o staging_panak Panak.ShellExt\Panak.ShellExt.csproj
if %ERRORLEVEL% neq 0 (
    echo Error publishing Panak.ShellExt.
    popd
    exit /b %ERRORLEVEL%
)
dotnet publish -c Release -r win-x64 --self-contained true -o staging_panak Uninstall\Uninstall.csproj
if %ERRORLEVEL% neq 0 (
    echo Error publishing Uninstall.exe.
    popd
    exit /b %ERRORLEVEL%
)

echo [3/5] Compressing payload...
if exist "Setup\PanakPayload.zip" del /F /Q "Setup\PanakPayload.zip"
powershell -NoProfile -Command "Compress-Archive -Path 'staging_panak\*' -DestinationPath 'Setup\PanakPayload.zip' -Force"
if %ERRORLEVEL% neq 0 (
    echo Error compressing payload.
    popd
    exit /b %ERRORLEVEL%
)

echo [4/5] Publishing Standalone Setup.exe...
if not exist "publish" mkdir "publish"
xcopy /E /I /Y "staging_panak\*" "publish\" >nul
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o publish Setup\Setup.csproj
if %ERRORLEVEL% neq 0 (
    echo Error publishing Setup.exe.
    popd
    exit /b %ERRORLEVEL%
)

echo [5/5] Cleaning temporary staging files...
if exist "staging_panak" rmdir /S /Q "staging_panak"
if exist "Setup\PanakPayload.zip" del /F /Q "Setup\PanakPayload.zip"

echo ===================================================
echo   Success! Standalone installer ready at:
echo   publish\Setup.exe
echo ===================================================

popd
