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
        }

        /// <summary>
        /// Met à jour le breadcrumb avec le chemin complet du fichier.
        /// Remplace les \ par " \ " pour un rendu lisible type Zed.
        /// </summary>
        public void SetBreadcrumb(string fullPath)
        {
            CrumbLabel.Text = string.IsNullOrWhiteSpace(fullPath)
                ? "Aucun fichier ouvert"
                : fullPath.Replace("\\", " \\ ");
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

        /// <summary>Met à jour la ligne de statut sous le bandeau IA.</summary>
        public void SetAiStatus(string message)
        {
            AiStatus.Text = message;
        }

        /// <summary>
        /// ★ AJOUT (30/08) : reflète l'état plein écran sur le bouton lui-même —
        /// Tom ne retrouvait pas comment revenir en arrière (rien n'indiquait que
        /// recliquer le même bouton fonctionnait).
        /// </summary>
        public void SetMaximizeIcon(bool maximized)
        {
            BtnMaximize.Text = maximized ? "⛝" : "⛶";
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
            var prompt = PromptEntry.Text?.Trim();

            if (string.IsNullOrWhiteSpace(prompt)) return;

            var model = ModelPicker.SelectedItem as string ?? "MOTO interne";

            PromptEntry.Text = string.Empty;
            AiPromptSubmitted?.Invoke(model, prompt);
        }

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
