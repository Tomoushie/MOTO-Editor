#Requires -Version 5.1
<#
.SYNOPSIS
    Garde-fou d'un lot visuel MOTO Editor : vérifie qu'un changement d'apparence
    n'a rien cassé et n'a rien changé d'autre que de l'apparence.

.DESCRIPTION
    Le dépôt n'a AUCUN test visuel automatisé, 479 avertissements de build, et
    MainPage.xaml a un historique de plantage sur changement de largeur de
    colonne. Ce script est donc le seul filet sous chaque lot de la refonte
    visuelle (Docs/design/Langage-visuel-spec.md, §10).

    Il contrôle trois choses :

    1. COMPILATION — 0 erreur, et le nombre d'avertissements ne dépasse pas la
       ligne de base mesurée (479 le 22/09). Un lot visuel ne doit jamais faire
       monter ce nombre : une valeur de style mal écrite produit typiquement un
       avertissement XAML, pas une erreur.
    2. PÉRIMÈTRE — seuls des fichiers .xaml ont changé. Si un .cs bouge dans un
       lot visuel, c'est le signe qu'on a touché à autre chose.
    3. NON-RÉGRESSION FONCTIONNELLE — le diff ne touche NI une liaison
       (Binding, x:DataType, x:Reference), NI un gestionnaire d'événement
       (Clicked, Tapped, TextChanged...), NI un x:Name. Un lot visuel ne change
       que des VALEURS d'apparence.

    Utilisation typique avant de committer un lot :

        pwsh scripts/visual-lot-verify.ps1

    Code de sortie 0 = le lot est propre, 1 = au moins un contrôle a échoué.

.PARAMETER BaselineWarnings
    Nombre d'avertissements de référence. Défaut : 479 (mesuré le 22/09 en
    Debug ET Release sur net8.0-windows10.0.19041.0).

.PARAMETER SkipBuild
    Ne pas relancer la compilation (utile pour ne rejouer que les contrôles de
    périmètre et de diff).

.PARAMETER SelfTest
    Vérifie que le détecteur de motifs interdits distingue bien un changement
    d'apparence d'un changement fonctionnel. Ne touche à rien d'autre.

.EXAMPLE
    pwsh scripts/visual-lot-verify.ps1
#>
[CmdletBinding()]
param(
    [int]$BaselineWarnings = 479,
    [switch]$SkipBuild,
    [switch]$SelfTest
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$framework = 'net8.0-windows10.0.19041.0'

# ── Motifs qui n'ont RIEN à faire dans un lot purement visuel ────────────────
# Un lot visuel change des valeurs (couleur, taille, marge, rayon, état).
# S'il touche à l'un de ces motifs, il change le COMPORTEMENT : c'est un autre
# chantier, qui doit être testé autrement.
$motifsInterdits = @(
    @{ Nom = 'x:Name';              Motif = 'x:Name\s*=' }
    @{ Nom = 'Binding';             Motif = '\{Binding' }
    @{ Nom = 'x:DataType';          Motif = 'x:DataType' }
    @{ Nom = 'x:Reference';         Motif = 'x:Reference' }
    @{ Nom = 'gestionnaire Clicked';Motif = '\bClicked\s*=' }
    @{ Nom = 'gestionnaire Tapped'; Motif = '\bTapped\s*=' }
    @{ Nom = 'gestionnaire Pressed';Motif = '\bPressed\s*=' }
    @{ Nom = 'TextChanged';         Motif = 'TextChanged\s*=' }
    @{ Nom = 'CheckedChanged';      Motif = 'CheckedChanged\s*=' }
    @{ Nom = 'SelectionChanged';    Motif = 'SelectionChanged\s*=' }
    @{ Nom = 'Command';             Motif = '\bCommand\s*=' }
    @{ Nom = 'IsVisible piloté';    Motif = 'IsVisible\s*=\s*"\{' }
    @{ Nom = 'espace de noms x:';   Motif = 'xmlns:x\s*=' }
)

function Test-MotifsInterdits {
    param([string[]]$Lignes)
    $trouves = @()
    foreach ($l in $Lignes) {
        if ($l -notmatch '^[+-]' -or $l -match '^(\+\+\+|---)') { continue }
        foreach ($m in $motifsInterdits) {
            if ($l -match $m.Motif) {
                $trouves += [pscustomobject]@{ Motif = $m.Nom; Ligne = $l.Trim() }
            }
        }
    }
    return $trouves
}

# ── Auto-test : prouve que le détecteur fonctionne vraiment ──────────────────
if ($SelfTest) {
    Write-Host '=== Auto-test du detecteur de motifs ===' -ForegroundColor Cyan
    $doitPasser = @(
        '-                        <Label Text="Titre" FontSize="14" />',
        '+                        <Label Text="Titre" FontSize="16" FontAttributes="Bold" />',
        '+                        <Border StrokeShape="RoundRectangle 8" Padding="12" />',
        '+                        <Color x:Key="Accent">#007ACC</Color>'
    )
    $doitEchouer = @(
        '+                        <Button x:Name="Envoyer" Text="Go" />',
        '+                        <Label Text="{Binding Titre}" />',
        '-                        <Button Clicked="OnEnvoyer" />',
        '+                        <Entry TextChanged="OnTexte" />',
        '+                        <Button Command="{Binding EnvoyerCommand}" />'
    )
    $ok = $true
    # Propriete testee : CHAQUE ligne d'apparence doit passer, CHAQUE ligne
    # fonctionnelle doit etre detectee AU MOINS UNE fois. On ne compte pas les
    # detections totales : une meme ligne peut legitimement declencher deux
    # motifs (ex. Command="{Binding ...}" = Command ET Binding).
    $r1 = Test-MotifsInterdits -Lignes $doitPasser
    if ($r1.Count -ne 0) { $ok = $false; Write-Host "  ECHEC : un changement d'apparence a ete signale a tort ($($r1.Count))" -ForegroundColor Red }
    else { Write-Host "  OK : $($doitPasser.Count) changements d'apparence acceptes" -ForegroundColor Green }

    $manques = 0
    foreach ($ligne in $doitEchouer) {
        $r = Test-MotifsInterdits -Lignes @($ligne)
        if ($r.Count -eq 0) { $manques++; Write-Host "  ECHEC : non detecte -> $ligne" -ForegroundColor Red }
    }
    if ($manques -gt 0) { $ok = $false }
    else { Write-Host "  OK : $($doitEchouer.Count) changements fonctionnels detectes" -ForegroundColor Green }

    if (-not $ok) { exit 1 }
    Write-Host 'Auto-test concluant.' -ForegroundColor Green
    exit 0
}

$echecs = @()
$alertes = @()

# ── 1. Compilation ──────────────────────────────────────────────────────────
if (-not $SkipBuild) {
    Write-Host '=== 1. Compilation (Debug) ===' -ForegroundColor Cyan
    $sortie = & dotnet build 'Moto.Editor/Moto.Editor.csproj' -f $framework -c Debug 2>&1
    $texte = $sortie -join "`n"

    $mErr = [regex]::Match($texte, '(\d+)\s+Erreur\(s\)')
    $mWrn = [regex]::Match($texte, '(\d+)\s+Avertissement\(s\)')

    if (-not $mErr.Success) {
        $echecs += 'Compilation : impossible de lire le nombre d''erreurs (build interrompu ou sortie inattendue).'
        Write-Host '  ECHEC : sortie de build illisible' -ForegroundColor Red
    } else {
        $nbErr = [int]$mErr.Groups[1].Value
        $nbWrn = if ($mWrn.Success) { [int]$mWrn.Groups[1].Value } else { -1 }

        if ($nbErr -ne 0) {
            $echecs += "Compilation : $nbErr erreur(s) — attendu 0."
            Write-Host "  ECHEC : $nbErr erreur(s)" -ForegroundColor Red
        } else {
            Write-Host '  OK : 0 erreur' -ForegroundColor Green
        }

        if ($nbWrn -lt 0) {
            $alertes += 'Compilation : nombre d''avertissements illisible.'
        } elseif ($nbWrn -gt $BaselineWarnings) {
            $echecs += "Avertissements : $nbWrn contre une ligne de base de $BaselineWarnings (+$($nbWrn - $BaselineWarnings))."
            Write-Host "  ECHEC : $nbWrn avertissements (base $BaselineWarnings)" -ForegroundColor Red
        } else {
            Write-Host "  OK : $nbWrn avertissements (base $BaselineWarnings)" -ForegroundColor Green
        }
    }
} else {
    Write-Host '=== 1. Compilation : ignoree (-SkipBuild) ===' -ForegroundColor DarkGray
}

# ── 2. Perimetre du diff ────────────────────────────────────────────────────
Write-Host '=== 2. Perimetre des fichiers modifies ===' -ForegroundColor Cyan
$modifies = @(git diff --name-only; git diff --cached --name-only) |
    Where-Object { $_ } | Sort-Object -Unique

if ($modifies.Count -eq 0) {
    Write-Host '  (aucun fichier modifie — rien a verifier)' -ForegroundColor DarkGray
} else {
    $horsXaml = $modifies | Where-Object { $_ -notmatch '\.xaml$' }
    if ($horsXaml) {
        $echecs += "Fichiers non-XAML modifies : $($horsXaml -join ', ')"
        Write-Host "  ECHEC : $($horsXaml.Count) fichier(s) non-XAML" -ForegroundColor Red
        $horsXaml | ForEach-Object { Write-Host "    $_" -ForegroundColor Red }
    } else {
        Write-Host "  OK : $($modifies.Count) fichier(s), tous .xaml" -ForegroundColor Green
    }
}

# ── 3. Non-regression fonctionnelle ─────────────────────────────────────────
Write-Host '=== 3. Le diff ne touche pas au comportement ===' -ForegroundColor Cyan
if ($modifies.Count -eq 0) {
    Write-Host '  (aucun diff a analyser)' -ForegroundColor DarkGray
} else {
    $diff = git diff -- $modifies
    $diff += git diff --cached -- $modifies
    if ($diff) {
        $trouves = Test-MotifsInterdits -Lignes $diff
        if ($trouves.Count -gt 0) {
            $echecs += "Le diff touche $($trouves.Count) motif(s) de comportement."
            Write-Host "  ECHEC : $($trouves.Count) motif(s) fonctionnel(s) dans le diff" -ForegroundColor Red
            $trouves | Group-Object Motif | ForEach-Object {
                Write-Host "    [$($_.Name)] x$($_.Count)" -ForegroundColor Red
            }
        } else {
            Write-Host '  OK : aucune liaison, aucun gestionnaire, aucun x:Name touche' -ForegroundColor Green
        }
    } else {
        Write-Host '  (diff vide)' -ForegroundColor DarkGray
    }
}

# ── Verdict ─────────────────────────────────────────────────────────────────
Write-Host ''
if ($alertes.Count -gt 0) {
    Write-Host 'Alertes :' -ForegroundColor Yellow
    $alertes | ForEach-Object { Write-Host "  - $_" -ForegroundColor Yellow }
}
if ($echecs.Count -gt 0) {
    Write-Host 'LOT REFUSE :' -ForegroundColor Red
    $echecs | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
    exit 1
}
Write-Host 'LOT CONFORME : compilation propre, perimetre respecte, comportement intact.' -ForegroundColor Green
exit 0
