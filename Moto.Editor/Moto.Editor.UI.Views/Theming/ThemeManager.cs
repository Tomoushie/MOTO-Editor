// UI/Theming/ThemeManager.cs
using System;
using System.Drawing;
using System.Windows.Forms;

namespace Moto.Editor.UI.Theming
{
    /// <summary>
    /// Thème clair / sombre pour MOTO Editor.
    /// Aucune dépendance externe : uniquement WinForms natif.
    /// </summary>
    public enum MotoTheme
    {
        Light,
        Dark
    }

    public static class ThemeManager
    {
        /// <summary>
        /// Applique le thème à toute l'interface.
        /// </summary>
        public static void Apply(Control root, MotoTheme theme)
        {
            var colors = theme == MotoTheme.Dark
                ? (
                    Background: Color.FromArgb(17, 18, 20),
                    Surface: Color.FromArgb(27, 28, 31),
                    Accent: Color.FromArgb(0, 122, 204),
                    Text: Color.FromArgb(232, 234, 237),
                    Border: Color.FromArgb(51, 53, 58)
                )
                : (
                    Background: Color.FromArgb(247, 248, 250),
                    Surface: Color.White,
                    Accent: Color.FromArgb(0, 102, 204),
                    Text: Color.FromArgb(24, 26, 30),
                    Border: Color.FromArgb(214, 217, 223)
                );

            ApplyRecursive(
                root,
                colors.Background,
                colors.Surface,
                colors.Accent,
                colors.Text,
                colors.Border
            );
        }

        private static void ApplyRecursive(
            Control control,
            Color background,
            Color surface,
            Color accent,
            Color text,
            Color border)
        {
            control.ForeColor = text;

            if (control is Button button)
            {
                button.BackColor = surface;
                button.FlatStyle = FlatStyle.Flat;
                button.FlatAppearance.BorderSize = 0;
                button.FlatAppearance.BorderColor = border;
                button.Cursor = Cursors.Hand;
            }
            else if (control is TextBox || control is RichTextBox || control is ListView || control is ListBox)
            {
                control.BackColor = surface;
                control.ForeColor = text;
            }
            else if (control is TabControl tabControl)
            {
                tabControl.DrawMode = TabDrawMode.OwnerDrawFixed;
                tabControl.SizeMode = TabSizeMode.Fixed;
                tabControl.ItemSize = new Size(150, 30);
                tabControl.Tag = accent;

                tabControl.DrawItem -= TabControl_DrawItem;
                tabControl.DrawItem += TabControl_DrawItem;
            }
            else
            {
                control.BackColor = background;
            }

            foreach (Control child in control.Controls)
            {
                ApplyRecursive(child, background, surface, accent, text, border);
            }
        }

        /// <summary>
        /// Onglets stylés.
        /// </summary>
        private static void TabControl_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (sender is not TabControl tabControl)
            {
                return;
            }

            var accent = tabControl.Tag is Color accentColor
                ? accentColor
                : Color.DodgerBlue;

            var selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;

            var background = selected
                ? accent
                : Color.FromArgb(35, 36, 40);

            var textColor = selected
                ? Color.White
                : Color.FromArgb(214, 216, 220);

            using var brush = new SolidBrush(background);
            e.Graphics.FillRectangle(brush, e.Bounds);

            TextRenderer.DrawText(
                e.Graphics,
                tabControl.TabPages[e.Index].Text,
                tabControl.Font,
                e.Bounds,
                textColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
            );
        }
    }
}
