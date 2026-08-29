namespace Moto.Core.Settings;

public partial class SettingsCatalog
{
    public partial class EditorSettings
    {
        public partial class UxSettings
        {
            // 1 ligne = 1 paramètre
            public SettingItem<bool> CompactMode { get; } = new("editor.ux.compactMode", false, "Réduit paddings/marges pour plus de densité");
            public SettingItem<bool> FocusMode { get; } = new("editor.ux.focusMode", false, "Masque les panneaux latéraux pour se concentrer");
            public SettingItem<bool> InlineDiffPreview { get; } = new("editor.ux.inlineDiff", true, "Prévisualisation diff inline avant application");
            public SettingItem<bool> SessionBookmarksEnabled { get; } = new("editor.ux.sessionBookmarks", true, "Mémorise les onglets/positions entre sessions");
            public SettingItem<bool> AdaptiveFontRendering { get; } = new("editor.ux.adaptiveFont", true, "Rendu de police adaptatif selon le DPI");
            public SettingItem<bool> KeyboardFirstOnboarding { get; } = new("editor.ux.keyboardOnboarding", true, "Onboarding orienté clavier au premier lancement");
            public SettingItem<int> ThemeMicroTuningBrightness { get; } = new("editor.ux.themeBrightness", 0, "Micro-ajustement luminosité du thème (-10 à +10)");
            public SettingItem<bool> CommandPaletteEnabled { get; } = new("editor.ux.commandPalette", true, "Active la palette de commandes");
            public SettingItem<bool> ProactiveSuggestionsEnabled { get; } = new("editor.ux.proactiveSuggestions", true, "Active les suggestions proactives");
            public SettingItem<bool> ContextEngineEnabled { get; } = new("editor.ux.contextEngine", true, "Active l'engine de contexte");
        }
    }
}
