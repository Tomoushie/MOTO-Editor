Dépendances internes — carte des modules et relations

# MOTO Editor — Dépendances des modules

Ce document décrit les dépendances internes entre les modules :
- Editor
- Core
- Engine (XENO)
- Installer
- SignTool
- Shared

---

# 1. Dépendances globales

Moto.Editor → Moto.Core → Snake2000.Engine
Moto.Editor → Shared
Moto.Installer → Shared
Moto.SignTool → Shared


---

# 2. Moto.Editor dépend de :

### Moto.Core
- CortexEngine
- NeuralMode
- AIWorkspace
- ContextEngine
- SpeculativeDecoder
- LayeredModelLoader
- SmartModelManager
- AdaptiveResourceGovernor
- InferenceWatchdog

### Snake2000.Engine
- XENO Pipeline
- Agents
- Analyse de projet

### Shared
- PayloadExtractor
- Ed25519
- UpdateManifest
- AtomicUpdater

### Moto.Installer (indirect)
- via AutoUpdateService (délégation)

---

# 3. Moto.Core dépend de :

### Shared
- Sha256Helper
- Ed25519 (via SignTool)
- UpdateManifest (lecture)
- AtomicUpdater (indirect)

### Snake2000.Engine
- XENO Pipeline
- Agents

---

# 4. Snake2000.Engine dépend de :

### Moto.Core
- CortexEngine
- NeuralMode
- AIWorkspace

### Shared
- aucun (XENO est indépendant du système de mise à jour)

---

# 5. Moto.Installer dépend de :

### Shared
- PayloadExtractor
- PayloadVerifier
- ResumableDownloader
- AtomicUpdater

### Moto.SignTool
- uniquement au build (pas au runtime)

---

# 6. Moto.SignTool dépend de :

### Shared
- Ed25519
- Sha256Helper
- UpdateManifest

---

# 7. Shared dépend de :

### Aucun module
Shared est indépendant et portable.

---

# 8. Résumé

Shared = base commune
SignTool = signature
Installer = update + atomic
Core = IA embarquée
Engine = XENO pipeline
Editor = IDE + UI
