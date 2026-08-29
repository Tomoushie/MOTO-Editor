// Moto.Core/Views/BeginnerViewManager.cs
using System;
using System.Collections.Generic;

namespace Moto.Core.Views
{
    /// <summary>
    /// Mode d'affichage. Logique pure, aucune dépendance UI.
    /// Compatible MAUI, WinForms, WPF, tests unitaires.
    /// </summary>
    public enum ViewMode
    {
        Beginner,
        Expert
    }

    /// <summary>
    /// Gère la visibilité des panneaux selon le mode.
    /// La couche UI (MAUI) s'abonne à ModeChanged pour appliquer la visibilité.
    /// </summary>
    public class BeginnerViewManager
    {
        /// <summary>
        /// Identifiants des panneaux contrôlés.
        /// </summary>
        public static class PanelIds
        {
            public const string Terminal = "Terminal";
            public const string Diagnostics = "Diagnostics";
            public const string MiniMap = "MiniMap";
            public const string AiPanel = "AiPanel";
            public const string LearnPanel = "LearnPanel";
            public const string QuickActions = "QuickActions";
        }

        public ViewMode CurrentMode { get; private set; } = ViewMode.Beginner;

        /// <summary>
        /// Déclenché quand le mode change.
        /// La vue MAUI s'abonne pour mettre à jour IsVisible.
        /// </summary>
        public event Action<ViewMode> ModeChanged;

        /// <summary>
        /// Bascule entre Beginner et Expert.
        /// </summary>
        public void Toggle()
        {
            SetMode(CurrentMode == ViewMode.Beginner ? ViewMode.Expert : ViewMode.Beginner);
        }

        /// <summary>
        /// Applique un mode explicite.
        /// </summary>
        public void SetMode(ViewMode mode)
        {
            CurrentMode = mode;
            ModeChanged?.Invoke(mode);
        }

        /// <summary>
        /// Indique si un panneau doit être visible dans le mode donné.
        /// Méthode pure, testable sans UI.
        /// </summary>
        public bool ShouldBeVisible(string panelId, ViewMode mode)
        {
            bool isExpert = mode == ViewMode.Expert;

            switch (panelId)
            {
                // Visible uniquement en Expert
                case PanelIds.Terminal:
                case PanelIds.Diagnostics:
                case PanelIds.MiniMap:
                    return isExpert;

                // Visible uniquement en Beginner
                case PanelIds.LearnPanel:
                case PanelIds.QuickActions:
                    return !isExpert;

                // Toujours visible
                case PanelIds.AiPanel:
                    return true;

                default:
                    return true;
            }
        }
    }
}
