#Requires -Version 5.1
<#
.SYNOPSIS
    Convertit les espacements (Padding/Margin/Spacing) à valeur UNIQUE codés
    en dur vers les jetons du langage visuel MOTO Editor (Phase 1 bis).

.DESCRIPTION
    Suite de scripts/visual-tokens-apply.ps1 (police + rayons, déjà appliqué).
    Périmètre identique : uniquement Docs/design/Langage-visuel-spec.md §3.
    Les valeurs COMPOSÉES ("Padding=14,10") ne sont volontairement PAS
    touchées ici — elles demandent un choix par paire, pas une table.

    Sécurité : n'écrit que si -Apply est passé. Sans lui, affiche ce qui
    changerait (dry-run). Le dépôt git est le filet : un fichier abîmé se
    restaure par `git checkout`.

.PARAMETER Files
    Fichiers XAML à traiter (chemins relatifs au dépôt).

.PARAMETER Apply
    Écrit réellement les fichiers. Sans ce commutateur : dry-run.

.EXAMPLE
    pwsh scripts/visual-spacing-apply.ps1 -Files Moto.Editor/Views/GitPanelView.xaml
    pwsh scripts/visual-spacing-apply.ps1 -Files Moto.Editor/Views/GitPanelView.xaml -Apply
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string[]]$Files,
    [switch]$Apply
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
# Lecture/écriture .NET explicites en UTF-8 SANS BOM (cohérent avec les
# fichiers XAML existants du dépôt) — Get-Content/Set-Content de Windows
# PowerShell 5.1 devinent mal l'encodage d'un fichier UTF-8 sans BOM et
# corrompent les accents/emoji en écriture. pwsh (7) n'a pas ce défaut,
# mais ce script doit rester utilisable depuis PS 5.1 aussi.
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
function Read-Utf8($p) { [System.IO.File]::ReadAllText($p, [System.Text.Encoding]::UTF8) }
function Write-Utf8($p, $t) { [System.IO.File]::WriteAllText($p, $t, $utf8NoBom) }

# Table de conversion — Docs/design/Rapport-conversion-jetons.md §3 (22/09).
# 0 est volontairement absent : il reste inchangé (séparateur à angle droit).
$spaceMap = @{
    '1' = 'SpaceXs'; '2' = 'SpaceXs'; '3' = 'SpaceXs'; '4' = 'SpaceXs'; '6' = 'SpaceXs'
    '8' = 'SpaceSm'; '9' = 'SpaceSm'; '10' = 'SpaceSm'; '12' = 'SpaceSm'
    '14' = 'SpaceMd'; '16' = 'SpaceMd'; '18' = 'SpaceMd'; '20' = 'SpaceMd'
    '22' = 'SpaceLg'; '24' = 'SpaceLg'
}
$props = 'Padding', 'Margin', 'Spacing'

$total = 0
$rapport = @()

foreach ($rel in $Files) {
    $path = Join-Path $root $rel
    if (-not (Test-Path $path)) { Write-Warning "introuvable : $rel"; continue }
    $avant = Read-Utf8 $path
    $apres = $avant
    $n = 0

    foreach ($prop in $props) {
        foreach ($k in $spaceMap.Keys) {
            $jeton = $spaceMap[$k]
            # Valeur UNIQUE uniquement : pas de virgule après (sinon composé,
            # laissé intact) et pas de chiffre avant/après (sinon "14" matcherait
            # dans "140").
            $motif = "$prop=`"" + [regex]::Escape($k) + "`""
            $rempl = "$prop=`"{StaticResource $jeton}`""
            $c = ([regex]::Matches($apres, $motif)).Count
            if ($c -gt 0) { $apres = $apres -replace $motif, $rempl; $n += $c }
        }
    }

    $total += $n
    $rapport += [pscustomobject]@{ Fichier = $rel; Espacements = $n }

    if ($Apply -and $apres -ne $avant) { Write-Utf8 $path $apres }
}

$rapport | Where-Object { $_.Espacements -gt 0 } | Format-Table -AutoSize | Out-String | Write-Host
if ($Apply) { Write-Host "APPLIQUÉ" -ForegroundColor Yellow } else { Write-Host "DRY-RUN (aucune écriture) — relancer avec -Apply" -ForegroundColor DarkGray }
Write-Host ("Espacements convertis : {0}" -f $total)
