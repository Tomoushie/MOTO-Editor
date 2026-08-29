# scripts/generate-icons.ps1
# Génère les icônes MOTO Editor (PNG multi-tailles + ICO Windows + splash).
# Usage : pwsh scripts/generate-icons.ps1 -Source assets/moto_logo_source.png
param(
    [string]$Source = "assets/moto_logo_source.png",
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $Source)) { throw "PNG source introuvable : $Source" }

# System.Drawing (Windows uniquement) — fallback ImageMagick sinon
$useDrawing = $IsWindows -or ($PSVersionTable.PSEdition -eq "Desktop")
if (-not $useDrawing) {
    Write-Host "⚠️ Hors Windows : utilisez ImageMagick :" -ForegroundColor Yellow
    Write-Host "   magick $Source -resize 512x512 Resources/AppIcon/appicon.png"
    Write-Host "   magick $Source -define icon:auto-resize=256,128,64,48,32,16 Platforms/Windows/appicon.ico"
    exit 0
}

Add-Type -AssemblyName System.Drawing

# ── Répertoires cibles ──
$dirs = @(
    "$Root/Resources/AppIcon",
    "$Root/Resources/Splash",
    "$Root/Resources/Images",
    "$Root/Platforms/Windows"
)
foreach ($d in $dirs) { New-Item -ItemType Directory -Force -Path $d | Out-Null }

$src = [System.Drawing.Image]::FromFile((Resolve-Path $Source).Path)

function Resize-Png([System.Drawing.Image]$img, [int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode  = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.SmoothingMode      = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.DrawImage($img, 0, 0, $size, $size)
    $g.Dispose()
    return $bmp
}

function Save-Png([System.Drawing.Bitmap]$bmp, [string]$path) {
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Host "✅ $path"
}

# ── 1. PNG applicatifs ──
Save-Png (Resize-Png $src 512) "$Root/Resources/AppIcon/appicon.png"
Save-Png (Resize-Png $src 512) "$Root/Resources/Images/moto_logo.png"
Save-Png (Resize-Png $src 128) "$Root/Resources/Images/moto_logo_128.png"
Save-Png (Resize-Png $src 32)  "$Root/Resources/Images/moto_logo_32.png"

# ── 2. Splash : logo centré sur fond #0B1526 ──
$splash = New-Object System.Drawing.Bitmap(512, 512)
$g = [System.Drawing.Graphics]::FromImage($splash)
$g.Clear([System.Drawing.Color]::FromArgb(255, 11, 21, 38))   # #0B1526
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g.DrawImage($src, 128, 128, 256, 256)                        # logo centré (50%)
$g.Dispose()
Save-Png $splash "$Root/Resources/Splash/splash.png"

# ── 3. ICO multi-tailles (entrées PNG, format Vista+) ──
$sizes = @(256, 128, 64, 48, 32, 16)
$pngEntries = @()
foreach ($s in $sizes) {
    $bmp = Resize-Png $src $s
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    $pngEntries += ,@($s, $ms.ToArray())
    $ms.Dispose()
}

$ico = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter($ico)

# ICONDIR
$bw.Write([uint16]0)                       # réservé
$bw.Write([uint16]1)                       # type = icône
$bw.Write([uint16]$pngEntries.Count)       # nombre d'images

# ICONDIRENTRY (calcul des offsets)
$headerSize = 6 + 16 * $pngEntries.Count
$offset = $headerSize
$entries = @()
foreach ($e in $pngEntries) {
    $s = $e[0]; $data = $e[1]
    $dim = if ($s -ge 256) { 0 } else { $s }   # 0 = 256
    $entries += ,@($dim, $data.Length, $offset)
    $offset += $data.Length
}
foreach ($e in $entries) {
    $bw.Write([byte]$e[0])        # largeur
    $bw.Write([byte]$e[0])        # hauteur
    $bw.Write([byte]0)            # palette
    $bw.Write([byte]0)            # réservé
    $bw.Write([uint16]1)          # plans
    $bw.Write([uint16]32)         # bpp
    $bw.Write([uint32]$e[1])      # taille données
    $bw.Write([uint32]$e[2])      # offset
}
foreach ($e in $pngEntries) { $bw.Write($e[1]) }

$bw.Flush()
[System.IO.File]::WriteAllBytes("$Root/Platforms/Windows/appicon.ico", $ico.ToArray())
$bw.Dispose(); $ico.Dispose()
$src.Dispose()

Write-Host "✅ $Root/Platforms/Windows/appicon.ico (multi-tailles)"
Write-Host "🎉 Icônes générées avec succès."
