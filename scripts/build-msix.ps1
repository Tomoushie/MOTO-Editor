# scripts/build-msix.ps1
param(
    [string]$Config = "Release",
    [string]$PfxPath = "",
    [string]$PfxPassword = ""
)
$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..")

# Options de signature (si certificat fourni)
$signArgs = @()
if ($PfxPath) {
    $signArgs += "-p:AppxPackageSigningEnabled=true"
    $signArgs += "-p:PackageCertificateKeyFile=$PfxPath"
    $signArgs += "-p:PackageCertificatePassword=$PfxPassword"
}

# Publie en générant le MSIX (mode packagé)
dotnet publish "$root/Moto.Editor/Moto.Editor.csproj" -c $Config `
    -f net8.0-windows10.0.19041.0 `
    -p:GenerateAppxPackageOnBuild=true `
    -p:WindowsAppSDKSelfContained=true `
    @signArgs

# Récupère le MSIX produit
$msix = Get-ChildItem -Path "$root/Moto.Editor/bin/$Config" -Recurse -Filter *.msix |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1

if ($msix) {
    New-Item -ItemType Directory -Force -Path "$root/dist/msix" | Out-Null
    Copy-Item $msix.FullName "$root/dist/msix/MotoEditor.msix" -Force
    Write-Host "✅ MSIX prêt : dist/msix/MotoEditor.msix" -ForegroundColor Green
} else {
    Write-Host "❌ MSIX non trouvé" -ForegroundColor Red
    exit 1
}
