# scripts/compare-benchmarks.ps1
# Item 62 — Compare les benchmarks (Lite/Standard/Full) au baseline.
# Usage CI : compare-benchmarks.ps1 -TolerancePercent 15 -Baseline ./baselines/benchmark-baseline.json
param(
    [double]$TolerancePercent = 15,
    [string]$Baseline = "./baselines/benchmark-baseline.json",
    [string]$ResultsGlob = "./benchmark-*.json"
)

$ErrorActionPreference = "Stop"

function Read-Benchmark([string]$path) {
    if (-not (Test-Path $path)) { throw "Fichier introuvable : $path" }
    return Get-Content $path -Raw | ConvertFrom-Json
}

# 1. Charger le baseline
$baseline = Read-Benchmark $Baseline
Write-Host "📏 Baseline chargé : $Baseline"

# 2. Trouver le résultat courant (le plus récent)
$candidates = Get-ChildItem -Path $ResultsGlob -ErrorAction SilentlyContinue |
              Sort-Object LastWriteTime -Descending
if (-not $candidates) { throw "Aucun résultat benchmark trouvé ($ResultsGlob)." }
$currentPath = $candidates[0].FullName
$current = Read-Benchmark $currentPath
Write-Host "🧪 Résultat courant : $currentPath"

# 3. Métriques à comparer (plus haut = mieux pour tokens/s ; plus bas = mieux pour latence/RAM)
$higherIsBetter = @("tokensPerSecond")
$lowerIsBetter  = @("latencyP95Ms", "ramMb")

$failures = @()
$tiers = @("Lite", "Standard", "Full")

foreach ($tier in $tiers) {
    $b = $baseline.tiers.$tier
    $c = $current.tiers.$tier
    if (-not $b -or -not $c) {
        Write-Warning "Tier '$tier' manquant (baseline ou courant). Ignoré."
        continue
    }

    foreach ($metric in ($higherIsBetter + $lowerIsBetter)) {
        $bv = [double]$b.$metric
        $cv = [double]$c.$metric
        if ($bv -eq 0) { continue }

        $driftPercent = (($cv - $bv) / $bv) * 100

        if ($higherIsBetter -contains $metric) {
            # On tolère une baisse jusqu'à -Tolerance%
            if ($driftPercent -lt -$TolerancePercent) {
                $failures += "[$tier] $metric : $bv -> $cv ($([math]::Round($driftPercent,1))%) < -$TolerancePercent%"
            }
        } else {
            # On tolère une hausse jusqu'à +Tolerance%
            if ($driftPercent -gt $TolerancePercent) {
                $failures += "[$tier] $metric : $bv -> $cv (+$([math]::Round($driftPercent,1))%) > +$TolerancePercent%"
            }
        }
        Write-Host ("  {0,-8} {1,-16} base={2,-10} cur={3,-10} drift={4:+0.0;-0.0}%" -f $tier, $metric, $bv, $cv, $driftPercent)
    }
}

# 4. Verdict
if ($failures.Count -gt 0) {
    Write-Host "`n❌ Dérives hors tolérance ($TolerancePercent%) :" -ForegroundColor Red
    $failures | ForEach-Object { Write-Host "   - $_" -ForegroundColor Red }
    exit 1
}
Write-Host "`n✅ Tous les tiers sont dans la tolérance ±$TolerancePercent%." -ForegroundColor Green
exit 0
