<!-- Docs/README.md -->
# 🏍️ MOTO Editor

**Un IDE de bureau avec une intelligence interne, locale et hors-ligne.**

MOTO Editor est un éditeur de code pour **Windows** (.NET MAUI / WinUI 3)
doté d'une intelligence interne : il analyse ton projet, génère du code,
produit sa documentation et s'assiste lui-même — **sans cloud, sans compte,
sans modèle externe obligatoire**.

> « Un IDE qui pense comme un senior, explique comme un prof,
> et code comme toi. »

---

## ✨ Pourquoi MOTO Editor ?

| Problème des IDE classiques | Réponse MOTO |
|-----------------------------|--------------|
| IA dépendante du cloud | **IA 100% locale** (Cortex + Neural + XENO) |
| Courbe d'apprentissage raide | **Modes Beginner / Tutor / No-Code** |
| Documentation jamais à jour | **Doc Engine** auto-générée |
| Perte de contexte | **Time Machine** + **Cortex Memory** |
| Fonctionnalités à moitié branchées | Chantier de fiabilité en cours (voir *État réel* ci-dessous) |

## 🚀 Démarrage rapide

Prérequis : **.NET SDK 8** et le **workload MAUI** (`dotnet workload install maui`).

```bash
# Compiler (le framework cible est OBLIGATOIRE : le projet est multi-cible)
dotnet build Moto.Editor/Moto.Editor.csproj -f net8.0-windows10.0.19041.0

# Lancer (même remarque — sans -f, la commande échoue)
dotnet run --project Moto.Editor -f net8.0-windows10.0.19041.0

# Ouvrir un projet : 📂 → choisir le dossier
# L'IA s'initialise (Cortex + Neural + Workspace) automatiquement.
```

> ⚠️ **NuGet est configuré par projet** dans ce dépôt (l'accès global à
> nuget.org est bloqué sur la machine de développement d'origine). Ne pas
> chercher à « réparer » une config NuGet globale : ça fonctionne tel quel.

## 🖥️ Plateformes réellement supportées

| Plateforme | Statut | Détail |
|---|---|---|
| **Windows 10/11** | ✅ Supportée et testée | Cible `net8.0-windows10.0.19041.0`, Windows App SDK 1.8 |
| **macOS** | ⚠️ Cible déclarée, **non validée** | `net8.0-maccatalyst` n'est ajoutée que si le build est lancé **depuis macOS**. Elle n'est donc jamais construite depuis Windows et n'est validée par personne aujourd'hui. |
| **Linux / Android / iOS** | ❌ Non supportées | Aucune cible dans le `.csproj`. Le *Platform Engine* sait **produire** des projets Avalonia Linux ; MOTO Editor lui-même n'y tourne pas. |

## 📦 Distribution

- **Raccourci Bureau** (« MOTO Editor ») → pointe vers `bin/Release/.../win10-x64/Moto.Editor.exe`.
- **Paquet MSIX** signé pour le sideload local (`-p:WindowsPackageType=MSIX`).
- Le **MSIX installé** et le **dossier `bin/`** sont **deux exécutables distincts**,
  jamais reconstruits ensemble — toujours tester celui qu'on croit tester.

## ⚠️ État réel du projet (à lire avant de se fier aux catalogues)

Ce dépôt contient beaucoup de code réel, mais aussi des fonctionnalités
**déclarées faites** qui ne sont pas branchées. Les fichiers
`FEATURES.md`, `Roadmap.md` et `FeatureCatalog.cs` se sont révélés
**optimistes à plusieurs reprises** : ne jamais les prendre pour argent
comptant sans vérifier dans le code.

Exemples documentés et vérifiés :

- **Réglages** : le catalogue en compte ~300, mais seuls **4** sont
  réellement appliqués par `SettingsApplier`. Le reste est affiché et
  persisté sans effet.
- **Vue fractionnée (⧉)** : les réglages « Split vertical/horizontal »
  existent dans le catalogue, **sans aucun code derrière**.
- **LSP Roslyn** : annoncé « à venir » en v1.0 dans la roadmap, et
  pourtant coché comme livré dans `FEATURES.md`. Il n'est pas fonctionnel.
- **Collab temps réel / CRDT** : même situation — la brique n'est pas
  raccordée.
- **Plugins** : un seul plugin (`SampleFormatPlugin`) compile et s'exécute
  réellement. Les autres projets de plugin du dépôt ciblent une interface
  qui n'existe pas.

L'objectif assumé du projet est de rendre cet écart visible et de le
réduire, pas de le masquer : **une fonctionnalité n'existe que si elle
fonctionne de bout en bout.**

## 🗺️ Suite

Voir `product/Roadmap.md` (jalons) et `product/Idées-à-implémenter.txt`
(idées triées :
faisables / déjà faites / vision). Mise en garde identique : ces deux
fichiers sont des **intentions**, pas un état des lieux.

**Direction technique à moyen terme (décidée le 22/09, pas encore commencée)** :
remplacer le moteur d'édition (`WebView2`) par un rendu direct (SkiaSharp)
pour la vitesse. Une réécriture complète en Rust est envisagée par la
suite, mais explicitement repoussée à **après** que le logiciel soit
pleinement opérationnel — pas un chantier en cours.

---
<!--
  NOTE D'ENTRETIEN (pour les contributeurs et les agents IA)
  Ce fichier vit dans Docs/. Il n'existe PAS de README.md à la racine du
  dépôt, donc c'est bien celui-ci que voit un visiteur — ne pas le traiter
  comme une simple note interne.
  Dernière vérification factuelle : 22/09. Chaque affirmation ci-dessus a été
  contrôlée dans le code (.csproj, SettingsApplier, FEATURES.md). Si un fait
  change (nouvelle plateforme ciblée, réglages réellement branchés), mettre à
  jour ce fichier AU LIEU d'ajouter une promesse de plus.
-->
