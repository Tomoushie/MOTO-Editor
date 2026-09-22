Gestion des échecs de mise à jour — atomique, rollback, résilient

```markdown
# MOTO Editor — Update Failure Handling

Ce document décrit la gestion des échecs de mise à jour :
- Vérification cryptographique
- Extraction sécurisée
- Swap atomique
- Rollback automatique
- Logs
- Codes d’erreur

---

# 1. Types d’échecs

## 1.1. Crypto
- Signature Ed25519 invalide
- Hash SHA256 incorrect
- Clé publique absente

## 1.2. Extraction
- ZIP corrompu
- Zip Slip détecté
- Fichier manquant

## 1.3. Atomic Swap
- Move échoué
- Permissions insuffisantes
- Fichiers verrouillés

## 1.4. Rollback
- Backup introuvable
- Restore échoué

## 1.5. Network
- Téléchargement interrompu
- Resume impossible
- Mirrors indisponibles

---

# 2. Pipeline de gestion d’échec

## Étape 1 — Vérification cryptographique
Si signature invalide :

Installer.Update.SignatureInvalid
→ update refusée
→ rollback non nécessaire


## Étape 2 — Extraction temporaire
Si extraction échoue :

## Étape 3 — Swap atomique
Si swap échoue :

Installer.Update.AtomicSwapFailed
→ rollback automatique
→ Installer.Update.RollbackApplied

## Étape 4 — Rollback
Si rollback échoue :

Installer.Update.RollbackFailed
→ état critique


---

# 3. Logs associés

Exemples :

```json
{
  "module": "Installer",
  "category": "Update",
  "level": "Error",
  "code": "Installer.Update.SignatureInvalid",
  "message": "Signature Ed25519 invalide"
}

{
  "module": "Installer",
  "category": "Update",
  "level": "Warn",
  "code": "Installer.Update.RollbackApplied",
  "message": "Rollback automatique appliqué"
}

4. Résumé
✔ Crypto → refus
✔ Extraction → refus
✔ Swap → rollback
✔ Rollback → sécurité
✔ Logs → JSON
✔ Codes d’erreur → standardisés
