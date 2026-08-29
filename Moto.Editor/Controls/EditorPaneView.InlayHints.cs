// Moto.Editor/Controls/EditorPaneView.InlayHints.cs
using Microsoft.Maui.Controls;
using Moto.Core.LSP.InlayHints;

namespace Moto.Editor.Controls
{
    public partial class EditorPaneView
    {
        private InlayHintService? _inlayHintService;
        private InlayHintOverlay? _inlayOverlay;
        private string? _currentFilePath;

        // Dimensions moyennes (à affiner via mesure réelle du renderer)
        private const double CharWidth = 7.2;
        private const double LineHeight = 18.0;

        public void InitializeInlayHints(InlayHintService service)
        {
            _inlayHintService = service;

            _inlayOverlay = new InlayHintOverlay
            {
                InputTransparent = true,
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill
            };

            if (this is Grid grid)
                grid.Children.Add(_inlayOverlay);

            _inlayHintService.HintsUpdated += hints =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                    _inlayOverlay?.RenderHints(hints, CharWidth, LineHeight));
            };
        }

        /// <summary>Appelé depuis EditorChanged pour rafraîchir les hints dynamiquement.</summary>
        public void NotifyTextChangedForInlayHints(string filePath, string content)
        {
            _currentFilePath = filePath;
            _inlayHintService?.RequestRefresh(filePath, content, 0, 200);
        }

        /// <summary>Rafraîchit les hints selon la position du curseur (hints dynamiques).</summary>
        public void OnCursorMoved(int line)
        {
            if (_inlayHintService == null || _currentFilePath == null) return;
            // Fenêtre autour du curseur : ±25 lignes
            _inlayHintService.RequestRefresh(
                _currentFilePath,
                EditorText ?? string.Empty,
                Math.Max(0, line - 25),
                line + 25);
        }
    }
}
