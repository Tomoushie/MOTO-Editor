# scripts/install-moto.ps1
# Installe MOTO Editor sur Windows (MSIX depuis GitHub Releases)

$ErrorActionPreference = "Stop"

Write-Host "🪟 Windows détecté — Installation de MOTO Editor..." -ForegroundColor Cyan

# URL de la dernière release (à adapter selon votre repo)
$releaseUrl = "https://api.github.com/repos/votre-org/moto-editor/releases/latest"

try {
    Write-Host "📡 Récupération des informations de la dernière release..." -ForegroundColor Yellow
    $release = Invoke-RestMethod -Uri $releaseUrl -Method Get

    $asset = $release.assets | Where-Object { $_.name -like "moto-editor-win-x64.msix" }

    if (-not $asset) {
        throw "Artefact Windows non trouvé dans la dernière release"
    }

    $downloadUrl = $asset.browser_download_url
    $tempPath = "$env:TEMP\moto-editor.msix"

    Write-Host "⬇️  Téléchargement depuis $downloadUrl..." -ForegroundColor Yellow
    Invoke-WebRequest -Uri $downloadUrl -OutFile $tempPath -UseBasicParsing

    Write-Host "📦 Installation du paquet MSIX..." -ForegroundColor Yellow
    Add-AppxPackage -Path $tempPath -ErrorAction Stop

    # Nettoyer le fichier temporaire
    Remove-Item $tempPath -Force -ErrorAction SilentlyContinue

    Write-Host "✅ MOTO Editor installé avec succès !" -ForegroundColor Green
    Write-Host "🚀 Lancez MOTO Editor depuis le menu Démarrer." -ForegroundColor Green

} catch {
    Write-Host "❌ Erreur lors de l'installation : $_" -ForegroundColor Red
    exit 1
}
