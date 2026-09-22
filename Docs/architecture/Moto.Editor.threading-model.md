Modèle threading — UI, IA, XENO, installateur, sécurité

# MOTO Editor — Threading Model

Ce document décrit le modèle threading de MOTO Editor :
- UI thread
- IA threads
- XENO threads
- Installateur threads
- Shared threads

---

# 1. Objectifs

- Pas de deadlocks
- Pas de race conditions
- Pas de contention
- Pas de blocage UI
- Pas de threads inutiles

---

# 2. Threading UI (MAUI/WinUI)

## 2.1. UI thread = sacré
- aucune opération lourde
- aucune opération IA
- aucune opération XENO
- aucune opération IO

## 2.2. Toujours utiliser :
MainThread.BeginInvokeOnMainThread(...)

---

# 3. Threading IA

## 3.1. CortexEngine
- thread-safe
- accès séquentiel
- pas de concurrence

## 3.2. SpeculativeDecoder
- thread dédié
- pas de blocage UI

## 3.3. LayeredModelLoader
- chargement en background
- synchronisation via Task

---

# 4. Threading XENO Pipeline

## 4.1. Pipeline séquentiel
Scanner → Analyzer → Synthesizer → Connector → Validator

## 4.2. Agents spécialisés
- threads isolés
- pas de partage d’état mutable

---

# 5. Threading Installateur

## 5.1. ResumableDownloader
- thread IO
- pas de blocage UI

## 5.2. PayloadExtractor
- thread IO
- extraction séquentielle

## 5.3. AtomicUpdater
- thread unique
- swap atomique

---

# 6. Threading Shared

## 6.1. Ed25519
- pure CPU
- thread-safe

## 6.2. Sha256Helper
- pure CPU
- thread-safe

---

# 7. Synchronisation

## 7.1. Pas de locks lourds
## 7.2. Utiliser `SemaphoreSlim`
## 7.3. Utiliser `Interlocked` pour les compteurs
## 7.4. Pas de `lock(this)`

---

# 8. Résumé

- ✔ UI thread protégé  
- ✔ IA thread-safe  
- ✔ XENO séquentiel  
- ✔ Installateur IO optimisé  
- ✔ Shared thread-safe
