$ErrorActionPreference='Stop'
Push-Location $PSScriptRoot
try {
    dotnet test Sync/tests/Backend.Tests/Backend.Tests.csproj --verbosity minimal
    if($LASTEXITCODE -ne 0){throw 'Backend tests failed'}
    dotnet test RaidClipPlugin/Tests/RaidClipPlugin.Tests.csproj --verbosity minimal
    if($LASTEXITCODE -ne 0){throw 'Plugin tests failed'}
    pnpm --dir twitch-extension install --frozen-lockfile
    if($LASTEXITCODE -ne 0){throw 'Frontend restore failed'}
    pnpm --dir twitch-extension build
    if($LASTEXITCODE -ne 0){throw 'Frontend build failed'}
    pnpm --dir twitch-extension test
    if($LASTEXITCODE -ne 0){throw 'Frontend tests failed'}
    pnpm --dir twitch-extension build:demo
    if($LASTEXITCODE -ne 0){throw 'Separate demo build failed'}
    pnpm --dir twitch-extension test:e2e
    if($LASTEXITCODE -ne 0){throw 'Browser tests failed'}
    dotnet publish RaidClipPlugin/RaidClipPlugin.csproj -c Release -r win-x64 --self-contained true -o artifacts/Windows-ActionChannel-Test
    if($LASTEXITCODE -ne 0){throw 'Windows publish failed'}
} finally {Pop-Location}
