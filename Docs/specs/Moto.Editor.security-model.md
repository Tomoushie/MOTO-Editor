Modèle de sécurité complet — signature, vérification, rollback, atomique

# MOTO Editor — Modèle de sécurité

Ce document décrit les mécanismes de sécurité intégrés dans :
- l’éditeur
- l’installateur
- le pipeline de mise à jour
- la chaîne de confiance Ed25519

---

# 1. Signature Ed25519

## Clés
- `update.priv` → clé privée (NE JAMAIS COMMITTER)
- `update.pub` → clé publique (embarqué dans BuildKeys.cs)

## Signature
Le manifeste est signé via :

Ed25519.Sign(hash(payload.zip), update.priv)

## Vérification
L’installateur vérifie :

Ed25519.Verify(hash(payload.zip), signature, BuildKeys.pub)


---

# 2. Hash SHA256

Chaque fichier du payload est hashé :

Sha256Helper.ComputeFile(path)

Le payload.zip est hashé :

Sha256Helper.ComputeFile(path)

Le payload.zip est hashé :

PayloadSha256


Le manifeste contient :
- hash par fichier
- hash du payload
- signature Ed25519

---

# 3. AtomicUpdater — mise à jour atomique

Processus :
1. Backup → `installDir.backup`
2. Extraction → `tempExtract`
3. Swap → `Directory.Move(tempExtract, installDir)`
4. Rollback automatique si erreur

Garanties :
- jamais de version cassée
- jamais de fichiers partiellement écrits
- mise à jour sûre même en cas de crash

---

# 4. ResumableDownloader — téléchargement résumable

Fonctionnalités :
- HTTP Range
- reprise après coupure
- mirrors CDN
- fallback automatique

Sécurité :
- pas de corruption de payload
- pas de fichiers incomplets

---

# 5. PayloadExtractor — extraction sécurisée

Protection Zip Slip :

if (!target.StartsWith(fullDest)) throw


Garanties :
- aucune extraction hors dossier
- aucune écriture non autorisée

---

# 6. Installateur per-user

Sécurité :
- pas d’UAC
- pas d’admin
- pas de privilèges élevés
- pas de modifications système dangereuses

---

# 7. MSIX (entreprise)

Sécurité :
- signature MSIX
- sandbox Windows
- intégration Store / Intune
- isolation des fichiers

---

# 8. Résumé

- ✔ Ed25519  
- ✔ SHA256  
- ✔ AtomicUpdater  
- ✔ Rollback  
- ✔ Resume  
- ✔ Mirrors  
- ✔ Zip Slip guard  
- ✔ Per-user  
- ✔ MSIX entreprise
