Flux complet installateur + MSIX

# MOTO Editor — Flux d'installation

Ce document décrit les deux modes d’installation :
- Installateur fait-maison (95 % des utilisateurs)
- MSIX (5 % entreprise)

---

# 1. Installateur fait-maison (Moto.Installer)

## Étape 1 — Détection OS
OsDetector.Detect()
→ Windows / macOS / Linux

## Étape 2 — Répertoire per-user
- Windows → %LocalAppData%\Programs\MotoEditor
- macOS → ~/Applications/Moto Editor
- Linux → ~/.local/share/moto-editor

## Étape 3 — Vérification que l’éditeur n’est pas en cours

ProcessGuard.IsRunning()

## Étape 4 — Extraction du payload

PayloadExtractor.ExtractTo(installDir)


## Étape 5 — Raccourcis
- Windows → .lnk via WScript.Shell
- Linux → .desktop + symlink
- macOS → chmod +x

## Étape 6 — Désinstallation per-user
- Windows → HKCU\Software\...\Uninstall\MotoEditor
- Script uninstall.cmd

---

# 2. Mise à jour via installateur

## Étape 1 — Vérification cryptographique

PayloadVerifier.VerifyPayload(payload.zip, payload.json)

## Étape 2 — Extraction temporaire

PayloadExtractor.ExtractTo(tempExtract)

## Étape 3 — Swap atomique

AtomicUpdater.Apply(installDir, tempExtract)


## Étape 4 — Rollback automatique
Si erreur → restore backup.

## Étape 5 — Relance de l’éditeur

---

# 3. Installation MSIX (entreprise)

## Étape 1 — Build MSIX

scripts/build-msix.ps1
→ dist/msix/MotoEditor.msix


## Étape 2 — Signature
- Certificat PFX
- PublisherDisplayName
- PackageDescription

## Étape 3 — Déploiement
- Sideloading → Add-AppxPackage
- Intune → App Win32/LOB
- GPO → Provisioned package
- Microsoft Store → Soumission MSIX signé

---

# 4. Résumé

Installateur fait-maison :
- ✔ per-user  
- ✔ sans UAC  
- ✔ sans dépendance  
- ✔ multi-OS  
- ✔ rollback  
- ✔ atomique  
- ✔ silencieux  
- ✔ delta  
- ✔ signature Ed25519  

MSIX :
- ✔ entreprise  
- ✔ Intune  
- ✔ Store  
- ✔ signature
