<!-- Docs/index.md -->
# 📘 MOTO Editor — Documentation

> Sommaire de la documentation du projet.
> ⚠️ Une partie de ces documents est **générée** (voir *Maintenance* en bas) :
> les fichiers marqués 🤖 sont régénérables et ne doivent pas être édités à la
> main, leurs modifications seraient écrasées.

## 🚪 Commencer ici

| Document | Contenu |
|----------|---------|
| [README.md](README.md) | Présentation, démarrage rapide, plateformes supportées, état réel du projet |
| [product/FEATURES.md](product/FEATURES.md) | Catalogue des fonctionnalités |
| [product/Roadmap.md](product/Roadmap.md) | Jalons v0.1 → v1.1+ |
| [product/Idées-à-implémenter.txt](product/Idées-à-implémenter.txt) | Idées triées : déjà fait / faisable / vision |

⚠️ **À lire avant de faire confiance aux catalogues** : `FEATURES.md` et
`Roadmap.md` sont **optimistes** — plusieurs fonctionnalités y sont cochées
comme livrées alors qu'elles ne sont pas branchées (réglages, split-pane,
LSP Roslyn, Collab/CRDT, IA embarquée ONNX). Le README détaille les cas
vérifiés. Même mise en garde que pour `FeatureCatalog.cs`.

## 🏗️ architecture/

| Document | Contenu |
|----------|---------|
| [Architecture.md](architecture/Architecture.md) | Règles d'architecture et couches (Editor / MOTO AI / Ollama / XENO) |
| [Arborescence.md](architecture/Arborescence.md) | Arbre des modules (partiellement daté : ne liste pas les 14 projets actuels) |
| [Modules-overview.md](architecture/Modules-overview.md) | Vue d'ensemble des modules en un schéma |
| [Moteurs-IA-internes.md](architecture/Moteurs-IA-internes.md) | Tableau des moteurs IA internes |
| [Moto.Editor.architecture.md](architecture/Moto.Editor.architecture.md) | 🤖 Architecture complète, 6 modules |
| [Moto.Editor.module-dependencies.md](architecture/Moto.Editor.module-dependencies.md) | 🤖 Dépendances internes |
| [Moto.Editor.memory-model.md](architecture/Moto.Editor.memory-model.md) | 🤖 Modèle mémoire |
| [Moto.Editor.threading-model.md](architecture/Moto.Editor.threading-model.md) | 🤖 Modèle de threading |

## 📐 specs/

Spécifications techniques (majoritairement 🤖 générées) :
[code-style](specs/Moto.Editor.code-style.md) ·
[dev-guidelines](specs/Moto.Editor.dev-guidelines.md) ·
[naming-conventions](specs/Moto.Editor.naming-conventions.md) ·
[crypto-spec (Ed25519)](specs/Moto.Editor.crypto-spec.md) ·
[security-model](specs/Moto.Editor.security-model.md) ·
[logging-spec](specs/Moto.Editor.logging-spec.md) ·
[error-codes](specs/Moto.Editor.error-codes.md) ·
[performance-guide](specs/Moto.Editor.performance-guide.md) ·
[testing-strategy](specs/Moto.Editor.testing-strategy.md) ·
[update-manifest-spec](specs/Moto.Editor.update-manifest-spec.md) ·
[update-failure-handling](specs/Moto.Editor.update-failure-handling.md)

## ⚙️ process/

| Document | Contenu |
|----------|---------|
| [CONTRIBUTING.md](process/CONTRIBUTING.md) | Règles de contribution et conventions |
| [Moto.Editor.directory-structure.md](process/Moto.Editor.directory-structure.md) | 🤖 Structure des dossiers |
| [Moto.Editor.build-pipeline.md](process/Moto.Editor.build-pipeline.md) | 🤖 Pipeline de build |
| [Moto.Editor.release-process.md](process/Moto.Editor.release-process.md) | 🤖 Processus de release |
| [Moto.Editor.installation-flow.md](process/Moto.Editor.installation-flow.md) | 🤖 Flux d'installation (installateur + MSIX) |
| [RELEASE-CHECKLIST.md](process/RELEASE-CHECKLIST.md) | Checklist de release |
| [Flux-bout-en-bout.md](process/Flux-bout-en-bout.md) | Chaîne tag → payload → signature → updater (scripts et CI réels) |

## 📁 Dossiers de contenu

| Dossier | Contenu |
|---------|---------|
| `Documents/` | Documents de présentation et de vente (business plan, analyses, manuel utilisateur, documentation API) |
| `Zen/` | Documents de référence Zed / Claude / JetBrains (ex-`Pour Claude/`) |
| `inspirations/` | Captures d'écran et sources de la maquette « Claude shell » |
| `probes/` | Rapports bruts des sondes multi-agents (JSON) |

## Maintenance

Les fichiers marqués 🤖 sont produits par l'orchestrateur/`DocEngine`
(commande `/doc` ou bouton 📚 dans l'application) et décrivent l'état du
**projet cible** une fois générés. Toute modification manuelle d'un fichier
🤖 sera écrasée à la prochaine génération si `doc_auto_update` est actif.

Les autres documents (README, CONTRIBUTING, Roadmap, FEATURES, specs
manuelles) sont **écrits à la main** et peuvent être édités.
