$ErrorActionPreference='Stop'
Push-Location $PSScriptRoot
try {
    dotnet test RaidClipPlugin/Tests/RaidClipPlugin.Tests.csproj --verbosity minimal
    if($LASTEXITCODE -ne 0){throw 'Plugin tests failed'}
    dotnet test Sync/tests/Backend.Tests/Backend.Tests.csproj --verbosity minimal
    if($LASTEXITCODE -ne 0){throw 'Backend tests failed'}
    dotnet publish RaidClipPlugin/RaidClipPlugin.csproj -c Release -r win-x64 --self-contained true -o artifacts/Windows-Test
    if($LASTEXITCODE -ne 0){throw 'Windows publish failed'}
} finally {Pop-Location}
