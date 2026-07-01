@echo off
setlocal
cd /d "%~dp0"

where dotnet >nul 2>nul
if errorlevel 1 (
    echo .NET SDK wurde nicht gefunden.
    echo Bitte installiere das aktuelle .NET 10 SDK und starte diese Datei erneut.
    pause
    exit /b 1
)

echo Erstelle RaidClipPlugin.exe ...
dotnet publish RaidClipPlugin.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o publish

if errorlevel 1 (
    echo.
    echo Der Build ist fehlgeschlagen.
    pause
    exit /b 1
)

echo.
echo Fertig: %~dp0publish\RaidClipPlugin.exe
start "" explorer.exe "%~dp0publish"
pause
