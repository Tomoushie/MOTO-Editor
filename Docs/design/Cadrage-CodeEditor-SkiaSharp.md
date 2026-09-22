# Cadrage — remplacer le WebView de CodeEditorView par un rendu SkiaSharp direct

> Écrit le 22/09, avant tout envoi à l'Orchestrator. Décision de lancer ce
> chantier : voir `CLAUDE.md` § "Stratégie de vitesse à terme" (décision de
> Tom du 22/09). Rien n'est encore codé — ce document sert à cadrer ce qui
> part à l'Orchestrator, pour relire un périmètre net plutôt qu'un gros pavé.

## 1. Ce qui existe réellement aujourd'hui (vérifié dans le code, pas supposé)

`Moto.Editor/Controls/CodeEditorView.xaml.cs` (306 lignes) n'héberge **pas**
Monaco ni CodeMirror : c'est un éditeur maison en HTML/JS embarqué dans un
`<WebView>`, très simple —

- Un `<textarea>` transparent superposé à un `<div>` qui affiche le HTML
  coloré (astuce classique : la coloration syntaxique est un rendu, pas une
  vraie saisie ; c'est le textarea invisible en dessous qui reçoit le
  clavier).
- Coloration syntaxique = **une seule regex** générique pseudo-C# (mots-clés
  `public/private/static/void/...`, chaînes, `//` commentaires, nombres) —
  appliquée telle quelle **quel que soit le langage du fichier ouvert**. Un
  fichier Python ou XAML reçoit la même regex C#.
- Gouttière = numéros de ligne en texte brut, pas de repliement de code.
- Mini-map = barres grises proportionnelles à la longueur de chaque ligne
  sur un `<canvas>`, pas un vrai aperçu du texte.
- Ghost text = un bandeau texte + Tab pour insérer (`SetGhost`), déjà câblé
  à une source IA externe (pas dans le périmètre de ce chantier).
- Pont C# ↔ JS : `EvaluateJavaScriptAsync` (C# → JS) et deux URLs sentinelles
  interceptées dans `Navigating` (`moto://changed`, `moto://sel`, JS → C#).
  Le contenu est poussé en **Base64** — contournement d'un vrai bug de
  `EvaluateJavaScriptAsync` sur WinUI qui échoue silencieusement dès qu'une
  chaîne contient un saut de ligne échappé (documenté dans le fichier,
  trouvé le 03/09).

## 2. Le contrat public à reproduire à l'identique

`EditorPaneView` (qui héberge `CodeEditorView` sous le nom `Editor`) n'est
qu'une délégation directe — **aucune logique intermédiaire** :

| Membre | Type | Rôle |
|---|---|---|
| `Text` | propriété bindable, two-way | Contenu du document |
| `FontSizeMode` | propriété bindable | Taille de police (réglage `buffer_font_size`) |
| `EditorChanged` | événement `EventHandler<string>` | Levé à chaque frappe |
| `GoToLine(int)` | méthode | Utilisé par le Navigation Assistant |
| `SetMinimapVisible(bool)` | méthode | Réglage `minimap_show` |
| `SetGhost(string)` | méthode | Suggestion IA (Pair Programming) |
| `GetSelectedText()` | méthode | Pour `/selection` du chat |

**Seuls 2 réglages vivants** touchent l'éditeur (`SettingsApplier.cs`) :
`buffer_font_size` et `minimap_show`. Le thème est toujours sombre (aucune
palette claire réelle n'existe — déjà su).

**Vérifié : aucun autre fichier du dépôt n'accède à `Web`/
`EvaluateJavaScriptAsync` de `CodeEditorView` directement.** Les overlays
qui auraient pu dépendre de coordonnées pixel de l'éditeur
(`RemoteCursorOverlay`, `InlayHintOverlay`, `BreakpointGutterOverlay`,
`GutterQuickActions`) ne le référencent pas — ils sont indépendants ou non
câblés. Périmètre du chantier : **2 fichiers seulement**
(`CodeEditorView.xaml` + `.xaml.cs`), zéro fichier appelant à modifier.

## 3. Le vrai coût — pas celui qu'on croit

Le rendu (dessiner du texte coloré avec SkiaSharp) est la partie **facile**.
Ce qu'un `<textarea>` de navigateur donne gratuitement aujourd'hui et qu'il
faudra **réécrire à la main** avec SkiaSharp (qui ne fait QUE dessiner, sans
aucune notion de texte éditable) :

- Saisie clavier + IME (accents, composition) — piège classique, et ce
  dépôt a déjà trouvé un bug d'encodage sournon sur ce pont WinUI (base64
  ci-dessus) : aucune raison de croire ce terrain plus simple ici.
- Curseur et sélection (souris : clic, glisser, double-clic mot, triple-clic
  ligne ; clavier : flèches, Maj+flèches, Ctrl+flèches).
- Presse-papiers (couper/copier/coller).
- Undo/redo — actuellement gratuit via l'historique natif du navigateur ;
  à reconstruire avec une vraie pile de commandes.
- Défilement (molette, barre de défilement, glisser la mini-map — déjà
  existant, à refaire).
- Mesure de texte précise pour convertir une position de clic en
  ligne/colonne (police à chasse fixe, donc calculable, mais à faire soi-même).

C'est exactement la même liste que Windows Terminal a dû refaire pour son
propre remplacement WebView-like → Direct2D (précédent cité dans
`CLAUDE.md`, gain ×2 à ×10 mesuré chez eux). Estimation confirmée : coût
moyen, semaines à quelques mois — pas quelques jours.

## 4. Ce qu'on NE corrige PAS pendant ce chantier (périmètre fermé)

Pour rester un chantier borné et testable, portage fidèle du comportement
actuel, **pas** une occasion d'améliorer au passage :
- Coloration syntaxique reste une seule regex générique (pas de grammaire
  par langage) — déjà limité aujourd'hui, ce chantier ne l'aggrave ni ne le
  corrige.
- Mini-map reste des barres, pas un vrai aperçu.
- Pas de repliement de code, pas de multi-curseur.

**Question ouverte pour Tom** (pas une obligation, une opportunité) :
puisqu'on va de toute façon écrire la gestion clavier nous-mêmes, l'absence
actuelle d'événement `KeyPressed` (qui bloquait le déclenchement Tab pour
les variantes IA, voir `MainPage.Extensions.cs`) disparaît naturellement.
À signaler comme bénéfice collatéral, pas à traiter comme un chantier à part.

## 5. Plan de bascule / rollback

1. Le contrôle actuel n'est pas touché tant que le nouveau n'est pas prêt.
2. Nouveau contrôle développé en parallèle (ex. `CodeEditorViewSkia`), même
   contrat public que la section 2.
3. Bascule = **2 points, pas un seul** (corrigé le 22/09 après l'avoir fait
   pour de vrai — l'affirmation "un seul point" ci-dessus était fausse) :
   le nom de classe utilisé dans `EditorPaneView.xaml`, ET le type du
   paramètre `editor` dans `SettingsApplier.ApplyAll`/`Subscribe`
   (`Settings/SettingsApplier.cs`), qui prend le type concret
   `CodeEditorView` en paramètre, pas une interface. Rollback = revenir aux
   2 anciens types, 2 fichiers.
4. Ancien `CodeEditorView` (WebView) supprimé seulement après confirmation
   manuelle de Tom sur la parité (frappe, undo, mini-map, ghost text,
   Navigation Assistant, `/selection`).

## 6. Découpage proposé pour l'Orchestrator (incréments testables)

Même méthode que le chantier visuel (dry-run → apply → build vert → commit
par lot), adaptée ici en incréments fonctionnels plutôt qu'en lots de fichiers :

1. **Rendu statique** : gouttière + texte coloré (même regex qu'aujourd'hui)
   dans un `SKCanvasView`, contenu réglé par `Text`, pas encore éditable.
2. **Saisie + curseur + sélection** clavier et souris.
3. **Undo/redo + presse-papiers.**
4. **Mini-map + ghost text** (parité avec l'existant).
5. **Bascule** dans `EditorPaneView.xaml` + suppression de l'ancien WebView
   une fois Tom confirmé la parité.

Chaque incrément : build Debug+Release vert, test manuel par Tom, commit
séparé — pas un seul gros commit final.

## 7. Options pour la suite (à trancher par Tom)

- **A. Tout envoyer d'un coup à l'Orchestrator** (les 5 incréments en une
  demande) — un seul gros verdict à relire, risque de tout re-générer si
  un point de détail cloche.
- **B. Envoyer incrément par incrément** (recommandé, cohérent avec la
  méthode déjà utilisée sur ce dépôt) — plus de tours, mais chaque lot
  reste petit à relire et à tester.
- **C. Ne lancer que l'incrément 1** (preuve visuelle) avant de décider si
  on va au bout — le plus prudent, permet de voir le rendu réel avant de
  s'engager sur les incréments 2-4 (les plus coûteux).

## 8. État au 22/09 — incrément 1 fait, en test visuel

Tom a choisi l'option C. `CodeEditorViewSkia` (`Moto.Editor/Controls/`)
existe, compile seul (commit `f973741`). 2 défauts réels trouvés en
relisant le brouillon de l'Orchestrator AVANT de l'intégrer (voir le
message de ce commit) — confirme qu'une relecture + un vrai build restent
indispensables, la validation isolée de `/generate-batch` (Roslyn hors
contexte projet) n'aurait vu ni l'un ni l'autre.

**Câblage de test EN COURS, PAS COMMITÉ** (2 fichiers modifiés dans l'arbre
de travail, à garder ou annuler selon le verdict de Tom) :
`EditorPaneView.xaml` (type de l'élément `Editor`) et
`Settings/SettingsApplier.cs` (type du paramètre `editor`, voir correction
§5 ci-dessus). App relancée avec ce câblage, aucune exception au démarrage
(journal Breadcrumb propre). Vérification visuelle par outil impossible
(limite connue de computer-use avec l'exe Debug lancé manuellement — voir
mémoire Claude) : Tom doit regarder son propre écran.

Écart cosmétique connu, pas bloquant pour ce test : un commentaire `// texte`
ne colore que le `//` lui-même, pas le reste de la ligne (la tokenisation
découpe par espaces avant de détecter le commentaire).
