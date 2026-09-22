Règles de développement — claires, strictes, professionnelles

# MOTO Editor — Dev Guidelines

Ce document définit les règles de développement pour MOTO Editor, afin de garantir :
- Cohérence
- Maintenabilité
- Performance
- Sécurité
- Compatibilité multi-OS

---

# 1. Principes fondamentaux

## 1.1. Additif, jamais destructif
Toute modification doit :
- ajouter des fonctionnalités,
- étendre les modules,
- ne jamais casser l’existant.

## 1.2. Séparation stricte des responsabilités
- Editor = UI + IDE
- Core = IA embarquée
- Engine = XENO Pipeline
- Installer = installation + update
- SignTool = signature
- Shared = logique commune

## 1.3. Pas de dépendances externes
- Pas de librairies tierces
- Pas de frameworks additionnels
- Pas de runtime externe
- Pas de scripts non maîtrisés

---

# 2. Règles de code

## 2.1. Pas de logique dans les constructeurs
Les constructeurs doivent rester simples.

## 2.2. Méthodes courtes
Objectif :
- < 40 lignes
- < 3 niveaux d’imbrication

## 2.3. Pas de duplication
Toute logique commune → `Shared/`.

## 2.4. Pas de static global
Uniquement pour :
- helpers
- crypto
- utilitaires purs

---

# 3. Règles MAUI / WinUI

## 3.1. Pas de code-behind lourd
Toute logique doit passer par les services DI.

## 3.2. Pas de singletons UI
L’UI doit rester stateless.

## 3.3. Pas de dépendance directe vers Core
Toujours passer par les services.

---

# 4. Règles IA

## 4.1. IA = pure logique
Aucune dépendance vers :
- UI
- Installer
- SignTool

## 4.2. Pas de side-effects
Les moteurs IA doivent être déterministes.

---

# 5. Règles de sécurité

## 5.1. Jamais de clé privée dans le code
Les clés privées sont dans `keys/` (gitignored).

## 5.2. Vérification systématique
Toute mise à jour doit être :
- hashée (SHA256)
- signée (Ed25519)
- vérifiée

## 5.3. Extraction sécurisée
Protection Zip Slip obligatoire.

---

# 6. Règles de performance

## 6.1. Pas de LINQ dans les boucles critiques
Utiliser des boucles classiques.

## 6.2. Utiliser Span<T> si nécessaire
Pour les opérations sur buffers.

## 6.3. MemoryMappedFile pour les modèles IA
Toujours.

---

# 7. Règles de documentation

## 7.1. XML obligatoire pour les classes publiques
## 7.2. Commentaires utiles uniquement
## 7.3. Pas de commentaires inutiles

---

# 8. Résumé

- ✔ Cohérence
- ✔ Sécurité
- ✔ Performance
- ✔ Maintenabilité
