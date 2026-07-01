@echo off
setlocal
title RaidClipPlugin - EXE erstellen
cd /d "%~dp0"

set "DOTNET=%ProgramFiles%\dotnet\dotnet.exe"
if not exist "%DOTNET%" (
  echo.
  echo FEHLER: .NET 10 SDK wurde nicht gefunden.
  echo Installiere es von https://dotnet.microsoft.com/download/dotnet/10.0
  echo und starte diese Datei danach erneut.
  echo.
  pause
  exit /b 1
)

echo Erstelle die eigenstaendige Windows-EXE...
"%DOTNET%" publish "RaidClipPlugin.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "%~dp0fertige-exe"
if errorlevel 1 (
  echo.
  echo Der Build ist fehlgeschlagen. Die Fehlermeldung steht oben.
  pause
  exit /b 1
)

echo.
echo FERTIG!
echo Die App liegt hier:
echo %~dp0fertige-exe\RaidClipPlugin.exe
explorer "%~dp0fertige-exe"
pause
