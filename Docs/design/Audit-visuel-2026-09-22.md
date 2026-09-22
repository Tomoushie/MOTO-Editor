# Audit visuel — MOTO Editor
**Date : 22/09 · Objectif : passer le visuel de « cheap » à « élevé » puis « vendable / triple A »**

> Échelle de Tom (2 axes, 6 niveaux) : `cheap → faible → moyen → élevé → vendable → triple A`
> **État constaté : Visuel = cheap · Backend/structure = élevé**
> Références visées : VS Code · Zed · JetBrains · Claude Code (Cowork)

---

## 1. Diagnostic : la cause racine est mesurée, pas supposée

MOTO Editor possède **déjà** un système de jetons (couleurs, espacements,
ombres, typographie) — construit par passes successives entre le 01/09 et le
08/09. Le problème n'est pas son absence : **c'est qu'il n'est pas utilisé.**

**Périmètre mesuré (important)** : le dépôt contient 92 fichiers XAML hors
`Docs/`, dont **21 sont exclus du build** (`MauiXaml Remove` dans
`Moto.Editor.csproj`). Le comptage ci-dessous porte donc sur les **71 fichiers
réellement compilés** — la maquette `Docs/inspirations/ClaudeShellView.xaml`
et les vues mortes ne sont pas comptées comme dette visuelle, puisque
personne ne les voit.

| Valeur | Occurrences codées en dur |
|---|---|
| `FontSize="<nombre>"` | **418** |
| `Padding="<nombre>"` | **292** |
| `CornerRadius` / `RoundRectangle` | **146** |
| Couleurs hexadécimales | **138** |
| **Total** | **994 valeurs visuelles en dur** |

En regard, l'usage réel des jetons censés les gouverner :

| Jeton | Usage réel (71 fichiers compilés) |
|---|---|
| `SpaceXs` / `SpaceSm` / `SpaceMd` / `SpaceLg` / `SpaceXl` | **0 · 0 · 0 · 0 · 0** |
| `ShadowSm` / `ShadowMd` / `ShadowLg` | **0 · 1 · 1** |
| `AccentHover` / `AccentMuted` (ajoutés le 08/09) | **0 · 0** |
| `MotoHoverRow` (style de ligne de liste) | **2** |
| `FontFamilyUi` | **9 fichiers sur 71** |

**Conséquence directe, et c'est tout le problème :** l'application est
peinte par ≈1 000 décisions prises une par une, écran par écran, sans échelle
commune. Les **espacements** sont tous des nombres choisis à la main. Les
**ombres** (l'élévation, ce qui distingue une surface flottante d'un fond) ne
servent quasi jamais.

**Et les deux mesures les plus parlantes :**

- **21 tailles de police distinctes.** Pas sept ou huit : **vingt-et-une**.
  Dont des **demi-pixels** (10,5 · 11,5 · 12,5) et des valeurs isolées
  (7, 9, 15, 17, 21, 22, 24, 26, 28, 30). Le cœur de la distribution tient
  dans une bande de 4 px — 10 (×39), 11 (×94), 11,5 (×8), 12 (×122),
  12,5 (×11), 13 (×43), 14 (×48) — c'est-à-dire **sept tailles qui se
  disputent le même rôle**. Une échelle réelle en compte une par rôle.
- **13 rayons d'arrondi distincts** : 0, 6, 7, 8, 9, 10, 12, 14, 15, 16, 17,
  24, et un **75** (l'avatar rond de `AboutView`). Personne ne choisit « 7 »
  ni « 15 » volontairement : c'est la signature d'ajustements à l'œil,
  répétés jusqu'à ce que ça « ait l'air bien » à un endroit précis, sans
  jamais valoir ailleurs.

Le rendu qui en résulte est exactement ce qu'on appelle « cheap » : chaque
élément est *individuellement acceptable* mais **rien ne se répète**, donc
l'œil ne perçoit aucune grille, aucune hiérarchie, aucune intention.

### Défauts structurels du thème lui-même

| # | Constat | Pourquoi c'est visible à l'écran |
|---|---|---|
| 1 | **Aucun jeton de graisse** (`FontWeight`/`FontAttributes`) : la typographie n'a que des tailles | La hiérarchie repose uniquement sur la taille. Les références utilisent **taille + graisse** — c'est ce qui fait « dessiné » plutôt que « brut ». |
| 2 | **Aucun jeton de hauteur de ligne** | Les paragraphes sont serrés ; les références aèrent le texte long. |
| 3 | **Échelle typographique réduite à 4 valeurs** (11 / 13 / 18 / 22) et **11 px pour le texte secondaire** | 11 px est trop petit pour du texte d'interface courant ; VS Code est à 13 px de base. |
| 4 | **Aucun jeton de rayon** | D'où les 13 valeurs distinctes ci-dessus (dont un 75). |
| 5 | **Aucun jeton de mouvement** (durée, courbe) | Aucune transition nulle part → l'UI ne « répond » pas au survol d'un panneau, à l'ouverture d'un menu. |
| 6 | **État `Disabled` absent** de tous les styles | Un bouton inactif ne se distingue pas d'un bouton actif. Idem **`Focused`** sur les boutons (seul `Entry` l'a) — grave pour un IDE piloté au clavier. |
| 7 | **Deux familles de bordures concurrentes** : `BorderCol` (#3A3B40 opaque) et `BorderSoft`/`BorderMuted` (blanc 10 %), sans règle d'usage | Les séparateurs ne se ressemblent pas d'un écran à l'autre. |
| 8 | **`Segoe UI Variable` utilisé dans 13 fichiers sur 93** | Les 80 autres héritent de la police par défaut de la plateforme → **deux typographies cohabitent**. De plus, cette police n'existe pas sur Windows 10 (cible supportée : 10.0.17763) → repli silencieux. |

---

## 2. Décision de marque non tranchée : l'accent est incohérent

Trois sources se contredisent :

| Source | Accent | Famille |
|---|---|---|
| `MotoTheme.xaml` (**le code réel**) | `#D97757` | orange terracotta (Claude) |
| `Docs/Documents/Présentation_détaillée_du_projet.html` §13 | `#007ACC` | bleu (VS Code) |
| `QWEN.md` §5 | `#007ACC` | bleu (VS Code) |

Les références visées sont **bleues** (VS Code `#007ACC`, Zed, JetBrains).
Claude Code, lui, est orange. Aujourd'hui le code a choisi l'orange sans que
la documentation suive — et l'accent a **55 usages**, c'est la couleur la
plus identitaire de l'app.

**À trancher avant toute refonte de palette** : l'accent de MOTO est-il
l'orange Claude (cohérent avec l'UX visée) ou le bleu IDE (cohérent avec les
concurrents) ? Ce choix conditionne tout le reste.

*Note : `QWEN.md` §5 est faux sur toute sa ligne (il annonce `#111214` /
`#1B1C1F` / `#007ACC`, le code contient `#1E2025` / `#202126` / `#D97757`).*

---

## 3. Ce que font les références, et que MOTO ne fait pas

Constaté en comparant les captures de référence du dépôt
(`Docs/inspirations/`, `Docs/images/`) au code actuel.

### Claude Code / Cowork (la cible UX de Tom)
- **Vue fractionnée** : deux conversations côte à côte, chacune avec son
  en-tête propre. C'est exactement l'idée B1 du fichier d'idées — la capture
  existe déjà dans le dépôt.
- **Composeur** : champ arrondi, placeholder d'invite (`/` pour les
  commandes), rangée de puces sous le champ (mode, modèle, envoi).
- **Bulles de message** : le message utilisateur a un fond/bordure propre, la
  réponse est en texte nu. La distinction de rôle se fait par la **surface**,
  pas par la couleur.
- **Appels d'outils** : lignes discrètes, glyphe + libellé + durée en gris,
  encadrées — très lisibles sans être bruyantes.
- **Panneau « Tâches en arrière-plan »** : carte avec titre, section « En
  cours », nom de workflow, méta-données, **points de progression**, phases
  dépliables, puis un **tableau** (agent / modèle / tokens / heure).
- **Beaucoup d'air**, séparateurs fins, arrondis 8–12 px, aucune ombre sur le
  chrome.

### Zed
- Chrome **entièrement plat** : un seul changement de fond par état
  (normal / survol / sélectionné), **jamais** de bordure ni de relief.
  *(Ce principe est déjà écrit dans un commentaire de `MotoTheme.xaml` — mais
  appliqué à seulement 2 endroits.)*
- Hiérarchie **monochrome** par la graisse et l'opacité, pas par la couleur.
- Arbre de projet à droite, très aéré, chevrons discrets.

### JetBrains (New UI)
- **Densité maîtrisée** : beaucoup d'information, mais alignée au pixel.
- **Onglets** : onglet actif avec fond distinct **+ liseré d'accent**, icône
  de type de fichier colorée.
- **Fil d'Ariane** dans l'éditeur, **indices en ligne** (inlay hints) en gris
  italique.
- **Barre de statut** dense et informative (branche, ligne:colonne, encodage,
  fin de ligne).
- Icônes de fichiers **colorées et cohérentes**, jamais des glyphes
  disparates.

### VS Code
- Barre d'activité + barre latérale + onglets + fil d'Ariane : une **grille
  stricte** que l'œil retrouve partout.
- Thème sombre **neutre** (gris bleutés), l'accent est réservé aux éléments
  actifs — jamais décoratif.

### Synthèse : les 5 écarts qui coûtent le plus cher
1. **Pas de grille** — espacements/rayons improvisés (994 valeurs en dur,
   13 rayons, 21 tailles de police).
2. **Pas de hiérarchie typographique** — taille seule, ni graisse ni hauteur
   de ligne, 11 px partout.
3. **Pas d'états** — survol rare, `Disabled`/`Focused` absents, aucune
   ligne de liste traitée (2 usages de `MotoHoverRow`).
4. **Pas de profondeur** — 2 usages d'ombre au total : tout est au même plan.
5. **Pas de mouvement** — zéro transition, aucun retour visuel.

---

## 4. Plan proposé

Principe directeur : **rendre le système existant opérant**, plutôt que d'en
écrire un nouveau à côté. Les jetons sont déjà là et déjà pensés ; il faut
les compléter, puis les faire consommer par le code.

### Phase 0 — Compléter le socle (`MotoTheme.xaml`)
- Échelle typographique **taille + graisse + hauteur de ligne** (et non
  taille seule), avec un rôle par niveau (affichage / titre / sous-titre /
  corps / secondaire / mono).
- Échelle de **rayons** (petit / moyen / grand / pilule).
- Échelle de **mouvement** (durées courte/moyenne, courbe standard).
- États manquants : `Disabled`, `Focused` (anneau de focus), `Selected`.
- Famille de police tranchée (repli Windows 10 inclus).
- Taille d'icône normalisée.

### Phase 1 — Rendre le socle opérant
- Conversion des 994 valeurs en dur vers les jetons, **par lots vérifiés**
  (un lot = un écran ou un composant, build à 0 erreur, contrôle visuel).
- Styles **implicites** pour les contrôles de base, afin que le défaut soit
  correct sans rien écrire.

### Phase 2 — Composants
Barre de titre et onglets · lignes de listes et arborescence de fichiers ·
chrome de l'éditeur (fil d'Ariane, onglets) · chat et composeur · cartes et
panneaux flottants · champs, boutons, badges · barres de défilement ·
états vides et de chargement.

### Phase 3 — Écrans entiers
Accueil → Explorateur + Éditeur → Panneaux IA → Réglages.

### Phase 4 — Mouvement et finition
Transitions d'ouverture de menus/panneaux, survols systématiques, états
vides, squelettes de chargement.

### Garde-fous (propres à ce dépôt)
- `MainPage.xaml` a un **historique de plantage sur changement de largeur de
  colonne** → tout changement de grille se teste séparément.
- La fenêtre est **sans bordure permanente** depuis le 08/09 (chantier « rendu
  100% custom ») → le chrome est à nous, donc toute incohérence de barre de
  titre est visible et de notre responsabilité.
- **Aucun test visuel automatisé** dans le dépôt → chaque lot doit être
  validé à l'œil par Tom. Un lot = un commit.
- Les modifications purement visuelles ne doivent **jamais** changer le
  comportement : build à 0 erreur avant/après, aucun handler touché.

---

## 5. Ce qui reste à décider

1. **Accent** : orange Claude (`#D97757`, état actuel du code) ou bleu IDE
   (`#007ACC`, annoncé partout ailleurs) ? — conditionne toute la palette.
2. **Point de départ** : Phase 0 seule (socle), ou Phase 0 + un écran pilote
   pour juger du niveau atteignable avant de généraliser ?
3. **Police** : rester sur `Segoe UI Variable` (Win 11) avec repli explicite,
   ou `Segoe UI` simple pour une cohérence Windows 10/11 ?

---

## Références
- Captures de référence : `Docs/inspirations/`, `Docs/images/`
- Jetons actuels : `Moto.Editor/Themes/MotoTheme.xaml`
- Écran pilote probable : `Moto.Editor/Views/HomeView.xaml`
