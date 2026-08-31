// Moto.Editor/Views/SettingsWindowView.xaml.cs
// ★ AJOUT (31/08) : fenêtre de Réglages façon Zed (capture fournie par Tom) —
// flottante, déplaçable, redimensionnable, 15 catégories (mêmes noms que la
// capture), ~95 réglages au total.
//
// Honnêteté sur la portée : TOUS les réglages ci-dessous sont réellement
// enregistrés (SettingsEngine, persistant d'une session à l'autre). Ceux
// listés dans RealEffectKeys ont un vrai effet vérifié dans le logiciel
// (thème, police, mini-map, terminal, mode de puissance). Les autres
// (la grande majorité, par catégories entières comme Débogueur/Contrôle de
// version/Collaboration/Réseau/Langages) se sauvegardent correctement mais
// n'ont pas encore de fonctionnalité réelle branchée derrière — comme
// Santé du projet/Snapshot Time Machine déjà signalés à Tom. Construire les
// 90+ FONCTIONNALITÉS elles-mêmes (pas juste leurs réglages) est un chantier
// largement plus grand, hors de portée de cette passe.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.Maui.Controls;
using Moto.Core.Settings;

namespace Moto.Editor.Views
{
    public enum SettingKind { Toggle, Number, Text, Dropdown, Button }

    public sealed record SettingDef(
        string Key, string Title, string Description, SettingKind Kind,
        object DefaultValue, string[]? Options = null);

    public partial class SettingsWindowView : ContentView
    {
        /// <summary>Clés dont l'effet réel est vérifié ailleurs dans le logiciel.</summary>
        private static readonly HashSet<string> RealEffectKeys = new()
        {
            "theme_mode", "buffer_font_size", "minimap_show", "terminal_show", "power_mode"
        };

        private readonly ObservableCollection<string> _categories = new();
        private readonly Dictionary<string, List<SettingDef>> _bySection;
        private double _startX, _startY, _startW, _startH;

        /// <summary>Déclenché pour les réglages à effet réel (mêmes id que SettingsMenuView).</summary>
        public event Action<string, object>? RealSettingChanged;

        public SettingsWindowView()
        {
            InitializeComponent();
            _bySection = BuildDefinitions();

            foreach (var section in _bySection.Keys)
                _categories.Add(section);
            CategoryList.ItemsSource = _categories;
            CategoryList.SelectedItem = "General";
            // ★ Filet de sécurité : SelectionChanged n'est pas garanti de se
            // déclencher pour une sélection posée par code avant que le contrôle
            // soit dans l'arbre visuel (fenêtre encore IsVisible=False à cet
            // instant) — appelé directement pour ne jamais laisser le panneau de
            // détail vide à la première ouverture.
            RenderSection("General");
        }

        /// <summary>
        /// ★ CORRECTION (31/08) : Tom signale que l'engrenage n'ouvrait "aucune
        /// fenêtre" — aucun bug trouvé dans le chemin d'ouverture lui-même (vérifié
        /// ligne par ligne), mais WindowFrame.TranslationX/Y (glisser) et
        /// WidthRequest/HeightRequest (redimensionner) ne sont RÉINITIALISÉS nulle
        /// part : un seul glissement accidentel avant que Tom ne comprenne que rien
        /// n'était encore ouvert aurait suffi à repositionner la fenêtre hors-écran
        /// pour TOUTES les ouvertures suivantes (IsVisible=true, mais invisible à
        /// l'œil). Réinitialisé par précaution à chaque Show(), que ce soit la
        /// cause réelle ou non — de toute façon la bonne pratique pour un "rouvrir".
        /// Accepte aussi une catégorie cible (utilisé par le menu ⚙ : "Thèmes" ouvre
        /// direct sur Appearance, "Raccourcis" sur Keymap, etc.).
        /// </summary>
        public void Show(string category = "General")
        {
            WindowFrame.TranslationX = 0;
            WindowFrame.TranslationY = 0;
            WindowFrame.WidthRequest = 900;
            WindowFrame.HeightRequest = 620;
            IsVisible = true;
            CategoryList.SelectedItem = category;
            RenderSection(category);
        }

        private void OnCloseClicked(object sender, EventArgs e) => IsVisible = false;

        private void OnCategorySelected(object sender, SelectionChangedEventArgs e)
        {
            if (e.CurrentSelection.Count == 0 || e.CurrentSelection[0] is not string section) return;
            RenderSection(section);
        }

        // ------------------------------------------------------------------
        // Déplacement (barre de titre) / redimensionnement (coin bas-droit)
        // ------------------------------------------------------------------

        private void OnDragPanUpdated(object sender, PanUpdatedEventArgs e)
        {
            switch (e.StatusType)
            {
                case GestureStatus.Started:
                    _startX = WindowFrame.TranslationX;
                    _startY = WindowFrame.TranslationY;
                    break;
                case GestureStatus.Running:
                    WindowFrame.TranslationX = _startX + e.TotalX;
                    WindowFrame.TranslationY = _startY + e.TotalY;
                    break;
            }
        }

        private void OnResizePanUpdated(object sender, PanUpdatedEventArgs e)
        {
            switch (e.StatusType)
            {
                case GestureStatus.Started:
                    _startW = WindowFrame.Width > 0 ? WindowFrame.Width : WindowFrame.WidthRequest;
                    _startH = WindowFrame.Height > 0 ? WindowFrame.Height : WindowFrame.HeightRequest;
                    break;
                case GestureStatus.Running:
                    WindowFrame.WidthRequest = Math.Max(WindowFrame.MinimumWidthRequest, _startW + e.TotalX);
                    WindowFrame.HeightRequest = Math.Max(WindowFrame.MinimumHeightRequest, _startH + e.TotalY);
                    break;
            }
        }

        // ------------------------------------------------------------------
        // Rendu d'une catégorie (généré en code : ~95 lignes similaires
        // écrites une par une en XAML auraient été bien plus risquées à relire)
        // ------------------------------------------------------------------

        private void RenderSection(string section)
        {
            DetailHost.Children.Clear();
            if (!_bySection.TryGetValue(section, out var defs)) return;

            DetailHost.Children.Add(new Label
            {
                Text = section,
                FontSize = 20,
                FontAttributes = FontAttributes.Bold,
                TextColor = (Color)Application.Current!.Resources["Txt1"]
            });

            foreach (var def in defs)
                DetailHost.Children.Add(BuildRow(def));
        }

        private View BuildRow(SettingDef def)
        {
            var txt1 = (Color)Application.Current!.Resources["Txt1"];
            var txt2 = (Color)Application.Current!.Resources["Txt2"];
            var border = (Color)Application.Current!.Resources["BorderCol"];

            var grid = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) } };

            var textCol = new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center };
            textCol.Children.Add(new Label { Text = def.Title, FontSize = 13, TextColor = txt1 });
            textCol.Children.Add(new Label { Text = def.Description, FontSize = 11, TextColor = txt2 });
            grid.Add(textCol, 0, 0);

            // ★ Utilise les getters typés (GetBool/Get/GetString), pas GetRaw : une
            // valeur relue depuis settings.json passe par un JsonElement dont
            // ToString() ne redonne pas le texte propre attendu — les getters
            // typés gèrent déjà cette conversion correctement (voir SettingsEngineCore).
            View control = def.Kind switch
            {
                SettingKind.Toggle => BuildToggle(def, SettingsEngine.Shared.GetBool(def.Key, Convert.ToBoolean(def.DefaultValue))),
                SettingKind.Number => BuildNumber(def, SettingsEngine.Shared.Get(def.Key, Convert.ToDouble(def.DefaultValue))),
                SettingKind.Text => BuildText(def, SettingsEngine.Shared.GetString(def.Key, def.DefaultValue?.ToString() ?? "")),
                SettingKind.Dropdown => BuildDropdown(def, SettingsEngine.Shared.GetString(def.Key, def.DefaultValue?.ToString() ?? def.Options![0])),
                SettingKind.Button => BuildButton(def),
                _ => new Label()
            };
            control.VerticalOptions = LayoutOptions.Center;
            grid.Add(control, 1, 0);

            return new Border
            {
                Padding = new Thickness(0, 0, 0, 14),
                Stroke = Colors.Transparent,
                Content = new VerticalStackLayout
                {
                    Children =
                    {
                        grid,
                        new BoxView { HeightRequest = 1, Color = border, Margin = new Thickness(0, 12, 0, 0) }
                    }
                }
            };
        }

        private Switch BuildToggle(SettingDef def, bool value)
        {
            var sw = new Switch { IsToggled = value };
            sw.Toggled += (s, e) => Persist(def.Key, e.Value);
            return sw;
        }

        private Entry BuildNumber(SettingDef def, double value)
        {
            var entry = new Entry { Text = value.ToString("0.##"), Keyboard = Keyboard.Numeric, WidthRequest = 80 };
            entry.Completed += (s, e) => { if (double.TryParse(entry.Text, out var v)) Persist(def.Key, v); };
            entry.Unfocused += (s, e) => { if (double.TryParse(entry.Text, out var v)) Persist(def.Key, v); };
            return entry;
        }

        private Entry BuildText(SettingDef def, string value)
        {
            var entry = new Entry { Text = value, WidthRequest = 180 };
            entry.Completed += (s, e) => Persist(def.Key, entry.Text ?? "");
            entry.Unfocused += (s, e) => Persist(def.Key, entry.Text ?? "");
            return entry;
        }

        private Picker BuildDropdown(SettingDef def, string value)
        {
            var picker = new Picker { WidthRequest = 180 };
            foreach (var opt in def.Options!) picker.Items.Add(opt);
            picker.SelectedIndex = Array.IndexOf(def.Options!, value) is var i && i >= 0 ? i : 0;
            picker.SelectedIndexChanged += (s, e) =>
            {
                if (picker.SelectedIndex >= 0) Persist(def.Key, def.Options![picker.SelectedIndex]);
            };
            return picker;
        }

        private Button BuildButton(SettingDef def)
        {
            var btn = new Button { Text = "Exécuter", Padding = new Thickness(12, 4) };
            btn.Clicked += (s, e) => Persist(def.Key, true);
            return btn;
        }

        private void Persist(string key, object value)
        {
            switch (value)
            {
                case bool b: SettingsEngine.Shared.Set(key, b); break;
                case double d: SettingsEngine.Shared.Set(key, d); break;
                case string s: SettingsEngine.Shared.Set(key, s); break;
                default: SettingsEngine.Shared.Set(key, value); break;
            }

            if (RealEffectKeys.Contains(key))
                RealSettingChanged?.Invoke(key, value);
        }

        // ------------------------------------------------------------------
        // Définitions (~95 réglages, 15 catégories — mêmes noms que Zed)
        // ------------------------------------------------------------------

        private static Dictionary<string, List<SettingDef>> BuildDefinitions() => new()
        {
            ["General"] = new()
            {
                new("general.reopen_last", "Rouvrir le dernier projet au démarrage", "Recharge automatiquement le dernier dossier importé.", SettingKind.Toggle, false),
                new("general.confirm_close_dirty", "Confirmer avant de fermer un fichier modifié", "Demande confirmation si l'onglet a des changements non enregistrés.", SettingKind.Toggle, true),
                new("editor.update.autocheck", "Vérifier les mises à jour au démarrage", "Recherche une nouvelle version de MOTO Editor au lancement.", SettingKind.Toggle, true),
                new("general.telemetry", "Télémétrie anonyme", "Aucune donnée n'est envoyée nulle part — MOTO Editor est 100% local.", SettingKind.Toggle, false),
                new("general.language", "Langue de l'interface", "Langue des menus et messages.", SettingKind.Dropdown, "Français", new[] { "Français", "English" }),
                new("general.auto_restart_ai", "Redémarrer l'IA locale automatiquement", "Relance Ollama si le moteur local plante en cours d'usage.", SettingKind.Toggle, true),
                new("general.startup_panel", "Panneau ouvert au démarrage", "Panneau affiché juste après l'écran d'accueil.", SettingKind.Dropdown, "Aucun", new[] { "Aucun", "Fichiers", "IA" }),
            },
            ["Appearance"] = new()
            {
                new("theme_mode", "Thème", "Apparence générale du logiciel.", SettingKind.Dropdown, "Dark", new[] { "Dark", "Light", "System" }),
                new("appearance.ui_font", "Police de l'interface", "Police utilisée pour les menus, hors éditeur.", SettingKind.Dropdown, "Segoe UI Variable", new[] { "Segoe UI Variable", "Segoe UI", "Consolas" }),
                new("appearance.density", "Densité de l'interface", "Espacement des menus et boutons.", SettingKind.Dropdown, "Confortable", new[] { "Confortable", "Compact" }),
                new("appearance.rounded_corners", "Coins arrondis", "Arrondit les panneaux et boîtes de dialogue.", SettingKind.Toggle, true),
                new("appearance.animations", "Animations d'interface", "Transitions de survol/ouverture des menus.", SettingKind.Toggle, true),
                new("appearance.accent_color", "Couleur d'accent", "Couleur utilisée pour les éléments actifs/sélectionnés.", SettingKind.Dropdown, "Orange", new[] { "Orange", "Bleu", "Vert", "Violet" }),
                new("appearance.taskbar_icon", "Icône de la barre des tâches", "Style de l'icône affichée dans la barre des tâches Windows.", SettingKind.Dropdown, "Défaut", new[] { "Défaut", "Monochrome" }),
            },
            ["Keymap"] = new()
            {
                new("keymap.scheme", "Jeu de raccourcis", "Convention générale des raccourcis clavier.", SettingKind.Dropdown, "MOTO", new[] { "MOTO", "VS Code", "Zed" }),
                new("keymap.command_palette", "Palette de commandes", "Raccourci pour ouvrir la palette de commandes.", SettingKind.Text, "Ctrl+Shift+P"),
                new("keymap.global_search", "Recherche globale", "Raccourci pour ouvrir la recherche de fichiers.", SettingKind.Text, "Ctrl+P"),
                new("keymap.ai_band", "Bandeau IA", "Raccourci pour ouvrir/fermer le bandeau IA flottant.", SettingKind.Text, "Ctrl+Shift+I"),
                new("keymap.new_file", "Nouveau fichier", "Raccourci pour créer un fichier vide.", SettingKind.Text, "Ctrl+N"),
                new("keymap.save", "Enregistrer", "Raccourci pour enregistrer le fichier actif.", SettingKind.Text, "Ctrl+S"),
            },
            ["Editor"] = new()
            {
                new("buffer_font_size", "Taille de police", "Taille du texte dans les fichiers ouverts (et le menu, voir Réglages précédents).", SettingKind.Number, 14.0),
                new("minimap_show", "Mini-map", "Aperçu compressé du fichier, à droite de l'éditeur.", SettingKind.Toggle, true),
                new("editor.word_wrap", "Retour à la ligne automatique", "Évite le défilement horizontal sur les lignes longues.", SettingKind.Toggle, false),
                new("editor.tab_size", "Taille de tabulation", "Nombre d'espaces représentés par une tabulation.", SettingKind.Number, 4.0),
                new("editor.spaces_not_tabs", "Espaces au lieu de tabulations", "Insère des espaces quand vous appuyez sur Tab.", SettingKind.Toggle, true),
                new("editor.show_line_numbers", "Afficher les numéros de ligne", "Numérotation dans la marge gauche de l'éditeur.", SettingKind.Toggle, true),
                new("editor.highlight_current_line", "Surligner la ligne actuelle", "Met légèrement en évidence la ligne du curseur.", SettingKind.Toggle, true),
                new("editor.format_on_save", "Formater à l'enregistrement", "Réindente le code automatiquement en sauvegardant.", SettingKind.Toggle, false),
                new("editor.autosave", "Enregistrement automatique", "Sauvegarde le fichier actif sans action manuelle.", SettingKind.Dropdown, "Désactivé", new[] { "Désactivé", "Après un délai", "À chaque frappe" }),
                new("editor.autosave_delay_ms", "Délai d'enregistrement auto (ms)", "Utilisé si 'Après un délai' est choisi ci-dessus.", SettingKind.Number, 1000.0),
                new("editor.blinking_cursor", "Curseur clignotant", "Fait clignoter le curseur texte.", SettingKind.Toggle, true),
                new("editor.indent_guides", "Guides d'indentation", "Lignes verticales discrètes marquant les niveaux d'indentation.", SettingKind.Toggle, false),
            },
            ["Languages & Tools"] = new()
            {
                new("lsp_diagnostics", "Analyse syntaxique en direct", "Souligne les erreurs pendant la frappe.", SettingKind.Toggle, true),
                new("lang.spellcheck_comments", "Vérification orthographique des commentaires", "Repère les fautes dans les commentaires de code.", SettingKind.Toggle, false),
                new("lang.autoformat_csharp", "Formatage automatique C#", "Applique les conventions de style .NET.", SettingKind.Toggle, false),
                new("lang.type_suggestions", "Suggestions de types", "Propose les types possibles pendant la frappe.", SettingKind.Toggle, true),
                new("lang.line_length_hint", "Longueur de ligne recommandée", "Affiche un repère visuel à cette colonne.", SettingKind.Number, 120.0),
                new("lang.dead_code_warning", "Avertir sur code mort détecté", "Signale les méthodes/variables jamais utilisées.", SettingKind.Toggle, false),
            },
            ["Search & Files"] = new()
            {
                new("search.include_hidden", "Inclure les fichiers cachés", "Recherche aussi dans les fichiers commençant par un point.", SettingKind.Toggle, false),
                new("search.excluded_folders", "Dossiers exclus", "Liste séparée par des virgules.", SettingKind.Text, "bin,obj,.git,node_modules"),
                new("search.case_sensitive", "Sensible à la casse par défaut", "Distingue majuscules/minuscules à l'ouverture de la recherche.", SettingKind.Toggle, false),
                new("search.max_results", "Nombre maximum de résultats", "Limite le nombre de fichiers retournés par une recherche.", SettingKind.Number, 50.0),
                new("search.live_preview", "Aperçu en direct des résultats", "Affiche un extrait du fichier sous chaque résultat.", SettingKind.Toggle, false),
                new("search.fuzzy", "Recherche floue", "Trouve aussi les noms de fichiers approximatifs.", SettingKind.Toggle, true),
            },
            ["Window & Layout"] = new()
            {
                new("power_mode", "Mode de puissance IA", "Compromis vitesse / qualité des réponses locales.", SettingKind.Dropdown, "Balanced", new[] { "Éco", "Balanced", "Ultra" }),
                new("layout.ai_dock_width", "Largeur du panneau IA (px)", "Largeur du dock IA quand un panneau y est ouvert.", SettingKind.Number, 500.0),
                new("layout.filetree_width", "Largeur de l'arborescence (px)", "Largeur du panneau Fichiers quand il est ouvert.", SettingKind.Number, 260.0),
                new("layout.start_maximized", "Toujours démarrer maximisé", "Ouvre la fenêtre en plein écran au lancement.", SettingKind.Toggle, false),
                new("layout.remember_position", "Mémoriser la position de la fenêtre", "Rouvre au même endroit qu'à la dernière fermeture.", SettingKind.Toggle, false),
                new("layout.compact_titlebar", "Barre de titre compacte", "Réduit la hauteur de la barre du haut.", SettingKind.Toggle, false),
            },
            ["Panels"] = new()
            {
                new("terminal_show", "Terminal visible par défaut", "Affiche le terminal dès l'ouverture d'un projet.", SettingKind.Toggle, false),
                new("panels.collab_always_on", "Panneau Collab toujours actif", "Garde la session de collaboration ouverte en arrière-plan.", SettingKind.Toggle, false),
                new("panels.auto_close_unused", "Fermer les panneaux inutilisés", "Referme automatiquement un panneau après un long moment d'inactivité.", SettingKind.Toggle, false),
                new("panels.cortex_auto_open", "Ouvrir Cortex automatiquement", "Affiche les suggestions Cortex dès qu'un fichier est ouvert.", SettingKind.Toggle, false),
                new("panels.pin_search", "Épingler le panneau Recherche", "Garde la recherche ouverte même en changeant d'onglet.", SettingKind.Toggle, false),
                new("panels.show_badges", "Badges de notification sur les panneaux", "Petit indicateur numérique sur les icônes de panneaux actifs.", SettingKind.Toggle, true),
            },
            ["Debugger"] = new()
            {
                new("debugger.breakpoints_enabled", "Activer les points d'arrêt", "Autorise la pose de points d'arrêt dans l'éditeur.", SettingKind.Toggle, true),
                new("debugger.auto_continue", "Continuer après une exception gérée", "Ne s'arrête pas sur les exceptions déjà interceptées (catch).", SettingKind.Toggle, false),
                new("debugger.inline_values", "Afficher les valeurs en ligne", "Montre la valeur des variables à côté du code pendant le débogage.", SettingKind.Toggle, true),
                new("debugger.log_calls", "Journaliser les appels de fonction", "Trace chaque appel de méthode pendant l'exécution.", SettingKind.Toggle, false),
                new("debugger.timeout_sec", "Timeout d'exécution (s)", "Arrête un débogage bloqué après ce délai.", SettingKind.Number, 30.0),
                new("debugger.verbose_console", "Console de débogage verbeuse", "Affiche des informations techniques supplémentaires.", SettingKind.Toggle, false),
            },
            ["Terminal"] = new()
            {
                new("terminal.shell", "Shell par défaut", "Interpréteur de commandes utilisé pour le terminal intégré.", SettingKind.Dropdown, "PowerShell", new[] { "PowerShell", "cmd", "Git Bash" }),
                new("terminal.font", "Police du terminal", "Police à chasse fixe utilisée dans le terminal.", SettingKind.Dropdown, "Consolas", new[] { "Consolas", "Cascadia Code", "Courier New" }),
                new("terminal.font_size", "Taille de police terminal", "Taille du texte affiché dans le terminal.", SettingKind.Number, 13.0),
                new("terminal.infinite_scroll", "Défilement infini", "Conserve tout l'historique de sortie du terminal.", SettingKind.Toggle, true),
                new("terminal.auto_copy_selection", "Copier automatiquement la sélection", "Copie le texte sélectionné sans Ctrl+C.", SettingKind.Toggle, false),
                new("terminal.close_on_exit", "Fermer à la fin de la commande", "Referme l'onglet terminal une fois la commande terminée.", SettingKind.Toggle, false),
            },
            ["Version Control"] = new()
            {
                new("vcs.show_gutter_indicators", "Indicateurs Git dans la marge", "Marque les lignes ajoutées/modifiées/supprimées.", SettingKind.Toggle, true),
                new("vcs.auto_fetch", "Récupérer (fetch) automatiquement", "Vérifie les changements distants au démarrage.", SettingKind.Toggle, false),
                new("vcs.show_branch_statusbar", "Afficher la branche dans la barre de statut", "Nom de la branche Git actuelle, en bas de la fenêtre.", SettingKind.Toggle, true),
                new("vcs.confirm_push", "Confirmer avant de pousser (push)", "Demande confirmation avant d'envoyer des commits.", SettingKind.Toggle, true),
                new("vcs.ignore_whitespace_diff", "Ignorer les espaces dans les diffs", "N'affiche pas les changements d'indentation seule.", SettingKind.Toggle, false),
                new("vcs.commit_message_max_len", "Longueur max. du message de commit", "Avertit au-delà de cette longueur de ligne de résumé.", SettingKind.Number, 72.0),
            },
            ["Collaboration"] = new()
            {
                new("collab.show_cursors", "Afficher les curseurs des participants", "Montre en direct où écrivent les autres personnes.", SettingKind.Toggle, true),
                new("collab.sound_notifications", "Notifications sonores", "Son court à l'arrivée d'un message.", SettingKind.Toggle, false),
                new("collab.typing_indicator", "Statut \"en train d'écrire\"", "Signale aux autres quand vous tapez un message.", SettingKind.Toggle, true),
                new("collab.history_days", "Historique conservé (jours)", "Durée de conservation des messages de session.", SettingKind.Number, 30.0),
                new("collab.allow_external_invites", "Autoriser les invitations externes", "Permet d'inviter des personnes hors de vos contacts.", SettingKind.Toggle, false),
            },
            ["AI"] = new()
            {
                new("ai.default_model", "Modèle par défaut", "Modèle utilisé pour les nouvelles conversations.", SettingKind.Dropdown, "MOTO interne", new[] { "MOTO interne", "Ollama", "OpenAI", "Anthropic", "Mistral" }),
                new("ai.temperature", "Température de génération", "Créativité des réponses (0 = strict, 1 = créatif).", SettingKind.Number, 0.2),
                new("ai.max_reply_length", "Longueur max. de réponse", "Nombre de caractères maximum par réponse.", SettingKind.Number, 8000.0),
                new("ai.auto_context", "Utiliser le contexte du fichier ouvert", "Envoie automatiquement le fichier actif à l'IA.", SettingKind.Toggle, true),
                new("ai.history_days", "Historique de conversation conservé (jours)", "Durée de conservation des échanges avec l'IA.", SettingKind.Number, 90.0),
                new("ai.confirm_apply_code", "Confirmer avant d'appliquer du code généré", "Demande validation avant de remplacer le contenu d'un fichier.", SettingKind.Toggle, true),
                new("ai.cortex_learning", "Apprentissage des habitudes (Cortex)", "Permet à Cortex d'apprendre de votre style de code.", SettingKind.Toggle, true),
            },
            ["Network"] = new()
            {
                new("network.http_proxy", "Proxy HTTP", "Laisser vide pour une connexion directe.", SettingKind.Text, ""),
                new("network.request_timeout_sec", "Timeout des requêtes (s)", "Délai avant abandon d'une requête réseau.", SettingKind.Number, 30.0),
                new("network.check_ollama_on_startup", "Vérifier Ollama au démarrage", "Teste la connexion au moteur IA local au lancement.", SettingKind.Toggle, true),
                new("network.allow_cloud_providers", "Autoriser les providers cloud", "Permet le fallback vers OpenAI/Anthropic/Mistral si configurés.", SettingKind.Toggle, true),
                new("network.preview_server_port", "Port du serveur de prévisualisation", "Port local utilisé par l'aperçu HTML en direct.", SettingKind.Number, 5050.0),
            },
            ["Developer"] = new()
            {
                new("dev.debug_mode", "Mode debug", "Active des options réservées au développement.", SettingKind.Toggle, false),
                new("dev.verbose_logging", "Journalisation détaillée", "Écrit plus d'informations dans le journal de démarrage.", SettingKind.Toggle, false),
                new("dev.show_breadcrumbs", "Afficher les breadcrumbs de démarrage", "Trace visible des étapes de lancement (diagnostic).", SettingKind.Toggle, false),
                new("dev.reload_ui", "Recharger l'interface", "Force un rafraîchissement de l'affichage sans redémarrer.", SettingKind.Button, false),
                new("dev.open_log_folder", "Ouvrir le dossier des journaux", "Ouvre l'explorateur Windows sur les fichiers de log.", SettingKind.Button, false),
                new("dev.reset_all_settings", "Réinitialiser tous les réglages", "Remet chaque réglage de cette fenêtre à sa valeur par défaut.", SettingKind.Button, false),
            },
        };
    }
}
