// UI/ModernTheme.cs
using System.Drawing;
using System.Windows.Forms;

namespace Moto.Editor.UI
{
    /// <summary>
    /// Thème moderne pour MOTO Editor.
    /// Gère un thème clair et un thème sombre sans dépendance externe.
    /// </summary>
    public enum MotoTheme
    {
        Light,
        Dark
    }

    public static class ModernTheme
    {
        /// <summary>
        /// Applique le thème à toute l'interface.
        /// </summary>
        public static void Apply(Control root, MotoTheme theme)
        {
            var background = theme == MotoTheme.Dark
                ? Color.FromArgb(17, 18, 20)
                : Color.FromArgb(247, 248, 250);

            var surface = theme == MotoTheme.Dark
                ? Color.FromArgb(28, 29, 33)
                : Color.White;

            var text = theme == MotoTheme.Dark
                ? Color.FromArgb(230, 232, 236)
                : Color.FromArgb(24, 26, 30);

            var accent = Color.FromArgb(0, 122, 204);

            ApplyRecursive(root, background, surface, text, accent);
        }

        private static void ApplyRecursive(
            Control control,
            Color background,
            Color surface,
            Color text,
            Color accent)
        {
            if (control is Button button)
            {
                button.BackColor = surface;
                button.ForeColor = text;
                button.FlatStyle = FlatStyle.Flat;
                button.FlatAppearance.BorderSize = 0;
                button.FlatAppearance.BorderColor = accent;
                button.Cursor = Cursors.Hand;
            }
            else if (control is TextBox ||
                     control is RichTextBox ||
                     control is ListBox ||
                     control is ListView)
            {
                control.BackColor = surface;
                control.ForeColor = text;
            }
            else
            {
                control.BackColor = background;
                control.ForeColor = text;
            }

            foreach (Control child in control.Controls)
            {
                ApplyRecursive(child, background, surface, text, accent);
            }
        }
    }
}
