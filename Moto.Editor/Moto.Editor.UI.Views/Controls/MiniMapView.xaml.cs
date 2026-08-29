// Moto.Editor/Controls/MiniMapView.xaml.cs (régénéré v2)
using System;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Moto.Core.Performance;

namespace Moto.Editor.Controls
{
    /// <summary>
    /// Mini-map v2 : compressée et throttlée.
    /// Le XAML reste identique (GraphicsView).
    /// </summary>
    public partial class MiniMapView : ContentView
    {
        private readonly MiniMapCompressor _compressor = new();
        private readonly CompressedMiniMapDrawable _drawable = new();
        private string[] _lines = Array.Empty<string>();

        public MiniMapView()
        {
            InitializeComponent();
            MiniMap.Drawable = _drawable;
        }

        /// <summary>
        /// Met à jour la mini-map.
        /// Le throttle évite de redessiner à chaque frappe.
        /// </summary>
        public void UpdateText(string text)
        {
            _lines = (text ?? string.Empty).Split('\n');

            if (!_compressor.ShouldRender())
            {
                return;
            }

            // Plage visible approximative : premières lignes affichées.
            _drawable.Frame = _compressor.Compress(_lines, 0, Math.Min(_lines.Length, 60));
            MiniMap.Invalidate();
        }
    }

    /// <summary>
    /// Dessin compressé : barres quantifiées + indicateur de viewport.
    /// </summary>
    public class CompressedMiniMapDrawable : IDrawable
    {
        public MiniMapFrame Frame { get; set; } = new();

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            canvas.FillColor = Colors.Gray;

            float width = dirtyRect.Width;
            float height = dirtyRect.Height;

            // Barres compressées.
            foreach (var bar in Frame.Bars)
            {
                float y = bar.Y * height;
                float w = bar.Width * (width - 6f);

                canvas.FillRectangle(3f, y, w, 2f);
            }

            // Indicateur de viewport.
            canvas.FillColor = Colors.White.WithAlpha(0.25f);
            canvas.FillRectangle(
                0f,
                Frame.ViewportY * height,
                width,
                Frame.ViewportHeight * height);
        }
    }
}
