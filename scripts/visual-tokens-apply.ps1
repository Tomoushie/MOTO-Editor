#Requires -Version 5.1
<#
.SYNOPSIS
    Convertit les valeurs visuelles codées en dur d'un fichier XAML vers les
    jetons du langage visuel MOTO Editor (Phase 1).

.DESCRIPTION
    Uniquement les correspondances NON AMBIGUËS de la table de conversion de
    Docs/design/Langage-visuel-spec.md. Les valeurs qui demandent un jugement
    au cas par cas (11 px d'un badge vs 11 px de texte courant, 18, 24) sont
    LAISSÉES INTACTES et listées en fin d'exécution — les trancher
    automatiquement produirait un contraste faux quelque part.

    Sécurité : n'écrit que si -Apply est passé. Sans lui, affiche ce qui
    changerait (dry-run). Le dépôt git est le filet : un fichier abîmé se
    restaure par `git checkout`.

.PARAMETER Files
    Fichiers XAML à traiter (chemins relatifs au dépôt).

.PARAMETER Apply
    Écrit réellement les fichiers. Sans ce commutateur : dry-run.

.EXAMPLE
    pwsh scripts/visual-tokens-apply.ps1 -Files Moto.Editor/Views/GitPanelView.xaml
    pwsh scripts/visual-tokens-apply.ps1 -Files Moto.Editor/Views/GitPanelView.xaml -Apply
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string[]]$Files,
    [switch]$Apply
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

# ── Table de conversion — correspondances non ambiguës uniquement ───────────
# Clé = valeur trouvée dans le XAML, valeur = nom du jeton.
$fontMap = @{
    '9' = 'FontSizeSmall'; '10' = 'FontSizeSmall'; '10.5' = 'FontSizeSmall'
    '12' = 'FontSizeBody'; '12.5' = 'FontSizeBody'; '13' = 'FontSizeBody'
    '14' = 'FontSizeSubheading'; '15' = 'FontSizeSubheading'
    '16' = 'FontSizeHeading'; '17' = 'FontSizeHeading'
    '20' = 'FontSizeTitle'; '21' = 'FontSizeTitle'; '22' = 'FontSizeTitle'
    '26' = 'FontSizeDisplay'; '28' = 'FontSizeDisplay'; '30' = 'FontSizeDisplay'
}
$radiusMap = @{
    '4' = 'RadiusXs'
    '6' = 'RadiusSm'; '7' = 'RadiusSm'
    '8' = 'RadiusMd'; '9' = 'RadiusMd'; '10' = 'RadiusMd'
    '12' = 'RadiusLg'; '14' = 'RadiusLg'; '15' = 'RadiusLg'
    '16' = 'RadiusLg'; '17' = 'RadiusLg'; '24' = 'RadiusLg'
    '75' = 'RadiusPill'
}
# Valeur numérique canonique de chaque jeton (voir MotoTheme.xaml).
$radiusValeur = @{
    RadiusXs = 4; RadiusSm = 6; RadiusMd = 8; RadiusLg = 12; RadiusPill = 999
}
# Valeurs volontairement NON converties (jugement au cas par cas).
$ambiguës = @('7', '11', '11.5', '18')

$totalFont = 0; $totalRad = 0; $totalAmbig = 0
$rapport = @()

foreach ($rel in $Files) {
    $path = Join-Path $root $rel
    if (-not (Test-Path $path)) { Write-Warning "introuvable : $rel"; continue }
    $avant = Get-Content $path -Raw
    $apres = $avant
    $nF = 0; $nR = 0

    foreach ($k in $fontMap.Keys) {
        $jeton = $fontMap[$k]
        $motif = 'FontSize="' + [regex]::Escape($k) + '"'
        $rempl = 'FontSize="{StaticResource ' + $jeton + '}"'
        $c = ([regex]::Matches($apres, $motif)).Count
        if ($c -gt 0) { $apres = $apres -replace $motif, $rempl; $nF += $c }
    }
    foreach ($k in $radiusMap.Keys) {
        $jeton = $radiusMap[$k]
        $canonique = $radiusValeur[$jeton]

        # Forme attribut : la valeur EST l'extension de balisage -> jeton direct.
        $m1 = 'CornerRadius="' + [regex]::Escape($k) + '"'
        $r1 = 'CornerRadius="{StaticResource ' + $jeton + '}"'
        $c1 = ([regex]::Matches($apres, $m1)).Count
        if ($c1 -gt 0) { $apres = $apres -replace $m1, $r1; $nR += $c1 }

        # ★ Forme `StrokeShape="RoundRectangle N"` : NE PAS écrire
        # `RoundRectangle {StaticResource ...}` — la valeur est passée telle
        # quelle à un TypeConverter qui attend du texte (« RoundRectangle 12 »),
        # et un mélange texte + extension de balisage n'est pas fiable : il
        # compilerait peut-être, mais rendrait au mieux sans effet, au pire de
        # travers — et AUCUN contrôle automatique ne le verrait. On normalise
        # donc vers la VALEUR NUMÉRIQUE CANONIQUE du jeton (ex. 9 -> 8, 24 -> 12),
        # ce qui supprime la dérive (les 7/9/15/17/24 ajustés à l'œil) sans
        # introduire de construction douteuse.
        $m2 = 'RoundRectangle ' + [regex]::Escape($k) + '(?![0-9])'
        $r2 = 'RoundRectangle ' + $canonique
        $c2 = ([regex]::Matches($apres, $m2)).Count
        if ($c2 -gt 0) { $apres = $apres -replace $m2, $r2; $nR += $c2 }
    }

    # Valeurs laissées telles quelles, pour information.
    $restantes = @()
    foreach ($a in $ambiguës) {
        $c = ([regex]::Matches($apres, 'FontSize="' + [regex]::Escape($a) + '"')).Count
        if ($c -gt 0) { $restantes += "$a px x$c" }
    }

    $totalFont += $nF; $totalRad += $nR; $totalAmbig += $restantes.Count
    $rapport += [pscustomobject]@{ Fichier = $rel; Polices = $nF; Rayons = $nR; Laisses = ($restantes -join ', ') }

    if ($Apply -and $apres -ne $avant) { Set-Content -Path $path -Value $apres -NoNewline -Encoding UTF8 }
}

$rapport | Format-Table -AutoSize | Out-String | Write-Host
if ($Apply) { Write-Host "APPLIQUÉ" -ForegroundColor Yellow } else { Write-Host "DRY-RUN (aucune écriture) — relancer avec -Apply" -ForegroundColor DarkGray }
Write-Host ("Polices converties : {0}   Rayons convertis : {1}" -f $totalFont, $totalRad)
