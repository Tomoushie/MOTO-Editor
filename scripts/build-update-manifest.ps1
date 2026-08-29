# scripts/build-update-manifest.ps1
# Génère le manifeste de mise à jour (hashs SHA256 par fichier + payload),
# puis orchestre Moto.SignTool : gen-keys (si absent) → sign → emit-buildkeys.
#
# Usage :
#   pwsh scripts/build-update-manifest.ps1 -Version "1.1.0"
#   pwsh scripts/build-update-manifest.ps1 -PayloadDir dist/payload -PayloadZip dist/payload.zip -KeysDir keys

param(
    [string]$PayloadDir = "dist/payload",
    [string]$PayloadZip = "dist/payload.zip",
    [string]$Version    = "1.0.0",
    [string]$KeysDir    = "keys"
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..")

# ── Chemins résolus ──
$PayloadDir = Join-Path $root $PayloadDir
$PayloadZip = Join-Path $root $PayloadZip
$KeysDir    = Join-Path $root $KeysDir
$Manifest   = [System.IO.Path]::ChangeExtension($PayloadZip, ".json")
$SignTool   = Join-Path $root "Moto.SignTool/Moto.SignTool.csproj"
$BuildKeys  = Join-Path $root "Shared/BuildKeys.cs"

Write-Host "🔐 Build du manifeste de mise à jour — v$Version" -ForegroundColor Cyan

# ══ 1. Payload : s'assure que payload.zip existe ══
if (-not (Test-Path $PayloadZip)) {
    if (Test-Path $PayloadDir) {
        Write-Host "📦 payload.zip absent → compression depuis $PayloadDir" -ForegroundColor Yellow
        Compress-Archive -Path "$PayloadDir/*" -DestinationPath $PayloadZip -Force
    } else {
        throw "Payload introuvable : ni $PayloadZip ni $PayloadDir. Lancez d'abord build-installer.ps1."
    }
}

# ══ 2. Clés : gen-keys SI ABSENT ══
if (-not (Test-Path (Join-Path $KeysDir "update.priv"))) {
    Write-Host "🔑 Clés absentes → génération (Moto.SignTool --gen-keys)" -ForegroundColor Yellow
    dotnet run --project $SignTool -- --gen-keys --out $KeysDir
    if ($LASTEXITCODE -ne 0) { throw "Échec de la génération des clés." }
}

# ══ 3. Manifeste : hash SHA256 par fichier + hash du payload ══
Write-Host "🧮 Calcul des hashs SHA256…" -ForegroundColor Yellow
$files = @()
Get-ChildItem -Path $PayloadDir -Recurse -File | ForEach-Object {
    $rel = $_.FullName.Substring((Resolve-Path $PayloadDir).Path.Length + 1).Replace("\", "/")
    $files += [ordered]@{
        Path   = $rel
        Sha256 = (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLower()
        Size   = $_.Length
    }
}

# Noms de propriétés EXACTS (System.Text.Json est sensible à la casse)
$manifest = [ordered]@{
    Version       = $Version
    PayloadSha256 = (Get-FileHash $PayloadZip -Algorithm SHA256).Hash.ToLower()
    Files         = $files
    Signature     = ""
}

$manifest | ConvertTo-Json -Depth 6 | Set-Content -Path $Manifest -Encoding UTF8
Write-Host "✅ Manifeste généré : $Manifest ($($files.Count) fichiers)" -ForegroundColor Green

# ══ 4. Signature Ed25519 du manifeste ══
Write-Host "✍️  Signature du manifeste (Moto.SignTool --sign-manifest)…" -ForegroundColor Yellow
dotnet run --project $SignTool -- --sign-manifest $Manifest --key (Join-Path $KeysDir "update.priv")
if ($LASTEXITCODE -ne 0) { throw "Échec de la signature du manifeste." }

# ══ 5. Régénération de Shared/BuildKeys.cs (clé publique embarquée) ══
Write-Host "🧬 Régénération de BuildKeys.cs (Moto.SignTool --emit-buildkeys)…" -ForegroundColor Yellow
dotnet run --project $SignTool -- --emit-buildkeys --pub (Join-Path $KeysDir "update.pub") --out $BuildKeys
if ($LASTEXITCODE -ne 0) { throw "Échec de la régénération de BuildKeys.cs." }

# ══ 6. Sécurité : la clé privée ne doit JAMAIS être committée ══
$gitignore = Join-Path $root ".gitignore"
if (Test-Path $gitignore) {
    $content = Get-Content $gitignore -Raw
    if ($content -notmatch [regex]::Escape("keys/*.priv")) {
        Add-Content -Path $gitignore -Value "`n# Clés de signature MOTO (NE JAMAIS committer)`nkeys/*.priv"
        Write-Host "🛡 .gitignore mis à jour (keys/*.priv exclu)." -ForegroundColor Green
    }
}

Write-Host "🎉 Chaîne de confiance complète : manifeste signé + BuildKeys.cs à jour." -ForegroundColor Green
Write-Host "   → payload   : $PayloadZip"
Write-Host "   → manifeste : $Manifest"
Write-Host "   ⚠ Publiez payload.zip + payload.json dans la release GitHub."
