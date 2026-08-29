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
                    c.Shortcut.ToLowerInvariant().Contains(normalized))
                .OrderByDescending(c => ComputeMatchScore(c, normalized))
                .Take(30)
                .ToList();
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
