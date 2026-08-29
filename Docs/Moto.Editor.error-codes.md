Spécification des codes d’erreur — clair, structuré, exploitable

# MOTO Editor — Error Codes Specification

Ce document définit les codes d’erreur utilisés dans :
- Moto.Editor
- Moto.Core
- Snake2000.Engine
- Moto.Installer
- Shared/
- Moto.SignTool

Les codes sont organisés par module et par catégorie.

---

# 1. Format général

Chaque erreur suit le format :

<Module>.<Category>.<Code>

Exemples :
- `Installer.Update.SignatureInvalid`
- `Core.Cortex.MemoryOverflow`
- `Editor.UI.PanelLoadFailed`

---

# 2. Modules

- Editor
- Core
- Engine
- Installer
- SignTool
- Shared

---

# 3. Catégories

- IO
- Network
- Crypto
- Update
- Pipeline
- UI
- Settings
- Model
- Agent

---

# 4. Codes d’erreur par module

## 4.1. Installer

### Update
- `Installer.Update.SignatureInvalid`  
  → Signature Ed25519 invalide

- `Installer.Update.HashMismatch`  
  → Hash SHA256 du payload incorrect

- `Installer.Update.ExtractionFailed`  
  → Extraction ZIP échouée

- `Installer.Update.AtomicSwapFailed`  
  → Swap atomique échoué

- `Installer.Update.RollbackApplied`  
  → Rollback automatique appliqué

### IO
- `Installer.IO.AccessDenied`
- `Installer.IO.PathInvalid`
- `Installer.IO.FileLocked`

---

## 4.2. Shared

### Crypto
- `Shared.Crypto.InvalidKey`
- `Shared.Crypto.SignatureInvalid`
- `Shared.Crypto.HashMismatch`

### Network
- `Shared.Network.DownloadFailed`
- `Shared.Network.ResumeFailed`
- `Shared.Network.MirrorFallback`

---

## 4.3. Core

### Model
- `Core.Model.LoadFailed`
- `Core.Model.MemoryMappedError`
- `Core.Model.LayerMissing`

### AI
- `Core.AI.SpeculativeFailure`
- `Core.AI.CortexMemoryOverflow`

---

## 4.4. Engine (XENO)

### Pipeline
- `Engine.Pipeline.StepFailed`
- `Engine.Pipeline.AgentCrashed`
- `Engine.Pipeline.ValidationFailed`

---

## 4.5. Editor

### UI
- `Editor.UI.PanelLoadFailed`
- `Editor.UI.ThemeError`
- `Editor.UI.WindowInitFailed`

### Settings
- `Editor.Settings.LoadFailed`
- `Editor.Settings.SaveFailed`

---

# 5. Résumé

- ✔ Codes d’erreur structurés  
- ✔ Modules séparés  
- ✔ Catégories claires  
- ✔ Utilisable par Claude Code
