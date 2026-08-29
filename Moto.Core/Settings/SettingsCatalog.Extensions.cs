// Moto.Core/Settings/SettingsCatalog.Extensions.cs (NOUVEAU)
namespace Moto.Core.Settings
{
    /// <summary>
    /// Extensions du catalogue : Version Control, Collaboration,
    /// AI (Agent Configuration + Edit Predictions), Network, Developer.
    /// Aucun paramètre existant n'est modifié ou supprimé.
    /// </summary>
    public static partial class SettingsCatalog
    {
        static partial void RegisterExtensions()
        {
            // ==================== VERSION CONTROL ====================
            T("git_integration", "Version Control", "Git Integration", "Intégration Git", "Active les fonctionnalités Git (panneau, gutter, blame).", true);

            // Git Gutter
            E("git_gutter_visibility", "Version Control", "Git Gutter", "Visibilité", "Affiche le statut Git dans la gouttière de l'éditeur.", "Tracked Files", "Tracked Files", "All Files", "Never");
            I("git_gutter_debounce", "Version Control", "Git Gutter", "Debounce (ms)", "Délai avant mise à jour du gutter.", 0, 0, 5000, 50);

            // Inline Git Blame
            T("git_blame_enabled", "Version Control", "Inline Git Blame", "Activé", "Affiche le blame Git sur la ligne focus.", true);
            E("git_blame_location", "Version Control", "Inline Git Blame", "Position", "Où rendre le blame inline.", "Inline", "Inline", "Right");
            I("git_blame_delay", "Version Control", "Inline Git Blame", "Délai (ms)", "Délai avant affichage du blame.", 0, 0, 5000, 50);
            I("git_blame_padding", "Version Control", "Inline Git Blame", "Padding", "Espacement après la ligne source.", 7, 0, 40);
            I("git_blame_min_column", "Version Control", "Inline Git Blame", "Colonne minimum", "Colonne minimale d'affichage.", 0, 0, 200, 5);
            T("git_blame_commit_summary", "Version Control", "Inline Git Blame", "Résumé du commit", "Affiche le résumé dans le blame.", false);

            // Git Blame View
            T("git_blame_avatar", "Version Control", "Git Blame View", "Afficher l'avatar", "Avatar de l'auteur du commit.", true);

            // Branch Picker
            T("git_branch_author", "Version Control", "Branch Picker", "Nom de l'auteur", "Auteur dans le branch picker.", true);

            // File Diff
            T("git_diff_full_file", "Version Control", "File Diff", "Fichier complet par défaut", "Ouvre le diff complet au lieu des seuls changements.", true);

            // Git Hunks
            E("git_hunk_style", "Version Control", "Git Hunks", "Style des hunks", "Affichage visuel des hunks.", "Staged Hollow", "Staged Hollow", "Solid", "Pattern");
            E("git_diff_base", "Version Control", "Git Hunks", "Base du diff", "HEAD (non commité) ou branche par défaut.", "Head", "Head", "Default Branch");
            E("git_path_style", "Version Control", "Git Hunks", "Style de chemin", "Nom d'abord ou chemin d'abord.", "File Name First", "File Name First", "Path First");
            T("git_stage_restore_buttons", "Version Control", "Git Hunks", "Boutons stage/restore", "Boutons sur les hunks de diff.", true);

            // ==================== COLLABORATION ====================
            T("collab_mute_on_join", "Collaboration", "Calls", "Muet à l'arrivée", "Micro coupé en rejoignant un appel.", false);
            T("collab_share_on_join", "Collaboration", "Calls", "Partager à l'arrivée", "Partage le projet en rejoignant un canal vide.", false);
            A("collab_test_audio", "Collaboration", "Calls", "Tester l'audio", "Teste le micro et les haut-parleurs.", "TestAudio", "Test Audio");
            E("collab_output_device", "Collaboration", "Calls", "Périphérique de sortie", "Sélection du périphérique de sortie.", "System Default", "System Default");
            E("collab_input_device", "Collaboration", "Calls", "Périphérique d'entrée", "Sélection du périphérique d'entrée.", "System Default", "System Default");

            // ==================== AI (étendu) ====================
            // General
            T("ai_disabled", "AI", "General", "Désactiver l'IA", "Désactive toutes les fonctionnalités IA.", false);
            E("threads_sidebar_side", "AI", "General", "Côté de la sidebar threads", "Côté de la fenêtre pour les conversations.", "Left", "Left", "Right");
            A("llm_providers", "AI", "General", "Providers LLM", "Configure les providers de modèles natifs.", "ConfigureProviders");
            A("external_agents", "AI", "General", "Agents externes", "Agents connectés via Agent Client Protocol.", "ExternalAgents");
            A("mcp_servers", "AI", "General", "Serveurs MCP", "Serveurs Model Context Protocol.", "McpServers");

            // Agent Configuration
            A("agent_skills", "AI", "Agent Configuration", "Skills", "Skills installées globalement ou par projet.", "AgentSkills");
            A("agent_sandbox", "AI", "Agent Configuration", "Sandbox", "Permissions du terminal sandbox de l'agent.", "AgentSandbox");
            A("tool_permissions", "AI", "Agent Configuration", "Permissions des outils", "Auto-allow / auto-deny / confirmation par motif.", "ToolPermissions");
            T("single_file_review", "AI", "Agent Configuration", "Revue fichier unique", "Cartes d'édition dans les buffers single-file.", true);
            T("enable_feedback", "AI", "Agent Configuration", "Feedback", "Pouces haut/bas sur les éditions de l'agent.", true);
            E("notify_agent_waiting", "AI", "Agent Configuration", "Notification en attente", "Quand notifier que l'agent attend.", "All Screens", "All Screens", "Active Screen", "Never");
            E("play_sound_agent_done", "AI", "Agent Configuration", "Son de fin d'agent", "Joue un son quand l'agent termine.", "When Hidden", "When Hidden", "Always", "Never");
            T("expand_edit_card", "AI", "Agent Configuration", "Carte d'édition étendue", "Aperçu du diff dans le panneau agent.", true);
            T("expand_terminal_card", "AI", "Agent Configuration", "Carte terminal étendue", "Sortie complète des commandes.", true);
            S("terminal_thread_init_cmd", "AI", "Agent Configuration", "Commande init thread terminal", "Commande auto au démarrage d'un thread terminal.", "");
            E("thinking_display", "AI", "Agent Configuration", "Affichage du raisonnement", "Comment afficher les blocs de pensée.", "Auto", "Auto", "Preview", "Always Expanded", "Always Collapsed");
            T("cancel_on_terminal_stop", "AI", "Agent Configuration", "Annuler à l'arrêt du terminal", "Le stop du terminal annule la génération.", true);
            T("use_modifier_to_send", "AI", "Agent Configuration", "Modificateur pour envoyer", "cmd/ctrl+enter pour envoyer.", false);
            I("message_editor_min_lines", "AI", "Agent Configuration", "Lignes min éditeur", "Lignes minimales de l'éditeur de message.", 4, 1, 20);
            T("show_turn_stats", "AI", "Agent Configuration", "Statistiques du tour", "Temps écoulé et durée du tour.", true);
            T("show_merge_conflict", "AI", "Agent Configuration", "Indicateur de conflit", "Indicateur de conflit de merge dans la barre de statut.", true);
            S("auto_compact_threshold", "AI", "Agent Configuration", "Seuil auto-compact", "Seuil de compactage (ex : 90%).", "90%");

            // Edit Predictions
            A("configure_edit_predictions", "AI", "Edit Predictions", "Providers de prédictions", "Providers de prédictions d'édition.", "ConfigureEditPredictions");
            E("ep_data_collection", "AI", "Edit Predictions", "Collecte de données", "Collecte d'entraînement (open source uniquement).", "Yes", "Yes", "No");
            T("show_edit_predictions", "AI", "Edit Predictions", "Prédictions d'édition", "Affiche les prédictions immédiatement.", true);
            S("ep_disable_language_scope", "AI", "Edit Predictions", "Désactiver par langage", "Scopes de langage désactivés (json).", "[]");
            E("ep_display_mode", "AI", "Edit Predictions", "Mode d'affichage", "Eager (inline) ou Subtle (touche modificateur).", "Eager", "Eager", "Subtle");

            // ==================== NETWORK ====================
            S("network_proxy", "Network", "Network", "Proxy", "Proxy pour les requêtes réseau.", "");
            S("server_url", "Network", "Network", "URL du serveur", "URL du serveur MOTO à contacter.", "https://moto.editor");

            // ==================== DEVELOPER ====================
            T("perf_profiler", "Developer", "Instrumentation", "Profileur de performance", "Collecte les timings des tâches (mémoire accrue).", false);
        }

        /// <summary>Helper pour les paramètres de type Action (bouton).</summary>
        private static void A(string id, string cat, string sec, string title, string desc, string actionId, string label = "Configurer")
        {
            All.Add(new SettingDefinition
            {
                Id = id,
                Category = cat,
                Section = sec,
                Title = title,
                Description = desc,
                Type = SettingType.Action,
                ActionId = actionId,
                ActionLabel = label
            });
        }
    }
}
