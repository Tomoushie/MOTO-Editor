namespace Moto.Core.Settings;

public partial class SettingsCatalog
{
    public partial class EditorSettings
    {
        public UxAdvancedSettings UxAdvanced { get; } = new();

        public partial class UxAdvancedSettings
        {
            // ══ Vague B (P2) ══
            public SettingItem<bool> CompactMode { get; } = new("editor.uxa.compactMode", false, "Mode compact (densité max)");
            public SettingItem<bool> FocusMode { get; } = new("editor.uxa.focusMode", false, "Mode focus (isolation)");
            public SettingItem<bool> DiffInlineAdvanced { get; } = new("editor.uxa.diffInlineAdv", true, "Diff inline avancé");
            public SettingItem<bool> VisualBookmarks { get; } = new("editor.uxa.visualBookmarks", true, "Bookmarks visuels");
            public SettingItem<bool> RefactorInlinePreview { get; } = new("editor.uxa.refactorPreview", true, "Preview refactor inline");
            public SettingItem<bool> AccessibilityAudit { get; } = new("editor.uxa.accessibilityAudit", true, "Audit accessibilité");
            public SettingItem<bool> ContextualHelpOverlay { get; } = new("editor.uxa.contextualHelp", true, "Overlay aide contextuelle");
            public SettingItem<bool> WorkspaceTemplates { get; } = new("editor.uxa.workspaceTemplates", true, "Templates de workspace");
            public SettingItem<bool> DynamicPanels { get; } = new("editor.uxa.dynamicPanels", true, "Panels dynamiques");

            // ══ Vague C (P3) ══
            public SettingItem<bool> AdaptiveFontRendering { get; } = new("editor.uxa.adaptiveFont", true, "Rendu police adaptatif (DPI)");
            public SettingItem<bool> KeyboardFirstOnboarding { get; } = new("editor.uxa.keyboardOnboarding", true, "Onboarding clavier");
            public SettingItem<int> ThemeMicroTuning { get; } = new("editor.uxa.themeMicroTuning", 0, "Micro-réglage thème (-10/+10)");
            public SettingItem<bool> FluidInteractions { get; } = new("editor.uxa.fluidInteractions", true, "Interactions fluides");
            public SettingItem<bool> MicroUxAnimations { get; } = new("editor.uxa.microAnimations", true, "Animations micro-UX");
            public SettingItem<int> AnimationSpeedMs { get; } = new("editor.uxa.animationSpeedMs", 150, "Vitesse animations (ms)");
        }
    }
}
