# Moto Marketplace Dashboard (React + TypeScript)

## Stack
- React 18 + TypeScript + Vite
- Tailwind CSS (thème MOTO)
- Zustand (état global)
- React Query (fetch API)
- JWT auth (localStorage)

## Routes
- `/login` — Authentification
- `/` — Catalogue plugins/themes/langues/snippets
- `/publish` — Soumettre un nouveau plugin (signature requise)
- `/profile` — Mes plugins, stats, clés API
- `/admin` — Modération (réservé)

## Composants
- `PluginCard` — preview + install + rating
- `SearchBar` — recherche + filtres (category, kind, language)
- `SignatureBadge` — vérification Ed25519
- `AnalyticsChart` — downloads / rating (Recharts)
