namespace Moto.Core.Settings;

public partial class SettingsCatalog
{
    public partial class EditorSettings
    {
        public UpdateSettings Update { get; } = new();

        public partial class UpdateSettings
        {
            // 1 ligne = 1 paramètre
            public SettingItem<bool> AutoCheck { get; } = new("editor.update.autoCheck", true, "Vérifier les mises à jour au démarrage");
            public SettingItem<string> ReleaseUrl { get; } = new("editor.update.releaseUrl", "https://api.github.com/repos/votre-org/moto-editor/releases/latest", "URL de la dernière release");
            public SettingItem<string> Channel { get; } = new("editor.update.channel", "stable", "Canal de mise à jour (stable/beta)");
        }
    }
}
