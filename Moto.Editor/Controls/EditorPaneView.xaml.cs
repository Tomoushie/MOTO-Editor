// Moto.Editor/Controls/EditorPaneView.xaml.cs (v5 — avec ExportRequested)
using System;
using Microsoft.Maui.Controls;
using Moto.Core.Settings;
using Moto.Editor.Models;
using Moto.Editor.Settings;

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

        /// <summary>Basculer la visibilité du bandeau IA.</summary>
        private void OnAiClicked(object s, EventArgs e)
        {
            AiBand.IsVisible = !AiBand.IsVisible;

            if (AiBand.IsVisible) PromptEntry.Focus();
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

        // ------------------------------------------------------------------
        // ★ AJOUT (28/09) : réglages « Fenêtre & Layout / Tab Bar » (clés tabs_*)
        // ------------------------------------------------------------------

        /// <summary>
        /// Applique les réglages <c>tabs_*</c> à la barre d'onglets et à ses onglets.
        ///
        /// Appelée par <c>MainPage.ApplyLayoutSettings</c> (démarrage, sortie de plein écran,
        /// action de layout) et à chaque changement d'un réglage <c>tabs_*</c> depuis la
        /// fenêtre Réglages (<c>SettingsWindow.RealSettingChanged</c>) — sans ce second
        /// chemin, un réglage modifié n'aurait d'effet qu'au prochain démarrage.
        ///
        /// Avant cet ajout, AUCUNE de ces clés n'était lue par du code compilé : la fenêtre
        /// Réglages les affichait, l'interface les ignorait (mesuré par
        /// <c>scripts/settings-coverage.ps1</c>).
        /// </summary>
        public void ApplySettings(SettingsEngine settings)
        {
            if (settings is null) return;

            // Barre d'onglets entière, puis ses deux groupes d'actions. ⚠️ Le second argument est
            // le défaut DÉCLARÉ au catalogue : GetBool(clé) sans défaut renvoie false pour une clé
            // absente du store (le moteur ne connaît pas le catalogue) — sur une installation
            // neuve, la barre d'onglets aurait donc disparu. Voir TabBarSettings.
            TabBarRow.IsVisible = settings.GetBool("tabs_show", TabBarSettings.DeclaredBool("tabs_show"));
            TabBarActions.IsVisible = settings.GetBool("tabs_bar_buttons", TabBarSettings.DeclaredBool("tabs_bar_buttons"));

            // Précédent/suivant se cachent ENSEMBLE (un seul réglage pour la paire) ; le fil
            // d'Ariane est dans une autre colonne du même Grid et reste visible.
            var navVisible = settings.GetBool("tabs_nav_buttons", TabBarSettings.DeclaredBool("tabs_nav_buttons"));
            BtnNavBack.IsVisible = navVisible;
            BtnNavForward.IsVisible = navVisible;

            // Le reste (icône de fichier, pastille de diagnostics, croix de fermeture) est
            // propre à CHAQUE onglet : le DataTemplate ne peut lire que des propriétés de
            // l'onglet lui-même, donc chaque EditorDocument reçoit sa copie. Le mappage
            // vit dans TabBarSettings, partagé avec la création d'onglet (MainViewModel),
            // sinon un onglet ouvert après le réglage garderait l'ancien état.
            if (TabsList.ItemsSource is System.Collections.IEnumerable items)
                foreach (var item in items)
                    if (item is EditorDocument doc)
                        TabBarSettings.Apply(doc, settings);
        }

        /// <summary>Mode de fermeture de la croix où elle ne se montre qu'au survol.</summary>
        private const string TabCloseHoverMode = "Hover";

        private void OnTabPointerEntered(object sender, PointerEventArgs e) => SetTabCloseHovered(sender, true);

        private void OnTabPointerExited(object sender, PointerEventArgs e) => SetTabCloseHovered(sender, false);

        /// <summary>
        /// Survol d'un onglet (réglage <c>tabs_show_close</c> = « Hover »). Ne fait rien dans les
        /// autres modes : l'état de la croix y est déjà entièrement décidé par le réglage.
        /// </summary>
        private void SetTabCloseHovered(object sender, bool hovered)
        {
            if ((sender as Element)?.BindingContext is not EditorDocument doc) return;
            if (!string.Equals(doc.CloseMode, TabCloseHoverMode, StringComparison.OrdinalIgnoreCase)) return;
            doc.SetCloseHovered(hovered);
        }
    }
}
