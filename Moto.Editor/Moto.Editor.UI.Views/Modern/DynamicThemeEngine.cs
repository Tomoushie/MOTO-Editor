// UI/Modern/DynamicThemeEngine.cs
using System;
using System.Drawing;
using System.Windows.Forms;

namespace Moto.Editor.UI.Modern
{
    public enum MotoTheme
    {
        System,
        Light,
        Dark
    }

    /// <summary>
    /// Thème dynamique clair / sombre.
    /// </summary>
    public class DynamicThemeEngine
    {
        public MotoTheme Current { get; private set; } = MotoTheme.Dark;

        public event Action<MotoTheme> ThemeChanged;

        public void SetTheme(MotoTheme theme)
        {
            Current = theme;
            ThemeChanged?.Invoke(theme);
        }

        public void Apply(Form root)
        {
            bool dark = Current != MotoTheme.Light;

            var background = dark
                ? Color.FromArgb(17, 18, 20)
                : Color.FromArgb(247, 248, 250);

            var surface = dark
                ? Color.FromArgb(27, 28, 31)
                : Color.White;

            var text = dark
                ? Color.FromArgb(232, 234, 237)
                : Color.FromArgb(24, 26, 30);

            ApplyRecursive(root, background, surface, text);
        }

        private void ApplyRecursive(Control control, Color background, Color surface, Color text)
        {
            control.BackColor = control is Button ? surface : background;
            control.ForeColor = text;

            foreach (Control child in control.Controls)
            {
                ApplyRecursive(child, background, surface, text);
            }
        }
    }
}
