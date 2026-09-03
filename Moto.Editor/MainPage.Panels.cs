// Moto.Editor/MainPage.Panels.cs (v29 — extraction des panneaux IA)
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.ApplicationModel;
using Moto.Core.AI.Cortex;
using Moto.Core.AI.Neural;
using Moto.Core.AI.Workspace;
using Moto.Core.Doc;
using Moto.Core.Settings;
using Moto.Editor.Views;

namespace Moto.Editor
{
    /// <summary>
    /// Partial class : gestion des panneaux IA (Cortex, Neural, Workspace, Galerie, Analytics).
    /// </summary>
    public partial class MainPage
    {
        // ------------------------------------------------------------------
        // Panneaux IA v3
        // ------------------------------------------------------------------
        private void WireAiPanels()
        {
            _workspacePanel.ApplyRequested += OnWorkspaceApply;
            _neuralPanel.CodeGenerated += code =>
            {
                var doc = _viewModel.SelectedDocument;
                if (doc != null)
                {
                    doc.Text = doc.Text + "\n\n" + code;
                    EditorPane.EditorText = doc.Text;
                }
            };
            _cortexPanel.ModeChanged += mode =>
            {
                SettingsEngine.Shared.Set("cortex_mode", mode.ToString());
                StatusBar.SetStatus($"🧠 Mode Cortex : {mode}");
            };
        }

        /// <summary>
        /// ★ AJOUT (03/09, nettoyage — sonde de modularité, Gap D) : les 4
        /// méthodes ci-dessous recopiaient chacune la même liste "masquer les
        /// autres panneaux du groupe" — un panneau oublié dans une seule des 4
        /// listes aurait pu rester affiché en même temps qu'un autre sans que
        /// rien ne le signale. Une seule liste ici : un futur panneau ajouté à
        /// ce groupe (Cortex/Neural/Workspace/Gallery/Analytics) n'a plus qu'un
        /// seul endroit à toucher. Comportement identique à avant : bascule le
        /// panneau demandé (ouvre si fermé, ferme si déjà ouvert) après avoir
        /// masqué tous les autres.
        /// </summary>
        private void ShowOnlyAiGroupPanel(ContentView panel)
        {
            bool willOpen = !panel.IsVisible;

            foreach (var p in new ContentView[] { _cortexPanel, _neuralPanel, _workspacePanel, _pluginGallery, _analyticsDashboard })
                p.IsVisible = false;

            panel.IsVisible = willOpen;
            RefreshAiDockColumnWidth();
        }

        private void OnCortexClicked(object sender, EventArgs e)
        {
            ShowOnlyAiGroupPanel(_cortexPanel);
            if (_cortexPanel.IsVisible && _viewModel.SelectedDocument != null)
                _cortexPanel.LoadSuggestions(_viewModel.SelectedDocument.Path, _viewModel.SelectedDocument.Text);
        }

        private void OnNeuralClicked(object sender, EventArgs e) => ShowOnlyAiGroupPanel(_neuralPanel);

        private void OnWorkspaceClicked(object sender, EventArgs e)
        {
            ShowOnlyAiGroupPanel(_workspacePanel);
            if (_workspacePanel.IsVisible) _workspacePanel.Analyze();
        }

        private void OnGalleryClicked()
        {
            ShowOnlyAiGroupPanel(_pluginGallery);
            if (_pluginGallery.IsVisible) _pluginGallery.LoadGallery();
        }

        /// <summary>
        /// ★ AJOUT (30/08, refonte Zen) : AiDockPanel (colonne 0 — Cortex/Neural/
        /// Workspace/Gallery/Analytics/Debug/Platform) est masqué par défaut (voir
        /// MainPage.xaml) pour éviter la "zone noire" toujours visible même vide,
        /// repérée par Tom. Sa colonne ("Auto") se replie donc à 0 automatiquement
        /// tant qu'il est masqué. Ré-affiché ici dès qu'au moins un des panneaux
        /// qu'il héberge est visible.
        /// </summary>
        private void RefreshAiDockColumnWidth()
        {
            // ★ CORRECTION (31/08, point 16) : _searchPanel retiré de ce calcul —
            // Recherche vit maintenant en superposition centrée, plus dans ce dock
            // (voir AddFloatingPanel, asCenteredOverlay). La laisser ici aurait rouvert
            // inutilement la colonne de gauche (vide) à chaque recherche.
            // ★ CORRECTION (01/09, revue croisée — régression trouvée) : la liste
            // figée de champs (_platformPanel.IsVisible || _cortexPanel.IsVisible ||
            // ...) ignorait qu'un panneau glissé vers PanelHostRight (chantier
            // "glisser-déposer entre docks") reste IsVisible=true sur son PROPRE
            // champ tout en ayant quitté PanelHost — "any" restait donc vrai après
            // un déplacement vers la droite, gardant le dock IA affiché (vide,
            // 500px) à côté du contenu correctement affiché à droite. Vérifier les
            // enfants RÉELLEMENT présents dans PanelHost (même patron que
            // RefreshExplorerPanelHostVisibility) règle ça sans liste à maintenir.
            // ★ RETRAIT (02/09) : AiHost.IsVisible retiré (stub supprimé, remplacé
            // par _aiChatPanel — déjà couvert par le check PanelHost.Children
            // ci-dessous, comme Cortex/Neural/Workspace).
            // ★ RETRAIT (02/09, état des lieux) : ChatHost/ThreadHost.IsVisible
            // retirés du calcul — ces 2 étiquettes mortes ("💬 Chat"/"🧵 Threads",
            // jamais reliées à un vrai contenu) repassaient à visible=true après un
            // simple cycle Maximiser/Restaurer (OnMaximizeToggled) et pouvaient à
            // elles seules garder ce dock ouvert en affichant 2 labels inertes.
            // Stubs entièrement supprimés (MainPage.xaml + OnMaximizeToggled).
            bool any = PanelHost.Children.Any(c => c is Border b && b.IsVisible);
            AiDockPanel.IsVisible = any;
            // ★ AJOUT (01/09) : même patron que RefreshExplorerHandleVisibility —
            // la poignée d'étirement du dock IA ne doit apparaître (et réagir) que
            // quand le dock est réellement affiché.
            AiDockResizeHandle.IsVisible = any;
            // ★ AJOUT (01/09, glisser-déposer entre docks) : un panneau migré vers
            // PanelHostRight peut être cause de rien ci-dessus (il n'est plus dans
            // PanelHost, donc "any" peut être false même s'il est affiché à droite)
            // — reflet indépendant, jamais mélangé au calcul ci-dessus.
            RefreshExplorerPanelHostVisibility();
        }

        /// <summary>
        /// ★ AJOUT (01/09, chantier "panneaux modulaires" — 4e étape) : symétrique
        /// de RefreshAiDockColumnWidth, mais pour la zone de panneaux migrés sous
        /// l'explorateur (PanelHostRight) — n'affiche cette zone que si au moins
        /// un des panneaux qui s'y trouvent est actuellement visible (sinon la
        /// ligne "Auto" qui la contient se replie à 0, même patron IsVisible déjà
        /// établi partout dans ce fichier).
        /// </summary>
        private void RefreshExplorerPanelHostVisibility()
        {
            ExplorerPanelHostWrapper.IsVisible = PanelHostRight.Children.Any(c => c is Border b && b.IsVisible);
        }

        // ------------------------------------------------------------------
        // ★ AJOUT (31/08, point 7) : étirement de l'explorateur/sidebar à la souris
        // ------------------------------------------------------------------
        private double _explorerStartWidth;

        /// <summary>
        /// Appelé partout où ExplorerPanel/Sidebar.IsVisible change, pour que la
        /// poignée d'étirement n'apparaisse (et ne réagisse) que quand l'un des deux
        /// est réellement affiché — même patron que RefreshAiDockColumnWidth.
        /// </summary>
        private void RefreshExplorerHandleVisibility()
        {
            ExplorerResizeHandle.IsVisible = ExplorerPanel.IsVisible || Sidebar.IsVisible;
        }

        /// <summary>
        /// Redimensionne ExplorerPanel ET Sidebar ensemble (même largeur, qu'ils
        /// soient visibles ou non tous les deux) — sinon changer de panneau
        /// (Fichiers/Sessions) ferait sauter la largeur de la colonne à chaque fois.
        /// Glisser vers la GAUCHE (TotalX négatif) doit AGRANDIR le panneau
        /// (il est sur le bord droit de la fenêtre) : d'où le signe "moins".
        /// ★ CORRECTION (01/09, "changer de côté") : le signe dépend maintenant du
        /// bord ACTUEL (_panelsSwapped), pas d'une identité fixe — une fois les
        /// panneaux inversés, l'explorateur se retrouve à GAUCHE et c'est glisser
        /// vers la DROITE qui l'agrandit (signe opposé).
        /// ★ CORRECTION (01/09, revue croisée) : le signe est désormais capturé une
        /// fois pour toutes au Started (comme la largeur de départ l'était déjà),
        /// au lieu d'être relu en direct à chaque Running. Aujourd'hui rien ne
        /// permet d'inverser les panneaux EN PLEIN GLISSER (le "capture" natif du
        /// pointeur pendant un drag bloque implicitement le menu ⚙) — mais si
        /// "panellayout" gagne un jour un raccourci clavier ou une autre entrée
        /// sans capture exclusive, relire _panelsSwapped en direct aurait fait
        /// sauter la largeur en plein milieu du geste (TotalX est cumulatif depuis
        /// Started, pas incrémental). Capturer le signe rend cette garantie
        /// explicite dans le code plutôt que de reposer sur un comportement
        /// plateforme implicite.
        /// </summary>
        private void OnExplorerResizePanUpdated(object sender, PanUpdatedEventArgs e)
        {
            switch (e.StatusType)
            {
                case GestureStatus.Started:
                    _explorerStartWidth = ExplorerPanel.WidthRequest > 0 ? ExplorerPanel.WidthRequest : 260;
                    _explorerStartSign = _panelsSwapped ? 1 : -1;
                    break;
                case GestureStatus.Running:
                    var newWidth = Math.Clamp(_explorerStartWidth + _explorerStartSign * e.TotalX, 180, 640);
                    ExplorerPanel.WidthRequest = newWidth;
                    Sidebar.WidthRequest = newWidth;
                    break;
            }
        }

        // ------------------------------------------------------------------
        // ★ AJOUT (01/09, chantier "panneaux modulaires" — 1re étape) : même
        // étirement à la souris, généralisé au dock IA (colonne 0, à gauche).
        // ------------------------------------------------------------------
        private double _aiDockStartWidth;
        private double _explorerStartSign;
        private double _aiDockStartSign;

        /// <summary>
        /// Le dock IA est à GAUCHE et sa poignée est sur son bord DROIT (contraire
        /// de l'explorateur) : glisser vers la DROITE (TotalX positif) doit donc
        /// AGRANDIR le panneau — signe opposé à OnExplorerResizePanUpdated.
        /// ★ CORRECTION (01/09, "changer de côté") : même remarque que ci-dessus,
        /// le signe suit _panelsSwapped plutôt qu'une identité fixe.
        /// ★ CORRECTION (01/09, revue croisée) : signe capturé au Started, même
        /// raison que OnExplorerResizePanUpdated ci-dessus.
        /// </summary>
        private void OnAiDockResizePanUpdated(object sender, PanUpdatedEventArgs e)
        {
            switch (e.StatusType)
            {
                case GestureStatus.Started:
                    _aiDockStartWidth = AiDockPanel.WidthRequest > 0 ? AiDockPanel.WidthRequest : 500;
                    _aiDockStartSign = _panelsSwapped ? -1 : 1;
                    break;
                case GestureStatus.Running:
                    var newWidth = Math.Clamp(_aiDockStartWidth + _aiDockStartSign * e.TotalX, 280, 700);
                    AiDockPanel.WidthRequest = newWidth;
                    break;
            }
        }

        // ------------------------------------------------------------------
        // ★ AJOUT (01/09, chantier "panneaux modulaires" — dock du bas) : même
        // étirement à la souris, généralisé au Terminal (dock du bas). Verticale
        // cette fois (hauteur), pas horizontale.
        // ------------------------------------------------------------------
        private double _bottomDockStartHeight;

        /// <summary>
        /// La poignée est sur le bord HAUT du dock (VerticalOptions="Start" dans
        /// MainPage.xaml) : glisser vers le HAUT (TotalY négatif) doit AGRANDIR
        /// le panneau (il "pousse" son bord haut plus loin de son bord bas fixe)
        /// — même logique que OnExplorerResizePanUpdated (poignée sur le bord
        /// GAUCHE, glisser vers la GAUCHE agrandit), transposée à la verticale.
        /// ★ CORRECTION (01/09, revue croisée) : plafond réduit 500→360 — au-delà,
        /// combiné à une fenêtre réduite à sa taille minimale (voir App.xaml.cs,
        /// window.MinimumHeight), la ligne "*" du contenu principal (Accueil/
        /// Éditeur/Explorateur) aurait pu être écrasée jusqu'à (quasi) 0px et
        /// reproduire le crash WinRT déjà documenté (CollectionView/ItemsRepeater
        /// arrangée dans un rectangle dégénéré) — cette fois via la famine
        /// naturelle d'une ligne "*" plutôt que via un GridLength à 0 posé par
        /// code, mais la même classe de plantage.
        /// </summary>
        private void OnBottomDockResizePanUpdated(object sender, PanUpdatedEventArgs e)
        {
            switch (e.StatusType)
            {
                case GestureStatus.Started:
                    _bottomDockStartHeight = TerminalPanel.HeightRequest > 0 ? TerminalPanel.HeightRequest : 220;
                    break;
                case GestureStatus.Running:
                    var newHeight = Math.Clamp(_bottomDockStartHeight - e.TotalY, 120, 360);
                    TerminalPanel.HeightRequest = newHeight;
                    break;
            }
        }

        // ------------------------------------------------------------------
        // ★ AJOUT (01/09, "changer de côté") : ligne "Disposition des panneaux"
        // du menu ⚙ (GearMenuView, id "panellayout" — voir MainPage.Routing.cs,
        // qui affichait jusqu'ici "Pas encore disponible."). Échange le dock IA
        // et l'explorateur/sidebar de côté d'un coup, sans glisser-déposer à la
        // souris — portée V2 du chantier "panneaux modulaires" choisie par Tom
        // parmi 3 options (le glisser-déposer libre façon Zed reste hors scope,
        // chantier séparé plus gros).
        // ------------------------------------------------------------------
        private bool _panelsSwapped;

        /// <summary>
        /// Place le dock IA et l'explorateur/sidebar (+ leurs poignées
        /// d'étirement) dans les colonnes correspondant à l'état actuel de
        /// _panelsSwapped. Ne touche JAMAIS RootGrid.ColumnDefinitions (colonnes 0
        /// et 2 restent "Auto" en XAML, comme documenté en tête de MainPage.xaml)
        /// — seulement Grid.Column sur les éléments eux-mêmes, même patron déjà
        /// utilisé ailleurs dans ce fichier (ex. _infoOverlay dans
        /// InitializeInfoOverlayAndUpdates), pour éviter le crash WinRT
        /// documenté (une colonne mesurant exactement 0px ne doit jamais être
        /// arrangée directement). Chaque poignée est réancrée sur le bord
        /// adjacent à la colonne centrale, quel que soit le côté où elle se
        /// trouve désormais.
        /// ★ CORRECTION (01/09, revue croisée — régression trouvée) : ciblait
        /// ExplorerPanel/Sidebar directement, ce qui était correct tant qu'ils
        /// étaient des enfants DIRECTS de RootGrid. Depuis leur imbrication dans
        /// ExplorerDockPanel (chantier "glisser-déposer entre docks", même
        /// session), leur Grid.Column ne veut plus rien dire pour RootGrid — c'est
        /// ExplorerDockPanel lui-même qu'il faut déplacer. Sans ce correctif, le
        /// bouton "changer de côté" déjà livré et confirmé par Tom aurait
        /// superposé le dock IA et le dock explorateur dans la même colonne dès
        /// le premier clic (trouvé par une revue croisée AVANT tout test manuel).
        /// </summary>
        private void ApplySidePanelLayout()
        {
            int aiColumn = _panelsSwapped ? 2 : 0;
            int explorerColumn = _panelsSwapped ? 0 : 2;

            Grid.SetColumn(AiDockPanel, aiColumn);
            Grid.SetColumn(AiDockResizeHandle, aiColumn);
            Grid.SetColumn(ExplorerDockPanel, explorerColumn);
            Grid.SetColumn(ExplorerResizeHandle, explorerColumn);

            AiDockResizeHandle.HorizontalOptions = _panelsSwapped ? LayoutOptions.Start : LayoutOptions.End;
            ExplorerResizeHandle.HorizontalOptions = _panelsSwapped ? LayoutOptions.End : LayoutOptions.Start;
        }

        private void OnWorkspaceApply(Moto.Core.AI.Workspace.WorkspaceSuggestion suggestion)
        {
            if (!string.IsNullOrWhiteSpace(suggestion.FilePath) && File.Exists(suggestion.FilePath))
            {
                _viewModel.OpenFilePath(suggestion.FilePath);
                if (suggestion.Line > 0)
                    EditorPane.GoToLine(suggestion.Line);
                StatusBar.SetStatus($"🏗 {suggestion.Title}");
            }
            else
            {
                StatusBar.SetStatus($"💡 {suggestion.Title}");
            }
        }

        /// <summary>Titre affiché dans l'en-tête de chaque panneau ancré (PanelHost).</summary>
        private static string TitleFor(ContentView panel) => panel switch
        {
            PlatformView => "🖥️ Plateforme",
            CortexView => "🧠 Cortex",
            NeuralView => "🤖 Neural",
            AiChatView => "💬 MOTO AI",
            AIWorkspaceView => "🧩 Workspace",
            PluginGalleryView => "🧱 Plugins",
            AnalyticsDashboardView => "📊 Analytics",
            DebugPanelView => "🐞 Debug",
            Views.SearchView => "🔍 Recherche",
            _ => panel.GetType().Name
        };

        /// <summary>
        /// ★ AJOUT (03/09, "détacher un panneau" — sonde de modularité, Gap B) :
        /// identifiant attendu par OpenSpecializedWindow (MainPage.Extensions.cs)
        /// pour chaque type de panneau. Null pour Recherche (overlay centré, pas
        /// de fenêtre spécialisée pour elle) — AddFloatingPanel n'affiche alors
        /// pas de bouton "détacher".
        /// </summary>
        private static string? KindFor(ContentView panel) => panel switch
        {
            PlatformView => "platform",
            CortexView => "cortex",
            NeuralView => "neural",
            AiChatView => "aichat",
            AIWorkspaceView => "workspace",
            PluginGalleryView => "plugin",
            AnalyticsDashboardView => "analytics",
            DebugPanelView => "debug",
            _ => null
        };

        /// <summary>
        /// ★ CORRECTION (30/08) : les panneaux flottaient tous au même endroit
        /// (par-dessus le contenu, même marge) et se superposaient entre eux — repéré
        /// par Tom. Ancrés maintenant dans PanelHost (colonne 3, déjà existante), un
        /// par un, avec un en-tête titré + bouton ✕ pour fermer (aucun panneau flottant
        /// de ce projet n'avait de bouton fermer jusqu'ici). La visibilité de l'en-tête
        /// suit automatiquement celle du panneau (liaison IsVisible) : tout le code
        /// existant qui fait `_xPanel.IsVisible = ...` continue de fonctionner tel quel.
        /// </summary>
        /// <summary>
        /// ★ CORRECTION (31/08, point 16) : "asCenteredOverlay" ajouté — Tom veut que
        /// Recherche s'ouvre en superposition centrée dans la fenêtre principale,
        /// pas dans le dock IA à gauche comme les autres panneaux de cette liste
        /// (Neural/Workspace/Gallery/Analytics/Debug restent inchangés, à gauche).
        /// Même en-tête (titre + ✕), juste un parent différent.
        /// </summary>
        /// <param name="preferRightHost">
        /// ★ AJOUT (01/09, revue croisée — régression trouvée) : quand un panneau
        /// EXISTANT est reconstruit à neuf (RebindPanels, plus bas — Cortex/
        /// Neural/Workspace, reconstruits à chaque ouverture de projet avec le
        /// vrai moteur), la nouvelle instance atterrissait TOUJOURS dans PanelHost
        /// (gauche), même si l'utilisateur avait déplacé l'ancienne vers
        /// PanelHostRight (droite) — son choix de placement était silencieusement
        /// annulé sans message à chaque ouverture de dossier. RebindPanels capture
        /// maintenant l'hôte de l'ancien wrapper AVANT de le retirer et le
        /// retransmet ici pour que le remplaçant réapparaisse au même endroit.
        /// </param>
        private void AddFloatingPanel(ContentView panel, bool asCenteredOverlay = false, bool preferRightHost = false)
        {
            var close = new Button
            {
                Text = "✕", WidthRequest = 28, HeightRequest = 24, FontSize = 12,
                Padding = 0, BackgroundColor = Colors.Transparent,
                TextColor = (Color)Application.Current!.Resources["Txt2"]
            };
            close.Clicked += (s, e) => panel.IsVisible = false;

            var header = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto) } };
            header.Add(new Label
            {
                Text = TitleFor(panel), FontSize = 13, FontAttributes = FontAttributes.Bold,
                VerticalOptions = LayoutOptions.Center,
                TextColor = (Color)Application.Current!.Resources["Txt1"]
            });

            // ★ AJOUT (03/09, "détacher un panneau" — sonde de modularité, Gap B) :
            // sort ce panneau dans sa propre fenêtre OS via WindowManager/
            // OpenSpecializedWindow — plomberie déjà existante et fonctionnelle
            // (jusqu'ici accessible uniquement par la commande cachée
            // "/window <kind>" tapée dans la barre IA, aucun bouton n'y menait).
            // Ouvre une INSTANCE FRAÎCHE du même type de panneau dans la nouvelle
            // fenêtre (pas littéralement celle-ci déplacée) — même limite déjà
            // documentée pour "/window", pas un vrai "glisser l'onglet hors de la
            // fenêtre" (chantier séparé, plus gros). Absent pour Recherche
            // (KindFor retourne null pour l'overlay centré, pas de fenêtre
            // spécialisée équivalente).
            var kind = KindFor(panel);
            if (kind != null)
            {
                var detach = new Button
                {
                    Text = "⧉", WidthRequest = 28, HeightRequest = 24, FontSize = 12,
                    Padding = 0, BackgroundColor = Colors.Transparent,
                    TextColor = (Color)Application.Current!.Resources["Txt2"]
                };
                ToolTipProperties.SetText(detach, "Détacher dans une nouvelle fenêtre");
                detach.Clicked += (s, e) => OpenSpecializedWindow(kind);
                header.Add(detach, 1);
            }

            header.Add(close, 2);

            var wrapper = new Border
            {
                Stroke = (Color)Application.Current!.Resources["BorderCol"],
                BackgroundColor = (Color)Application.Current!.Resources["BgPanel"],
                StrokeShape = new RoundRectangle { CornerRadius = 8 },
                Padding = 8,
                Content = new VerticalStackLayout { Spacing = 6, Children = { header, panel } }
            };
            wrapper.SetBinding(IsVisibleProperty, new Binding(nameof(IsVisible), source: panel));

            panel.IsVisible = false;

            // ★ AJOUT (01/09, chantier "panneaux modulaires" — 3e puis 4e étape,
            // glisser-déposer) : réordonner les panneaux du dock IA en les glissant
            // (3e étape), puis les déplacer vers l'AUTRE dock (4e étape, voir
            // OnPanelDroppedOn/FindPanelWrapper/RemoveFromCurrentHost plus bas —
            // généralisés pour chercher/retirer dans PanelHost ET PanelHostRight).
            // Même mécanisme (DragGestureRecognizer/DropGestureRecognizer +
            // DataPackage.Properties) déjà éprouvé et fonctionnel dans
            // SidebarView.xaml.cs (réordonnancement des sessions de chat) — pas
            // réinventé. Ne s'applique pas à Recherche (asCenteredOverlay) : cette
            // vue n'est pas dans la liste réordonnable/déplaçable.
            if (!asCenteredOverlay)
            {
                wrapper.ClassId = TitleFor(panel);

                var drag = new DragGestureRecognizer();
                drag.DragStarting += (s, e) => e.Data.Properties["panelClassId"] = wrapper.ClassId;
                header.GestureRecognizers.Add(drag);

                var drop = new DropGestureRecognizer();
                drop.Drop += (s, e) => OnPanelDroppedOn(wrapper, e);
                wrapper.GestureRecognizers.Add(drop);
            }

            if (asCenteredOverlay)
            {
                // ★ CORRECTION (31/08, point 12) : Column="1" (colonne centrale) plutôt
                // que ColumnSpan="3" — Tom trouvait la Recherche "pas totalement au
                // milieu". Avec ColumnSpan="3", le centre calculé inclut les colonnes
                // 0/2 (dock IA / explorateur), qui ne font PAS toujours 0px (dès qu'un
                // dossier est ouvert, l'explorateur a une vraie largeur) — le "milieu"
                // se décale alors visiblement du vrai centre de la fenêtre. Même
                // correctif déjà appliqué à AiBar/CollabPanel plus tôt cette session,
                // pour exactement la même raison.
                Grid.SetRow(wrapper, 2);
                Grid.SetColumn(wrapper, 1);
                wrapper.HorizontalOptions = LayoutOptions.Center;
                wrapper.VerticalOptions = LayoutOptions.Center;
                RootGrid.Children.Add(wrapper);
            }
            else
            {
                (preferRightHost ? PanelHostRight : PanelHost).Children.Add(wrapper);
            }
        }

        /// <summary>
        /// Déplace le wrapper (Border) dont le ClassId correspond au panneau glissé
        /// juste avant <paramref name="target"/> — dans PanelHost OU PanelHostRight,
        /// quel que soit l'hôte où se trouve ACTUELLEMENT target (permet donc de
        /// déplacer un panneau d'un dock à l'autre en le lâchant sur un panneau
        /// déjà présent là-bas, pas seulement de le réordonner sur place). Appelé
        /// par le DropGestureRecognizer de chaque wrapper (voir AddFloatingPanel).
        /// Retire TOUJOURS la source avant de chercher l'index cible : IndexOf(target)
        /// reste valide après coup (target est une référence d'objet, pas une
        /// position) et évite tout calcul de décalage d'index à la main, que source
        /// et target partagent le même hôte ou non.
        /// </summary>
        private void OnPanelDroppedOn(Border target, DropEventArgs e)
        {
            e.Handled = true;
            var draggedId = e.Data.Properties.TryGetValue("panelClassId", out var v) ? v as string : null;
            if (string.IsNullOrEmpty(draggedId))
                return;

            var source = FindPanelWrapper(draggedId);
            if (source == null || source == target)
                return;

            var targetHost = PanelHostRight.Children.Contains(target) ? PanelHostRight.Children : PanelHost.Children;
            RemoveFromCurrentHost(source);
            targetHost.Insert(targetHost.IndexOf(target), source);
            RefreshAiDockColumnWidth();
        }

        /// <summary>
        /// ★ AJOUT (01/09, chantier "panneaux modulaires" — 4e étape) : lâcher un
        /// panneau sur une zone VIDE (pas sur un wrapper existant) — nécessaire
        /// pour le tout premier panneau déplacé vers un hôte encore vide (sinon
        /// aucun wrapper n'existe là-bas pour recevoir le drop). Ajouté à la fin
        /// de l'hôte cible. Câblé sur AiDockPanel et ExplorerDockPanel eux-mêmes
        /// (voir WirePanelHostDropZones, appelée une seule fois) plutôt que sur
        /// PanelHost/PanelHostRight directement, pour que la zone de drop reste
        /// valide même quand la liste est vide et donc visuellement minuscule.
        /// </summary>
        private void OnEmptyHostDropped(IList<IView> targetHost, DropEventArgs e)
        {
            e.Handled = true;
            var draggedId = e.Data.Properties.TryGetValue("panelClassId", out var v) ? v as string : null;
            if (string.IsNullOrEmpty(draggedId))
                return;

            var source = FindPanelWrapper(draggedId);
            if (source == null)
                return;

            RemoveFromCurrentHost(source);
            targetHost.Add(source);
            RefreshAiDockColumnWidth();
        }

        /// <summary>Cherche le wrapper (Border) d'un panneau par son ClassId, dans
        /// PanelHost ou PanelHostRight — quel que soit l'hôte où il se trouve.</summary>
        private Border FindPanelWrapper(string classId)
        {
            foreach (var child in PanelHost.Children)
                if (child is Border b && b.ClassId == classId) return b;
            foreach (var child in PanelHostRight.Children)
                if (child is Border b && b.ClassId == classId) return b;
            return null;
        }

        /// <summary>Retire un wrapper de QUEL QUE SOIT l'hôte où il se trouve —
        /// Remove() sur une liste qui ne contient pas l'élément est un no-op, donc
        /// pas besoin de savoir lequel des deux avant d'appeler.</summary>
        private void RemoveFromCurrentHost(Border wrapper)
        {
            PanelHost.Children.Remove(wrapper);
            PanelHostRight.Children.Remove(wrapper);
        }

        /// <summary>
        /// ★ AJOUT (01/09, chantier "panneaux modulaires" — 4e étape) : zones de
        /// drop "hôte vide" — voir OnEmptyHostDropped. Appelée UNE SEULE FOIS
        /// (juste après le foreach qui construit les 7 panneaux du dock IA dans
        /// WirePanels, MainPage.xaml.cs) : câbler ceci DANS AddFloatingPanel
        /// l'aurait répété une fois par panneau (7 gestionnaires identiques sur le
        /// même élément, drop traité 7 fois).
        /// </summary>
        private void WirePanelHostDropZones()
        {
            var dropOnAiDock = new DropGestureRecognizer();
            dropOnAiDock.Drop += (s, e) => OnEmptyHostDropped(PanelHost.Children, e);
            AiDockPanel.GestureRecognizers.Add(dropOnAiDock);

            var dropOnExplorerDock = new DropGestureRecognizer();
            dropOnExplorerDock.Drop += (s, e) => OnEmptyHostDropped(PanelHostRight.Children, e);
            ExplorerDockPanel.GestureRecognizers.Add(dropOnExplorerDock);
        }

        // ------------------------------------------------------------------
        // LoadWorkspace
        // ------------------------------------------------------------------
        private void LoadWorkspace(string path)
        {
            // ★ AJOUT (02/09, persistance de session) : mémorise ce dossier comme
            // "dernier projet ouvert" pour le rouvrir automatiquement au prochain
            // lancement (voir OnPageLoaded, MainPage.xaml.cs). LoadWorkspace est LE
            // seul point d'entrée réel d'un import de projet (chip "Projet
            // logiciel", bouton Importer, commande "/cd" — voir MainPage.UI.cs et
            // MainPage.Routing.cs) ; le mode Sandbox appelle ExplorerPanel.LoadFolder
            // directement, jamais LoadWorkspace, donc son dossier temporaire jeté en
            // fin de session n'est jamais mémorisé ici par construction.
            SettingsEngine.Shared.Set("workspace.last_folder", path);

            _currentRoot = path;
            _chatService.WorkspaceRoot = path;
            _aiService.SetWorkspace(path);
            ExplorerPanel.LoadFolder(path);

            // ★ AJOUT (30/08, refonte Zen) : l'arborescence (colonne 2, à droite) est
            // masquée par défaut — demandé par Tom : "non ouvert par défaut, jusqu'à
            // ce qu'on importe une location". Un dossier vient d'être importé avec
            // succès (ce point est atteint par TOUS les chemins d'import : chip
            // "Projet logiciel", bouton Importer, AutoProjectBuilder) → on l'affiche.
            ExplorerPanel.IsVisible = true;
            Sidebar.IsVisible = false;
            RefreshExplorerHandleVisibility();
            _searchPanel.SetRoot(path);

            StatusBar.SetLocked(_lock.IsLocked(path));

            _aiSettings = new Moto.Core.Settings.AiSettingsService(SettingsEngine.Shared, path);

            _platformPanel.SetWorkspace(path);
            if (SettingsEngine.Shared.GetBool("platform_auto_detect"))
                _platformPanel.Analyze();

            _cortex?.Dispose();
            _workspace?.Dispose();
            _docEngine?.Dispose();

            _cortex = new CortexEngine(path);
            _neural = new NeuralMode(path, new CortexMemory(path));
            _workspace = new AIWorkspace(path);
            _docEngine = new DocEngine(path);
            // ★ AJOUT (03/09, sonde disponibilité premium) : DocEngine génère déjà
            // de vraies docs (6 fichiers .md dans .moto/docs/) à chaque ouverture de
            // projet ET à chaque auto-régénération (FileSystemWatcher), mais
            // DocPanelView.Load(report) n'était jamais appelée — le panneau
            // "Documentation" (MainPage.xaml, x:Name="DocPanel") restait vide en
            // permanence, confirmé par la sonde 9 agents du même soir.
            // DocumentationUpdated est levé à la fin de GenerateAsync (DocEngine.cs:143),
            // donc cet abonnement couvre la génération initiale ET les mises à jour
            // automatiques suivantes, pas juste le premier appel.
            _docEngine.DocumentationUpdated += report =>
                MainThread.BeginInvokeOnMainThread(() => DocPanel.Load(report));

            Home.SetCoreServices(_cortex, _workspaceState);

            RebindPanels();

            _ = Task.Run(() => _neural.Train());
            _ = _workspace.InitializeAsync().ContinueWith(_ =>
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (_workspacePanel.IsVisible) _workspacePanel.Analyze();
                }));

            if (SettingsEngine.Shared.GetBool("doc_on_project_open"))
                _ = _docEngine.GenerateAsync();

            StatusBar.SetStatus($"🧠 Cortex + 🧬 Neural + 🏗 Workspace initialisés.");
        }

        /// <summary>Retire de son hôte actuel l'en-tête (Border) qui enveloppe ce panneau.</summary>
        private void RemoveFloatingPanel(ContentView panel)
        {
            // panel.Parent = VerticalStackLayout (Content du Border) ; son propre
            // Parent = le Border ajouté à PanelHost OU PanelHostRight (voir
            // AddFloatingPanel).
            // ★ CORRECTION (01/09, glisser-déposer entre docks) : ciblait PanelHost
            // uniquement — un panneau déplacé vers PanelHostRight n'en était jamais
            // retiré ici, laissant un wrapper fantôme (contenu figé, détaché du
            // champ _xPanel réassigné juste après par l'appelant, voir plus bas)
            // pendant qu'un wrapper frais réapparaissait à gauche. RemoveFromCurrentHost
            // cherche dans les deux hôtes, comme le reste du mécanisme de glisser-déposer.
            if (panel.Parent?.Parent is Border wrapper)
                RemoveFromCurrentHost(wrapper);
        }

        private void RebindPanels()
        {
            // ★ CORRECTION (30/08) : la détection "déjà ajouté" testait
            // `panel.Parent is Grid` (vrai avant, quand les panneaux flottaient
            // directement dans RootGrid). Ils sont maintenant enveloppés dans un
            // Border ajouté à PanelHost (VerticalStackLayout) — voir AddFloatingPanel.
            // ★ CORRECTION (01/09, revue croisée — régression trouvée) : capture
            // AVANT RemoveFloatingPanel si l'ancien wrapper était dans PanelHostRight
            // (droite) — sinon le remplaçant retombait TOUJOURS à gauche, annulant
            // silencieusement le déplacement choisi par l'utilisateur à chaque
            // ouverture de dossier (voir AddFloatingPanel, param preferRightHost).
            if (_cortexPanel.Parent != null)
            {
                bool wasOnRight = WasOnRightHost(_cortexPanel);
                RemoveFloatingPanel(_cortexPanel);
                _cortexPanel = new CortexView(_cortex);
                AddFloatingPanel(_cortexPanel, preferRightHost: wasOnRight);
            }
            if (_neuralPanel.Parent != null)
            {
                bool wasOnRight = WasOnRightHost(_neuralPanel);
                RemoveFloatingPanel(_neuralPanel);
                _neuralPanel = new NeuralView(_neural);
                AddFloatingPanel(_neuralPanel, preferRightHost: wasOnRight);
            }
            if (_workspacePanel.Parent != null)
            {
                bool wasOnRight = WasOnRightHost(_workspacePanel);
                RemoveFloatingPanel(_workspacePanel);
                _workspacePanel = new AIWorkspaceView(_workspace);
                AddFloatingPanel(_workspacePanel, preferRightHost: wasOnRight);
            }
            WireAiPanels();
        }

        /// <summary>Le wrapper de ce panneau est-il ACTUELLEMENT dans PanelHostRight
        /// (droite) plutôt que PanelHost (gauche) ? Voir RebindPanels.</summary>
        private bool WasOnRightHost(ContentView panel) =>
            panel.Parent?.Parent is Border wrapper && PanelHostRight.Children.Contains(wrapper);

        // ★ LSP Roslyn (OmniSharp) : mis de côté pour cette passe (voir Moto.Core.csproj),
        // ce bloc de câblage se raccrochait à LanguageServerManager, exclu de la build.

        // ------------------------------------------------------------------
        // Stats réelles
        // ------------------------------------------------------------------
        private void RefreshHomeStats()
        {
            try
            {
                int threads = 0, messages = 0, chars = 0;
                if (_chatService.Threads != null)
                {
                    threads = _chatService.Threads.Count;
                    foreach (var t in _chatService.Threads)
                    {
                        messages += t.Messages?.Count ?? 0;
                        foreach (var m in t.Messages)
                            chars += m.Content?.Length ?? 0;
                    }
                }
                var tokens = chars / 4;
                var cortex = _cortex?.GetStats();
                Home.SetStats(
                    values: new[]
                    {
                        threads.ToString(),
                        messages.ToString(),
                        FormatCompact(tokens),
                        (cortex?.TotalPatterns ?? 0).ToString()
                    },
                    titles: new[]
                    {
                        "Sessions",
                        "Messages",
                        "Tokens",
                        "Patterns appris"
                    });
            }
            catch { }
        }

        private static string FormatCompact(int n) =>
            n >= 1_000_000 ? (n / 1_000_000.0).ToString("0.0M") :
            n >= 1_000 ? (n / 1_000.0).ToString("0.0K") :
            n.ToString();
    }
}
