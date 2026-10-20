param([string]$OutputDirectory=(Join-Path $PSScriptRoot 'artifacts/RPG-Actions-2.1.24'))
$ErrorActionPreference='Stop'
$artifactPath=[System.IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path -LiteralPath $artifactPath){throw 'Ziel existiert bereits. Für einen neuen Build einen neuen Ausgabeordner angeben; alte Builds bleiben erhalten.'}
New-Item -ItemType Directory -Path $artifactPath | Out-Null
function Invoke-Checked([string]$Executable,[string[]]$TaskArguments){
    & $Executable @TaskArguments
    if($LASTEXITCODE -ne 0){throw "Prüfung/Build fehlgeschlagen: $Executable"}
}
Push-Location $PSScriptRoot
try{
    Invoke-Checked dotnet @('test','RaidClipPlugin/Tests/RaidClipPlugin.Tests.csproj','-v','minimal')
    Invoke-Checked dotnet @('test','Sync/tests/Backend.Tests/Backend.Tests.csproj','-v','minimal')
    Invoke-Checked pnpm.cmd @('--dir','twitch-extension','build:repro')
    Invoke-Checked pnpm.cmd @('--dir','twitch-extension','test')
    Invoke-Checked pnpm.cmd @('--dir','twitch-extension','build:demo')
    Invoke-Checked pnpm.cmd @('--dir','twitch-extension','test:actions')
    Invoke-Checked pnpm.cmd @('--dir','twitch-extension','build:hosted-test')
    Invoke-Checked dotnet @('publish','RaidClipPlugin/RaidClipPlugin.csproj','-c','Release','-r','win-x64','--self-contained','true','-p:IsolatedActionsTest=true','-p:Version=2.1.24-actions-test.1','-p:FileVersion=2.1.24.0','-p:AssemblyVersion=2.1.24.0','-o',(Join-Path $artifactPath 'Windows-Test'))
    Invoke-Checked dotnet @('publish','RaidClipPlugin/RaidClipPlugin.csproj','-c','Release','-r','win-x64','--self-contained','true','-p:IsolatedActionsTest=false','-p:Version=2.1.24-actions-test.1','-p:FileVersion=2.1.24.0','-p:AssemblyVersion=2.1.24.0','-o',(Join-Path $artifactPath 'Windows-Integration-RC'))
    Invoke-Checked dotnet @('publish','Sync/src/Backend/Backend.csproj','-c','Release','-r','linux-x64','--self-contained','false','-t:PublishContainer','-p:ContainerImageFormat=OCI','-p:ContainerBaseImage=mcr.microsoft.com/dotnet/aspnet:10.0','-p:ContainerUser=1654','-p:ContainerRepository=backend','-p:ContainerImageTag=2.1.24-actions-test.1',('-p:ContainerArchiveOutputPath='+(Join-Path $artifactPath 'backend.oci.tar')),'-p:Version=2.1.24-actions-test.1')
    Compress-Archive -Path (Join-Path $PSScriptRoot 'twitch-extension/dist-hosted-test/*') -DestinationPath (Join-Path $artifactPath 'Funjii-Otter-RPG-0.0.2-Actions-HostedTest.zip')
    Compress-Archive -Path (Join-Path $artifactPath 'Windows-Test/*') -DestinationPath (Join-Path $artifactPath 'RaidClipPlugin-2.1.24-Actions-Test.zip')
    Compress-Archive -Path (Join-Path $artifactPath 'Windows-Integration-RC/*') -DestinationPath (Join-Path $artifactPath 'RaidClipPlugin-2.1.24-Integration-RC.zip')
}finally{Pop-Location}
Write-Host "Lokale Testpakete erstellt: $artifactPath. Kein Cloud-Deployment und kein Upload ausgeführt."
