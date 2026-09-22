Conventions de nommage — cohérence totale pour Claude Code

# MOTO Editor — Naming Conventions

Ce document définit les conventions de nommage utilisées dans tout le projet.

---

# 1. Classes
Format :

PascalCase

Exemples :
- `CortexEngine`
- `SpeculativeDecoder`
- `AtomicUpdater`
- `PayloadExtractor`

---

# 2. Interfaces
Format :

IName

Exemples :
- `IPlatformShell`
- `IInlayHintProvider`
- `ISpecializedAgent`

---

# 3. Méthodes
Format :

PascalCase

Exemples :
- `ApplyUpdate()`
- `ExtractTo()`
- `VerifyPayload()`
- `DownloadAsync()`

---

# 4. Propriétés
Format :

PascalCase

Exemples :
- `Version`
- `PayloadSha256`
- `Files`
- `Signature`

---

# 5. Champs privés
Format :

_camelCase

Exemples :
- `_settings`
- `_log`
- `_pulseActive`
- `_hotkey`

---

# 6. Paramètres
Format :

camelCase

Exemples :
- `payloadPath`
- `installDir`
- `manifestPath`

---

# 7. Variables locales
Format :

camelCase

Exemples :
- `tempExtract`
- `backup`
- `target`
- `rel`

---

# 8. Enums
Format :

PascalCase

Exemples :
- `MotoHostOs`
- `TargetOs`

---

# 9. Dossiers
Format :

PascalCase

Exemples :
- `Moto.Editor`
- `Moto.Core`
- `Shared`
- `Docs`

---

# 10. Résumé
- ✔ Cohérence totale
- ✔ Pas de mélange de styles
- ✔ Lisibilité maximale
