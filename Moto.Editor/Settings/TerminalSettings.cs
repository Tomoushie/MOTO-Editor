// Moto.Editor/Settings/TerminalSettings.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Moto.Core.Settings;
using Moto.Editor.Services;

namespace Moto.Editor.Settings
{
    /// <summary>
    /// ★ AJOUT (01/10) : lit et applique les réglages de la famille « Terminal »
    /// (clés <c>terminal_*</c>) — le VRAI dock du bas
    /// (<see cref="Moto.Editor.Views.TerminalPanelView"/>), alimenté par
    /// <c>Moto.Core.Services.TerminalService</c> (cmd/bash en direct).
    ///
    /// POURQUOI CE FICHIER : ces clés étaient déclarées au catalogue (catégorie
    /// « Terminal », 22 clés) mais lues par AUCUN code compilé — la fenêtre
    /// Réglages les affichait, rien ne les appliquait (constat
    /// <c>scripts/settings-coverage.ps1</c>). Même méthode que
    /// <see cref="TabBarSettings"/>, <see cref="PanelSettings"/>,
    /// <see cref="GitPanelSettings"/> et <see cref="DockPanelSettings"/> :
    /// un seul endroit qui traduit le catalogue en effets réels.
    ///
    /// ⚠️ PIÈGE PAYÉ DEUX FOIS (28/09, 01/10) — c'est LE piège de ce dépôt :
    /// <c>SettingsEngine.GetBool("clé")</c> SANS second argument renvoie
    /// <c>false</c> quand la clé est absente du store (le moteur ne consulte
    /// JAMAIS le catalogue). Sur une installation neuve, appliquer
    /// <c>terminal_audible_bell</c> sans défaut aurait donc MASQUÉ la valeur
    /// déclarée. Toutes les lectures ci-dessous passent explicitement le défaut
    /// DÉCLARÉ, seule source de vérité.
    ///
    /// OÙ C'EST APPLIQUÉ :
    ///   - <c>terminal_font_size</c>, <c>terminal_font_family</c>,
    ///     <c>terminal_default_height</c> : <c>MainPage.ApplyTerminalSettings</c>
    ///     (appelée par <c>ApplyLayoutSettings</c> et par le préfixe
    ///     <c>terminal_</c> de <c>SettingsWindow.RealSettingChanged</c>) ;
    ///   - <c>terminal_max_scroll_lines</c>, <c>terminal_audible_bell</c> :
    ///     <c>MainViewModel.OnTerminalOutput</c> (à chaque ligne du shell) ;
    ///   - <c>terminal_shell</c>, <c>terminal_working_dir</c>,
    ///     <c>terminal_env_vars</c>, <c>terminal_detect_venv</c> :
    ///     <c>MainViewModel.StartTerminal</c> (seul point de démarrage du shell)
    ///     → <c>TerminalService.Start(dir, TerminalStartOptions)</c> — le service
    ///     reste ignorant des réglages, comme exigé par sa localisation Core ;
    ///   - <c>terminal_breadcrumbs</c> : titre de l'en-tête via
    ///     <c>MainViewModel.TerminalTitle</c> (posé par
    ///     <c>ApplyTerminalSettings</c>, recalculé à chaque démarrage de shell).
    ///
    /// CE QUI RESTE INERTE DANS CETTE FAMILLE (une raison par clé, jamais un
    /// oubli) :
    ///   - <c>terminal_font_weight</c> : MAUI 8 n'a PAS de `FontWeight` sur
    ///     `Label` (seulement `FontAttributes` None/Bold/Italic — `FontWeight`
    ///     n'est arrivé qu'en MAUI 10) : une graisse numérique 100-900 n'a
    ///     aucun support d'affichage ici ;
    ///   - <c>terminal_cursor_shape</c> / <c>terminal_cursor_blinking</c> /
    ///     <c>terminal_alternate_scroll</c> : suppose un émulateur VT. La
    ///     sortie est une liste de lignes (`CollectionView` de `TerminalLine`),
    ///     le seul curseur réel est celui du `Entry` de saisie (natif WinUI,
    ///     non configurable) — il n'y a ni curseur de terminal ni écran
    ///     alterné à configurer ;
    ///   - <c>terminal_option_as_meta</c> : clé Option = touche Meta, sémantique
    ///     macOS ; l'application est Windows uniquement (`net8.0-windows`) ;
    ///   - <c>terminal_copy_on_select</c> / <c>terminal_keep_selection_on_copy</c>
    ///     : la sortie n'est PAS sélectionnable (des `Label` dans une
    ///     `CollectionView`, sans modèle de sélection) — câbler exigerait de
    ///     construire ce modèle ;
    ///   - <c>terminal_open_links_mouse</c> : aucune détection de liens dans la
    ///     sortie du shell (donnée inexistante) ;
    ///   - <c>terminal_default_width</c> : le terminal est un dock DU BAS qui
    ///     occupe les 3 colonnes du <c>RootGrid</c> — il n'a pas de largeur
    ///     réglable (seule sa hauteur est ajustable, poignée
    ///     <c>BottomDockResizeHandle</c>) ;
    ///   - <c>terminal_show_scrollbar</c> : contrairement au panneau Git (qui
    ///     enferme son contenu dans un `ScrollView` explicite), la sortie du
    ///     terminal est une `CollectionView`, qui n'expose PAS la visibilité de
    ///     scrollbar en MAUI 8 — l'appliquer reviendrait à fouiller le
    ///     `ControlTemplate` WinUI natif (instable d'une version à l'autre) ;
    ///   - <c>terminal_scroll_multiplier</c> : MAUI ne permet pas de configurer
    ///     le pas de défilement de la molette d'une `CollectionView` ;
    ///   - <c>terminal_thread_init_cmd</c> : annonce une commande au démarrage
    ///     d'un « thread terminal » — le concept de thread terminal n'existe
    ///     nulle part dans le code (recherche complète 01/10) ;
    ///   - <c>terminal_min_contrast</c> : promet un seuil de contraste
    ///     <b>APCA</b> (0-106). APCA est un algorithme précis (Myndex, Lc) dont
    ///     une approximation changerait les couleurs du terminal au nom d'un
    ///     standard que le code ne calcule pas réellement — exactement le
    ///     réglage « affichant faux » interdit par le dépôt. Câbler exige le
    ///     référentiel APCA officiel appliqué aux jetons de thème
    ///     (Txt1/Txt2/Error vs BgChrome).
    /// </summary>
    internal static class TerminalSettings
    {
        // ------------------------------------------------------------------
        // Défauts DÉCLARÉS (SettingsCatalog) — voir le piège expliqué plus haut.
        // ------------------------------------------------------------------

        /// <summary>Défaut déclaré au catalogue pour un réglage booléen.</summary>
        internal static bool DeclaredBool(string id) => SettingsCatalog.ById(id)?.Default is bool value && value;

        /// <summary>Défaut déclaré au catalogue pour un réglage entier.</summary>
        internal static int DeclaredInt(string id) => SettingsCatalog.ById(id)?.Default is int value ? value : 0;

        /// <summary>Défaut déclaré au catalogue pour un réglage texte (jamais nul).</summary>
        internal static string DeclaredString(string id) => SettingsCatalog.ById(id)?.Default as string ?? string.Empty;

        // ------------------------------------------------------------------
        // Accès typés
        // ------------------------------------------------------------------

        /// <summary>
        /// Taille de police du terminal, bornée aux bornes DÉCLARÉES au
        /// catalogue (8..30, défaut 15).
        /// </summary>
        internal static int FontSize(SettingsEngine s)
        {
            var declared = DeclaredInt("terminal_font_size");
            return Math.Clamp(s.GetInt("terminal_font_size", declared), 8, 30);
        }

        /// <summary>
        /// Famille de police du terminal (défaut déclaré : « Consolas »).
        /// Une valeur vide au stockage retombe sur le défaut déclaré plutôt que
        /// de produire un Label sans police (le XAML réclame une famille non
        /// vide pour rester sur la même métrique que la saisie).
        /// </summary>
        internal static string FontFamily(SettingsEngine s)
        {
            var raw = s.GetString("terminal_font_family", DeclaredString("terminal_font_family"));
            return string.IsNullOrWhiteSpace(raw) ? DeclaredString("terminal_font_family") : raw;
        }

        /// <summary>
        /// Hauteur du dock terminal, bornée aux bornes DÉCLARÉES (100..1200,
        /// défaut 320). Le XAML part de 220 ; le défaut déclaré (320) prime au
        /// premier ApplyTerminalSettings — c'est lui que la fenêtre Réglages
        /// affiche, donc c'est lui qui doit s'appliquer.
        /// </summary>
        internal static int DefaultHeight(SettingsEngine s)
        {
            var declared = DeclaredInt("terminal_default_height");
            return Math.Clamp(s.GetInt("terminal_default_height", declared), 100, 1200);
        }

        /// <summary>
        /// Lignes d'historique conservées (0 = illimité, convention déjà
        /// utilisée par <c>gp_commit_max_len</c> au catalogue). bornes 0..100000.
        /// </summary>
        internal static int MaxScrollLines(SettingsEngine s)
        {
            var declared = DeclaredInt("terminal_max_scroll_lines");
            return Math.Clamp(s.GetInt("terminal_max_scroll_lines", declared), 0, 100000);
        }

        /// <summary>Sonnerie sur le caractère BEL (défaut déclaré : Off).</summary>
        internal static bool AudibleBell(SettingsEngine s)
            => s.GetBool("terminal_audible_bell", DeclaredBool("terminal_audible_bell"));

        // ==================================================================
        // ★ AJOUT (01/10, 2e lot de la famille) : environnement du shell.
        // Consultés par MainViewModel.StartTerminal, qui construit les
        // TerminalStartOptions passées à TerminalService.Start.
        // ==================================================================

        /// <summary>Titre du dock en breadcrumbs (défaut déclaré : Off).</summary>
        internal static bool Breadcrumbs(SettingsEngine s)
            => s.GetBool("terminal_breadcrumbs", DeclaredBool("terminal_breadcrumbs"));

        /// <summary>Valeurs possibles de <c>terminal_working_dir</c> (enum du catalogue).</summary>
        internal const string DirModeProject = "Current Project Directory";
        internal const string DirModeHome = "Home";
        internal const string DirModeCustom = "Custom";

        /// <summary>
        /// Répertoire de départ du shell, selon <c>terminal_working_dir</c>
        /// (défaut déclaré : « Current Project Directory »).
        /// Le résultat n'est JAMAIS vide : sans projet ouvert, c'est le profil
        /// utilisateur — exactement le repli que <c>TerminalService.Start</c>
        /// appliquait déjà avant d'être réglé.
        /// </summary>
        internal static string ResolveStartDirectory(SettingsEngine s, string? projectPath, out string? warning)
        {
            warning = null;
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var mode = s.GetString("terminal_working_dir", DeclaredString("terminal_working_dir"));

            if (string.Equals(mode, DirModeHome, StringComparison.Ordinal))
            {
                return home;
            }

            if (string.Equals(mode, DirModeCustom, StringComparison.Ordinal))
            {
                // Le catalogue déclare L'ENUM « Custom » mais aucune clé compagnon
                // portant le chemin. Choisir un dossier nous-mêmes = inventer une
                // donnée (interdit) ; faire croire que « Custom » s'applique en
                // démarrant ailleurs serait un réglage AFFICHANT FAUX. On le dit
                // donc explicitement dans le terminal et on garde le répertoire
                // projet/utilisateur.
                warning = "terminal_working_dir = « Custom » : aucune clé de chemin n'existe au catalogue — répertoire projet/utilisateur conservé.";
                return string.IsNullOrWhiteSpace(projectPath) ? home : projectPath;
            }

            // Current Project Directory (défaut) — ou valeur inconnue.
            return string.IsNullOrWhiteSpace(projectPath) ? home : projectPath;
        }

        /// <summary>
        /// Shell demandé par <c>terminal_shell</c> (défaut déclaré : « System »).
        /// Renvoie le KIND (pour la logique venv ci-dessous) plus le nom de
        /// programme/arguments à passer au service ; FileName null = laisser le
        /// service choisir comme avant (cmd.exe / /bin/bash).
        /// Vérifié le 01/10 : powershell.exe fournit déjà son invite avec stdin
        /// redirigé, mais bash SANS <c>-i</c> exécute sans aucun invite — d'où
        /// l'argument <c>-i</c>. « bash » = bash.exe résolu dans le PATH (WSL ou
        /// Git selon l'installation ; absent → le message d'erreur du service
        /// s'affiche dans le terminal, rien n'est masqué).
        /// </summary>
        internal enum TerminalShellKind { System, Cmd, PowerShell, Bash }

        internal static (TerminalShellKind Kind, string? FileName, string? Arguments) ResolveShell(SettingsEngine s)
        {
            var mode = s.GetString("terminal_shell", DeclaredString("terminal_shell"));
            switch (mode)
            {
                case "cmd":
                    return (TerminalShellKind.Cmd, "cmd.exe", null);
                case "PowerShell":
                    return (TerminalShellKind.PowerShell, "powershell.exe", null);
                case "bash":
                    return (TerminalShellKind.Bash, "bash.exe", "-i");
                default:
                    // « System » (défaut) ou valeur inconnue : comportement historique.
                    return OperatingSystem.IsWindows()
                        ? (TerminalShellKind.Cmd, null, null)
                        : (TerminalShellKind.Bash, null, null);
            }
        }

        /// <summary>
        /// Variables d'<c>terminal_env_vars</c> (défaut déclaré : <c>{}</c>).
        /// JSON clé-valeur texte ; JSON invalide = rien d'appliqué + avertissement
        /// annoncé dans le terminal (jamais d'échec silencieux).
        /// </summary>
        internal static IReadOnlyDictionary<string, string>? ResolveEnvVars(SettingsEngine s, out string? warning)
        {
            warning = null;
            var raw = s.GetString("terminal_env_vars", DeclaredString("terminal_env_vars"));
            if (string.IsNullOrWhiteSpace(raw) || raw.Trim() == "{}")
            {
                return null;
            }

            try
            {
                var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(raw);
                if (parsed is { Count: > 0 })
                {
                    return parsed;
                }
                return null;
            }
            catch (JsonException ex)
            {
                warning = $"terminal_env_vars n'est pas un JSON valide ({{\"CLE\":\"VAL\"}}) — ignoré. ({ex.Message})";
                return null;
            }
        }

        /// <summary>
        /// Commande d'activation d'environnement virtuel Python selon
        /// <c>terminal_detect_venv</c> (défaut déclaré : Oui). Probe UNIQUEMENT le
        /// répertoire de départ (pas d'arborescence récursive : on active ce qui
        /// est dans le dossier lancé, comme un cd classique).
        /// bash = renvoyé null : l'activation Windows (<c>Scripts\activate</c>)
        /// n'est pas portable vers un shell POSIX choisi par l'utilisateur
        /// (Git bash et WSL ne partagent pas les mêmes chemins) — laisser
        /// l'utilisateur l'activer à la main vaut mieux qu'envoyer une commande
        /// qui échouerait silencieusement.
        /// </summary>
        internal static string? ResolveInitialCommand(SettingsEngine s, string? startDirectory, TerminalShellKind kind)
        {
            if (kind == TerminalShellKind.Bash || string.IsNullOrWhiteSpace(startDirectory))
            {
                return null;
            }
            if (!s.GetBool("terminal_detect_venv", DeclaredBool("terminal_detect_venv")))
            {
                return null;
            }

            try
            {
                foreach (var name in new[] { ".venv", "venv", "env" })
                {
                    var root = Path.Combine(startDirectory, name);
                    if (kind == TerminalShellKind.PowerShell)
                    {
                        // PowerShell DOIT utiliser Activate.ps1 : exécuter
                        // activate.bat depuis PowerShell lance un cmd enfant et ne
                        // modifie PAS la session en cours.
                        var ps1 = Path.Combine(root, "Scripts", "Activate.ps1");
                        if (File.Exists(ps1))
                        {
                            return $"& '{ps1.Replace("'", "''")}'";
                        }
                    }
                    else
                    {
                        var bat = Path.Combine(root, "Scripts", "activate.bat");
                        if (File.Exists(bat))
                        {
                            return $"call \"{bat}\"";
                        }
                    }
                }
            }
            catch
            {
                // Chemin illisible/gone : pas d'activation, jamais de plantage.
            }

            return null;
        }
    }
}
