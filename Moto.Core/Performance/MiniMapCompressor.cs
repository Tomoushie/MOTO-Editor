// Moto.Core/Performance/MiniMapCompressor.cs
using System;
using System.Collections.Generic;

namespace Moto.Core.Performance
{
    public class MiniMapBar
    {
        /// <summary>Position verticale normalisée (0..1).</summary>
        public float Y { get; set; }

        /// <summary>Largeur normalisée (0..1) proportionnelle à la longueur de ligne.</summary>
        public float Width { get; set; }
    }

    public class MiniMapFrame
    {
        public List<MiniMapBar> Bars { get; } = new();
        public float ViewportY { get; set; }
        public float ViewportHeight { get; set; }
        public int LinesPerBar { get; set; } = 1;
    }

    /// <summary>
    /// 20. Mini-map compressée.
    /// - Downsampling : 1 barre = N lignes (adaptatif à la taille du fichier).
    /// - Throttle : au maximum 1 rendu toutes les RefreshIntervalMs.
    /// - Largeurs quantifiées : moins de pixels à dessiner.
    /// </summary>
    public class MiniMapCompressor
    {
        /// <summary>Nombre maximal de barres dessinées.</summary>
        public int MaxBars { get; set; } = 100;

        /// <summary>Intervalle minimal entre deux rendus (throttle).</summary>
        public int RefreshIntervalMs { get; set; } = 400;

        private DateTime _lastRenderUtc = DateTime.MinValue;

        /// <summary>True si un rendu est autorisé (throttle).</summary>
        public bool ShouldRender()
        {
            var now = DateTime.UtcNow;

            if ((now - _lastRenderUtc).TotalMilliseconds < RefreshIntervalMs)
            {
                return false;
            }

            _lastRenderUtc = now;
            return true;
        }

        /// <summary>
        /// Compresse le texte en barres + position du viewport.
        /// </summary>
        public MiniMapFrame Compress(string[] lines, int viewportStart, int viewportEnd)
        {
            var frame = new MiniMapFrame();

            if (lines == null || lines.Length == 0)
            {
                return frame;
            }

            // Downsampling adaptatif : 1 barre regroupe N lignes.
            int linesPerBar = Math.Max(1, (int)Math.Ceiling(lines.Length / (double)MaxBars));
            frame.LinesPerBar = linesPerBar;

            int barCount = (int)Math.Ceiling(lines.Length / (double)linesPerBar);

            for (int b = 0; b < barCount; b++)
            {
                int maxLen = 0;

                int from = b * linesPerBar;
                int to = Math.Min(lines.Length, from + linesPerBar);

                for (int i = from; i < to; i++)
                {
                    maxLen = Math.Max(maxLen, Math.Min(lines[i].Length, 120));
                }

                // Largeur quantifiée par pas de 0.1 : moins de redraws.
                float width = MathF.Round((maxLen / 120f) * 10f) / 10f;

                if (width > 0.05f)
                {
                    frame.Bars.Add(new MiniMapBar
                    {
                        Y = b / (float)barCount,
                        Width = width
                    });
                }
            }

            // Viewport normalisé pour l'indicateur de position.
            frame.ViewportY = viewportStart / (float)lines.Length;
            frame.ViewportHeight = Math.Max(0.02f, (viewportEnd - viewportStart) / (float)lines.Length);

            return frame;
        }
    }
}
