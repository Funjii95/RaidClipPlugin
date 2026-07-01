param(
    [Parameter(Mandatory = $true)]
    [string]$DownloadUrl,

    [string]$Changelog = "Neue Funktionen und Verbesserungen.",

    [string]$PackagePath = ""
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$projectFile = Join-Path $projectRoot "RaidClipPlugin.csproj"
[xml]$project = Get-Content $projectFile
$version = [string]$project.Project.PropertyGroup.Version

if (-not $DownloadUrl.StartsWith("https://", [StringComparison]::OrdinalIgnoreCase)) {
    throw "Die Downloadadresse muss mit https:// beginnen."
}

if ([string]::IsNullOrWhiteSpace($PackagePath)) {
    $PackagePath = Join-Path $projectRoot "installer-output\RaidClipPlugin-Update-$version.zip"
}
elseif (-not [IO.Path]::IsPathRooted($PackagePath)) {
    $PackagePath = Join-Path $projectRoot $PackagePath
}

if (-not (Test-Path $PackagePath)) {
    throw "Das Update-Paket wurde nicht gefunden. Zuerst BUILD_INSTALLER.bat starten."
}

$extension = [IO.Path]::GetExtension($PackagePath)
if ($extension -notin @(".zip", ".exe")) {
    throw "Das Update-Paket muss eine ZIP- oder EXE-Datei sein."
}

$hash = (Get-FileHash -Path $PackagePath -Algorithm SHA256).Hash
$manifest = [ordered]@{
    latestVersion = $version
    downloadUrl = $DownloadUrl
    changelog = $Changelog
    sha256 = $hash
}

$target = Join-Path $projectRoot "installer-output\update.json"
$manifest | ConvertTo-Json | Set-Content -Path $target -Encoding UTF8

Write-Host ""
Write-Host "GitHub-Update-Datei erstellt:" -ForegroundColor Green
Write-Host $target
Write-Host ""
Write-Host "Update-ZIP und update.json als Assets des GitHub Releases hochladen."
