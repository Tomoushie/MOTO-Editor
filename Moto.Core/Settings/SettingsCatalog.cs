// Moto.Core/Settings/SettingsCatalog.cs (régénéré complètement)
using System.Collections.Generic;

namespace Moto.Core.Settings
{
    /// <summary>
    /// Catalogue complet des paramètres de MOTO Editor.
    /// Classe PARTIELLE : les catégories étendues (Version Control, Collaboration,
    /// AI étendu, Network, Developer) sont dans SettingsCatalog.Extensions.cs
    /// via la méthode partielle RegisterExtensions().
    /// Aucun paramètre supprimé.
    /// </summary>
    public static partial class SettingsCatalog
    {
        public static List<SettingDefinition> All { get; } = new List<SettingDefinition>();

        static SettingsCatalog()
        {
            // ==================== GÉNÉRAL ====================
            T("accessible_mode", "Général", "Général", "Mode accessible", "Optimise l'interface pour les technologies d'assistance.", false);
            E("close_no_tabs", "Général", "Général", "Fermeture sans onglets", "Action quand on ferme l'élément actif sans onglets.", "Platform Default", "Platform Default", "Close Window", "Keep Window");
            E("last_window_closed", "Général", "Général", "Dernière fenêtre fermée", "Comportement à la fermeture de la dernière fenêtre.", "Platform Default", "Platform Default", "Quit", "Keep");
            T("system_path_prompts", "Général", "Général", "Boîtes de dialogue système", "Utiliser les dialogues natifs pour Ouvrir/Enregistrer.", true);
            T("system_prompts", "Général", "Général", "Confirmations système", "Utiliser les dialogues natifs pour les confirmations.", true);
            T("redact_private", "Général", "Général", "Masquer les valeurs privées", "Cache les valeurs des variables dans les fichiers privés.", true);
            S("private_files", "Général", "Général", "Fichiers privés", "Globs considérés comme privés.", "*.env;*.pem;*.key");
            T("trust_all_projects", "Général", "Sécurité", "Confiance aux projets", "Évite le mode restreint pour tous les projets.", false);
            T("restore_unsaved", "Général", "Restauration", "Restaurer les buffers non sauvegardés", "Restaure les fichiers modifiés au redémarrage.", true);
            E("restore_on_startup", "Général", "Restauration", "Restauration au démarrage", "Quoi restaurer de la session précédente.", "Last Session", "Last Session", "None");
            T("telemetry_diagnostics", "Général", "Confidentialité", "Télémétrie diagnostics", "Envoi de rapports de crash (désactivé par défaut).", false);
            T("telemetry_metrics", "Général", "Confidentialité", "Télémétrie usage", "Envoi de données d'usage (désactivé par défaut).", false);
            T("auto_update", "Général", "Mise à jour", "Mise à jour automatique", "Vérifier automatiquement les mises à jour.", true);

            // ==================== APPARENCE ====================
            E("theme_mode", "Apparence", "Thème", "Mode du thème", "Thème statique ou dynamique.", "Dynamic", "Dynamic", "Light", "Dark");
            E("light_theme", "Apparence", "Thème", "Thème clair", "Thème utilisé en mode clair.", "One Light", "One Light", "Solarized Light");
            E("dark_theme", "Apparence", "Thème", "Thème sombre", "Thème utilisé en mode sombre.", "One Dark", "One Dark", "Solarized Dark");
            S("buffer_font_family", "Apparence", "Police éditeur", "Police de l'éditeur", "Famille de police du texte.", "Consolas");
            I("buffer_font_size", "Apparence", "Police éditeur", "Taille police éditeur", "Taille du texte de l'éditeur.", 14, 8, 40);
            I("buffer_font_weight", "Apparence", "Police éditeur", "Graisse police éditeur", "Graisse du texte (100-900).", 400, 100, 900, 100);
            E("line_height", "Apparence", "Police éditeur", "Hauteur de ligne", "Hauteur de ligne du texte.", "Comfortable", "Comfortable", "Standard");
            S("ui_font_family", "Apparence", "Police UI", "Police de l'interface", "Famille de police des éléments UI.", "Segoe UI");
            I("ui_font_size", "Apparence", "Police UI", "Taille police UI", "Taille des éléments UI.", 14, 8, 30);
            I("agent_font_size", "Apparence", "Police agent", "Taille police agent", "Taille du texte du panneau IA.", 13, 8, 30);
            T("cursor_blink", "Apparence", "Curseur", "Curseur clignotant", "Le curseur clignote dans l'éditeur.", true);
            E("cursor_shape", "Apparence", "Curseur", "Forme du curseur", "Forme du curseur d'édition.", "Bar", "Bar", "Block", "Underscore");
            T("reduce_motion", "Apparence", "Curseur", "Réduire les animations", "Désactive les mouvements non essentiels.", false);
            I("code_fade", "Apparence", "Surbrillance", "Estompage du code inutile", "Intensité d'estompage (0-100).", 30, 0, 100, 5);
            E("current_line_highlight", "Apparence", "Surbrillance", "Ligne courante", "Comment surligner la ligne courante.", "All", "All", "Line", "Gutter", "None");
            T("selection_highlight", "Apparence", "Surbrillance", "Surligner la sélection", "Surligne les occurrences sélectionnées.", true);
            T("rounded_selection", "Apparence", "Surbrillance", "Sélection arrondie", "Coins arrondis sur la sélection.", true);
            T("indent_guides", "Apparence", "Guides", "Guides d'indentation", "Guides verticaux d'indentation.", true);
            T("wrap_guides", "Apparence", "Guides", "Guides de retour ligne", "Colonnes de retour ligne.", false);

            // ==================== RACCOURCIS ====================
            E("base_keymap", "Raccourcis", "Raccourcis", "Keymap de base", "Jeu de raccourcis de référence.", "VSCode", "VSCode", "Zed", "Emacs", "SublimeText");
            T("vim_mode", "Raccourcis", "Édition modale", "Mode Vim", "Active le mode Vim.", false);
            T("helix_mode", "Raccourcis", "Édition modale", "Mode Helix", "Active le mode Helix.", false);

            // ==================== ÉDITEUR ====================
            E("auto_save", "Éditeur", "Enregistrement auto", "Enregistrement automatique", "Quand enregistrer automatiquement.", "Off", "Off", "On Focus Change", "After Delay");
            I("auto_save_delay", "Éditeur", "Enregistrement auto", "Délai auto-save (ms)", "Délai avant enregistrement.", 1000, 200, 10000, 100);
            T("scroll_beyond_last_line", "Éditeur", "Défilement", "Défiler après la fin", "Défilement au-delà de la dernière ligne.", false);
            I("vertical_scroll_margin", "Éditeur", "Défilement", "Marge défilement vertical", "Lignes visibles autour du curseur.", 1, 0, 20);
            T("hover_popover", "Éditeur", "Survol", "Popover de survol", "Documentation au survol.", true);
            T("signature_help", "Éditeur", "Survol", "Aide à la signature", "Aide aux paramètres de fonctions.", true);
            T("show_gutter", "Éditeur", "Gouttière", "Afficher la gouttière", "Colonne des numéros de ligne.", true);
            T("line_numbers", "Éditeur", "Gouttière", "Numéros de ligne", "Affiche les numéros de ligne.", true);
            T("relative_line_numbers", "Éditeur", "Gouttière", "Numéros relatifs", "Numéros relatifs au curseur.", false);
            T("scrollbar_show", "Éditeur", "Barre de défilement", "Barre de défilement", "Affiche la barre de défilement.", true);
            T("scrollbar_diagnostics", "Éditeur", "Barre de défilement", "Diagnostics dans la barre", "Marque erreurs/warnings.", true);
            T("minimap_show", "Éditeur", "Mini-carte", "Afficher la mini-carte", "Mini-map compressée.", true);
            I("minimap_max_width", "Éditeur", "Mini-carte", "Largeur max mini-carte", "Largeur maximale en pixels.", 100, 40, 300, 10);
            I("tab_size", "Éditeur", "Indentation", "Taille des tabulations", "Espaces par tabulation.", 4, 1, 16);
            T("hard_tabs", "Éditeur", "Indentation", "Tabulations réelles", "Tabulations au lieu d'espaces.", false);
            T("auto_indent", "Éditeur", "Indentation", "Indentation automatique", "Indentation des nouvelles lignes.", true);
            E("soft_wrap", "Éditeur", "Retour ligne", "Retour ligne automatique", "Mode de retour ligne.", "Editor Width", "Editor Width", "Preferred Line Length", "Off");
            I("preferred_line_length", "Éditeur", "Retour ligne", "Longueur de ligne préférée", "Colonne de retour ligne.", 100, 40, 200, 10);
            T("format_on_save", "Éditeur", "Formatage", "Formater à l'enregistrement", "Formate à la sauvegarde.", false);
            T("autoclose_brackets", "Éditeur", "Fermeture auto", "Fermer les accolades", "Ferme ( ) [ ] { }.", true);
            T("autoclose_quotes", "Éditeur", "Fermeture auto", "Fermer les guillemets", "Ferme \" et '.", true);
            T("show_whitespace", "Éditeur", "Espaces", "Afficher les espaces", "Visualise les espaces.", false);
            T("remove_trailing_whitespace", "Éditeur", "Espaces", "Nettoyer les fins de ligne", "Supprime les espaces de fin.", true);
            T("completions_enabled", "Éditeur", "Complétion", "Complétion activée", "Active l'autocomplétion.", true);
            I("fetch_timeout", "Éditeur", "Complétion", "Timeout complétion (ms)", "0 = attendre indéfiniment.", 0, 0, 10000, 500);
            T("inlay_hints", "Éditeur", "Inlay Hints", "Inlay hints", "Indications inline.", false);

            // ==================== RECHERCHE & FICHIERS ====================
            T("search_whole_word", "Recherche & Fichiers", "Recherche", "Mot entier", "Recherche par mots entiers par défaut.", false);
            T("search_case_sensitive", "Recherche & Fichiers", "Recherche", "Sensible à la casse", "Recherche sensible à la casse.", false);
            T("search_smartcase", "Recherche & Fichiers", "Recherche", "Smartcase", "Casse automatique selon la requête.", false);
            T("search_include_ignored", "Recherche & Fichiers", "Recherche", "Inclure les ignorés", "Inclut les fichiers ignorés.", false);
            T("search_regex", "Recherche & Fichiers", "Recherche", "Regex", "Recherche par expressions régulières.", false);
            T("search_wrap", "Recherche & Fichiers", "Recherche", "Boucler la recherche", "La recherche reboucle au début.", true);
            T("search_center_on_match", "Recherche & Fichiers", "Recherche", "Centrer sur le résultat", "Centre l'éditeur sur le match.", false);
            E("seed_search_from_cursor", "Recherche & Fichiers", "Recherche", "Requête depuis le curseur", "Pré-remplit la requête avec le mot sous le curseur.", "Always", "Always", "Never", "On Selection");
            E("file_finder_include_ignored", "Recherche & Fichiers", "File Finder", "Ignorés dans le finder", "Utilise les fichiers gitignorés.", "Smart", "Smart", "Always", "Never");
            T("file_finder_icons", "Recherche & Fichiers", "File Finder", "Icônes de fichiers", "Icônes dans le file finder.", true);
            T("file_finder_skip_focus", "Recherche & Fichiers", "File Finder", "Skip focus actif", "Ne pas focus le fichier actif dans les résultats.", true);
            S("file_scan_exclusions", "Recherche & Fichiers", "File Scan", "Exclusions de scan", "Globs exclus du scan (json).", "[\"**/.git\",\"**/node_modules\"]");
            S("file_scan_inclusions", "Recherche & Fichiers", "File Scan", "Inclusions de scan", "Globs toujours inclus (json).", "[]");
            I("file_scan_depth", "Recherche & Fichiers", "File Scan", "Profondeur de scan", "Profondeur d'indexation (0 = illimité).", 5, 0, 20);
            E("scan_symbolic_links", "Recherche & Fichiers", "File Scan", "Liens symboliques", "Quand scanner les liens symboliques.", "Expanded", "Expanded", "Never", "Always");
            T("restore_file_state", "Recherche & Fichiers", "File Scan", "Restaurer l'état des fichiers", "Restaure l'état à la réouverture.", true);
            T("close_on_file_delete", "Recherche & Fichiers", "File Scan", "Fermer si fichier supprimé", "Ferme les onglets des fichiers supprimés.", false);

            // ==================== LANGAGES & OUTILS ====================
            T("lsp_enabled", "Langages & Outils", "LSP", "Activer le LSP", "Moteur de langage maison.", true);
            T("lsp_completions", "Langages & Outils", "LSP", "Complétion LSP", "Complétion via le moteur.", true);
            T("lsp_diagnostics", "Langages & Outils", "LSP", "Diagnostics LSP", "Diagnostics via le moteur.", true);
            T("lsp_highlights", "Langages & Outils", "LSP", "Highlights LSP", "Surbrillance sémantique.", true);
            E("max_severity", "Langages & Outils", "Diagnostics", "Sévérité maximale", "Filtrage des diagnostics.", "All", "All", "Warning", "Error");
            T("include_warnings", "Langages & Outils", "Diagnostics", "Inclure les warnings", "Affiche les avertissements.", true);
            T("inline_diagnostics", "Langages & Outils", "Diagnostics inline", "Diagnostics inline", "Erreurs dans le texte.", false);
            T("prettier_allowed", "Langages & Outils", "Prettier", "Autoriser Prettier", "Formatage Prettier si présent.", false);
            S("file_types", "Langages & Outils", "Types de fichiers", "Associations de fichiers", "Mapping extensions → langage.", "{}");

            // ==================== DÉBOGUEUR ====================
            E("stepping_granularity", "Débogueur", "Général", "Granularité du pas-à-pas", "Granularité des opérations de debug.", "Line", "Line", "Statement", "Instruction");
            T("save_breakpoints", "Débogueur", "Général", "Sauvegarder les breakpoints", "Breakpoints réutilisés entre sessions.", true);
            I("debugger_timeout", "Débogueur", "Général", "Timeout (ms)", "Timeout de connexion à l'adaptateur de debug.", 2000, 100, 30000, 100);
            T("log_dap", "Débogueur", "Général", "Logger les communications DAP", "Journalise les échanges DAP.", true);
            T("format_dap_logs", "Débogueur", "Général", "Formater les logs DAP", "Formate les messages DAP loggés.", true);

            // ==================== TERMINAL ====================
            E("terminal_shell", "Terminal", "Environnement", "Shell", "Shell à utiliser.", "System", "System", "cmd", "PowerShell", "bash");
            E("terminal_working_dir", "Terminal", "Environnement", "Répertoire de travail", "Répertoire au lancement.", "Current Project Directory", "Current Project Directory", "Home", "Custom");
            S("terminal_env_vars", "Terminal", "Environnement", "Variables d'environnement", "Paires clé-valeur (json).", "{}");
            T("terminal_detect_venv", "Terminal", "Environnement", "Détecter les env virtuels", "Active l'env Python virtuel si trouvé.", true);
            I("terminal_font_size", "Terminal", "Police", "Taille police terminal", "Taille du texte du terminal.", 15, 8, 30);
            S("terminal_font_family", "Terminal", "Police", "Police du terminal", "Famille de police du terminal.", "Consolas");
            I("terminal_font_weight", "Terminal", "Police", "Graisse police terminal", "Graisse (100-900).", 400, 100, 900, 100);
            E("terminal_cursor_shape", "Terminal", "Affichage", "Forme du curseur", "Curseur du terminal.", "Block", "Block", "Bar", "Underline", "Hollow");
            E("terminal_cursor_blinking", "Terminal", "Affichage", "Clignotement curseur", "Comportement de clignotement.", "Terminal Controlled", "Terminal Controlled", "On", "Off");
            E("terminal_alternate_scroll", "Terminal", "Affichage", "Alternate scroll", "Scroll alternatif (apps type Vim).", "On", "On", "Off");
            I("terminal_min_contrast", "Terminal", "Affichage", "Contraste minimal", "Contraste APCA minimal (0-106).", 45, 0, 106);
            T("terminal_option_as_meta", "Terminal", "Comportement", "Option comme Meta", "Touche Option = touche Meta.", false);
            T("terminal_copy_on_select", "Terminal", "Comportement", "Copier la sélection", "Copie auto la sélection.", true);
            T("terminal_keep_selection_on_copy", "Terminal", "Comportement", "Garder la sélection", "Conserve la sélection après copie.", true);
            T("terminal_open_links_mouse", "Terminal", "Comportement", "Liens en mode souris", "Ouvre les liens même en mode souris.", true);
            E("terminal_audible_bell", "Terminal", "Comportement", "Sonnerie audible", "Joue un son sur le caractère BEL.", "Off", "Off", "On");
            I("terminal_default_width", "Terminal", "Layout", "Largeur par défaut", "Largeur du terminal docké (px).", 640, 200, 2000, 20);
            I("terminal_default_height", "Terminal", "Layout", "Hauteur par défaut", "Hauteur du terminal docké (px).", 320, 100, 1200, 20);
            I("terminal_max_scroll_lines", "Terminal", "Avancé", "Lignes d'historique max", "Lignes conservées en scrollback.", 10000, 0, 100000, 500);
            I("terminal_scroll_multiplier", "Terminal", "Avancé", "Multiplicateur de scroll", "Multiplicateur de la molette.", 1, 1, 10);
            T("terminal_breadcrumbs", "Terminal", "Toolbar", "Breadcrumbs", "Titre du terminal en breadcrumbs.", false);
            E("terminal_show_scrollbar", "Terminal", "Scrollbar", "Barre de défilement", "Quand afficher la scrollbar.", "Auto", "Auto", "Always", "Never");

            // ==================== FENÊTRE & LAYOUT ====================
            T("sb_project_panel", "Fenêtre & Layout", "Status Bar", "Bouton panneau projet", "Bouton projet dans la barre de statut.", true);
            T("sb_language", "Fenêtre & Layout", "Status Bar", "Bouton langage actif", "Langage actif dans la barre de statut.", true);
            T("sb_encoding", "Fenêtre & Layout", "Status Bar", "Bouton encodage", "Encodage actif dans la barre de statut.", false);
            T("sb_cursor_position", "Fenêtre & Layout", "Status Bar", "Bouton position curseur", "Position du curseur dans la barre de statut.", true);
            T("sb_line_endings", "Fenêtre & Layout", "Status Bar", "Bouton fins de ligne", "Fins de ligne dans la barre de statut.", false);
            T("sb_terminal", "Fenêtre & Layout", "Status Bar", "Bouton terminal", "Bouton terminal dans la barre de statut.", true);
            T("sb_diagnostics", "Fenêtre & Layout", "Status Bar", "Bouton diagnostics", "Compteurs erreurs/warnings.", true);
            T("sb_search", "Fenêtre & Layout", "Status Bar", "Bouton recherche", "Bouton recherche projet.", true);
            T("sb_debugger", "Fenêtre & Layout", "Status Bar", "Bouton débogueur", "Bouton débogueur.", true);
            T("sb_active_file", "Fenêtre & Layout", "Status Bar", "Nom du fichier actif", "Nom du fichier dans la barre de statut.", false);
            T("tb_branch_icon", "Fenêtre & Layout", "Title Bar", "Icône statut branche", "Indicateur git sur l'icône de branche.", false);
            T("tb_branch_name", "Fenêtre & Layout", "Title Bar", "Nom de la branche", "Nom de branche dans la titlebar.", true);
            T("tb_worktree", "Fenêtre & Layout", "Title Bar", "Nom du worktree", "Worktree dans la titlebar.", true);
            T("tb_project_items", "Fenêtre & Layout", "Title Bar", "Éléments du projet", "Hôte et nom du projet.", true);
            T("tb_onboarding", "Fenêtre & Layout", "Title Bar", "Bannière d'accueil", "Bannière des nouvelles fonctionnalités.", true);
            T("tb_sign_in", "Fenêtre & Layout", "Title Bar", "Bouton connexion", "Bouton de connexion.", true);
            T("tb_user_menu", "Fenêtre & Layout", "Title Bar", "Menu utilisateur", "Menu utilisateur.", true);
            T("tb_user_picture", "Fenêtre & Layout", "Title Bar", "Photo utilisateur", "Photo de l'utilisateur.", true);
            T("tb_menus", "Fenêtre & Layout", "Title Bar", "Afficher les menus", "Menus dans la titlebar.", false);
            E("tb_button_layout", "Fenêtre & Layout", "Title Bar", "Disposition des boutons", "Position des contrôles de fenêtre.", "Platform Default", "Platform Default", "Left", "Right");
            T("tabs_show", "Fenêtre & Layout", "Tab Bar", "Barre d'onglets", "Affiche la barre d'onglets.", true);
            T("tabs_git_status", "Fenêtre & Layout", "Tab Bar", "Statut git dans les onglets", "Statut git sur les onglets.", false);
            T("tabs_file_icons", "Fenêtre & Layout", "Tab Bar", "Icônes dans les onglets", "Icônes de fichier dans les onglets.", false);
            E("tabs_close_position", "Fenêtre & Layout", "Tab Bar", "Position du bouton fermer", "Position du bouton de fermeture.", "Right", "Right", "Left");
            I("tabs_max", "Fenêtre & Layout", "Tab Bar", "Onglets maximum", "0 = illimité.", 0, 0, 50);
            T("tabs_nav_buttons", "Fenêtre & Layout", "Tab Bar", "Boutons d'historique", "Boutons précédent/suivant.", true);
            T("tabs_bar_buttons", "Fenêtre & Layout", "Tab Bar", "Boutons de la barre", "Boutons New/Split/Zoom.", true);
            T("tabs_pinned_layout", "Fenêtre & Layout", "Tab Bar", "Onglets épinglés séparés", "Rangée séparée au-dessus.", false);
            E("tabs_activate_on_close", "Fenêtre & Layout", "Tab Settings", "Activer à la fermeture", "Onglet activé après fermeture.", "History", "History", "Neighbour", "Left Neighbour");
            E("tabs_show_diagnostics", "Fenêtre & Layout", "Tab Settings", "Diagnostics dans les onglets", "Erreurs/warnings dans les onglets.", "Off", "Off", "On");
            E("tabs_show_close", "Fenêtre & Layout", "Tab Settings", "Bouton fermer", "Comportement du bouton fermer.", "Hover", "Hover", "Always", "Hidden");
            T("preview_enabled", "Fenêtre & Layout", "Preview Tabs", "Onglets aperçu", "Onglets temporaires en aperçu.", true);
            T("preview_project_panel", "Fenêtre & Layout", "Preview Tabs", "Aperçu depuis le projet", "Aperçu au clic simple du panneau projet.", true);
            T("preview_file_finder", "Fenêtre & Layout", "Preview Tabs", "Aperçu depuis le finder", "Aperçu depuis le file finder.", false);
            T("preview_multibuffer", "Fenêtre & Layout", "Preview Tabs", "Aperçu multi-buffer", "Aperçu depuis les résultats de recherche.", true);
            T("preview_code_nav", "Fenêtre & Layout", "Preview Tabs", "Aperçu navigation code", "Aperçu lors de la navigation (définition...).", false);
            T("preview_keep_on_nav", "Fenêtre & Layout", "Preview Tabs", "Garder l'aperçu en navigation", "Conserve l'aperçu lors de la navigation.", false);
            E("bottom_dock_layout", "Fenêtre & Layout", "Layout", "Layout du dock bas", "Disposition du dock inférieur.", "Contained", "Contained", "Full", "Left Aligned", "Right Aligned");
            I("centered_left_padding", "Fenêtre & Layout", "Layout", "Padding gauche centré", "Padding gauche du layout centré (0-100).", 20, 0, 100, 5);
            I("centered_right_padding", "Fenêtre & Layout", "Layout", "Padding droit centré", "Padding droit du layout centré (0-100).", 20, 0, 100, 5);
            T("focus_follows_mouse", "Fenêtre & Layout", "Layout", "Focus suit la souris", "Focus au survol des panneaux.", false);
            I("focus_follows_debounce", "Fenêtre & Layout", "Layout", "Debounce focus (ms)", "Délai avant changement de focus.", 250, 0, 2000, 50);
            T("use_system_window_tabs", "Fenêtre & Layout", "Window", "Onglets de fenêtre système", "macOS : fenêtres en onglets.", false);
            E("fullscreen_mode", "Fenêtre & Layout", "Window", "Mode plein écran", "Comportement du plein écran.", "Native", "Native", "Immersion");
            E("window_decorations", "Fenêtre & Layout", "Window", "Décorations de fenêtre", "Décorations client ou serveur.", "Client", "Client", "Server");
            I("inactive_opacity", "Fenêtre & Layout", "Pane Modifiers", "Opacité inactive", "Opacité des panneaux inactifs (0-100).", 100, 0, 100, 5);
            I("border_size", "Fenêtre & Layout", "Pane Modifiers", "Taille des bordures", "Bordure autour du panneau actif.", 0, 0, 10);
            T("zoomed_padding", "Fenêtre & Layout", "Pane Modifiers", "Padding zoomé", "Padding des panneaux zoomés.", true);
            E("vertical_split_direction", "Fenêtre & Layout", "Pane Split Direction", "Split vertical", "Direction du split vertical.", "Right", "Right", "Left");
            E("horizontal_split_direction", "Fenêtre & Layout", "Pane Split Direction", "Split horizontal", "Direction du split horizontal.", "Down", "Down", "Up");

            // ==================== PANNEAUX ====================
            E("pp_dock", "Panneaux", "Project Panel", "Dock du panneau projet", "Position de l'explorateur.", "Right", "Right", "Left");
            I("pp_width", "Panneaux", "Project Panel", "Largeur par défaut", "Largeur du panneau projet (px).", 240, 120, 800, 20);
            T("pp_hide_gitignore", "Panneaux", "Project Panel", "Masquer .gitignore", "Cache les entrées gitignorées.", false);
            E("pp_entry_spacing", "Panneaux", "Project Panel", "Espacement des entrées", "Espacement des lignes.", "Comfortable", "Comfortable", "Standard");
            T("pp_file_icons", "Panneaux", "Project Panel", "Icônes de fichiers", "Icônes dans l'explorateur.", true);
            T("pp_folder_icons", "Panneaux", "Project Panel", "Icônes de dossiers", "Icônes ou chevrons.", true);
            T("pp_git_status", "Panneaux", "Project Panel", "Statut git", "Statut git dans l'explorateur.", true);
            I("pp_indent", "Panneaux", "Project Panel", "Taille d'indentation", "Indentation des éléments imbriqués.", 20, 8, 40, 2);
            T("pp_auto_reveal", "Panneaux", "Project Panel", "Révélation automatique", "Révèle le fichier actif dans l'explorateur.", true);
            T("pp_horizontal_scroll", "Panneaux", "Project Panel", "Scroll horizontal", "Scroll horizontal dans l'explorateur.", true);
            T("pp_git_indicator", "Panneaux", "Project Panel", "Indicateur git", "Indicateur git à côté des noms.", false);
            T("pp_hide_hidden", "Panneaux", "Project Panel", "Masquer les cachés", "Cache les fichiers cachés.", true);
            T("pp_count_badge", "Panneaux", "Project Panel", "Badge compteur", "Badge du nombre de terminaux.", false);
            T("op_button", "Panneaux", "Outline Panel", "Bouton outline", "Bouton outline dans la barre de statut.", true);
            E("op_dock", "Panneaux", "Outline Panel", "Dock outline", "Position du panneau outline.", "Right", "Right", "Left", "Bottom");
            T("op_auto_reveal", "Panneaux", "Outline Panel", "Révélation auto", "Révèle l'entrée correspondante.", true);
            T("op_auto_fold", "Panneaux", "Outline Panel", "Repli auto", "Replie les dossiers à un seul enfant.", true);
            E("op_indent_guides", "Panneaux", "Outline Panel", "Guides d'indentation", "Guides dans l'outline.", "Always", "Always", "On Hover", "Never");
            T("gp_button", "Panneaux", "Git Panel", "Bouton git", "Bouton git dans la barre de statut.", true);
            E("gp_dock", "Panneaux", "Git Panel", "Dock git", "Position du panneau git.", "Right", "Right", "Left", "Bottom");
            T("gp_starts_open", "Panneaux", "Git Panel", "Ouvert au démarrage", "Panneau git ouvert au départ.", false);
            I("gp_width", "Panneaux", "Git Panel", "Largeur par défaut", "Largeur du panneau git (px).", 360, 200, 1000, 20);
            E("gp_status_style", "Panneaux", "Git Panel", "Style de statut", "Style des statuts git.", "Icon", "Icon", "Label");
            S("gp_fallback_branch", "Panneaux", "Git Panel", "Branche par défaut", "Branche si non détectée.", "main");
            E("gp_sort", "Panneaux", "Git Panel", "Trier par", "Tri des entrées git.", "Path", "Path", "Name", "Status");
            E("gp_group", "Panneaux", "Git Panel", "Grouper par", "Groupement des entrées.", "Status", "Status", "Folder");
            T("gp_collapse_untracked", "Panneaux", "Git Panel", "Replier les non trackés", "Replie les diffs non trackés.", false);
            T("gp_tree_view", "Panneaux", "Git Panel", "Vue arborescente", "Arborescence au lieu de liste plate.", false);
            T("gp_diff_stats", "Panneaux", "Git Panel", "Stats de diff", "Ajouts/suppressions par fichier.", true);
            E("gp_click_behavior", "Panneaux", "Git Panel", "Clic principal", "Action au clic sur un fichier modifié.", "Project Diff", "Project Diff", "File Diff");
            T("gp_count_badge", "Panneaux", "Git Panel", "Badge compteur", "Badge des changements non commités.", false);
            I("gp_commit_max_len", "Panneaux", "Git Panel", "Longueur max commit", "Longueur max du titre de commit (0 = illimité).", 0, 0, 200, 10);
            E("gp_scrollbar", "Panneaux", "Git Panel", "Scrollbar", "Affichage de la scrollbar.", "Auto", "Auto", "Always", "Never");
            E("dp_dock", "Panneaux", "Debugger Panel", "Dock débogueur", "Position du panneau débogueur.", "Bottom", "Bottom", "Right", "Left");
            T("cp_button", "Panneaux", "Collaboration Panel", "Bouton collaboration", "Bouton dans la barre de statut.", true);
            E("cp_dock", "Panneaux", "Collaboration Panel", "Dock collaboration", "Position du panneau.", "Right", "Right", "Left");
            I("cp_width", "Panneaux", "Collaboration Panel", "Largeur par défaut", "Largeur du panneau (px).", 260, 150, 800, 20);
            T("ap_button", "Panneaux", "Agent Panel", "Bouton agent", "Bouton panneau IA dans la barre de statut.", true);
            E("ap_dock", "Panneaux", "Agent Panel", "Dock agent", "Position du panneau IA.", "Left", "Left", "Right", "Bottom");
            T("ap_flexible", "Panneaux", "Agent Panel", "Taille flexible", "Largeur flexible quand docké latéralement.", true);
            I("ap_width", "Panneaux", "Agent Panel", "Largeur par défaut", "Largeur du panneau IA (px).", 640, 200, 1200, 20);
            I("ap_height", "Panneaux", "Agent Panel", "Hauteur par défaut", "Hauteur du panneau IA (px).", 320, 100, 1200, 20);
            T("ap_limit_width", "Panneaux", "Agent Panel", "Limiter la largeur", "Contenu centré à largeur max.", true);
            I("ap_max_width", "Panneaux", "Agent Panel", "Largeur max contenu", "Largeur max du contenu (px).", 850, 400, 2000, 50);

            // ==================== AGENT ====================
            E("default_model", "Agent", "Modèle", "Modèle par défaut", "Moteur du chat IA.", "MOTO interne", "MOTO interne", "Ollama (qwen2.5-coder:7b)", "OpenAI", "Anthropic", "Mistral");
            T("prefer_internal", "Agent", "Modèle", "Priorité au moteur interne", "Moteur local avant providers externes.", true);
            E("power_mode", "Agent", "Performance", "Mode de puissance", "Niveau d'analyse de MOTO AI.", "Balanced", "Eco", "Balanced", "Turbo", "Ultra");
            T("ai_cache_enabled", "Agent", "Performance", "Cache IA", "Réponses mémorisées instantanées.", true);
            T("auto_compact", "Agent", "Conversation", "Compactage automatique", "Compacte l'historique long.", true);
            I("temperature", "Agent", "Génération", "Température (x100)", "Créativité des modèles externes.", 70, 0, 200, 5);
            I("max_tokens", "Agent", "Génération", "Tokens max", "Longueur maximale des réponses.", 4096, 256, 32000, 256);
            T("thread_persistence", "Agent", "Conversation", "Threads persistants", "Sauvegarde les conversations.", true);
            T("auto_doc", "Agent", "Documentation", "Auto-Doc", "Documentation mise à jour auto.", true);

            // ← AJOUT : enregistre les catégories étendues
            // (Version Control, Collaboration, AI étendu, Network, Developer)
            // définies dans SettingsCatalog.Extensions.cs
            RegisterExtensions();
        }

        /// <summary>
        /// Méthode partielle implémentée dans SettingsCatalog.Extensions.cs.
        /// Si le fichier Extensions est absent, le projet ne compile pas :
        /// c'est voulu, pour garantir que les catégories étendues existent.
        /// </summary>
        static partial void RegisterExtensions();

        /// <summary>Retrouve une définition par son id.</summary>
        public static SettingDefinition ById(string id)
        {
            return All.Find(d => d.Id == id);
        }

        // ------------------------------------------------------------------
        // Helpers de déclaration
        // ------------------------------------------------------------------

        private static void T(string id, string cat, string sec, string title, string desc, bool def)
        {
            All.Add(new SettingDefinition { Id = id, Category = cat, Section = sec, Title = title, Description = desc, Type = SettingType.Toggle, Default = def });
        }

        private static void I(string id, string cat, string sec, string title, string desc, int def, int min, int max, int step = 1)
        {
            All.Add(new SettingDefinition { Id = id, Category = cat, Section = sec, Title = title, Description = desc, Type = SettingType.Int, Default = def, Min = min, Max = max, Step = step });
        }

        private static void E(string id, string cat, string sec, string title, string desc, string def, params string[] options)
        {
            var d = new SettingDefinition { Id = id, Category = cat, Section = sec, Title = title, Description = desc, Type = SettingType.Enum, Default = def };
            d.Options.AddRange(options);
            All.Add(d);
        }

        private static void S(string id, string cat, string sec, string title, string desc, string def)
        {
            All.Add(new SettingDefinition { Id = id, Category = cat, Section = sec, Title = title, Description = desc, Type = SettingType.String, Default = def });
        }
    }
}
