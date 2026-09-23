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

## 9. Décision de Tom (22/09) — incrément 1 validé, suite lancée

3 bugs réels trouvés (espaces/ponctuation collés, gouttière vide, panneaux
IA empilés) et corrigés, plus le retour à la ligne ajouté en fonctionnalité
(demandé par Tom, hors périmètre "portage fidèle" initial). Vérifié sur
l'exe Debug ET sur le raccourci Bureau (Release) — les deux pointent vers
des dossiers de build différents, voir la règle §10 de
`Docs/design/Langage-visuel-spec.md`.

Tom confirme : « tout fonctionne ». Câblage de test (`EditorPaneView.xaml`,
`SettingsApplier.cs`) **rendu définitif** (plus de mention "temporaire").
Choisi parmi 3 options proposées : lancer l'incrément 2 (saisie clavier,
curseur, sélection souris) plutôt que de s'arrêter là ou revenir au WebView.

**Rappel important, à ne pas laisser croire le contraire à Tom** : le
rendu validé est **statique** -- `CodeEditorViewSkia` n'a encore aucune
saisie, aucun curseur, aucune sélection (`GoToLine`/`SetMinimapVisible`/
`SetGhost`/`GetSelectedText` sont des méthodes vides). L'incrément 2 est
le morceau le plus gros et le plus risqué du chantier (§3 ci-dessus) --
`CodeEditorView` (WebView) reste dans le dépôt, non supprimé, tant que la
parité n'est pas confirmée (§5 point 4).

## 10. Incrément 2 — architecture retenue et découpage

**Principe** (repris de l'ancien éditeur, §1 : un `<textarea>` invisible
recevait vraiment le clavier, le `<div>` visible n'était qu'un rendu) :
un contrôle MAUI `Editor` (multi-ligne) caché derrière le `SKCanvasView`
reçoit réellement la saisie clavier/IME/presse-papiers -- gratuit, déjà
fourni par MAUI, pas à réinventer. `CodeEditorViewSkia` lit son état
(`Text`, `CursorPosition`, `SelectionLength`) pour piloter le rendu
(texte, curseur clignotant, surbrillance de sélection).

**Vérifié empiriquement (build réel, pas une supposition)** contre le
MAUI 8.0.100 réellement résolu par ce projet : `Entry`/`Editor` exposent
bien `CursorPosition` (int, get/set) et `SelectionLength` (int, get/set).
0 erreur de compilation sur un test isolé le 22/09.

**Piège découvert pendant cette vérification** : le namespace racine du
projet s'appelle `Moto.Editor` -- à l'intérieur de `Moto.Editor.Controls`,
écrire `Editor` tout court résout vers le NAMESPACE `Moto.Editor`, pas
vers le contrôle MAUI (`CS0118`). Toute référence au contrôle doit être
qualifiée : `Microsoft.Maui.Controls.Editor`.

**Découpage en sous-étapes testables** (mécanique identique au reste du
chantier : petits lots, un build vert par lot) :
- **2a** (prochaine étape) : `Editor` MAUI caché ajouté au XAML, `Text`
  synchronisé dans les deux sens avec la propriété publique existante,
  le focus du canevas route vers ce contrôle cache -- **objectif : pouvoir
  taper du texte et le voir apparaître**, sans curseur ni sélection dessinés
  encore.
- 2b : curseur clignotant (position dessinée à partir de `CursorPosition`
  converti ligne/colonne, puis pixel -- doit tenir compte du retour à la
  ligne visuel ajouté au rendu, une ligne source pouvant occuper plusieurs
  lignes visuelles).
- 2c : surbrillance de sélection (`SelectionLength` + `CursorPosition`),
  même conversion, potentiellement sur plusieurs lignes visuelles.
- 2d : clic souris → position de curseur (conversion inverse pixel → index
  de caractère).
- 2e : glisser-déposer → sélection à la souris ; double-clic mot,
  triple-clic ligne (si simple, sinon reporté).

Chaque sous-étape : un envoi à l'Orchestrateur, relecture avant intégration
(même discipline qu'incrément 1), build vert, test réel par Tom, commit
séparé.

## 11. Constats mesurés sur l'architecture 2a — le champ natif ne tient pas (22-23/09)

Tout ce qui suit est MESURÉ (journal Breadcrumb + géométrie de mise en page
relevée par un diagnostic temporaire, non commité), sur `CLAUDE.md` : 112 Ko,
1811 lignes, ligne la plus longue 353 caractères. Le 23/09, l'ouverture du
fichier et le redimensionnement de la fenêtre (1790 → 1500 → 1300 → 1100 → 1790)
ont été rejoués automatiquement, sans intervention manuelle.

| Constat | Mesure | Cause | État |
|---|---|---|---|
| Ouverture lente | fil UI bloqué ~5-6 s (6,2 s puis 5,1 s sur deux essais) | `HiddenInput` reçoit tout le document ; le TextBox WinUI le met en page en entier | **NON corrigé** |
| Texte écrasé sur une seule ligne | écho tardif : `len=112379 lf=0 cr=1811` | le TextBox WinUI range/renvoie ses sauts de ligne en `\r` ; l'écho arrive après la retombée de `_syncInProgress` | corrigé (76eec22 : garde `IsFocused` + `NormalizeNewlines`) |
| Avertissements jaunes fantômes | emojis en couleur du champ natif, placés selon SA mise en page | WinUI les dessine malgré `TextColor` transparent | corrigé (76eec22 : `Opacity="0"`) |
| Explorateur poussé hors de la fenêtre | fenêtre 1500 : explorateur à x=1484 (hors du visible), panneau éditeur = toute la largeur ; `DesiredSize.Width` de `HiddenInput` = largeur offerte (1484, puis 1284, puis 1084) | un TextBox dont le texte remplit la ligne réclame toute la largeur qu'on lui offre ; la colonne `*` du Grid MAUI ne descend pas sous cette demande, donc la colonne `Auto` de l'explorateur est repoussée. Absent avec l'écran d'accueil seul (`HomeView` dw=640). Signalé par Tom | **NON corrigé** |
| Rendu coûteux | 15-33 ms par repeinte (126 ms la première), pour 1812 lignes sources | toutes les lignes sont tokenisées et dessinées à chaque clignotement du curseur (530 ms), même hors écran ; pas de découpage par zone visible | **NON corrigé** |
| Pas de défilement | le canevas ne montre que la première page | jamais prévu au découpage du §6 (oubli) ; le WebView défilait nativement | **NON traité** |

**Conclusion** : les trois premiers défauts sont des symptômes du même choix —
faire porter le DOCUMENT ENTIER par un contrôle natif dont on ne maîtrise ni la
mise en page ni la vitesse. Piste recommandée (à valider par Tom) : le contrôle
possède le document (texte, curseur, sélection, défilement) ; le champ caché
ne reste qu'un capteur clavier, quasi vide, donc sans coût ni influence sur la
mise en page. Il faudra alors écrire les touches de navigation/édition
(flèches, début/fin, page, suppression, entrée, tabulation, Ctrl+A/C/X/V),
prévoir les touches mortes/IME (test AZERTY par Tom), et découper le rendu par
zone visible avec défilement — ce qui prépare aussi 2c-2e.

Piège technique retenu : pour reproduire un problème de mise en page sans
intervenir à la main, ouvrir un fichier au démarrage via une variable
d'environnement (crochet temporaire dans `MainPage.OnPageLoaded`) et
redimensionner la fenêtre par `SetWindowPos` depuis `powershell.exe` — la
fenêtre lancée depuis un shell démarre réduite, `ShowWindow(h, 9)` la
restaure d'abord.
