// Moto.Editor/Controls/EditorPaneView.xaml.cs (v5 — avec ExportRequested)
using System;
using Microsoft.Maui.Controls;
using Moto.Editor.Models;

namespace Moto.Editor.Controls
{
    /// <summary>
    /// Panneau éditeur type Zed :
    /// - navigation ← → (historique)
    /// - onglets avec badge d'erreurs
    /// - breadcrumb (localisation du fichier)
    /// - bouton ⛶ plein écran
    /// - bouton ⬇ export (txt, md, html, pdf, docx, odt, rtf...)
    /// - bouton 🤖 bandeau IA (modèle + prompts qui modifient le code en direct)
    /// - bouton ⧉ split
    /// </summary>
    public partial class EditorPaneView : ContentView
    {
        // ------------------------------------------------------------------
        // Événements émis vers MainPage
        // ------------------------------------------------------------------

        /// <summary>Demande de retour en arrière dans l'historique de fichiers.</summary>
        public event Action BackRequested;

        /// <summary>Demande d'avancer dans l'historique de fichiers.</summary>
        public event Action ForwardRequested;

        /// <summary>Demande d'agrandir l'éditeur en plein écran (= écran du logiciel).</summary>
        public event Action MaximizeRequested;

        /// <summary>Demande de séparer l'éditeur en deux panneaux.</summary>
        public event Action SplitRequested;

        /// <summary>Demande d'ouvrir un fichier (via le bouton "Open File").</summary>
        public event Action OpenFileRequested;

        /// <summary>
        /// Demande d'export du fichier actif (déclenchée par le bouton ⬇).
        /// Consommée par MainPage qui ouvre le ExportMenuView.
        /// </summary>
        public event Action ExportRequested;

        /// <summary>
        /// ★ AJOUT (30/08, 3e passe) : demande de prévisualisation du fichier actif
        /// (bouton 🌐). Consommée par MainPage qui pilote LivePreviewView.
        /// </summary>
        public event Action PreviewRequested;

        /// <summary>
        /// (modèle, prompt) envoyés depuis le bandeau IA pour modification du code.
        /// </summary>
        public event Action<string, string> AiPromptSubmitted;

        /// <summary>
        /// ★ AJOUT (24/09, écriture générative) : le bouton ■ (qui remplace ➤ pendant qu'un modèle écrit) a été cliqué : arrêter la génération.
        /// </summary>
        public event Action? AiCancelRequested;

        /// <summary>★ AJOUT (24/09) : « ↩ Annuler » — remettre le fichier comme avant la dernière modification faite depuis le bandeau IA.</summary>
        public event Action? AiUndoRequested;

        /// <summary>Sélection d'un onglet → transmise à MainPage.</summary>
        public event Action<EditorDocument> TabSelected;

        /// <summary>
        /// ★ AJOUT (31/08) : fermeture d'un onglet via son ✕ → transmise à MainPage
        /// (qui retire le document du ViewModel). Voir MainViewModel.RemoveDocument.
        /// </summary>
        public event Action<EditorDocument> TabClosed;

        /// <summary>
        /// Modification du texte par l'utilisateur.
        /// Branché sur CodeEditorView.EditorChanged.
        /// </summary>
        public event EventHandler<string> EditorChanged
        {
            add => Editor.EditorChanged += value;
            remove => Editor.EditorChanged -= value;
        }

        /// <summary>★ AJOUT (25/09) : raccourci de MOTO tapé pendant que le curseur est dans le code (voir CodeEditorView.ShortcutPressed).</summary>
        public event Action<string>? ShortcutPressed
        {
            add => Editor.ShortcutPressed += value;
            remove => Editor.ShortcutPressed -= value;
        }

        // ------------------------------------------------------------------
        // Constructeur
        // ------------------------------------------------------------------

        public EditorPaneView()
        {
            InitializeComponent();
            ModelPicker.SelectedIndex = 0; // "MOTO interne"
        }

        // ------------------------------------------------------------------
        // ★ AJOUT (26/09, décision de Tom) : liste de modèles du bandeau IA — les services en ligne n'y figurent qu'une fois leur clé
        // ajoutée ; la dernière ligne, « Ajouter un service en ligne… », demande à MainPage d'ouvrir Clés API.
        // ------------------------------------------------------------------

        /// <summary>La ligne « Ajouter un service en ligne… » a été choisie : ouvrir Clés API.</summary>
        public event Action? AddOnlineServiceRequested;

        private bool _settingModels;
        private string _bandModel = "MOTO interne";

        /// <summary>Remplace la liste (ChatService.ModelChoices) ; le modèle choisi le reste s'il y figure encore, sinon retour au premier.</summary>
        public void SetModelChoices(System.Collections.Generic.IReadOnlyList<string> choices)
        {
            if (choices.Count == 0) return;
            _settingModels = true;
            try
            {
                ModelPicker.ItemsSource = new System.Collections.Generic.List<string>(choices);
                if (!System.Linq.Enumerable.Contains(choices, _bandModel)) _bandModel = choices[0];
                ModelPicker.SelectedItem = _bandModel;
            }
            finally
            {
                _settingModels = false;
            }
        }

        private void OnModelPickerChanged(object? sender, EventArgs e)
        {
            if (_settingModels || ModelPicker.SelectedItem is not string model) return;
            if (model == Moto.Editor.Services.ChatService.AddOnlineServiceLabel)
            {
                _settingModels = true;
                ModelPicker.SelectedItem = _bandModel; // pas un modèle : le choix d'avant reste
                _settingModels = false;
                AddOnlineServiceRequested?.Invoke();
                return;
            }
            _bandModel = model;
        }

        // ------------------------------------------------------------------
        // API publique : binding depuis MainPage
        // ------------------------------------------------------------------

        /// <summary>Source des onglets (Documents du MainViewModel).</summary>
        public void BindTabs(System.Collections.IEnumerable documents)
        {
            TabsList.ItemsSource = documents;
        }

        /// <summary>
        /// ★ AJOUT (30/08) : synchronise visuellement l'onglet sélectionné dans le
        /// CollectionView. Nécessaire car un fichier peut être sélectionné par un
        /// chemin AUTRE qu'un clic sur un onglet déjà visible (explorateur, réponse
        /// IA auto-ouverte) — sans ça, le CollectionView ne montre jamais l'onglet
        /// comme actif et un futur clic dessus ne redéclenche rien (déjà "sélectionné"
        /// à ses yeux, alors qu'aucun contenu n'a jamais été chargé).
        /// </summary>
        public void SelectTab(object document)
        {
            TabsList.SelectedItem = document;

            // ★ (25/09) : l'onglet actif est dessiné d'après EditorDocument.IsActive (voir le XAML), pas d'après l'état
            // visuel « Selected » du CollectionView, qui ne s'appliquait pas sous Windows.
            if (TabsList.ItemsSource is System.Collections.IEnumerable items)
                foreach (var item in items)
                    if (item is EditorDocument doc)
                        doc.IsActive = ReferenceEquals(doc, document);

            // ★ AJOUT (26/09) : la colonne centrale étant bornée (MainPage.UpdateCenterWidthBound), les onglets en trop défilent au lieu
            // de pousser l'explorateur hors de la fenêtre — l'onglet actif est donc ramené dans la vue, comme dans VS Code. Différé : un
            // onglet qui vient d'être ajouté n'a pas encore de place dans la liste.
            if (document is not null)
                Dispatcher.Dispatch(() =>
                {
                    try { TabsList.ScrollTo(document, position: ScrollToPosition.MakeVisible, animate: false); }
                    catch (Exception) { /* onglet fermé entre-temps : rien à montrer */ }
                });
        }

        /// <summary>
        /// Met à jour le breadcrumb avec le chemin complet du fichier.
        /// Remplace les \ par " \ " pour un rendu lisible type Zed.
        /// </summary>
        public void SetBreadcrumb(string fullPath)
        {
            _crumbPath = fullPath;
            RenderBreadcrumb();
            // ★ AJOUT (25/09) : la coloration suit le type du fichier affiché (appelé à chaque changement de document).
            Editor.SetLanguageFromPath(fullPath);
        }

        private string? _crumbPath;
        private string? _workspaceRoot;

        private static Microsoft.Maui.Graphics.Color ThemeColor(string key, string fallback)
            => Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Microsoft.Maui.Graphics.Color color
                ? color
                : Microsoft.Maui.Graphics.Color.FromArgb(fallback);

        /// <summary>★ AJOUT (25/09) : dossier du projet ouvert — le fil d'Ariane affiche le chemin relatif à ce dossier.</summary>
        public string? WorkspaceRoot
        {
            get => _workspaceRoot;
            set { _workspaceRoot = value; RenderBreadcrumb(); }
        }

        /// <summary>
        /// ★ REFAIT (25/09) : « Moto.Core › Moto.AI › CodeApply.cs » (relatif au projet ; nom du fichier en clair, dossiers
        /// estompés) au lieu de « C: \ Users \ nowak \ … » ; le chemin complet reste dans l'infobulle.
        /// </summary>
        private void RenderBreadcrumb()
        {
            if (string.IsNullOrWhiteSpace(_crumbPath))
            {
                CrumbLabel.FormattedText = null;
                CrumbLabel.Text = "Aucun fichier ouvert";
                ToolTipProperties.SetText(CrumbLabel, null);
                return;
            }
            var shown = _crumbPath;
            if (!string.IsNullOrWhiteSpace(_workspaceRoot))
            {
                var root = _workspaceRoot.TrimEnd('\\', '/') + System.IO.Path.DirectorySeparatorChar;
                if (shown.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                    shown = System.IO.Path.GetFileName(_workspaceRoot.TrimEnd('\\', '/')) + System.IO.Path.DirectorySeparatorChar + shown.Substring(root.Length);
            }
            var parts = shown.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
            var text = new FormattedString();
            var dim = ThemeColor("Txt3", "#6B7280");
            var bright = ThemeColor("Txt1", "#E5E7EB");
            for (var i = 0; i < parts.Length; i++)
            {
                var last = i == parts.Length - 1;
                text.Spans.Add(new Span { Text = parts[i], TextColor = last ? bright : dim });
                if (!last) text.Spans.Add(new Span { Text = "  ›  ", TextColor = dim });
            }
            CrumbLabel.FormattedText = text;
            ToolTipProperties.SetText(CrumbLabel, _crumbPath);
        }

        /// <summary>Contenu de l'éditeur (two-way).</summary>
        public string EditorText
        {
            get => Editor.Text;
            set => Editor.Text = value;
        }

        /// <summary>Navigue vers une ligne donnée (utilisé par Navigation Assistant).</summary>
        public void GoToLine(int line) => Editor.GoToLine(line);

        /// <summary>Affiche/masque la mini-map (consommé par le paramètre minimap_show).</summary>
        public void SetMinimapVisible(bool visible) => Editor.SetMinimapVisible(visible);

        /// <summary>
        /// GHOST TEXT (Pair Programming) : affiche une suggestion grise ;
        /// l'utilisateur accepte avec Tab (délégation à CodeEditorView).
        /// </summary>
        public void SetGhost(string suggestion) => Editor.SetGhost(suggestion);

        /// <summary>Texte sélectionné dans l'éditeur (pour /selection du chat).</summary>
        public string GetSelectedText() => Editor.GetSelectedText();

        /// <summary>★ AJOUT (25/09, « Appliquer » dans le chat) : sélection ou curseur (texte aux « \n »), ou null si l'éditeur n'a pas été cliqué.</summary>
        public (int Start, int Length)? GetSelectionRange() => Editor.GetSelectionRange();

        /// <summary>Met à jour la ligne de statut sous le bandeau IA.</summary>
        public void SetAiStatus(string message)
        {
            AiStatus.Text = message;
        }

        private bool _aiBusy;

        /// <summary>
        /// ★ AJOUT (24/09, écriture générative) : pendant qu'un modèle écrit, ➤ devient ■ (arrêter) et le champ de saisie est grisé —
        /// jusqu'ici rien n'indiquait qu'une demande était en cours ni ne permettait de l'arrêter.
        /// </summary>
        public void SetAiBusy(bool busy)
        {
            _aiBusy = busy;
            AiSendButton.Text = busy ? MotoIcons.Stop : MotoIcons.Send;
            ToolTipProperties.SetText(AiSendButton, busy ? "Arrêter la génération" : "Envoyer");
            PromptEntry.IsEnabled = !busy;
        }

        /// <summary>★ AJOUT (24/09) : montre ou cache « ↩ Annuler » (visible seulement si le fichier affiché a une modification IA à défaire).</summary>
        public void SetAiUndoAvailable(bool available) => AiUndoButton.IsVisible = available;

        /// <summary>★ AJOUT (24/09) : ouvre le bandeau IA (s'il était fermé) pour qu'une réponse affichée dans sa ligne d'état soit visible.</summary>
        public void ShowAiBand() => AiBand.IsVisible = true;

        // ★ AJOUT (27/09, point 3 de Tom) : la barre centrale flottante est retirée ; ce bandeau devient LA petite barre qui modifie le
        // fichier ouvert, et n'apparaît que sur demande (Ctrl+Maj+I, bouton 🤖, palette « Modifier le fichier avec l'IA »).

        /// <summary>Le bandeau est ouvert et le curseur est dans son champ de saisie.</summary>
        public bool IsAiBandFocused => AiBand.IsVisible && PromptEntry.IsFocused;

        /// <summary>Ouvre le bandeau (s'il était fermé) et met le curseur dans son champ.</summary>
        public void OpenAiBand()
        {
            AiBand.IsVisible = true;
            PromptEntry.Focus();
        }

        /// <summary>
        /// Ferme le bandeau. Refusé pendant qu'un modèle écrit : son ■ (arrêter) doit rester à portée.
        /// Le curseur n'est PAS rendu au code : WebView.Focus() fait planter MOTO (arrêt natif 0xc0000409, reproduit le 27/09) ;
        /// on reclique dans le code.
        /// </summary>
        public bool CloseAiBand()
        {
            if (_aiBusy) return false;
            AiBand.IsVisible = false;
            return true;
        }

        /// <summary>
        /// ★ AJOUT (30/08) : reflète l'état plein écran sur le bouton lui-même —
        /// Tom ne retrouvait pas comment revenir en arrière (rien n'indiquait que
        /// recliquer le même bouton fonctionnait).
        /// </summary>
        public void SetMaximizeIcon(bool maximized)
        {
            BtnMaximize.Text = maximized ? MotoIcons.BackToWindow : MotoIcons.FullScreen;
            ToolTipProperties.SetText(BtnMaximize, maximized
                ? "Revenir à la disposition normale"
                : "Agrandir la zone (plein écran)");
        }

        // ------------------------------------------------------------------
        // Handlers des boutons de la toolbar (colonne 0)
        // ------------------------------------------------------------------

        private void OnBackClicked(object s, EventArgs e) => BackRequested?.Invoke();
        private void OnForwardClicked(object s, EventArgs e) => ForwardRequested?.Invoke();
        private void OnMaximizeClicked(object s, EventArgs e) => MaximizeRequested?.Invoke();
        private void OnSplitClicked(object s, EventArgs e) => SplitRequested?.Invoke();
        private void OnOpenFileClicked(object s, EventArgs e) => OpenFileRequested?.Invoke();

        /// <summary>
        /// Bouton ⬇ : demande d'export du fichier actif.
        /// MainPage écoute ExportRequested pour ouvrir le ExportMenuView.
        /// </summary>
        private void OnExportClicked(object s, EventArgs e) => ExportRequested?.Invoke();

        /// <summary>Bouton 🌐 : demande de prévisualisation du fichier actif.</summary>
        private void OnPreviewClicked(object s, EventArgs e) => PreviewRequested?.Invoke();

        // ------------------------------------------------------------------
        // Handlers bandeau IA
        // ------------------------------------------------------------------

        /// <summary>Basculer la visibilité du bandeau IA (★ 27/09 : il reste ouvert pendant qu'un modèle écrit, voir CloseAiBand).</summary>
        private void OnAiClicked(object s, EventArgs e)
        {
            if (AiBand.IsVisible) CloseAiBand();
            else OpenAiBand();
        }

        /// <summary>
        /// Envoi d'un prompt depuis le bandeau IA :
        /// transmet le modèle sélectionné + le texte à MainPage
        /// qui appliquera la modification en direct sur le fichier actif.
        /// </summary>
        private void OnAiSendClicked(object s, EventArgs e)
        {
            if (_aiBusy)
            {
                AiCancelRequested?.Invoke();
                return;
            }

            var prompt = PromptEntry.Text?.Trim();

            if (string.IsNullOrWhiteSpace(prompt)) return;

            var model = ModelPicker.SelectedItem as string ?? "MOTO interne";

            PromptEntry.Text = string.Empty;
            AiPromptSubmitted?.Invoke(model, prompt);
        }

        private void OnAiUndoClicked(object s, EventArgs e) => AiUndoRequested?.Invoke();

        /// <summary>
        /// Sélection d'un onglet → transmise à MainPage pour charger le document.
        /// </summary>
        private void OnTabSelected(object s, SelectionChangedEventArgs e)
        {
            if (e.CurrentSelection.Count > 0 && e.CurrentSelection[0] is EditorDocument doc)
            {
                TabSelected?.Invoke(doc);
            }
        }

        /// <summary>Bouton ✕ d'un onglet : ferme le document (voir TabClosed).</summary>
        private void OnTabCloseTapped(object s, TappedEventArgs e)
        {
            if (e.Parameter is EditorDocument doc)
            {
                TabClosed?.Invoke(doc);
            }
        }
    }
}
