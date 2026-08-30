namespace Moto.Core.Settings;

public partial class SettingsCatalog
{
    public static CollabSettings Collab { get; } = new();

    public partial class CollabSettings
    {
        // 1 ligne = 1 paramètre
        public SettingItem<bool> ReviewLanesEnabled { get; } = new("collab.review.lanesEnabled", true, "Lanes de review légères sans PR");
        public SettingItem<bool> OfflineReviewQueueEnabled { get; } = new("collab.review.offlineQueue", true, "File de commentaires hors-ligne");
        public SettingItem<bool> SharedScratchpadsEnabled { get; } = new("collab.scratchpads.enabled", true, "Scratchpads partagés P2P");
        public SettingItem<bool> AnnotationLayersEnabled { get; } = new("collab.annotations.enabled", true, "Couches d'annotation par ligne");
        public SettingItem<bool> MeetingNotesLinkingEnabled { get; } = new("collab.annotations.meetingNotes", true, "Notes de réunion liées aux fichiers");
        public SettingItem<bool> PairSessionsEnabled { get; } = new("collab.pair.sessionsEnabled", true, "Sessions pair time-boxed");
        public SettingItem<int> PairSessionDefaultMinutes { get; } = new("collab.pair.defaultMinutes", 25, "Durée par défaut session pair");
        public SettingItem<bool> PresenceAwareSuggestions { get; } = new("collab.presence.gateHeavyAi", true, "Suspend l'IA lourde si beaucoup de collaborateurs");
        public SettingItem<int> PresenceHeavyAiThreshold { get; } = new("collab.presence.heavyAiThreshold", 3, "Nb collaborateurs au-delà duquel l'IA lourde est suspendue");
        public SettingItem<bool> LightweightPrEnabled { get; } = new("collab.pr.enabled", true, "Intégration PR légère (open/close/comment)");
        public SettingItem<bool> SharedRunConfigsEnabled { get; } = new("collab.runconfigs.enabled", true, "Configurations de lancement partagées");
        public SettingItem<bool> WhiteboardEnabled { get; } = new("collab.whiteboard.enabled", true, "Tableau blanc vectoriel léger");
        public SettingItem<bool> RoleBasedUiEnabled { get; } = new("collab.roles.enabled", true, "UI adaptée au rôle (editor/reviewer/observer)");
    }
}
