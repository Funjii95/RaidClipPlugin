param([Parameter(Mandatory = $true)][string]$ProjectDirectory)

$base64Path = Join-Path $ProjectDirectory "Assets\RaidClipPlugin.ico.b64"
$iconPath = Join-Path $ProjectDirectory "Assets\RaidClipPlugin.ico"
$base64 = (Get-Content -Raw -LiteralPath $base64Path) -replace "\s", ""
[IO.File]::WriteAllBytes($iconPath, [Convert]::FromBase64String($base64))
