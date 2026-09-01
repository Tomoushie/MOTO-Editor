namespace Moto.UI.DesignSystem
{
    public static class Palette
    {
        // Fond principal : gris anthracite neutre
        public static readonly Color Background = Color.FromArgb("#121212");
        public static readonly Color Surface = Color.FromArgb("#1E1E1E");
        public static readonly Color Panel = Color.FromArgb("#242424");

        // Couleurs d’accent : bleu électrique et or industriel
        public static readonly Color AccentPrimary = Color.FromArgb("#0078D4"); // Bleu Microsoft
        public static readonly Color AccentSecondary = Color.FromArgb("#CBA135"); // Or chaud

        // Texte : contraste fort et lisibilité AAA
        public static readonly Color TextPrimary = Color.FromArgb("#FFFFFF");
        public static readonly Color TextSecondary = Color.FromArgb("#B0B0B0");
        public static readonly Color TextDisabled = Color.FromArgb("#606060");

        // États interactifs
        public static readonly Color Hover = Color.FromArgb("#2A2A2A");
        public static readonly Color Active = Color.FromArgb("#0078D4");
        public static readonly Color Error = Color.FromArgb("#FF4C4C");
        public static readonly Color Success = Color.FromArgb("#4CAF50");
    }
}
