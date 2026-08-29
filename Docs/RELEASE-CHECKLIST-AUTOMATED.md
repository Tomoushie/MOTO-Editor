# 📦 MOTO Editor — Checklist de Release Automatisée

> **Version**: v40+  
> **Dernière mise à jour**: Août 2026

---

## ✅ Pré-publication (automatisé via CI)

### Tests

- [ ] Tests unitaires verts sur `main`
- [ ] Tests E2E verts sur `main`
- [ ] Coverage > 80% sur `Moto.Core`
- [ ] Tests de robustesse : modèle corrompu, téléchargement interrompu, circuit breaker

### Benchmarks

- [ ] Benchmark intégré exécuté sur les 3 tiers (lite/standard/full)
- [ ] Rapport JSON/CSV généré et attaché à la release
- [ ] Pas de régression > 10% vs version précédente
- [ ] Dual ≥ 1.5× baseline sur charges simples

### Binary size

- [ ] Binary size audit OK (< 150 MB)
- [ ] Justification si dépassement (assets, native libs)
- [ ] Symbols strippés en Release

### Sécurité

- [ ] Signature Ed25519 des manifests modèles activée
- [ ] Vérification SHA256 avant activation
- [ ] Pas de path traversal possible
- [ ] Télémétrie opt-in documentée

### Documentation

- [ ] `Docs/AI-OPTIMIZATIONS.md` à jour
- [ ] `Docs/RELEASE-CHECKLIST.md` à jour
- [ ] Release notes rédigées

---

## 🚀 Publication

- [ ] GitHub Release créée
- [ ] Artéfacts uploadés (Windows, Linux, macOS)
- [ ] Checksums SHA256 publiés
- [ ] Lien de téléchargement vérifié

---

## 🔄 Post-publication

- [ ] Monitoring des erreurs pendant 24h
- [ ] Collecte des feedbacks utilisateurs
- [ ] Vérification des métriques de performance
- [ ] Plan de rollback prêt si nécessaire

---

## 📊 Release notes minimales

## v40.0 - Optimisations IA embarquées

### Nouvelles fonctionnalités
- Memory-mapped inference (-40% RAM)
- Parallel decoding (+30% vitesse)
- KV-cache compression (-50% cache)
- Dynamic quantization switching
- Auto-tier switching thermique

### Activation/désactivation
- ai.embedded.useMemoryMapping = true/false
- ai.embedded.enableParallelDecoding = true/false
- ai.embedded.enableKvCacheCompression = true/false

### Warnings

- Thermal switching : bascule tier Lite si > 85°C
- Quantization switching : Q4 → Q3 si RAM > 85%

### Dépannage rapide

- Vérifiez les logs : %AppData%/MotoEditor/DebugLogs/
- Ouvrez AiMonitoringView (icône 🧠)
- Utilisez le bouton "Envoyer les logs" si circuit breaker ouvert
