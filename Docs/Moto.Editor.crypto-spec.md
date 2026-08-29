Spécification cryptographique — Ed25519, SHA256, BuildKeys

```markdown
# MOTO Editor — Crypto Specification

Ce document décrit le modèle cryptographique utilisé par :
- Moto.Editor
- Moto.Installer
- Moto.SignTool
- Shared/

---

# 1. Algorithmes

## 1.1. SHA256
Utilisé pour :
- hash du payload.zip
- hash des fichiers
- hash du manifeste

## 1.2. Ed25519 (fait-maison)
Utilisé pour :
- signature du manifeste
- vérification du manifeste

Implémentation :
- 100 % maison
- sans dépendance externe
- compatible RFC 8032

---

# 2. Clés

## 2.1. Clé privée (update.priv)
- générée via Moto.SignTool
- stockée dans `keys/`
- gitignored
- jamais committée

## 2.2. Clé publique (update.pub)
- embarquée dans Shared/BuildKeys.cs
- utilisée par l’installateur

---

# 3. Signature

Processus :
1. Calcul du hash SHA256 du payload.zip
2. Signature Ed25519 du hash
3. Insertion dans `Signature`

---

# 4. Vérification

Processus :
1. Recalcul du hash SHA256 du payload.zip
2. Vérification Ed25519 via BuildKeys.pub
3. Rejet si signature invalide

---

# 5. AtomicUpdater + Crypto

La mise à jour atomique dépend de la crypto :
- si signature invalide → update refusée
- si hash invalide → update refusée
- si extraction invalide → rollback automatique

---

# 6. Résumé

- ✔ SHA256  
- ✔ Ed25519  
- ✔ Signature  
- ✔ Vérification  
- ✔ BuildKeys.cs  
- ✔ Chaîne de confiance complète
