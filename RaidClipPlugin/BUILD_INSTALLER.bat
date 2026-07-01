@echo off
setlocal
cd /d "%~dp0"
title Raid Clip Plugin - Installer und Update-Paket

echo ================================================
echo   Raid Clip Plugin - Release erstellen
echo ================================================
echo.

where dotnet >nul 2>nul
if errorlevel 1 (
    echo FEHLER: Das .NET SDK wurde nicht gefunden.
    echo Bitte installiere das aktuelle .NET SDK und starte diese Datei erneut.
    echo https://dotnet.microsoft.com/download
    pause
    exit /b 1
)

if exist "publish" rmdir /s /q "publish"
if exist "installer-output" rmdir /s /q "installer-output"
mkdir "installer-output"

echo [1/3] App wird fuer Windows vorbereitet ...
dotnet publish "RaidClipPlugin.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o "publish"
if errorlevel 1 (
    echo.
    echo FEHLER: Die App konnte nicht erstellt werden.
    pause
    exit /b 1
)

echo [2/3] ZIP fuer GitHub Auto-Update wird erstellt ...
powershell.exe -NoProfile -Command "Compress-Archive -Path 'publish\*' -DestinationPath 'installer-output\RaidClipPlugin-Update-1.2.2.zip' -Force"
if errorlevel 1 (
    echo.
    echo FEHLER: Das Update-ZIP konnte nicht erstellt werden.
    pause
    exit /b 1
)

set "ISCC=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
if exist "%ISCC%" goto compiler_found
set "ISCC=%ProgramFiles%\Inno Setup 6\ISCC.exe"
if exist "%ISCC%" goto compiler_found
set "ISCC="
for /f "delims=" %%I in ('where ISCC.exe 2^>nul') do if not defined ISCC set "ISCC=%%I"
if defined ISCC goto compiler_found

echo.
echo FEHLER: Inno Setup 6 wurde nicht gefunden.
echo Das Update-ZIP wurde bereits erstellt.
echo Fuer den normalen Installer bitte Inno Setup installieren:
echo https://jrsoftware.org/isdl.php
pause
exit /b 1

:compiler_found
echo [3/3] Windows-Installer wird erstellt ...
"%ISCC%" "Installer\RaidClipPlugin.iss"
if errorlevel 1 (
    echo.
    echo FEHLER: Der Installer konnte nicht erstellt werden.
    pause
    exit /b 1
)

echo.
echo FERTIG:
echo - RaidClipPlugin-Setup-1.2.2.exe
echo - RaidClipPlugin-Update-1.2.2.zip
echo Ordner: %~dp0installer-output
start "" explorer.exe "%~dp0installer-output"
pause
