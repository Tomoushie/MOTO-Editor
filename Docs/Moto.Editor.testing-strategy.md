Stratégie de tests — claire, hiérarchique, professionnelle

# MOTO Editor — Testing Strategy

Ce document décrit la stratégie de tests pour MOTO Editor.

---

# 1. Types de tests

## 1.1. Tests unitaires
Ciblent :
- Moto.Core
- Shared
- Snake2000.Engine

Objectifs :
- Vérifier la logique IA
- Vérifier les helpers
- Vérifier les crypto
- Vérifier les extracteurs

---

## 1.2. Tests d’intégration
Ciblent :
- Editor ↔ Core
- Editor ↔ Engine
- Installer ↔ Shared

Objectifs :
- Vérifier les interactions
- Vérifier les pipelines
- Vérifier les services DI

---

## 1.3. Tests E2E
Ciblent :
- CRDT
- XENO Pipeline
- Installateur
- Mise à jour atomique

Objectifs :
- Vérifier le comportement global
- Vérifier la cohérence multi-OS

---

# 2. Tests critiques

## 2.1. AtomicUpdater
- swap correct
- rollback correct
- backup correct

## 2.2. PayloadVerifier
- SHA256 correct
- Ed25519 correct
- signature correcte

## 2.3. ResumableDownloader
- resume correct
- mirrors correct
- fallback correct

## 2.4. PayloadExtractor
- Zip Slip guard
- extraction correcte

---

# 3. Tests UI (MAUI)

## 3.1. Tests de navigation
## 3.2. Tests de panels
## 3.3. Tests de overlays
## 3.4. Tests de commandes

---

# 4. Tests IA

## 4.1. CortexEngine
- mémoire cohérente
- embeddings corrects

## 4.2. SpeculativeDecoder
- accélération correcte
- cohérence des tokens

## 4.3. LayeredModelLoader
- chargement correct
- fallback correct

---

# 5. Tests XENO

## 5.1. Pipeline complet
## 5.2. Agents spécialisés
## 5.3. Analyse de projet
## 5.4. Génération cohérente

---

# 6. Tests installateur

## 6.1. Installation per-user
## 6.2. Raccourcis
## 6.3. Désinstallation
## 6.4. Mise à jour atomique

---

# 7. Résumé

- ✔ Unitaires  
- ✔ Intégration  
- ✔ E2E  
- ✔ UI  
- ✔ IA  
- ✔ XENO  
- ✔ Installer
