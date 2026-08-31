// Moto.Editor/Controls/BudgetRingDrawable.cs
// ★ AJOUT (31/08, point 7) : Tom voulait que la pastille budget ne soit plus
// pleine orange, mais gris/blanc semi-transparent quand vide, se remplissant
// progressivement (orange ou bleu) à mesure que le budget diminue. Comme
// MOTO Editor n'a pas de vraie limite de tokens (tout est local/MOTO AI —
// "Token : infini", déjà écrit ailleurs dans ce menu), UsedRatio reste à 0
// pour l'instant : l'anneau est vide, honnêtement, plutôt que de simuler une
// consommation qui n'existe pas. Le mécanisme de remplissage est réel et
// prêt, dès qu'une vraie notion de budget existera.
// API IDrawable/ICanvas standard MAUI (Microsoft.Maui.Graphics) — pas de
// dépendance externe.
using Microsoft.Maui.Graphics;

namespace Moto.Editor.Controls
{
    public sealed class BudgetRingDrawable : IDrawable
    {
        /// <summary>0..1 : part du budget consommée (0 = vide, honnête pour l'instant).</summary>
        public float UsedRatio { get; set; }

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            float size = System.Math.Min(dirtyRect.Width, dirtyRect.Height);
            float stroke = System.Math.Max(2f, size * 0.22f);
            float radius = (size - stroke) / 2f;
            var ring = new RectF(
                (dirtyRect.Width - radius * 2) / 2f,
                (dirtyRect.Height - radius * 2) / 2f,
                radius * 2, radius * 2);

            // Anneau de base (vide) : blanc légèrement transparent mais visible.
            canvas.StrokeColor = Colors.White.WithAlpha(0.28f);
            canvas.StrokeSize = stroke;
            canvas.DrawEllipse(ring);

            // Arc de remplissage proportionnel au budget consommé (bleu jusqu'à
            // 75%, puis orange — vide pour l'instant, voir commentaire ci-dessus).
            if (UsedRatio > 0f)
            {
                canvas.StrokeColor = UsedRatio > 0.75f ? Colors.Orange : Colors.DodgerBlue;
                canvas.StrokeSize = stroke;
                float sweep = 360f * System.Math.Clamp(UsedRatio, 0f, 1f);
                canvas.DrawArc(ring, -90f, -90f + sweep, false, false);
            }
        }
    }
}
