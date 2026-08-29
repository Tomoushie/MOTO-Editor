Modèle mémoire — allocations, buffers, IA, XENO, installateur

# MOTO Editor — Memory Model

Ce document décrit le modèle mémoire de MOTO Editor :
- IA embarquée
- XENO Pipeline
- Installateur
- Shared
- MAUI/WinUI

---

# 1. Objectifs

- Minimiser les allocations
- Minimiser les copies
- Minimiser la fragmentation
- Maximiser la stabilité
- Maximiser la prédictibilité

---

# 2. Mémoire IA

## 2.1. CortexEngine
- stockage compact
- dictionnaires optimisés
- pas de copies massives

## 2.2. NeuralMode
- embeddings en mémoire mappée
- pas de duplication
- pas de conversions inutiles

## 2.3. SpeculativeDecoder
- buffers réutilisés
- pas de ToArray()

---

# 3. Mémoire XENO Pipeline

## 3.1. Scanner
- streaming
- pas de stockage massif

## 3.2. Analyzer
- structures légères
- pas de LINQ lourd

## 3.3. Synthesizer
- génération en flux
- pas de concaténations massives

---

# 4. Mémoire Installateur

## 4.1. PayloadExtractor
- extraction fichier par fichier
- pas de stockage complet en RAM

## 4.2. AtomicUpdater
- swap de dossier
- pas de copies massives

## 4.3. ResumableDownloader
- buffer unique réutilisé

---

# 5. Mémoire Shared

## 5.1. Ed25519
- BigInteger optimisé
- pas de copies inutiles

## 5.2. Sha256Helper
- buffer unique
- pas de ToHex coûteux dans les boucles

---

# 6. Mémoire MAUI/WinUI

## 6.1. UI stateless
## 6.2. Panels légers
## 6.3. Overlays légers

---

# 7. Résumé

- ✔ IA optimisée  
- ✔ XENO optimisé  
- ✔ Installateur optimisé  
- ✔ Shared optimisé  
- ✔ UI optimisée
