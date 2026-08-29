Spécification du logging — complet, structuré, multi‑module

# MOTO Editor — Logging Specification

Ce document définit le système de logging utilisé dans :
- Moto.Editor
- Moto.Core
- Snake2000.Engine
- Moto.Installer
- Shared/
- Moto.SignTool

---

# 1. Objectifs

- Traçabilité
- Débogage
- Analyse
- Sécurité
- Observabilité

---

# 2. Format des logs

Format standard JSON :

```json
{
  "timestamp": "2026-08-29T14:32:10.123Z",
  "module": "Installer",
  "category": "Update",
  "level": "Error",
  "code": "Installer.Update.SignatureInvalid",
  "message": "Signature Ed25519 invalide",
  "details": { "payload": "payload.zip" }
}

# 3. Trace
- Debug
- Info
- Warn
- Error
- Critical
- Niveaux

---

# 4. Modules 
- Editor
- Core
- Engine
- Installer
- SignTool
- Shared

---

# 5. Catégories
- Update
- Crypto
- Network
- Pipeline
- Model
- UI
- Settings
- IO
- Agent

# 6. Règles de logging
- 6.1. Jamais de clé privée dans les logs
  → sécurité absolue

- 6.2. Jamais de contenu utilisateur sensible
  → respect de la vie privée

- 6.3. Logs structurés uniquement
  → JSON obligatoire

- 6.4. Pas de logs dans les boucles critiques
  → performance

- 6.5. Logs IA minimalistes
  → éviter la pollution

# 7. Logging par module
## Installer
- update start/end
- signature verification
- hash verification
- atomic swap
- rollback

Shared
- download resume
- mirror fallback
- crypto errors

Core
- model loading
- speculative decoding
- layered model activation

Engine
- pipeline steps
- agent execution
- validation

Editor
- UI load
- settings load/save
- window init

8. Résumé
✔ JSON structuré
✔ Modules séparés
✔ Catégories claires
✔ Sécurité respectée
