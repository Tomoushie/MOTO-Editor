# MOTO Editor — Architecture complète

MOTO Editor est un IDE IA multiplateforme, structuré en 6 modules principaux :
Moto.Editor/           → UI, IDE, Panels, Overlays, Command Palette
Moto.Core/             → IA embarquée, moteurs, settings, profils, suggestions
Snake2000.Engine/      → XENO Pipeline, agents, analyse de projet, génération
Shared/                → PayloadExtractor, Ed25519, UpdateManifest, AtomicUpdater
Moto.Installer/        → Installateur fait-maison + mise à jour atomique
Moto.SignTool/         → Génération de clés + signature Ed25519

---

## 1. Moto.Editor — UI + IDE
- MAUI + WinUI 3
- Panels : Home, Debug, Analytics, Plugins, AI Settings
- Overlays : Confirmation, Migration, Subscription, About
- Command Palette (ContextualActionsEngine)
- Hotkeys, System Menu, WindowsShellAdapter
- Intégration XENO (via services Core)

---

## 2. Moto.Core — IA embarquée
- CortexEngine (mémoire IA)
- NeuralMode (embeddings)
- AIWorkspace (contexte projet)
- ContextEngine (suggestions intelligentes)
- SpeculativeDecoder (inférence rapide)
- LayeredModelLoader (modèles multi-couches)
- SmartModelManager (gestion des modèles)
- AdaptiveResourceGovernor (gestion CPU/RAM)
- InferenceWatchdog (surveillance)
- SettingsEngine + Profiles

---

## 3. Snake2000.Engine — XENO Pipeline
Pipeline structuré :
Scanner → Analyzer → Synthesizer → Connector → Validator


Agents :
- AgentScanner
- AgentAnalyzer
- AgentSynthesizer
- AgentConnector
- AgentValidator

Rôle :
- Comprendre le projet
- Générer du code cohérent
- Connecter les modules
- Vérifier la cohérence

---

## 4. Shared — Logique commune
- PayloadExtractor (extraction ZIP sécurisée)
- UpdateManifest (hashs + signature)
- Sha256Helper
- Ed25519 (sign + verify)
- PayloadVerifier
- ResumableDownloader (resume + mirrors)
- AtomicUpdater (swap + rollback)

---

## 5. Moto.Installer — Installateur + Updater
- Installation per-user
- Extraction sécurisée
- Mise à jour atomique
- Rollback automatique
- Vérification SHA256 + Ed25519
- Raccourcis Windows/Linux/macOS
- MSIX pour entreprise

---

## 6. Moto.SignTool — Signature Ed25519
- Génération de clés (update.priv / update.pub)
- Signature du manifeste
- Emission de BuildKeys.cs (clé publique embarquée)

---

## Diagramme global

┌──────────────────────┐
│     Moto.Editor      │
│  (UI + IDE + Panels) │
└──────────┬───────────┘
│
▼
┌──────────────────────┐
│      Moto.Core       │
│  (IA embarquée)      │
└──────────┬───────────┘
│
▼
┌──────────────────────┐
│  Snake2000.Engine    │
│   (XENO Pipeline)    │
└──────────┬───────────┘
│
▼
┌──────────────────────┐
│       Shared         │
│ (Update + Crypto)    │
└──────────┬───────────┘
│
▼
┌───────────────────────────────┐
│ Moto.Installer + Moto.SignTool│
│ (Install + Update + Signature)│
└───────────────────────────────┘


---

# 📁 **Docs/Moto.Editor.update-pipeline.md**  
### *Pipeline complet de mise à jour — étape par étape*

```markdown
# MOTO Editor — Pipeline de mise à jour

Ce document décrit le pipeline complet de mise à jour, du build à l’application.

---

# 1. Build de la mise à jour

## Étape 1 — Build du payload

dotnet publish Moto.Editor -c Release -r win-x64 --self-contained
→ dist/payload/
→ dist/payload.zip


## Étape 2 — Génération du manifeste

scripts/build-update-manifest.ps1
→ dist/payload.json
→ hash SHA256 par fichier
→ hash SHA256 du payload.zip


## Étape 3 — Signature Ed25519

Moto.SignTool --sign-manifest payload.json --key update.priv
→ payload.json signé


## Étape 4 — Emission de BuildKeys.cs

Moto.SignTool --emit-buildkeys --pub update.pub
→ Shared/BuildKeys.cs


## Étape 5 — Publication GitHub Release
Publier :
- payload.zip
- payload.json
- manifest signé

---

# 2. Vérification côté éditeur

## Étape 1 — AutoUpdateService.CheckAsync()
- Télécharge la dernière release GitHub
- Compare la version locale
- Récupère l’URL du setup

## Étape 2 — Téléchargement résumable
ResumableDownloader.DownloadAsync(urls, staging/payload.zip)


## Étape 3 — Mise à jour silencieuse (optionnelle)
- Téléchargement en arrière-plan
- Application au prochain démarrage

---

# 3. Application de la mise à jour

## Étape 1 — Installateur en mode update

MotoEditor-Setup.exe --update --payload payload.zip --target installDir

## Étape 2 — Vérification cryptographique

PayloadVerifier.VerifyPayload(payload.zip, payload.json, BuildKeys.pub)

## Étape 3 — Extraction temporaire

PayloadExtractor.ExtractTo(tempExtract)

## Étape 4 — Swap atomique

AtomicUpdater.Apply(installDir, tempExtract)


## Étape 5 — Rollback automatique
Si erreur → restore backup.

## Étape 6 — Relance de l’éditeur

--relaunch Moto.Editor.exe


---

# 4. Résumé

- ✔ SHA256  
- ✔ Ed25519  
- ✔ Resume  
- ✔ Mirrors  
- ✔ Delta fichier  
- ✔ Atomique  
- ✔ Rollback  
- ✔ Multi-OS  
- ✔ Silencieux  
- ✔ Signé  
- ✔ Vérifié
