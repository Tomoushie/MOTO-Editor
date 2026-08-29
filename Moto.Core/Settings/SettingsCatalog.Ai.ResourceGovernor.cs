namespace Moto.Core.Settings;

public partial class SettingsCatalog
{
    public partial class AiSettings
    {
        public partial class AdvancedSettings
        {
            // 1 ligne = 1 paramètre
            public SettingItem<bool> CooperativeResourceMode { get; } = new("ai.resource.cooperativeMode", true, "Réduit l'empreinte MOTO si un modèle local externe tourne");
            public SettingItem<int> CooperativeScanIntervalSeconds { get; } = new("ai.resource.scanIntervalSec", 5, "Intervalle de détection des modèles externes");
            public SettingItem<bool> TrimMemoryOnCooperative { get; } = new("ai.resource.trimMemory", true, "Libère la mémoire inutilisée en mode coopératif");
            public SettingItem<bool> LowerPriorityOnCooperative { get; } = new("ai.resource.lowerPriority", true, "Abaisse la priorité du processus en mode coopératif");
        }
    }
}
