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
        }

        /// <summary>Met à jour l'icône + le texte de l'indicateur IA.</summary>
        public void SetAiStatus(string state)
        {
            AiStatusLabel.Text = state;
            AiStatusIcon.Text = state switch
            {
                "Idle" => "🧠", "Inferring" => "⚡", "Throttled" => "🐢", "Error" => "❌", _ => "🧠"
            };
        }

        /// <summary>Message principal affiché à gauche de la barre.</summary>
        public void SetStatus(string message)
        {
            StatusLabel.Text = message ?? string.Empty;
        }

        /// <summary>Compteurs d'erreurs/avertissements (dernier build).</summary>
        public void SetCounts(int errors, int warnings)
        {
            ErrorsLabel.Text = errors == 0 ? "✅ 0" : $"❌ {errors}";
            ErrorsLabel.TextColor = errors == 0
                ? (Color)Application.Current!.Resources["Txt2"]
                : Colors.OrangeRed;

            WarningsLabel.IsVisible = warnings > 0;
            WarningsLabel.Text = $"⚠ {warnings}";
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
