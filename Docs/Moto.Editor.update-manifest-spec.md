Spécification du manifeste de mise à jour — format officiel

# MOTO Editor — Update Manifest Specification

Ce document définit le format officiel du manifeste de mise à jour :
- Structure JSON
- Champs obligatoires
- Champs optionnels
- Signatures
- Hashs
- Delta updates

---

# 1. Format JSON

Exemple :

```json
{
  "Version": "1.1.0",
  "PayloadSha256": "abc123...",
  "Files": [
    {
      "Path": "Moto.Editor.exe",
      "Sha256": "def456...",
      "Size": 123456
    }
  ],
  "DeltaFrom": "1.0.0",
  "Signature": "fedcba..."
}

2. Champs obligatoires
Version (string)
Version du payload.

PayloadSha256 (string)
Hash SHA256 du fichier payload.zip.

Files (array)
Liste des fichiers du payload :

Path (string)

Sha256 (string)

Size (long)

Signature (string)
Signature Ed25519 du champ PayloadSha256.

3. Champs optionnels
DeltaFrom (string)
Version source pour les delta updates.

4. Règles de validation
4.1. Hash SHA256
Le hash du payload.zip doit correspondre à PayloadSha256.

4.2. Signature Ed25519
La signature doit être vérifiée via :
Ed25519.Verify(PayloadSha256, Signature, BuildKeys.pub)

4.3. Fichiers
Chaque fichier doit correspondre à :

Path

Sha256

Size

5. Delta updates
Si DeltaFrom est présent :

comparer les fichiers

télécharger uniquement les fichiers modifiés

reconstruire le payload local

6. Résumé
✔ Format JSON strict
✔ Hash SHA256
✔ Signature Ed25519
✔ Delta updates
✔ Validation complète
