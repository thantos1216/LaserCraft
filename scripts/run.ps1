$ErrorActionPreference = 'Stop'
Push-Location (Split-Path $PSScriptRoot -Parent)
try { dotnet run --project src/LaserCraft.Desktop; if ($LASTEXITCODE -ne 0) { throw 'LaserCraft failed to run.' } }
finally { Pop-Location }
