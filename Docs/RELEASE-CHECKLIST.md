# 📦 MOTO Editor — Checklist de Release

> **Version**: v40+  
> **Dernière mise à jour**: Août 2026  
> **Statut**: 🟡 En préparation

---

## 📋 Table des matières

1. [Pré-requis](#pré-requis)
2. [Checklist pré-release](#checklist-pré-release)
3. [Tests obligatoires](#tests-obligatoires)
4. [Sécurité](#sécurité)
5. [Performance](#performance)
6. [Documentation](#documentation)
7. [Publication](#publication)
8. [Rollback](#rollback)

---

## ✅ Pré-requis

### Environnement

| Composant | Version minimale | Vérification |
|-----------|------------------|--------------|
| .NET SDK | 8.0+ | `dotnet --version` |
| Visual Studio / VS Code | 2022+ / 1.85+ | IDE à jour |
| Ollama (optionnel) | 0.1.30+ | `ollama --version` |
| Git | 2.40+ | `git --version` |

### Matrice de test

| OS | Architecture | RAM min | Statut |
|----|-------------|---------|--------|
| Windows 10/11 | x64 | 8 GB | ⬜ À tester |
| Linux (Ubuntu 22.04+) | x64 | 8 GB | ⬜ À tester |
| macOS 13+ | x64/ARM64 | 8 GB | ⬜ À tester |

---

## 🔍 Checklist pré-release

### Code & Compilation

- [ ] `dotnet build --configuration Release` sans erreur
- [ ] `dotnet build --configuration Release` sans warning critique
- [ ] Analyse statique (SonarQube/Roslyn analyzers) sans blocant
- [ ] Aucun TODO/FIXME critique restant
- [ ] Dépendances NuGet à jour et sans vulnérabilité connue

### Fonctionnalités

- [ ] Éditeur : ouverture/sauvegarde de fichiers < 50 MB
- [ ] IA : complétion fonctionnelle avec Ollama
- [ ] IA : complétion fonctionnelle avec modèle embarqué (si Ollama absent)
- [ ] IA : basculement automatique Ollama ↔ Embarqué
- [ ] Terminal : exécution de commandes basiques
- [ ] Paramètres : persistance des ~300 paramètres
- [ ] Thèmes : clair/sombre fonctionnels

### Intégration XENO-SSS∞

- [ ] `XenoBridge` : connexion établie
- [ ] `XenoGateway` : délégation des opérations structurées
- [ ] Pas de parsing profond côté MOTO Editor

---

## 🧪 Tests obligatoires

### Tests E2E (scripts/run-e2e-tests.ps1 ou .sh)

- [ ] Suite complète exécutée sur Windows x64
- [ ] Suite complète exécutée sur Linux x64
- [ ] Suite complète exécutée sur macOS
- [ ] Rapport JSON/CSV généré et archivé

### Tests spécifiques

| Test | Commande | Statut |
|------|----------|--------|
| Modèle corrompu | `dotnet test --filter ModelCorruptionTests` | ⬜ |
| Téléchargement interrompu | `dotnet test --filter DownloadInterruptionTests` | ⬜ |
| Circuit breaker | `dotnet test --filter CircuitBreakerTests` | ⬜ |
| Reprise snapshot | `dotnet test --filter SnapshotResumeTests` | ⬜ |

### Stress tests

- [ ] Run 30 min sans fuite mémoire (croissance < 100 MB)
- [ ] Run 1h avec baseline CPU < 5% au repos
- [ ] Basculement thermique fonctionnel (si température > 85°C)
- [ ] Mode éco activé automatiquement si RAM < 8 GB

### Benchmarks

| Tier | Tokens/s min | RAM max | Latence p95 |
|------|-------------|---------|-------------|
| Lite | 15 | 512 MB | 200 ms |
| Standard | 8 | 2 GB | 800 ms |
| Full | 3 | 8 GB | 2000 ms |

- [ ] Benchmarks exécutés sur les 3 tiers
- [ ] Résultats exportés en JSON/CSV
- [ ] Comparaison avec la version précédente (pas de régression > 10%)

---

## 🔒 Sécurité

### Modèles IA

- [ ] Vérification SHA256 de tous les modèles embarqués
- [ ] Signature Ed25519 du manifeste des modèles
- [ ] Pas de path traversal possible (`ModelPaths.GetModelPath()`)
- [ ] Consentement utilisateur avant téléchargement (`ModelConsentDialog`)

### Données utilisateur

- [ ] Politique de confidentialité à jour
- [ ] Télémétrie opt-in uniquement (privacy-safe)
- [ ] Aucune donnée sensible dans les logs
- [ ] Anonymisation des métriques agrégées

### Code

- [ ] Revue sécurité des handlers de commandes
- [ ] Pas d'exécution de code arbitraire
- [ ] Validation des entrées utilisateur (prompt IA)
- [ ] Sandboxing des processus d'inférence (`Moto.InferenceHost`)

---

## ⚡ Performance

### Métriques à valider

| Métrique | Seuil | Mesuré | Statut |
|----------|-------|--------|--------|
| Démarrage à froid | < 3s | — | ⬜ |
| Ouverture fichier 1 MB | < 500ms | — | ⬜ |
| Complétion IA (lite) | < 200ms | — | ⬜ |
| RAM au repos | < 200 MB | — | ⬜ |
| RAM en inférence (standard) | < 2 GB | — | ⬜ |

### Optimisations actives

- [ ] Lazy-loading des services lourds (LSP, XENO, CRDT)
- [ ] Cache agressif (`AggressiveCacheManager`)
- [ ] Mode Ultra-Lite disponible
- [ ] Memory-mapped inference activée par défaut
- [ ] KV-cache compression activée par défaut
- [ ] Parallel decoding activé par défaut

---

## 📚 Documentation

### Utilisateur

- [ ] `Docs/README.md` à jour
- [ ] `Docs/AI-OPTIMIZATIONS.md` à jour
- [ ] `Docs/RELEASE-CHECKLIST.md` (ce fichier) à jour
- [ ] Guide d'installation multiplateforme
- [ ] FAQ dépannage

### Développeur

- [ ] `Docs/ARCHITECTURE.md` à jour
- [ ] `Docs/CONTRIBUTING.md` à jour
- [ ] Comments XML sur les APIs publiques
- [ ] Diagramme de flux de données

---

## 🚀 Publication

### Artéfacts

- [ ] Binaire Windows x64 (single-file)
- [ ] Binaire Linux x64 (single-file)
- [ ] Binaire macOS x64/ARM64 (single-file)
- [ ] Taille binaire < 150 MB (binary-size-audit)
- [ ] Signature des binaires (Authenticode / GPG)

### Modèles embarqués

- [ ] Phi-3 Mini Q4 (default)
- [ ] Qwen 2.5 1.5B Q4 (optionnel)
- [ ] Llama 3.2 1B/3B Q4 (optionnel)
- [ ] Manifeste signé (Ed25519)

### Distribution

- [ ] GitHub Release créée
- [ ] Notes de version rédigées
- [ ] Artéfacts uploadés
- [ ] Lien de téléchargement vérifié
- [ ] Checksums SHA256 publiés

---

## 🔄 Rollback

### Procédure

Si un problème critique est détecté après publication :

1. **Identifier la version stable précédente**
   ```bash
   git log --oneline --grep="Release" -1

## Créer une release corrective

  - [ ] Créer une release corrective
  - [ ] Publier la release corrective
  - [ ] Annuler la release stable actuelle
  - [ ] Annuler la release corrective
    - [ ] Annuler la release corrective sur GitHub

  Commande pour créer une release corrective :
    ```bash
    git tag -a v40.1 -m "Hotfix: description"
    git push origin v40.1
    ```

## Marquer la version défectueuse
    - GitHub Release → "Mark as pre-release"
    - Ajouter une note explicative

## Communiquer
   - Issue GitHub avec le tag regression
   - Notification dans les notes de version

## 📊 Récapitulatif des livrables

| Livrable | Fichier | Lignes | Statut |
|----------|---------|--------|--------|
| Script E2E Windows | `scripts/run-e2e-tests.ps1` | ~250 | ✅ |
| Script E2E Linux/macOS | `scripts/run-e2e-tests.sh` | ~200 | ✅ |
| Template dashboard | `config/dashboard-template.json` | ~200 | ✅ |
| Loader dashboard | `Moto.Editor/Services/DashboardConfigLoader.cs` | ~150 | ✅ |
| Extension AiMonitoringView | `Moto.Editor/Views/AiMonitoringView.xaml.cs` | +50 | ✅ |
| Checklist release | `Docs/RELEASE-CHECKLIST.md` | ~300 | ✅ |

---

## ✅ Validation architecturale

- ✅ Aucune fonctionnalité existante supprimée
- ✅ Handlers existants de `AiMonitoringView` préservés
- ✅ Compatible MAUI + WinUI 3
- ✅ Multiplateforme (Windows, Linux, macOS)
- ✅ Cohérence avec `MotoTheme.xaml` respectée
- ✅ Intégration avec le Watchdog et le circuit breaker existants
- ✅ Compatible avec `Moto.Tests/` (vague 4)
- ✅ Compatible avec `AiOptimizationsBenchmark` (vague 4)

---

**Prochaine étape ?** Souhaitez-vous :
1. Générer le script de **collecte de logs structurés** (JSON + upload sécurisé)
2. Créer le **workflow CI GitHub Actions** pour exécuter les tests nightly
3. Rédiger la **politique de confidentialité** (opt-in télémétrie)
4. Autre chose ?

Sévérité - Action - Délai
"🔴 Critique (crash, perte de données)" -Rollback immédiat - < 1h
"🟠 Majeur (fonctionnalité cassée)" - Hotfix ou rollback - < 24h
"🟡 Mineur (cosmétique)" - Fix dans la prochaine release - Prochaine release

Rôle - Nom - Date - Approbation
Lead Dev - 0 - 0 - ⬜
QA - 0 - 0 - ⬜
Sécurité - 0 - 0 - ⬜
Product Owner - 0 - 0 - ⬜
