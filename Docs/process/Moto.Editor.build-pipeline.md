Pipeline complet de build — clair, hiérarchique, compréhensible

# MOTO Editor — Build Pipeline

Ce document décrit le pipeline complet de build pour MOTO Editor, incluant :
- Build de l’éditeur
- Build de l’installateur
- Build du SignTool
- Build du payload
- Build du manifeste
- Signature Ed25519
- Génération de BuildKeys.cs
- CI multi-OS

---

# 1. Build de l’éditeur (Moto.Editor)

## Commande standard

dotnet publish Moto.Editor/Moto.Editor.csproj -c Release -r win-x64 --self-contained true -o dist/payload

## Résultat

dist/payload/
dist/payload.zip


## Multi-OS
- Windows → win-x64
- macOS → maccatalyst
- Linux → Core uniquement (MAUI non supporté)

---

# 2. Build du payload

Si `dist/payload.zip` n’existe pas :

Compress-Archive -Path dist/payload/* -DestinationPath dist/payload.zip


---

# 3. Build du manifeste

Script :

scripts/build-update-manifest.ps1

Produit :

dist/payload.json


Contient :
- Version
- PayloadSha256
- Files[] (Path, Sha256, Size)
- Signature (vide avant signature)

---

# 4. Signature Ed25519

SignTool :

dotnet run --project Moto.SignTool -- --sign-manifest dist/payload.json --key keys/update.priv


Résultat :
- `payload.json` signé

---

# 5. Génération de BuildKeys.cs

dotnet run --project Moto.SignTool -- --emit-buildkeys --pub keys/update.pub --out Shared/BuildKeys.cs


BuildKeys.cs contient :
- clé publique Ed25519 embarquée dans l’éditeur

---

# 6. CI multi-OS

## Windows
- Build MAUI Windows
- Publish win-x64
- Upload artefact

## macOS
- Build maccatalyst
- Publish osx-x64
- Upload artefact

## Linux
- Build Moto.Core
- Publish linux-x64 Core
- Upload artefact

## Release
- Zip artefacts
- Publier sur GitHub Release

---

# 7. Résumé

- ✔ Build multi-OS  
- ✔ Payload ZIP  
- ✔ Manifeste versionné  
- ✔ Signature Ed25519  
- ✔ BuildKeys.cs  
- ✔ CI multi-OS  
- ✔ Artefacts propres
