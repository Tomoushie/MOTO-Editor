// Moto.Editor/Views/StatusBarPanelView.xaml.cs
// Barre de statut MAUI (x:Name="StatusBar" dans MainPage.xaml).
// Nouveau fichier : voir le commentaire du .xaml pour le pourquoi.
using System;
using Microsoft.Maui.Controls;
using Moto.Core.Settings;
using Moto.Editor.Controls;

namespace Moto.Editor.Views
{
    public partial class StatusBarPanelView : ContentView
    {
        private InfoOverlay? _infoOverlay;

        // ★ AJOUT (01/10) : état de la puce « fichier actif » (réglage sb_active_file).
        // Deux conditions distinctes : le réglage est-il actif, et y a-t-il un fichier ?
        // Les séparer évite d'afficher une puce vide quand un fichier est ouvert mais
        // que le réglage est éteint — et inversement.
        private bool _showActiveFile;
        private string _activeFileName = string.Empty;

        // ★ AJOUT (01/10, tranche 2) : état de la puce « fins de ligne » (réglage
        // sb_line_endings). Deux conditions distinctes : le réglage est-il actif, et
        // une valeur réelle existe-t-elle ? (même patron que sb_active_file).
        private bool _showLineEndings;
        private string _lineEndings = string.Empty;

        // ★ AJOUT (01/10, tranche 2) : état de la puce « langage » (réglage sb_language).
        private bool _showLanguage;
        private string _language = string.Empty;

        /// <summary>★ AJOUT (01/10) : un avertissement existe-t-il ? (pour respecter sb_diagnostics).</summary>
        private bool _hasWarnings;

        /// <summary>★ AJOUT (01/10) : réglage sb_diagnostics actif ? (défaut déclaré : true).</summary>
        private bool _showDiagnostics = true;

        /// <summary>Indicateur IA (🧠) tapé — MainPage ouvre le monitoring.</summary>
        public event Action? AiMonitorTapped;

        // ★ AJOUT (01/10, décision C item 1) : 7 boutons d'action, un événement chacun.
        // MainPage les câble sur l'action réelle (OnActivitySelected / OpenSpecializedWindow /
        // bascule du terminal). Chaque événement n'est levé que si le bouton est visible —
        // la visibilité est pilotée par ApplySettings (clé sb_*/gp_*/cp_*/ap_* correspondante).
        public event Action? ProjectPanelTapped;
        public event Action? TerminalTapped;
        public event Action? SearchTapped;
        public event Action? DebuggerTapped;
        public event Action? GitTapped;
        public event Action? CollabTapped;
        public event Action? AiPanelTapped;

        public StatusBarPanelView()
        {
            InitializeComponent();

            // ★ AJOUT (02/09, "réveil facile" repéré par l'état des lieux) :
            // PerformanceStatusBarView — vue MAUI complète, backend
            // (Moto.Core.Performance.PerformanceProfiler) déjà réel et utilisé
            // ailleurs (ProfilingHeatmapExporter) — juste jamais branché nulle
            // part dans l'interface jusqu'ici. Construite en code (pas en XAML,
            // son constructeur a un paramètre) et insérée avant les autres puces,
            // même patron que Home/AiChatView ailleurs dans ce dépôt.
            // ★ StatusBarPanelView est déclarée directement dans MainPage.xaml —
            // construite très tôt, avant que la résolution DI (qui créerait le
            // singleton PerformanceProfiler à la demande) ait pu s'exécuter.
            // Instance peut donc encore valoir null ici : on la crée nous-mêmes
            // dans ce cas plutôt que de risquer un NullReferenceException.
            var profiler = Moto.Core.Performance.PerformanceProfiler.Instance
                ?? new Moto.Core.Performance.PerformanceProfiler();
            var perf = new PerformanceStatusBarView(profiler);
            // ★ (25/09) : plus de séparateur ajouté ici — le premier trait du XAML sépare déjà les mesures des compteurs
            // (les deux se touchaient : double trait visible sur capture).
            RightChips.Children.Insert(0, perf);
        }

        /// <summary>
        /// Met à jour le texte de l'indicateur IA.
        /// ★ CORRECTION (31/08) : icône (🧠/⚡/🐢/❌) retirée du XAML — "le texte
        /// suffit amplement" (Tom). Le mot d'état ("Idle"/"Inferring"/...) reste seul.
        /// </summary>
        public void SetAiStatus(string state)
        {
            AiStatusLabel.Text = $"Monitoring : {state}";
        }

        /// <summary>Message principal affiché à gauche de la barre.</summary>
        public void SetStatus(string message)
        {
            StatusLabel.Text = message ?? string.Empty;
        }

        /// <summary>Compteurs d'erreurs/avertissements (dernier build).</summary>
        public void SetCounts(int errors, int warnings)
        {
            // ★ CORRECTION (31/08) : icônes retirées ("le texte suffit amplement",
            // Tom) — la couleur (gris/orange) reste le signal réussite/échec.
            ErrorsLabel.Text = errors == 0 ? "0 erreur" : $"{errors} erreur(s)";
            ErrorsLabel.TextColor = errors == 0
                ? (Color)Application.Current!.Resources["Txt2"]
                : Colors.OrangeRed;

            // ★ MODIFIÉ (01/10) : la visibilité dépend maintenant de DEUX choses —
            // le réglage sb_diagnostics (appliqué par ApplySettings) et le fait qu'il
            // y ait réellement des avertissements. On mémorise le second pour que
            // ApplySettings puisse recomposer les deux sans connaître les compteurs.
            _hasWarnings = warnings > 0;
            WarningsLabel.IsVisible = _hasWarnings && _showDiagnostics;
            WarningsLabel.Text = $"{warnings} avertissement(s)";
        }

        /// <summary>Affiche/masque l'indicateur "mode sandbox".</summary>
        public void SetSandbox(bool active)
        {
            SandboxLabel.IsVisible = active;
            UpdateStateChips();
        }

        /// <summary>Affiche/masque le cadenas (projet protégé par mot de passe).</summary>
        public void SetLocked(bool locked)
        {
            LockedLabel.IsVisible = locked;
            UpdateStateChips();
        }

        // ★ (25/09) : le groupe (et son séparateur) n'apparaît que si au moins une des deux étiquettes est visible.
        private void UpdateStateChips() => StateChips.IsVisible = SandboxLabel.IsVisible || LockedLabel.IsVisible;

        /// <summary>
        /// Applique les réglages qui concernent la barre de statut elle-même.
        /// </summary>
        /// <remarks>
        /// ★ AJOUT (01/10) : cette méthode était VIDE (simple point d'extension
        /// « Rien à appliquer pour l'instant ») alors que la catégorie
        /// « Fenêtre &amp; Layout / Status Bar » déclare 10 réglages <c>sb_*</c> que
        /// RIEN ne lisait. Elle en applique maintenant 2, ceux dont la donnée existe
        /// réellement — voir <see cref="Moto.Editor.Settings.StatusBarSettings"/>,
        /// qui documente pourquoi les 8 autres ne sont pas câblables (boutons
        /// inexistants, ou absence totale de notion d'encodage / de fins de ligne /
        /// de position de curseur dans le dépôt).
        /// </remarks>
        public void ApplySettings(SettingsEngine settings)
        {
            if (settings is null) return;

            // Compteurs d'erreurs/avertissements : la donnée est déjà alimentée par
            // SetCounts() ; le réglage ne fait que choisir de l'afficher ou non.
            _showDiagnostics = Moto.Editor.Settings.StatusBarSettings.ShowDiagnostics(settings);
            ErrorsLabel.IsVisible = _showDiagnostics;
            WarningsLabel.IsVisible = _showDiagnostics && _hasWarnings;

            // Nom du fichier actif : masqué si le réglage est éteint OU si aucun
            // fichier n'est ouvert (jamais de contenu inventé).
            _showActiveFile = Moto.Editor.Settings.StatusBarSettings.ShowActiveFile(settings);
            UpdateActiveFileVisibility();

            // ★ AJOUT (01/10, tranche 2) : puce « fins de ligne » (sb_line_endings).
            _showLineEndings = Moto.Editor.Settings.StatusBarSettings.ShowLineEndings(settings);
            UpdateLineEndingsVisibility();

            // ★ AJOUT (01/10, tranche 2) : puce « langage » (sb_language).
            _showLanguage = Moto.Editor.Settings.StatusBarSettings.ShowLanguage(settings);
            UpdateLanguageVisibility();

            // ★ AJOUT (01/10, décision C item 1) : visibilité des 7 boutons d'action,
            // pilotée par leur clé respective. Chaque conteneur regroupe le bouton ET son
            // séparateur (voir le .xaml) : basculer le conteneur ne laisse aucun trait
            // orphelin. Les clés sont lues AVEC leur défaut déclaré (piège du GetBool sans
            // second argument, documenté dans StatusBarSettings.cs).
            ProjectPanelChip.IsVisible = Moto.Editor.Settings.StatusBarSettings.ShowProjectPanel(settings);
            TerminalChip.IsVisible = Moto.Editor.Settings.StatusBarSettings.ShowTerminal(settings);
            SearchChip.IsVisible = Moto.Editor.Settings.StatusBarSettings.ShowSearch(settings);
            DebuggerChip.IsVisible = Moto.Editor.Settings.StatusBarSettings.ShowDebugger(settings);
            GitChip.IsVisible = Moto.Editor.Settings.StatusBarSettings.ShowGit(settings);
            CollabChip.IsVisible = Moto.Editor.Settings.StatusBarSettings.ShowCollab(settings);
            AiPanelChip.IsVisible = Moto.Editor.Settings.StatusBarSettings.ShowAiPanel(settings);
        }

        /// <summary>
        /// ★ AJOUT (01/10) : nom du fichier actuellement affiché dans l'éditeur
        /// (réglage <c>sb_active_file</c>). Appelée par MainPage à l'ouverture d'un
        /// document, et avec <c>null</c> quand le dernier onglet est fermé.
        /// </summary>
        public void SetActiveFile(string? path)
        {
            _activeFileName = string.IsNullOrWhiteSpace(path)
                ? string.Empty
                : System.IO.Path.GetFileName(path);
            ActiveFileLabel.Text = _activeFileName;
            UpdateActiveFileVisibility();
        }

        /// <summary>Visible seulement si le réglage est actif ET qu'un fichier est ouvert.</summary>
        private void UpdateActiveFileVisibility()
            => ActiveFileLabel.IsVisible = _showActiveFile && _activeFileName.Length > 0;

        /// <summary>
        /// ★ AJOUT (01/10, tranche 2) : fins de ligne du fichier actif (réglage
        /// <c>sb_line_endings</c>). Valeur RÉELLE détectée par MainPage depuis
        /// <c>EditorDocument.Text</c> (« CRLF » / « LF »), ou <c>null</c> si aucun
        /// fichier / aucun retour à la ligne. La vue ne fait qu'afficher — elle ne
        /// devine jamais rien.
        /// </summary>
        public void SetLineEndings(string? eol)
        {
            _lineEndings = string.IsNullOrWhiteSpace(eol) ? string.Empty : eol;
            LineEndingsLabel.Text = _lineEndings;
            UpdateLineEndingsVisibility();
        }

        /// <summary>Visible seulement si le réglage est actif ET qu'une valeur réelle existe.</summary>
        private void UpdateLineEndingsVisibility()
            => LineEndingsLabel.IsVisible = _showLineEndings && _lineEndings.Length > 0;

        /// <summary>
        /// ★ AJOUT (01/10, tranche 2) : langage du fichier actif (réglage <c>sb_language</c>).
        /// Nom LISIBLE détecté par MainPage depuis <c>CodeEditorView.LanguageDisplayName(path)</c>,
        /// ou <c>null</c>/"" si aucun fichier ou extension inconnue. La vue ne fait
        /// qu'afficher — elle ne devine jamais rien.
        /// </summary>
        public void SetLanguage(string? language)
        {
            _language = string.IsNullOrWhiteSpace(language) ? string.Empty : language;
            LanguageLabel.Text = _language;
            UpdateLanguageVisibility();
        }

        /// <summary>Visible seulement si le réglage est actif ET qu'une valeur réelle existe.</summary>
        private void UpdateLanguageVisibility()
            => LanguageLabel.IsVisible = _showLanguage && _language.Length > 0;

        /// <summary>Branche l'overlay "À propos / mises à jour" sur le bouton ℹ️.</summary>
        public void InitializeInfoOverlay(InfoOverlay overlay)
        {
            _infoOverlay = overlay;
        }

        private void OnInfoTapped(object? sender, EventArgs e) => _infoOverlay?.Show();

        private void OnAiMonitorTapped(object? sender, EventArgs e) => AiMonitorTapped?.Invoke();

        // ★ AJOUT (01/10, décision C item 1) : handlers des 7 boutons d'action. Chacun ne fait
        // qu'invoquer l'événement correspondant — la VUE ignore quelle action est branchée
        // (toggle explorateur, fenêtre debug, etc.) : c'est MainPage qui décide, comme pour
        // AiMonitorTapped. Cela garde la vue découplée du routage (MainPage.Routing.cs /
        // MainPage.Extensions.cs) et évite toute dépendance circulaire.
        private void OnProjectPanelTapped(object? sender, EventArgs e) => ProjectPanelTapped?.Invoke();
        private void OnTerminalTapped(object? sender, EventArgs e) => TerminalTapped?.Invoke();
        private void OnSearchTapped(object? sender, EventArgs e) => SearchTapped?.Invoke();
        private void OnDebuggerTapped(object? sender, EventArgs e) => DebuggerTapped?.Invoke();
        private void OnGitTapped(object? sender, EventArgs e) => GitTapped?.Invoke();
        private void OnCollabTapped(object? sender, EventArgs e) => CollabTapped?.Invoke();
        private void OnAiPanelTapped(object? sender, EventArgs e) => AiPanelTapped?.Invoke();
    }
}
