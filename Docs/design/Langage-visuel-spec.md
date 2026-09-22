# Langage visuel MOTO Editor — spécification à valider
**Date : 22/09 · Statut : PROPOSITION — aucun XAML modifié tant que ce document n'est pas validé**

> Compagnon de `Audit-visuel-2026-09-22.md`. L'audit établit le diagnostic ;
> ce document propose les valeurs exactes. Objectif : rendre « cheap → élevé »
> possible **mécaniquement**, pas au jugé.

---

## 0. Contraintes d'API vérifiées (à ne pas supposer)

Vérifié le 22/09 dans la source officielle MAUI **8.0.100** (version réellement
résolue par le projet — `Microsoft.Maui.Controls.Core/8.0.100`, lue dans
`Moto.Editor/obj/project.assets.json`) :

| Propriété disponible sur `Label` / `Button` | Conséquence pour le design |
|---|---|
| `FontSize` | ✅ échelle de tailles |
| **`FontAttributes`** = `None` \| `Bold` \| `Italic` | ⚠️ **seule graisse disponible** |
| **`FontWeight`** | ❌ **N'EXISTE PAS** en MAUI 8 |
| `LineHeight` | ✅ interligne maîtrisable |
| `CharacterSpacing` | ✅ espacement des lettres |
| `TextTransform` | ✅ dont `Uppercase` |
| `FontFamily`, `FontAutoScalingEnabled`, `Opacity` | ✅ |

**Deux conséquences structurantes :**

1. **La hiérarchie ne peut pas reposer sur des graisses numériques**
   (400/500/600/700). Elle doit reposer sur **taille + Gras ou non + couleur/
   opacité + espacement**. C'est l'inverse de VS Code/Web, et ça se voit : ne
   pas concevoir une échelle « comme sur le web ».
2. **Ceci explique un fait du dépôt** : les 7 vues qui utilisent
   `FontWeight="SemiBold"`/`"Bold"` sur des `TextBlock` (`AdminDashboardView`,
   `AdvancedAiSettingsView`, `ModelConsentDialog`, `PerformanceDashboardView`,
   `RefactorPanel`, `SubscriptionOverlay`, `StatusBarView`) sont **toutes**
   dans la liste `MauiXaml Remove` du `.csproj`. Corrélation parfaite : elles
   ont été écrites en XAML WinUI dans un projet MAUI, elles ne compilent pas.
   *(Vérifié : `FontWeight` n'apparaît **que** dans des fichiers exclus.)*

---

## 1. Échelle typographique — 21 tailles actuelles → 10 rôles

Aujourd'hui : **21 tailles distinctes**, dont des demi-pixels (10,5 / 11,5 /
12,5) et des valeurs isolées (7, 9, 15, 17, 21, 22, 24, 26, 28, 30). Sept
tailles différentes se disputent le rôle « texte courant » (10 → 14).

### Décision D1 — taille de base : 12 px (actuel) ou 13 px (recommandé) ?

| | Option A — « fidèle » | **Option B — « moderne » (recommandée)** |
|---|---|---|
| Corps | **12** | **13** |
| Petit | 11 | 12 |
| Micro | 10 | 11 |
| Effet | Aucun décalage de mise en page ; le rendu reste dense | **+1 px partout** ; lecture plus aérée, conforme à VS Code (13) et JetBrains (13) |
| Risque | Le rendu reste « petit/Serré », une des causes du jugement « cheap » | Débordements possibles dans les listes et barres denses — à vérifier écran par écran |

**Recommandation : Option B.** 12 px de base est la marque d'un outil qui n'a
pas de charte ; 13 px est la norme des IDE cités en référence.

### Les 10 rôles (valeurs pour l'option B)

| Rôle | Taille | Graisse | Interligne | Espacement lettres | Usage |
|---|---|---|---|---|---|
| `Display` | 28 | Bold | 34 | — | Écran vide, onboarding (rare) |
| `Title` | 20 | Bold | 26 | — | Titre de page, titre de dialogue |
| `Heading` | 16 | Bold | 22 | — | En-tête de section |
| `Subheading` | 14 | Bold | 20 | — | Titre de panneau ou de carte |
| `Body` | **13** | None | 20 | — | Texte d'interface par défaut |
| `BodyStrong` | **13** | Bold | 20 | — | Élément actif, nom de fichier, emphase |
| `Small` | 12 | None | 16 | — | Métadonnées, texte secondaire |
| `Micro` | 11 | Bold | 14 | **+0,6, `Uppercase`** | Étiquettes de section, badges, compteurs |
| `Mono` | 13 | None | 20 | — | Code |
| `MonoSmall` | 12 | None | 18 | — | Code en ligne, diff |

Le rôle **`Micro`** est la trouvaille utile : c'est le procédé classique des
IDE modernes (petite capitale espacée) pour hiérarchiser **sans graisse
supplémentaire** — la seule technique disponible ici.

### Table de conversion (pour rendre la Phase 1 mécanique)

| Tailles actuelles | Rôle cible |
|---|---|
| 7, 9, 10, 10,5 | `Small` (12) — ces tailles sont sous le seuil de lisibilité |
| 11, 11,5 | `Micro` (11) si badge/étiquette ; `Small` (12) sinon |
| 12, 12,5, 13 | `Body` (13) |
| 14, 15 | `Subheading` (14) |
| 16, 17, 18 | `Heading` (16) |
| 20, 21, 22, 24 | `Title` (20) |
| 26, 28, 30 | `Display` (28) |

⚠️ **Zone de jugement** : 18 (×13), 24 et 15 restent ambigus selon l'élément.
Ces cas se tranchent **au vu de l'écran**, pas par table. C'est la seule
partie non mécanique.

---

## 2. Rayons — 13 valeurs actuelles → 5 jetons

Aujourd'hui : `0, 6, 7, 8, 9, 10, 12, 14, 15, 16, 17, 24, 75`.
Les valeurs 7, 9, 15, 17 et 75 ne sont pas des choix, ce sont des résidus
d'ajustement à l'œil.

| Jeton | Valeur | Usage exclusif |
|---|---|---|
| `RadiusXs` | **4** | Badges, micro-pastilles |
| `RadiusSm` | **6** | Champs, boutons, lignes de liste |
| `RadiusMd` | **8** | Cartes, panneaux internes |
| `RadiusLg` | **12** | Surfaces flottantes, modales, fenêtres spécialisées |
| `RadiusPill` | **999** | Puces, pastilles d'état, avatars (remplace le 75) |

**Table de conversion** : `0→0` (séparateurs à angle droit uniquement),
`6,7→RadiusSm` · `8,9,10→RadiusMd` · `12,14,15,16,17,24→RadiusLg` ·
`75→RadiusPill`.

---

## 3. Espacements — les 5 jetons existants, mais **appliqués**

`SpaceXs 4 · SpaceSm 8 · SpaceMd 16 · SpaceLg 24 · SpaceXl 32` existent déjà
et ne sont **utilisés nulle part** (0 usage). Ils sont corrects pour une
interface dense ; il n'y a rien à inventer, seulement à **consommer**.

Règle proposée : tout `Padding`/`Margin`/`Spacing` à valeur unique passe par
un jeton. Les valeurs composées (`Padding="14,10"`) se réécrivent
`Padding="{StaticResource SpaceLg},{StaticResource SpaceSm}"` **seulement si
la valeur s'en approche** — sinon on corrige vers le jeton le plus proche,
ce qui est précisément l'effet recherché (faire disparaître les 14, 10, 7…).

---

## 4. Mouvement — jetons à créer

Zéro transition aujourd'hui. MAUI **ne permet pas de déclarer des transitions
en XAML** : les animations s'écrivent en C#. Deux mécanismes complémentaires :

1. **Changements instantanés** (survol, appui, sélection) → `VisualState`,
   déjà en place, à généraliser.
2. **Transitions réelles** (ouverture de menu/panneau, bascule d'écran) →
   classe statique C# `MotoMotion` avec des durées nommées :

| Jeton | Durée | Usage |
|---|---|---|
| `Instant` | 80 ms | Retour d'appui |
| `Fast` | 120 ms | Survol, changement de couleur |
| `Normal` | 180 ms | Ouverture de menu, bascule de panneau |
| `Slow` | 260 ms | Superposition plein écran, modale |

Courbe standard : `Easing.CubicOut` en entrée, `Easing.CubicIn` en sortie.
**Règle** : aucune animation sur un chemin critique (frappe clavier,
défilement) — uniquement sur les surfaces.

---

## 5. États — la matrice à respecter partout

Aujourd'hui, `MotoHoverButton` définit `Normal / PointerOver / Pressed`
**et rien d'autre**. Absents : `Disabled` (un bouton inactif est
indiscernable) et `Focused` (un IDE au clavier sans anneau de focus). Seuls
2 éléments utilisent `MotoHoverRow`.

Tout style interactif devra définir :

| État | Traitement | Rôle |
|---|---|---|
| `Normal` | fond transparent | — |
| `PointerOver` | fond `BgHover` | feedback de survol |
| `Pressed` | fond plus foncé ou `AccentMuted` | feedback d'appui |
| **`Focused`** | **anneau 1 px `Accent`** (jeton `FocusRing`) | pilotage clavier |
| `Disabled` | opacité 0,4 · aucun survol | lisibilité de l'état |
| `Selected` | fond `BgHover` + barre 2 px `Accent` à gauche | listes, arborescence, onglets |

---

## 6. Bordures — une seule règle

Deux familles concurrentes aujourd'hui. Règle proposée :

| Cas | Jeton |
|---|---|
| Séparateur entre régions (hairline) | `DividerSoft` — blanc 10 % (ex-`BorderSoft`) |
| Contour de contrôle qui doit se voir (champ, bouton secondaire) | `BorderControl` = `#3A3B40` (ex-`BorderCol`) |
| Anneau de focus | `FocusRing` = `Accent` |
| Cadre de surface flottante | `DividerSoft` |

`BorderMuted` (doublon exact de `BorderSoft`) est fusionné.

---

## 7. Icônes — 4 tailles

Aucune taille d'icône normalisée aujourd'hui (les glyphes Segoe Fluent sont
posés en `FontSize` libre, ce qui explique une partie des 21 tailles).

| Jeton | Taille | Usage |
|---|---|---|
| `IconSm` | 14 | En ligne dans le texte |
| `IconMd` | 16 | Barres d'outils, listes (défaut) |
| `IconLg` | 20 | Navigation, en-têtes de panneau |
| `IconHero` | 24 | États vides, écrans d'accueil |

---

## 8. Élévation

3 jetons existent (`ShadowSm/Md/Lg`), **2 usages au total**. Principe déjà
écrit dans `MotoTheme.xaml` et à appliquer strictement (il vient de VS Code) :

- **Ombre uniquement sur les surfaces flottantes** : menus, menus déroulants,
  infobulles, modales, fenêtres spécialisées.
- **Jamais** sur le chrome permanent : barre de titre, panneaux ancrés,
  barre de statut, barre latérale.

---

## 9. Décisions à trancher

| # | Décision | Options | Recommandation |
|---|---|---|---|
| **D1** | Taille de base | 12 (fidèle) / **13 (moderne)** | **13** |
| **D2** | Accent | `#D97757` orange Claude (état actuel du code) / `#007ACC` bleu IDE | **`#007ACC`** — les références visées sont bleues, et l'orange reste disponible comme couleur de marque secondaire |
| **D3** | Police | `Segoe UI Variable` (actuel, 9 fichiers sur 71, absent de Windows 10) / `Segoe UI` (10 et 11) / **Inter embarquée** | **Inter embarquée** : cohérente partout, moderne, sous licence SIL OFL (redistribuable). ⚠️ Tension avec la doctrine « sans dépendance » — à arbitrer par Tom |
| **D4** | Interligne | activer `LineHeight` / laisser par défaut | **activer** sur les rôles ≥ 13 px |

---

## 10. Protocole de vérification (imposé par ce dépôt)

### Ligne de base mesurée (22/09, avant toute modification)

| Configuration | Résultat |
|---|---|
| `Debug` · `net8.0-windows10.0.19041.0` | **0 erreur · 479 avertissements** |
| `Release` · idem | **0 erreur · 479 avertissements** |

⚠️ **479 avertissements**, et non les « ~300 » annoncés par la présentation
projet (`Docs/Documents/Présentation_détaillée_du_projet.html`, §5) — la
dette de warnings est près de deux fois celle documentée. À retenir comme
référence : un lot visuel ne doit **pas** faire monter ce nombre.

Aucun test visuel automatisé n'existe. Donc, pour **chaque lot** :

1. Build `Debug` **et** `Release` à **0 erreur** (le raccourci Bureau pointe
   vers Release — piège documenté).
2. Aucun handler, aucune liaison, aucun `x:Name` modifié : un lot visuel ne
   change **que** des valeurs de style.
3. Un lot = un écran ou un composant = **un commit**, avec capture avant/
   après fournie par Tom.
4. Garde-fou spécifique : `MainPage.xaml` a un **historique de plantage sur
   changement de largeur de colonne** → toute modification de grille est
   testée isolément.

### Ordre proposé
`Phase 0` socle (ce document) → `Phase 1` conversion des 994 valeurs, par
lots → `Phase 2` composants → `Phase 3` écrans (Accueil en pilote) →
`Phase 4` mouvement.

---

## Références
- Diagnostic chiffré : `Docs/design/Audit-visuel-2026-09-22.md`
- Jetons actuels : `Moto.Editor/Themes/MotoTheme.xaml`
- Contrainte d'API : source MAUI 8.0.100 `src/Controls/src/Core/Label/Label.cs`
- Captures de référence : `Docs/inspirations/`, `Docs/images/`
