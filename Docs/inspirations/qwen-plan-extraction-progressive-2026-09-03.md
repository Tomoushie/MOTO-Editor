# Plan d'extraction progressive — Maquette Claude → MainPage (Qwen, 03/09)

Reçu de Tom en toute fin de session (budget quasi épuisé), PAS ENCORE VÉRIFIÉ
ni compilé. À traiter au prochain budget avec la même discipline que le
reste de cette session : diagnostic avant d'appliquer, ne pas faire
confiance à "additif, risque nul" sans avoir essayé de compiler.

Réserves déjà repérées à la lecture, avant même de tenter une compilation :
- Chantier 1 : `<Button.Flyout><Flyout Placement="Top">` — `Flyout` n'est
  PAS une API MAUI cross-plateforme (confirmé ce soir même sur
  ClaudeShellView : `Button.Flyout`/`MenuFlyout` n'existent pas, remplacés
  par `DisplayActionSheet`). Même correctif à refaire ici.
- Chantier 1 : référence un type `local:ShortcutHintTrigger` qui n'existe
  nulle part dans le dépôt — à créer ou à retirer.
- Chantier 5 : `Grid ... Column="3"` — syntaxe XAML invalide, doit être
  `Grid.Column="3"`.
- Chantier 4 (accueil) : le fichier reçu duplique une bonne partie de ce
  qui existe déjà réellement dans `HomeView.xaml`/`RefreshHomeStats`
  (stats réelles déjà câblées, voir CLAUDE.md) — à fusionner, pas à
  écraser.

5 chantiers proposés par Qwen (par ordre de livraison suggéré) :
1. Menu utilisateur complet (email + 10 items + chip "Nouveau") — Haute
   valeur, Faible risque (nuancé ci-dessus).
2. Modale Feedback (Envoyer des commentaires) — Haute valeur, Faible
   risque.
3. Popover contexte (spinner cliquable → limites) — Moyenne valeur,
   Faible risque.
4. Accueil enrichi (tuiles stats + heatmap) — Haute valeur, risque Moyen
   (à fusionner avec l'existant, voir réserve ci-dessus).
5. Panneau Cowork Instructions/Mémoire/Contexte — Moyenne valeur, risque
   Moyen (le plus complexe, touche au layout).

Le code complet de chaque chantier (XAML + code-behind) a été fourni par
Tom dans le chat — à récupérer depuis l'historique de conversation au
moment de traiter ce plan, pas reproduit ici en entier pour rester bref.
