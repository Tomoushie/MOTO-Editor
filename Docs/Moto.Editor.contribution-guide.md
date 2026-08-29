Guide de contribution — parfait pour Claude et tout futur dev

# MOTO Editor — Contribution Guide

Ce document explique comment contribuer au projet MOTO Editor.

---

# 1. Pré-requis

- .NET 8 SDK
- Visual Studio 2022 ou Rider
- MAUI workload installé
- Git
- PowerShell 7+
- Clé privée NON committée (keys/update.priv)

---

# 2. Structure du projet

Voir :
- Moto.Editor.directory-structure.md
- Moto.Editor.projectmap.json

---

# 3. Branches

- `main` → stable
- `develop` → développement
- `feature/*` → nouvelles fonctionnalités
- `fix/*` → corrections

---

# 4. Règles de commit

Format :

# MOTO Editor — Contribution Guide

Ce document explique comment contribuer au projet MOTO Editor.

---

# 1. Pré-requis

- .NET 8 SDK
- Visual Studio 2022 ou Rider
- MAUI workload installé
- Git
- PowerShell 7+
- Clé privée NON committée (keys/update.priv)

---

# 2. Structure du projet

Voir :
- Moto.Editor.directory-structure.md
- Moto.Editor.projectmap.json

---

# 3. Branches

- `main` → stable
- `develop` → développement
- `feature/*` → nouvelles fonctionnalités
- `fix/*` → corrections

---

# 4. Règles de commit

Format :

# MOTO Editor — Contribution Guide

Ce document explique comment contribuer au projet MOTO Editor.

---

# 1. Pré-requis

- .NET 8 SDK
- Visual Studio 2022 ou Rider
- MAUI workload installé
- Git
- PowerShell 7+
- Clé privée NON committée (keys/update.priv)

---

# 2. Structure du projet

Voir :
- Moto.Editor.directory-structure.md
- Moto.Editor.projectmap.json

---

# 3. Branches

- `main` → stable
- `develop` → développement
- `feature/*` → nouvelles fonctionnalités
- `fix/*` → corrections

---

# 4. Règles de commit

Format :

type(scope): message


Types :
- `feat` → nouvelle fonctionnalité
- `fix` → correction
- `perf` → optimisation
- `refactor` → refactorisation
- `docs` → documentation
- `build` → pipeline
- `test` → tests

Exemples :

feat(editor): ajout du panneau Analytics
fix(core): correction du LayeredModelLoader
perf(engine): optimisation du XENO Pipeline


---

# 5. Règles de pull request

Chaque PR doit contenir :
- description claire
- justification technique
- impact sur les modules
- tests associés (si applicable)
- mise à jour des docs (si applicable)

---

# 6. Règles de build

## 6.1. Build local

pwsh scripts/build-installer.ps1
pwsh scripts/build-update-manifest.ps1


## 6.2. Build CI
Automatique via GitHub Actions.

---

# 7. Règles de mise à jour

Toute mise à jour doit :
- générer un nouveau payload.zip
- générer un nouveau manifeste
- signer le manifeste
- régénérer BuildKeys.cs
- publier payload.zip + payload.json

---

# 8. Règles de sécurité

- Jamais de clé privée dans le repo
- Jamais de dépendance externe non maîtrisée
- Jamais de code non vérifié dans Shared/

---

# 9. Résumé

- ✔ Process clair
- ✔ Contributions propres
- ✔ Sécurité respectée
- ✔ Documentation obligatoire
