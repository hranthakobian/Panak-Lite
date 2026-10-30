@echo off
title Panak Uninstaller
echo Starting Panak Uninstaller Wizard...
pushd "%~dp0\.."
if exist "%LocalAppData%\Programs\Panak\Uninstall.exe" (
    start "" "%LocalAppData%\Programs\Panak\Uninstall.exe"
) else if exist "publish\Uninstall.exe" (
    start "" "publish\Uninstall.exe"
) else if exist "Uninstall\bin\Release\net8.0-windows10.0.19041.0\win-x64\Uninstall.exe" (
    start "" "Uninstall\bin\Release\net8.0-windows10.0.19041.0\win-x64\Uninstall.exe"
) else if exist "Uninstall\bin\Debug\net8.0-windows10.0.19041.0\Uninstall.exe" (
    start "" "Uninstall\bin\Debug\net8.0-windows10.0.19041.0\Uninstall.exe"
) else if exist "publish\Setup.exe" (
    start "" "publish\Setup.exe" --uninstall
) else if exist "%LocalAppData%\Programs\Panak\Setup.exe" (
    start "" "%LocalAppData%\Programs\Panak\Setup.exe" --uninstall
) else if exist "Setup\bin\Release\net8.0-windows10.0.19041.0\win-x64\Setup.exe" (
    start "" "Setup\bin\Release\net8.0-windows10.0.19041.0\win-x64\Setup.exe" --uninstall
) else if exist "Setup\bin\Debug\net8.0-windows10.0.19041.0\Setup.exe" (
    start "" "Setup\bin\Debug\net8.0-windows10.0.19041.0\Setup.exe" --uninstall
) else if exist "%LocalAppData%\Programs\Panak\Panak.exe" (
    start "" "%LocalAppData%\Programs\Panak\Panak.exe" --uninstall
) else if exist "publish\Panak.exe" (
    start "" "publish\Panak.exe" --uninstall
) else (
    echo Panak executable or uninstaller not found.
    pause
)
popd
