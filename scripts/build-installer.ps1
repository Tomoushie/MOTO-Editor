# scripts/build-installer.ps1
param([string]$Config = "Release", [string]$Rid = "win-x64")
$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..")

# 1. Publie l'éditeur (dossier self-contained)
dotnet publish "$root/Moto.Editor/Moto.Editor.csproj" -c $Config -r $Rid --self-contained true -o "$root/dist/payload"

# 2. Zip le payload
Compress-Archive -Path "$root/dist/payload/*" -DestinationPath "$root/Moto.Installer/payload.zip" -Force

# 3. Publie l'installateur single-file (embarque payload.zip)
dotnet publish "$root/Moto.Installer/Moto.Installer.csproj" -c $Config -r $Rid -o "$root/dist/installer"

Write-Host "✅ Installateur prêt : dist/installer/MotoEditor-Setup.exe" -ForegroundColor Green
