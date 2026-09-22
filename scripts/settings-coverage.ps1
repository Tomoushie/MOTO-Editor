#Requires -Version 5.1
<#
.SYNOPSIS
    Mesure la part RÉELLEMENT opérante du catalogue de réglages MOTO Editor.

.DESCRIPTION
    Le catalogue déclare plusieurs centaines de réglages, mais un réglage ne
    fait quelque chose que si du code LIT sa clé. Ce script compare les deux
    ensembles et produit un rapport par catégorie.

    Pourquoi c'est le verrou du palier « élevé → vendable » : la barre fixée
    par Tom exige que « tout ce qui est annoncé fonctionne ». Un réglage
    affiché dans la fenêtre Réglages mais jamais lu est une promesse non
    tenue — et il y en a des centaines.

    Périmètre : uniquement le code RÉELLEMENT COMPILÉ. Les fichiers listés
    dans <Compile Remove>/<MauiXaml Remove> des deux .csproj sont exclus —
    sinon on compte comme « opérant » un réglage lu par du code mort
    (c'est le cas de tous les ai.embedded.*, lus par le cluster ONNX qui
    n'est pas compilé). Même discipline que l'audit visuel.

.PARAMETER OutFile
    Rapport Markdown à produire.

.EXAMPLE
    pwsh scripts/settings-coverage.ps1
#>
[CmdletBinding()]
param(
    [string]$OutFile = 'Docs/design/Couverture-reglages.md'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

# ── Périmètre : code réellement compilé ─────────────────────────────────────
$exclus = @()
foreach ($p in 'Moto.Core/Moto.Core.csproj', 'Moto.Editor/Moto.Editor.csproj') {
    $t = Get-Content $p -Raw
    foreach ($m in [regex]::Matches($t, '(?:Compile|MauiXaml) Remove="([^"]+)"')) {
        $exclus += (Split-Path $m.Groups[1].Value -Leaf)
    }
}
$csCompiles = (Get-ChildItem -Recurse -Include *.cs -File | Where-Object {
    $_.FullName -notmatch '\\bin\\|\\obj\\|\\Docs\\' -and $exclus -notcontains $_.Name
}).FullName

# ── Clés DÉCLARÉES au catalogue, avec leur catégorie ────────────────────────
# Forme d'une déclaration : T("clé", "Catégorie", "Section", "Titre", ...)
$declare = @{}
foreach ($f in (Get-ChildItem 'Moto.Core/Settings' -Filter 'SettingsCatalog*.cs' | ForEach-Object { $_.FullName })) {
    $t = Get-Content $f -Raw
    foreach ($m in [regex]::Matches($t, '(?m)^\s*[TESI]\(\s*"([a-z0-9_\.]+)"\s*,\s*"([^"]+)"')) {
        $cle = $m.Groups[1].Value
        if (-not $declare.ContainsKey($cle)) { $declare[$cle] = $m.Groups[2].Value }
    }
}

# ── Clés LUES par du code compilé ───────────────────────────────────────────
$lues = @{}
foreach ($m in (Select-String -Path $csCompiles -Pattern 'Get(?:Bool|Int|String|Double)?\(\s*"([a-z0-9_\.]+)"' -AllMatches -EA SilentlyContinue)) {
    foreach ($x in $m.Matches) {
        $k = $x.Groups[1].Value
        if (-not $lues.ContainsKey($k)) { $lues[$k] = @() }
        $lues[$k] += $m.Path.Replace($root + '\', '')
    }
}

$actifs = $declare.Keys | Where-Object { $lues.ContainsKey($_) } | Sort-Object
$inertes = $declare.Keys | Where-Object { -not $lues.ContainsKey($_) } | Sort-Object
$horsCatalogue = $lues.Keys | Where-Object { -not $declare.ContainsKey($_) } | Sort-Object

# ── Rapport ─────────────────────────────────────────────────────────────────
$sb = New-Object System.Text.StringBuilder
$w = { param($t) [void]$sb.AppendLine($t) }
$pct = if ($declare.Count -gt 0) { [math]::Round(100.0 * $actifs.Count / $declare.Count, 1) } else { 0 }

& $w '# Couverture du catalogue de réglages'
& $w ''
& $w '> **Généré automatiquement — analyse seule, aucun fichier modifié.**'
& $w "> Source : ``scripts/settings-coverage.ps1`` · Périmètre : $($csCompiles.Count) fichiers .cs réellement compilés"
& $w ''
& $w '## Chiffres'
& $w ''
& $w '| Mesure | Valeur |'
& $w '|---|---|'
& $w "| Réglages DÉCLARÉS au catalogue | $($declare.Count) |"
& $w "| Clés lues par du code compilé | $($lues.Count) |"
& $w "| **Déclarés ET lus → réellement opérants** | **$($actifs.Count)** |"
& $w "| Déclarés mais INERTES | $($inertes.Count) |"
& $w "| **Part réellement opérante** | **$pct %** |"
& $w ''
& $w '## Réglages réellement opérants'
& $w ''
& $w '| Clé | Catégorie | Lue par |'
& $w '|---|---|---|'
foreach ($k in $actifs) {
    $src = ($lues[$k] | Sort-Object -Unique) -join ', '
    & $w "| ``$k`` | $($declare[$k]) | $src |"
}
& $w ''
& $w '## Réglages INERTES, par catégorie'
& $w ''
& $w 'Ce sont les réglages affichés dans la fenêtre Réglages dont AUCUN code'
& $w 'compilé ne lit la clé : ils sont persistés, mais sans effet. C''est la'
& $w 'matière première du palier « tout ce qui est annoncé fonctionne ».'
& $w ''
$parCat = @{}
foreach ($k in $inertes) {
    $c = $declare[$k]
    if (-not $parCat.ContainsKey($c)) { $parCat[$c] = @() }
    $parCat[$c] += $k
}
foreach ($c in ($parCat.Keys | Sort-Object { -$parCat[$_].Count })) {
    & $w "### $c — $($parCat[$c].Count) réglage(s) inerte(s)"
    & $w ''
    foreach ($k in ($parCat[$c] | Sort-Object)) { & $w "- ``$k``" }
    & $w ''
}
& $w '## Clés hors catalogue'
& $w ''
& $w 'Utilisées comme **état applicatif** (géométrie de fenêtre, dernier dossier,'
& $w 'jeton GitHub...), pas comme réglages utilisateur. Elles partagent le même'
& $w 'stockage que le catalogue — d''où la confusion possible.'
& $w ''
foreach ($k in $horsCatalogue) { & $w "- ``$k``" }

$outPath = Join-Path $root $OutFile
$dir = Split-Path $outPath -Parent
if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
$sb.ToString() | Set-Content -Path $outPath -Encoding UTF8

Write-Host "Rapport ecrit : $OutFile"
Write-Host ("  Declares        : {0}" -f $declare.Count)
Write-Host ("  Reellement lus  : {0}" -f $actifs.Count)
Write-Host ("  Inertes         : {0}" -f $inertes.Count)
Write-Host ("  Part operante   : {0} %" -f $pct)
