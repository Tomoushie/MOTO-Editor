
## Moteurs IA internes (Moto.Core/AI/)

| Moteur | Rôle | Persistance |
|--------|------|-------------|
| `MotoAiKernel` | routeur d'intentions + fallback providers | – |
| `ProjectUnderstandingEngine` | carte mentale (symboles, relations, issues) | – |
| `CortexEngine` | mémoire cognitive + style | `.moto/cortex/memory.json` |
| `NeuralMode` | embeddings TF-IDF + retrieval | index mémoire |
| `AIWorkspace` | orchestrateur proactif | – |
| `AutoLinkEngine` | refs manquantes + Apply/Dismiss | – |
| `ContextEngine` | suggestions senior proactives | – |
| `TimeMachineEngine` | snapshots sans Git | `.moto/timemachine/*.json` |
| `HealthMonitorEngine` | santé + complexité cyclomatique | – |
| `PatternDetectorEngine` | ECS/Factory/Singleton/Observer | – |
| `DocEngine` | 6 markdown auto | `.moto/docs/*.md` |
| `PlatformEngine` | portages + CI + validation incrémentale | – |
| `PerformanceEngine` | Eco/Balanced/Turbo/Ultra | settings |

## Système de paramètres (data-driven)

- `SettingsCatalog` (classe **partial**) : ~300 paramètres en 12+ catégories.
- Types : `Toggle / Int / Enum / String / Action`.
- `SettingsEngine.Shared` : persistance `%AppData%/MotoEditor/settings.json`.
- `SettingsApplier` : application live (thème, police, minimap…).
- **Ajouter un paramètre = 1 ligne** dans un fichier `SettingsCatalog.*.cs`.

## Modèle de performance

- `LazyFileLoader` : LRU 20 fichiers, chargement à la demande.
- `IncrementalHighlighter` : re-tokenise uniquement les lignes modifiées.
- `MiniMapCompressor` : downsampling + throttle 400 ms.
- `AiCacheEngine` : SHA256 + LRU + TTL 7 j.
- Watchers avec **debounce** (jamais de travail inutile).

## Flux de données type
