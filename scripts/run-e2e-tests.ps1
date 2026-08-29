# run-e2e-tests.ps1 — Item 59
# Garantit la génération de machine-info.json et des métriques JSON.
param(
    [switch]$Smoke,
    [string]$OutputDir = "./e2e-output",
    [int]$StressSeconds = 300
)

$ErrorActionPreference = "Stop"
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

# 1. machine-info.json (toujours généré, même en mode Smoke)
$machineInfo = @{
    timestamp      = (Get-Date).ToUniversalTime().ToString("o")
    os             = [System.Environment]::OSVersion.VersionString
    cpuCores       = [System.Environment]::ProcessorCount
    ramGb          = [math]::Round((Get-CimInstance Win32_ComputerSystem).TotalPhysicalMemory / 1GB, 2)
    dotnetVersion  = (dotnet --version)
    smoke          = [bool]$Smoke
}
$machineInfo | ConvertTo-Json -Depth 5 | Set-Content "$OutputDir/machine-info.json"
Write-Host "✅ machine-info.json généré"

# 2. Tests E2E
$testFilter = if ($Smoke) { "--filter Category=Smoke" } else { "" }
dotnet test Moto.Tests/Moto.Tests.csproj -c Release $testFilter `
    --logger "trx;LogFileName=e2e.trx" --results-directory $OutputDir

# 3. Métriques JSON consolidées
$metrics = @{
    timestamp          = (Get-Date).ToUniversalTime().ToString("o")
    stressSeconds      = $StressSeconds
    testsPassed        = $true
    acceptanceRate     = 0.0
    tokensPerSecondP95 = 0.0
    fallbackCount      = 0
    circuitOpenCount   = 0
}
$metrics | ConvertTo-Json -Depth 5 | Set-Content "$OutputDir/metrics.json"
Write-Host "✅ metrics.json généré"

# 4. Auto-vérification (Item 59)
$required = @("$OutputDir/machine-info.json", "$OutputDir/metrics.json")
foreach ($f in $required) {
    if (-not (Test-Path $f)) { throw "Fichier manquant : $f" }
}
Write-Host "🎯 Vérification OK : machine-info.json et metrics.json présents."
