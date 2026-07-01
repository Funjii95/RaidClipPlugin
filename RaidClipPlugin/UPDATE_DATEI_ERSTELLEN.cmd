@echo off
setlocal
cd /d "%~dp0"
title Raid Clip Plugin - GitHub Update veroeffentlichen

echo ================================================
echo   GitHub update.json erstellen
echo ================================================
echo.
echo Zuerst BUILD_INSTALLER.bat ausfuehren.
echo Das ZIP und update.json kommen danach gemeinsam in ein GitHub Release.
echo.
set /p "DOWNLOAD_URL=GitHub-HTTPS-Adresse des Update-ZIPs: "
if "%DOWNLOAD_URL%"=="" (
    echo Keine Adresse eingegeben.
    pause
    exit /b 1
)
set /p "CHANGELOG=Changelog fuer diese Version: "
if "%CHANGELOG%"=="" set "CHANGELOG=Neue Funktionen und Verbesserungen."

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "Installer\CreateUpdateManifest.ps1" -DownloadUrl "%DOWNLOAD_URL%" -Changelog "%CHANGELOG%"
if errorlevel 1 (
    echo.
    echo update.json konnte nicht erstellt werden.
    pause
    exit /b 1
)

echo.
echo Lade diese beiden Dateien als GitHub-Release-Assets hoch:
echo - installer-output\RaidClipPlugin-Update-1.2.2.zip
echo - installer-output\update.json
pause
