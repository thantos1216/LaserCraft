#requires -Version 7.0
param()
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
[xml]$buildProperties = Get-Content -Raw -LiteralPath (Join-Path $projectRoot 'Directory.Build.props')
$version = [string]$buildProperties.Project.PropertyGroup.Version
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Expected a numeric project version.' }
$artifactRoot = Join-Path $projectRoot 'artifacts'
$publishPath = Join-Path $artifactRoot "LaserCraft-$version-win-x64-portable"
$archivePath = "$publishPath.zip"
if ((Test-Path -LiteralPath $publishPath) -or (Test-Path -LiteralPath $archivePath)) { throw "Package output already exists: $publishPath. Preserve or rename the earlier package before rebuilding." }
& dotnet publish (Join-Path $projectRoot 'src/LaserCraft.Desktop/LaserCraft.Desktop.csproj') -c Release -r win-x64 --self-contained true -o $publishPath --nologo
if ($LASTEXITCODE -ne 0) { throw 'Portable publish failed; no archive created.' }
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs/help/USB-CONNECTION-TEST.md') -Destination (Join-Path $publishPath 'USB-CONNECTION-TEST.md')
$commit = (& git -C $projectRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Could not record source commit.' }
$dirty = [bool](& git -C $projectRoot status --porcelain)
@{ version=$version; commit=$commit; sourceDirty=$dirty; builtUtc=[DateTimeOffset]::UtcNow.ToString('O'); runtime='win-x64'; selfContained=$true } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $publishPath 'BUILD-INFO.json')
$manifest = Get-ChildItem -LiteralPath $publishPath -Recurse -File | Sort-Object FullName | ForEach-Object {
    [pscustomobject]@{ path=[IO.Path]::GetRelativePath($publishPath,$_.FullName); bytes=$_.Length; sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
}
$manifest | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $publishPath 'FILES-SHA256.json')
Compress-Archive -Path (Join-Path $publishPath '*') -DestinationPath $archivePath -CompressionLevel Optimal
Get-Item -LiteralPath $archivePath | Select-Object FullName,Length
Get-FileHash -LiteralPath $archivePath -Algorithm SHA256
