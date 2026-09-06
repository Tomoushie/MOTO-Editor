// Moto.Core/AI/Commands/CommandPaletteEngine.cs
// Routeur unifié pour la palette de commandes Ctrl+Shift+P.
// Agrège : commandes menu, actions contextuelles, commandes slash, plugins.
using System;
using System.Collections.Generic;
using System.Linq;
using Moto.Core.AI.Actions;

namespace Moto.Core.AI.Commands
{
    /// <summary>Catégorie d'une commande pour le grouping visuel.</summary>
    public enum CommandCategory
    {
        Menu,           // Commandes de menu classiques
        Action,         // Actions contextuelles
        Slash,          // Commandes slash (/export, /neural, etc.)
        Plugin,         // Commandes de plugins
        Navigation,     // Navigation (back, forward, etc.)
        Settings        // Paramètres
    }

    /// <summary>Une commande exécutable depuis la palette.</summary>
    public sealed class PaletteCommand
    {
        public string Id { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string Shortcut { get; init; } = string.Empty;
        public CommandCategory Category { get; init; }
        public string CommandText { get; init; } = string.Empty; // texte à envoyer au routeur
        public double Relevance { get; set; } = 1.0;
    }

    /// <summary>
    /// Moteur de palette de commandes : agrège toutes les sources
    /// et fournit une liste filtrable/rechercheable.
    /// </summary>
    public sealed class CommandPaletteEngine
    {
        private readonly ContextualActionsEngine _actionsEngine;
        private readonly List<PaletteCommand> _staticCommands;

        public CommandPaletteEngine(ContextualActionsEngine actionsEngine)
        {
            _actionsEngine = actionsEngine ?? throw new ArgumentNullException(nameof(actionsEngine));
            _staticCommands = BuildStaticCommands();
        }

        /// <summary>
        /// Retourne toutes les commandes disponibles, enrichies par le contexte.
        /// </summary>
        public IReadOnlyList<PaletteCommand> GetAllCommands(ActionContext? context = null)
        {
            var commands = new List<PaletteCommand>(_staticCommands);

            // Ajoute les actions contextuelles dynamiques
            if (context != null)
            {
                var actions = _actionsEngine.GetActions(context);
                foreach (var action in actions)
                {
                    commands.Add(new PaletteCommand
                    {
                        Id = $"action.{action.Id}",
                        Title = action.Title,
                        Description = action.Description,
                        Category = CommandCategory.Action,
                        CommandText = action.Command,
                        Relevance = action.Relevance
                    });
                }
            }

            return commands
                .OrderByDescending(c => c.Relevance)
                .ThenBy(c => c.Category)
                .ThenBy(c => c.Title)
                .ToList();
        }

        /// <summary>
        /// Filtre les commandes par requête de recherche.
        /// Matching fuzzy sur Title, Description et Category.
        /// </summary>
        public IReadOnlyList<PaletteCommand> Search(string query, ActionContext? context = null)
        {
            if (string.IsNullOrWhiteSpace(query))
                return GetAllCommands(context);

            var normalized = query.Trim().ToLowerInvariant();
            var all = GetAllCommands(context);

            return all
                .Where(c =>
                    c.Title.ToLowerInvariant().Contains(normalized) ||
                    c.Description.ToLowerInvariant().Contains(normalized) ||
                    c.Category.ToString().ToLowerInvariant().Contains(normalized) ||
                    c.Shortcut.ToLowerInvariant().Contains(normalized) ||
                    // ★ AJOUT (06/09) : le commentaire de la méthode promettait un
                    // "matching fuzzy" depuis le début, mais le code ne faisait que du
                    // Contains() exact — une faute de frappe ("termnal") ne trouvait
                    // jamais "terminal". Filet de secours en sous-séquence (façon
                    // fzf/VS Code : chaque lettre de la requête doit apparaître dans le
                    // titre, dans l'ordre, avec des trous permis).
                    IsFuzzySubsequence(c.Title.ToLowerInvariant(), normalized))
                .OrderByDescending(c => ComputeMatchScore(c, normalized))
                .Take(30)
                .ToList();
        }

        /// <summary>Vrai si chaque caractère de <paramref name="query"/> apparaît dans
        /// <paramref name="text"/>, dans le même ordre, avec des trous permis entre eux.</summary>
        private static bool IsFuzzySubsequence(string text, string query)
        {
            if (query.Length == 0) return true;
            var qi = 0;
            foreach (var ch in text)
            {
                if (ch == query[qi])
                {
                    qi++;
                    if (qi == query.Length) return true;
                }
            }
            return false;
        }

        private static double ComputeMatchScore(PaletteCommand command, string query)
        {
            double score = command.Relevance;

            if (command.Title.ToLowerInvariant().StartsWith(query)) score += 2.0;
            else if (command.Title.ToLowerInvariant().Contains(query)) score += 1.0;

            if (command.Description.ToLowerInvariant().Contains(query)) score += 0.5;

            return score;
        }

        /// <summary>Construit le catalogue statique de commandes.</summary>
        private static List<PaletteCommand> BuildStaticCommands()
        {
            return new List<PaletteCommand>
            {
                // ── Fichier ──
                new() { Id = "file.open", Title = "Ouvrir un dossier", Description = "Ouvre un dossier de projet.", Category = CommandCategory.Menu, CommandText = "menu:file.opendir", Shortcut = "Ctrl+O" },
                new() { Id = "file.save", Title = "Enregistrer", Description = "Enregistre le fichier courant.", Category = CommandCategory.Menu, CommandText = "menu:file.save", Shortcut = "Ctrl+S" },
                new() { Id = "file.import", Title = "Importer un projet", Description = "Importe un projet VS/VSCode.", Category = CommandCategory.Menu, CommandText = "menu:file.import" },
                new() { Id = "file.export", Title = "Exporter", Description = "Exporte le fichier courant.", Category = CommandCategory.Menu, CommandText = "/export" },

                // ── Édition ──
                new() { Id = "edit.search", Title = "Rechercher", Description = "Recherche dans le fichier.", Category = CommandCategory.Menu, CommandText = "menu:edit.search", Shortcut = "Ctrl+F" },
                new() { Id = "edit.commands", Title = "Palette de commandes", Description = "Ouvre cette palette.", Category = CommandCategory.Menu, CommandText = "/palette", Shortcut = "Ctrl+Shift+P" },

                // ── Affichage ──
                new() { Id = "view.explorer", Title = "Basculer l'explorateur", Description = "Affiche/cache l'explorateur.", Category = CommandCategory.Menu, CommandText = "menu:view.explorer", Shortcut = "Ctrl+B" },
                new() { Id = "view.terminal", Title = "Basculer le terminal", Description = "Affiche/cache le terminal.", Category = CommandCategory.Menu, CommandText = "menu:view.terminal", Shortcut = "Ctrl+`" },
                new() { Id = "view.maximize", Title = "Maximiser l'éditeur", Description = "Passe en plein écran.", Category = CommandCategory.Menu, CommandText = "menu:view.maximize" },

                // ── Navigation ──
                new() { Id = "nav.back", Title = "Retour", Description = "Navigue vers le fichier précédent.", Category = CommandCategory.Navigation, CommandText = "menu:nav.back", Shortcut = "Alt+←" },
                new() { Id = "nav.forward", Title = "Avancer", Description = "Navigue vers le fichier suivant.", Category = CommandCategory.Navigation, CommandText = "menu:nav.forward", Shortcut = "Alt+→" },

                // ── Exécution ──
                new() { Id = "run.build", Title = "Compiler", Description = "Compile le projet.", Category = CommandCategory.Menu, CommandText = "menu:run.build", Shortcut = "F5" },
                new() { Id = "run.play", Title = "Exécuter", Description = "Lance le projet.", Category = CommandCategory.Menu, CommandText = "menu:run.play" },
                new() { Id = "run.sandbox", Title = "Sandbox", Description = "Bascule en mode sandbox.", Category = CommandCategory.Menu, CommandText = "menu:run.sandbox" },

                // ── IA ──
                new() { Id = "ai.cortex", Title = "Cortex", Description = "Ouvre le panneau Cortex.", Category = CommandCategory.Menu, CommandText = "menu:ai.cortex" },
                new() { Id = "ai.neural", Title = "Neural Mode", Description = "Ouvre le Neural Mode.", Category = CommandCategory.Menu, CommandText = "menu:ai.neural" },
                new() { Id = "ai.workspace", Title = "Workspace IA", Description = "Ouvre le Workspace IA.", Category = CommandCategory.Menu, CommandText = "menu:ai.workspace" },
                new() { Id = "ai.gallery", Title = "Galerie de plugins", Description = "Parcourt et installe des plugins.", Category = CommandCategory.Menu, CommandText = "menu:ai.gallery" },
                // ★ AJOUT (03/09, sonde disponibilité premium) : la commande
                // "ai.doc" existait déjà (MainPage.Routing.cs) mais était absente de
                // la palette — le panneau Documentation était donc injoignable.
                new() { Id = "ai.doc", Title = "Documentation du projet", Description = "Affiche la documentation générée automatiquement (README, Architecture, Modules...).", Category = CommandCategory.Menu, CommandText = "menu:ai.doc" },
                // ★ AJOUT (02/09, réveil de MotoAiPage) : mode différent d'AiChatView —
                // une instruction unique + un chemin de workspace, exécutée d'un coup
                // (pas une conversation), avec un aperçu des changements de fichiers
                // avant application. Voir CLAUDE.md pour la limite connue (l'aperçu
                // de fichiers est toujours vide pour l'instant).
                new() { Id = "ai.motopage", Title = "MOTO AI (mode Débutant/Expert)", Description = "Exécute une instruction unique sur un workspace, avec aperçu des changements.", Category = CommandCategory.Menu, CommandText = "menu:ai.motopage" },
                // ★ AJOUT (03/09, réveil de GlobalDashboardView) : jamais navigable
                // auparavant (fichier exclu du build, .xaml sous un nom corrompu).
                new() { Id = "ai.globaldashboard", Title = "Tableau de bord global", Description = "Statistiques cumulées : fichiers, lignes, IA, exports, builds.", Category = CommandCategory.Menu, CommandText = "menu:ai.globaldashboard" },
                // ★ AJOUT (03/09, réveil de ThreadListView) : jamais navigable
                // auparavant (2 méthodes ChatService manquantes, voir CLAUDE.md).
                new() { Id = "ai.threadlist", Title = "Conversations (historique)", Description = "Liste et recherche les conversations IA passées.", Category = CommandCategory.Menu, CommandText = "menu:ai.threadlist" },
                // ★ AJOUT (03/09, maquette "shell type Claude Code" convertie par
                // Qwen) : fenêtre de test séparée, ne remplace pas l'interface
                // principale existante.
                new() { Id = "ai.claudeshell", Title = "Interface (maquette Claude Code)", Description = "Fenêtre de test : conversion XAML de l'interface Claude Code par Qwen.", Category = CommandCategory.Menu, CommandText = "menu:ai.claudeshell" },
                // ★ AJOUT (03/09, panneau "Tâches en arrière-plan" réel) : suit les
                // vrais appels IA (ChatService.Tasks), pas une simulation.
                new() { Id = "ai.backgroundtasks", Title = "Tâches en arrière-plan", Description = "Suit les appels IA en cours et récemment terminés.", Category = CommandCategory.Menu, CommandText = "menu:ai.backgroundtasks" },
                // ★ AJOUT (jalon 3, agents autonomes en tâche de fond) : première
                // vraie interface de ce chantier (voir /agent <objectif> dans le chat).
                new() { Id = "ai.agentruns", Title = "Agents en cours", Description = "Liste les agents autonomes actifs/récents, permet d'en arrêter un, montre leurs messages.", Category = CommandCategory.Menu, CommandText = "menu:ai.agentruns" },
                // ★ AJOUT (03/09, réveil de GitPanelView, trouvé par la sonde
                // disponibilité premium) : commit/push/pull/branches/diff/log réels,
                // jusqu'ici sans aucun point d'entrée.
                new() { Id = "git.panel", Title = "Git", Description = "Commit, push, pull, branches, diff, log.", Category = CommandCategory.Menu, CommandText = "menu:git.panel" },

                // ── Slash commands ──
                new() { Id = "slash.neural", Title = "Neural : Générer du code", Description = "Génère du code via Neural Mode.", Category = CommandCategory.Slash, CommandText = "/neural " },
                new() { Id = "slash.cortex", Title = "Cortex : Stats", Description = "Affiche les stats Cortex.", Category = CommandCategory.Slash, CommandText = "/cortex" },
                new() { Id = "slash.rollback", Title = "Rollback paramètres", Description = "Restaure le dernier backup.", Category = CommandCategory.Settings, CommandText = "/rollback-settings" },
                new() { Id = "slash.actions", Title = "Actions contextuelles", Description = "Liste les actions disponibles.", Category = CommandCategory.Action, CommandText = "/actions" },
                new() { Id = "slash.aisettings", Title = "AI Settings", Description = "Modifie les paramètres via IA.", Category = CommandCategory.Settings, CommandText = "/ai-settings" },

                // ── Paramètres ──
                new() { Id = "settings.open", Title = "Paramètres", Description = "Ouvre les paramètres.", Category = CommandCategory.Settings, CommandText = "menu:settings" },
                new() { Id = "settings.theme", Title = "Changer le thème", Description = "Bascule thème clair/sombre.", Category = CommandCategory.Settings, CommandText = "menu:view.theme" }
            };
        }
    }
}
