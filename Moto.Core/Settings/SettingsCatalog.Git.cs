namespace Moto.Core.Settings;

public partial class SettingsCatalog
{
    public static GitSettings Git { get; } = new();

    public partial class GitSettings
    {
        // 1 ligne = 1 paramètre
        public SettingItem<bool> GitEnabled { get; } = new("git.enabled", true, "Active l'intégration Git");
        public SettingItem<bool> AutoFetch { get; } = new("git.autoFetch", true, "Récupère les changements distants automatiquement");
        public SettingItem<int> AutoFetchIntervalMinutes { get; } = new("git.autoFetchIntervalMin", 5, "Intervalle auto-fetch");
        public SettingItem<bool> ConfirmBeforePush { get; } = new("git.confirmBeforePush", true, "Confirmer avant push");
    }
}
