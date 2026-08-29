// UI/Modern/IconFactory.cs
using System.Drawing;
using System.Drawing.Text;

namespace Moto.Editor.UI.Modern
{
    /// <summary>
    /// Fabrique d'icônes maison.
    /// Génère des icônes vectorielles simples sans assets externes.
    /// </summary>
    public static class IconFactory
    {
        public static Bitmap CreateGlyphIcon(string glyph, Color color, int size = 20)
        {
            var bitmap = new Bitmap(size, size);

            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                graphics.Clear(Color.Transparent);

                using var brush = new SolidBrush(color);
                using var font = new Font("Segoe UI", size * 0.55F);
                using var format = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };

                graphics.DrawString(
                    glyph,
                    font,
                    brush,
                    new RectangleF(0, 0, size, size),
                    format
                );
            }

            return bitmap;
        }
    }
}
