#Requires -Version 5.1
<#
.SYNOPSIS
    Rapport de conversion des valeurs visuelles codées en dur vers les jetons
    du langage visuel MOTO Editor (Docs/design/Langage-visuel-spec.md).

.DESCRIPTION
    ANALYSE SEULE (dry-run) : ce script ne modifie AUCUN fichier. Il produit un
    rapport Markdown qui dit, pour chaque valeur codée en dur, vers quel jeton
    elle irait — afin que la conversion soit relue et validée avant d'être
    appliquée.

    Périmètre : uniquement les fichiers XAML RÉELLEMENT COMPILÉS. Les fichiers
    listés dans <MauiXaml Remove> du .csproj et tout ce qui est sous Docs/ sont
    exclus — sinon on compte de la dette visuelle que personne ne voit (erreur
    commise une première fois dans l'audit, corrigée depuis).

.PARAMETER OutFile
    Chemin du rapport Markdown à produire.

.EXAMPLE
    pwsh scripts/visual-tokens-dryrun.ps1
#>
[CmdletBinding()]
param(
    [string]$OutFile = 'Docs/design/Rapport-conversion-jetons.md'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

# ── Échelle proposée (Docs/design/Langage-visuel-spec.md, option B) ──────────
# rôle -> taille cible en px
$roleSize = @{
    'Display' = 28; 'Title' = 20; 'Heading' = 16; 'Subheading' = 14
    'Body' = 13; 'BodyStrong' = 13; 'Small' = 12; 'Micro' = 11
    'Mono' = 13; 'MonoSmall' = 12
}
# valeur trouvée -> rôle(s) proposé(s). Deux rôles = cas à trancher à l'œil.
$fontMap = @{
    '7' = 'Small'; '9' = 'Small'; '10' = 'Small'; '10.5' = 'Small'
    '11' = 'Micro|Small'; '11.5' = 'Micro|Small'
    '12' = 'Body'; '12.5' = 'Body'; '13' = 'Body'
    '14' = 'Subheading'; '15' = 'Subheading'
    '16' = 'Heading'; '17' = 'Heading'; '18' = 'Heading|Title'
    '20' = 'Title'; '21' = 'Title'; '22' = 'Title'; '24' = 'Title|Display'
    '26' = 'Display'; '28' = 'Display'; '30' = 'Display'
}
$radiusMap = @{
    '0' = '0 (inchangé)'
    '6' = 'RadiusSm (6)'; '7' = 'RadiusSm (6)'
    '8' = 'RadiusMd (8)'; '9' = 'RadiusMd (8)'; '10' = 'RadiusMd (8)'
    '12' = 'RadiusLg (12)'; '14' = 'RadiusLg (12)'; '15' = 'RadiusLg (12)'
    '16' = 'RadiusLg (12)'; '17' = 'RadiusLg (12)'; '24' = 'RadiusLg (12)'
    '75' = 'RadiusPill'
}
$spaceScale = @(4, 8, 16, 24, 32)
$spaceNom = @{ 4 = 'SpaceXs'; 8 = 'SpaceSm'; 16 = 'SpaceMd'; 24 = 'SpaceLg'; 32 = 'SpaceXl' }

function Get-NearestSpace([double]$v) {
    $best = $spaceScale[0]
    foreach ($s in $spaceScale) { if ([math]::Abs($v - $s) -lt [math]::Abs($v - $best)) { $best = $s } }
    return $best
}

# ── Périmètre : XAML réellement compilé ─────────────────────────────────────
$csproj = Get-Content 'Moto.Editor/Moto.Editor.csproj' -Raw
$exclus = @()
foreach ($m in [regex]::Matches($csproj, 'MauiXaml Remove="([^"]+)"')) {
    $exclus += (Split-Path $m.Groups[1].Value -Leaf)
}
$fichiers = Get-ChildItem -Recurse -Include *.xaml -File |
    Where-Object {
        $_.FullName -notmatch '\\bin\\|\\obj\\|\\Docs\\' -and
        $_.Name -ne 'MotoTheme.xaml' -and
        $exclus -notcontains $_.Name
    }

# ── Collecte ────────────────────────────────────────────────────────────────
$fontHits = @{}; $radiusHits = @{}; $spaceHits = @{}; $dynFont = 0
$compositeRad = 0; $compositeSpc = 0
$parFichier = @{}

foreach ($f in $fichiers) {
    $texte = Get-Content $f.FullName -Raw
    $rel = $f.FullName.Replace($root + '\', '')
    $n = 0

    # FontSize numériques
    foreach ($m in [regex]::Matches($texte, 'FontSize="(\d+(?:\.\d+)?)"')) {
        $v = $m.Groups[1].Value
        if (-not $fontHits.ContainsKey($v)) { $fontHits[$v] = 0 }
        $fontHits[$v]++
        $n++
    }
    # FontSize déjà pilotés par un jeton (à NE PAS toucher)
    $dynFont += ([regex]::Matches($texte, 'FontSize="\{DynamicResource')).Count
    $dynFont += ([regex]::Matches($texte, 'FontSize="\{StaticResource')).Count

    # Rayons : deux syntaxes dans ce dépôt
    foreach ($m in [regex]::Matches($texte, 'CornerRadius="(\d+)"')) {
        $v = $m.Groups[1].Value
        if (-not $radiusHits.ContainsKey($v)) { $radiusHits[$v] = 0 }
        $radiusHits[$v]++; $n++
    }
    foreach ($m in [regex]::Matches($texte, 'RoundRectangle (\d+)')) {
        $v = $m.Groups[1].Value
        if (-not $radiusHits.ContainsKey($v)) { $radiusHits[$v] = 0 }
        $radiusHits[$v]++; $n++
    }
    # Rayons COMPOSÉS (CornerRadius="4,4,0,0") : non convertibles par table,
    # à traiter à la main — comptés à part pour ne pas les faire disparaître
    # du décompte.
    $compositeRad += ([regex]::Matches($texte, 'CornerRadius="\d+\s*,\s*\d+')).Count

    # Espacements à valeur unique (les valeurs composées "a,b" sont laissées
    # à l'appréciation : elles ne se convertissent pas mécaniquement)
    foreach ($prop in 'Padding', 'Spacing', 'Margin') {
        foreach ($m in [regex]::Matches($texte, "$prop=`"(\d+\.?\d*)`"")) {
            $v = $m.Groups[1].Value
            if (-not $spaceHits.ContainsKey($v)) { $spaceHits[$v] = 0 }
            $spaceHits[$v]++; $n++
        }
        # Valeurs composées : comptées, non converties
        $compositeSpc += ([regex]::Matches($texte, "$prop=`"\d+\.?\d*\s*,")).Count
    }

    $parFichier[$rel] = $n
}

# ── Rapport ─────────────────────────────────────────────────────────────────
$sb = New-Object System.Text.StringBuilder
$w = { param($t) [void]$sb.AppendLine($t) }

& $w '# Rapport de conversion vers les jetons visuels'
& $w ''
& $w '> **Généré automatiquement — analyse seule, aucun fichier modifié.**'
& $w "> Source : ``scripts/visual-tokens-dryrun.ps1`` · Périmètre : $($fichiers.Count) fichiers XAML réellement compilés"
& $w '> Échelle cible : `Docs/design/Langage-visuel-spec.md` (option B, corps 13 px)'
& $w ''
& $w 'Ce rapport existe pour rendre la conversion **relisible**. Les cas marqués'
& $w '« à trancher » ne se décident pas par table : ils dépendent de l''élément'
& $w '(un 11 px de badge et un 11 px de texte courant ne deviennent pas le même rôle).'
& $w ''

& $w '## 1. Tailles de police'
& $w ''
& $w "**$($dynFont) occurrences utilisent déjà un jeton** (`DynamicResource`/`StaticResource`) — elles ne doivent PAS être touchées."
& $w ''
& $w '| Valeur trouvée | Occurrences | Rôle proposé | Taille cible |'
& $w '|---|---|---|---|'
foreach ($k in ($fontHits.Keys | Sort-Object { [double]$_ })) {
    $role = $fontMap[$k]
    if (-not $role) { $role = '⚠ NON MAPPÉ' }
    $cible = ''
    if ($role -notmatch '\|' -and $role -ne '⚠ NON MAPPÉ') { $cible = "$($roleSize[$role]) px" }
    elseif ($role -match '\|') { $cible = 'à trancher' }
    $marque = ''
    if ($role -match '\|') { $marque = ' ⚠' }
    & $w "| $k px | $($fontHits[$k]) | $role$marque | $cible |"
}
& $w ''

& $w '## 2. Rayons d''arrondi'
& $w ''
& $w '| Valeur trouvée | Occurrences | Jeton proposé |'
& $w '|---|---|---|'
foreach ($k in ($radiusHits.Keys | Sort-Object { [int]$_ })) {
    $jeton = $radiusMap[$k]
    if (-not $jeton) { $jeton = '⚠ NON MAPPÉ' }
    & $w "| $k | $($radiusHits[$k]) | $jeton |"
}
& $w ''

& $w '## 3. Espacements (valeurs uniques seulement)'
& $w ''
& $w 'Les valeurs composées (`Padding="14,10"`) ne se convertissent pas'
& $w 'mécaniquement : elles demandent un choix. Seules les valeurs uniques sont'
& $w 'listées ici. Un écart affiché de 0 signifie que la valeur EST déjà sur'
& $w 'l''échelle ; toute autre valeur sera **corrigée** vers le jeton le plus'
& $w 'proche — c''est précisément l''effet recherché.'
& $w ''
& $w '| Valeur trouvée | Occurrences | Jeton le plus proche | Écart |'
& $w '|---|---|---|---|'
foreach ($k in ($spaceHits.Keys | Sort-Object { [double]$_ })) {
    $v = [double]$k
    if ($v -eq 0) {
        # 0 n'est pas une dérive : c'est un choix (collé au bord, pas de marge)
        & $w "| 0 | $($spaceHits[$k]) | **inchangé** (0 est volontaire) | — |"
        continue
    }
    $near = Get-NearestSpace $v
    $ecart = $v - $near
    $nom = $spaceNom[[int]$near]
    & $w "| $k | $($spaceHits[$k]) | $nom ($near) | $ecart |"
}
& $w ''

& $w '## 4. Fichiers les plus touchés (ordre de traitement suggéré)'
& $w ''
& $w '| Fichier | Valeurs à convertir |'
& $w '|---|---|'
foreach ($e in ($parFichier.GetEnumerator() | Sort-Object Value -Descending)) {
    if ($e.Value -gt 0) { & $w "| ``$($e.Key)`` | $($e.Value) |" }
}
& $w ''
& $w '## 5. Totaux'
& $w ''
$totFont = ($fontHits.Values | Measure-Object -Sum).Sum
$totRad  = ($radiusHits.Values | Measure-Object -Sum).Sum
$totSpc  = ($spaceHits.Values | Measure-Object -Sum).Sum
& $w "| Catégorie | Convertibles par table | Valeurs distinctes | Hors table (à la main) |"
& $w "|---|---|---|---|"
& $w "| FontSize | $totFont | $($fontHits.Count) | — |"
& $w "| Rayons | $totRad | $($radiusHits.Count) | $compositeRad rayons composés (`4,4,0,0`) |"
& $w "| Espacements | $totSpc (valeurs uniques) | $($spaceHits.Count) | $compositeSpc valeurs composées (`14,10`) |"
& $w "| **Total** | **$($totFont + $totRad + $totSpc)** | | **$($compositeRad + $compositeSpc)** |"
& $w ''
& $w 'Les **couleurs hexadécimales** (138 occurrences) ne sont pas traitées par ce'
& $w 'rapport : leur conversion dépend de la décision D2 (accent orange ou bleu)'
& $w 'et demande un jugement au cas par cas, pas une table.'

$outPath = Join-Path $root $OutFile
$dir = Split-Path $outPath -Parent
if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
$sb.ToString() | Set-Content -Path $outPath -Encoding UTF8

Write-Host "Rapport ecrit : $OutFile"
Write-Host ("  FontSize   : {0} occurrences, {1} valeurs distinctes" -f $totFont, $fontHits.Count)
Write-Host ("  Rayons     : {0} occurrences, {1} valeurs distinctes" -f $totRad, $radiusHits.Count)
Write-Host ("  Espacements: {0} occurrences, {1} valeurs distinctes" -f $totSpc, $spaceHits.Count)
Write-Host ("  Total      : {0}" -f ($totFont + $totRad + $totSpc))
