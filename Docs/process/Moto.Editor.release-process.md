Processus de release complet — clair, structuré, reproductible

# MOTO Editor — Release Process

Ce document décrit le processus complet de release pour MOTO Editor :
- Build multi-OS
- Payload ZIP
- Manifeste versionné
- Signature Ed25519
- BuildKeys.cs
- Publication GitHub Release
- MSIX (entreprise)

---

# 1. Préparation

## 1.1. Mettre à jour la version
Modifier dans :
- Moto.Editor.csproj → ApplicationDisplayVersion
- build-update-manifest.ps1 → -Version "X.Y.Z"

## 1.2. Nettoyer les artefacts
pwsh scripts/clean.ps1

---

# 2. Build multi-OS

## 2.1. Windows
dotnet publish Moto.Editor -c Release -r win-x64 --self-contained true -o dist/payload

## 2.2. macOS
dotnet publish Moto.Editor -c Release -f net8.0-maccatalyst -r osx-x64 --self-contained true -o dist/payload-mac

## 2.3. Linux (Core)
dotnet publish Moto.Core -c Release -r linux-x64 --self-contained true -o dist/payload-linux

---

# 3. Build du payload ZIP

Si absent :
Compress-Archive -Path dist/payload/* -DestinationPath dist/payload.zip

---

# 4. Build du manifeste + signature
pwsh scripts/build-update-manifest.ps1 -Version "X.Y.Z"

Produit :
- dist/payload.zip
- dist/payload.json (signé)
- Shared/BuildKeys.cs (clé publique embarquée)

---

# 5. Build MSIX (option entreprise)
pwsh scripts/build-msix.ps1 -Config Release -PfxPath moto-signing.pfx -PfxPassword moto

Produit :
- dist/msix/MotoEditor.msix

---

# 6. Publication GitHub Release

Publier :
- payload.zip
- payload.json
- MotoEditor-win-x64.zip
- MotoEditor-osx-x64.zip
- MotoEditor-linux-x64.zip
- MotoEditor.msix (optionnel)

---

# 7. Résumé

- ✔ Build multi-OS  
- ✔ Payload ZIP  
- ✔ Manifeste versionné  
- ✔ Signature Ed25519  
- ✔ BuildKeys.cs  
- ✔ MSIX entreprise  
- ✔ Release GitHub
