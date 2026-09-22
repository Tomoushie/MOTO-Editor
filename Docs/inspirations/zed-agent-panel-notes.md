# Notes sur zed-agent-panel-and-inline-transform.mp4 (fournie par Tom, 22/09)

Vidéo 1m50, capture d'écran de Zed en usage réel (pas une démo scénarisée).
11 images extraites toutes les 10s pour relecture (`ffmpeg -vf fps=1/10`),
pas de transcription audio faite. Deux éléments concrets, réutilisables :

## 1. Panneau Agent (barre latérale gauche)
- Liste de threads ("New Zed Agent Thread") en haut du panneau.
- Dans un thread : titre éditable, section **"Thinking" repliable** avant
  la réponse, puis les appels d'outils affichés comme des lignes distinctes
  ("List the BOT-X directory's contents").
- Composeur en bas : "Message the Zed Agent, @ to include context, / for
  commands", sélecteur de modèle juste au-dessus (ex. "qwen2.5-coder:7b").
- Vu sur 2 projets différents (MOTO-Editor lui-même, puis un autre dépôt
  "BOT-X") -- comportement cohérent d'un projet à l'autre.

**Rapprochement avec l'existant** : `ClaudeShellView.xaml` (déjà dans ce
dossier `inspirations/`) est le panneau IA déjà en place dans MOTO Editor --
cette vidéo est probablement à lire comme référence pour SON évolution, pas
une demande de nouveau panneau.

## 2. Barre "Transform" (édition inline)
- Sur un fichier ouvert (`benchmark_results.json`), une barre fine apparaît
  ANCRÉE dans l'éditeur (d'abord en haut à droite, puis pleine largeur sur
  la ligne 1) : "Transform... (Ctrl-Alt-I to chat -- ↑ for history, @ to
  include context)", avec son propre sélecteur de modèle et un bouton
  "Transform".
- Différence clé avec un panneau latéral : ancrée au CODE (ligne/sélection),
  pas dans une colonne séparée.

**Rapprochement avec l'existant** : `AiBand` dans `EditorPaneView.xaml`
("bandeau IA", Ligne 3 du Grid -- picker + Entry + bouton ➤) sert déjà ce
rôle mais en bandeau plein largeur permanent, pas en barre flottante ancrée
au code à la demande. Piste de convergence, pas encore décidée avec Tom.

## Non traité
Contenu audio non transcrit (pas d'outil de reconnaissance vocale
disponible dans cette session) -- si la vidéo contient une explication
orale plus précise que ce que montrent les images, elle n'est pas
capturée ici.
