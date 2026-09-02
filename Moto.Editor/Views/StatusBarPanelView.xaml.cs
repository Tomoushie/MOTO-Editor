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

        /// <summary>Indicateur IA (🧠) tapé — MainPage ouvre le monitoring.</summary>
        public event Action? AiMonitorTapped;

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
            RightChips.Children.Insert(0, new BoxView
            {
                WidthRequest = 1,
                Color = (Color)Application.Current!.Resources["BorderMuted"],
                Margin = new Thickness(0, 6, 8, 6)
            });
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

            WarningsLabel.IsVisible = warnings > 0;
            WarningsLabel.Text = $"{warnings} avertissement(s)";
        }

        /// <summary>Affiche/masque l'indicateur "mode sandbox".</summary>
        public void SetSandbox(bool active) => SandboxLabel.IsVisible = active;

        /// <summary>Affiche/masque le cadenas (projet protégé par mot de passe).</summary>
        public void SetLocked(bool locked) => LockedLabel.IsVisible = locked;

        /// <summary>
        /// Applique les réglages qui concernent la barre de statut elle-même.
        /// Pour l'instant la barre n'a pas de réglage dédié — méthode conservée
        /// comme point d'extension (appelée par MainPage.ApplyLayoutSettings).
        /// </summary>
        public void ApplySettings(SettingsEngine settings)
        {
            // Rien à appliquer pour l'instant : la barre affiche toujours
            // statut + compteurs + sandbox + verrou + info.
        }

        /// <summary>Branche l'overlay "À propos / mises à jour" sur le bouton ℹ️.</summary>
        public void InitializeInfoOverlay(InfoOverlay overlay)
        {
            _infoOverlay = overlay;
        }

        private void OnInfoTapped(object? sender, EventArgs e) => _infoOverlay?.Show();

        private void OnAiMonitorTapped(object? sender, EventArgs e) => AiMonitorTapped?.Invoke();
    }
}
