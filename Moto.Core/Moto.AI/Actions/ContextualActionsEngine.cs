// Moto.Core/AI/Actions/ContextualActionsEngine.cs
// Moteur d'actions contextuelles : propose des actions selon le contexte courant.
// Reste dans le domaine de MOTO Editor (UI, layout, terminal) sans parser le code.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Moto.Core.AI.Actions
{
    /// <summary>Type d'action contextuelle.</summary>
    public enum ContextualActionKind
    {
        Layout,      // Optimiser le layout
        Terminal,    // Tester le terminal
        Editor,      // Actions éditeur
        Ai,          // Actions IA
        Project      // Actions projet
    }

    /// <summary>Une action contextuelle proposée à l'utilisateur.</summary>
    public sealed class ContextualAction
    {
        public string Id { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public ContextualActionKind Kind { get; init; }
        public string Command { get; init; } = string.Empty;  // commande slash à exécuter
        public double Relevance { get; init; }                 // [0..1]
    }

    /// <summary>
    /// Contexte fourni au moteur pour décider des actions pertinentes.
    /// </summary>
    public sealed class ActionContext
    {
        public bool HasOpenDocument { get; init; }
        public bool IsTerminalVisible { get; init; }
        public bool IsMaximized { get; init; }
        public string? CurrentFilePath { get; init; }
        public bool HasErrors { get; init; }
        public int OpenTabsCount { get; init; }
    }

    /// <summary>
    /// Moteur d'actions contextuelles.
    /// Retourne des actions pertinentes selon l'état courant de l'éditeur.
    /// </summary>
    public sealed class ContextualActionsEngine
    {
        /// <summary>Catalogue statique de toutes les actions disponibles.</summary>
        private static readonly ContextualAction[] AllActions = new[]
        {
            new ContextualAction
            {
                Id = "layout.optimize",
                Title = "Optimiser le layout",
                Description = "Réorganise les panneaux pour l'espace disponible.",
                Kind = ContextualActionKind.Layout,
                Command = "/action layout-optimize",
                Relevance = 0.7
            },
            new ContextualAction
            {
                Id = "layout.maximize",
                Title = "Maximiser l'éditeur",
                Description = "Passe l'éditeur en plein écran.",
                Kind = ContextualActionKind.Layout,
                Command = "/action maximize",
                Relevance = 0.6
            },
            new ContextualAction
            {
                Id = "layout.restore",
                Title = "Restaurer le layout",
                Description = "Revient au layout par défaut.",
                Kind = ContextualActionKind.Layout,
                Command = "/action layout-restore",
                Relevance = 0.6
            },
            new ContextualAction
            {
                Id = "terminal.test",
                Title = "Tester le terminal",
                Description = "Ouvre le terminal et lance une commande de test.",
                Kind = ContextualActionKind.Terminal,
                Command = "/action terminal-test",
                Relevance = 0.8
            },
            new ContextualAction
            {
                Id = "terminal.open",
                Title = "Ouvrir le terminal",
                Description = "Affiche le terminal intégré.",
                Kind = ContextualActionKind.Terminal,
                Command = "/action terminal-open",
                Relevance = 0.7
            },
            new ContextualAction
            {
                Id = "editor.format",
                Title = "Formater le fichier",
                Description = "Applique le formatage au fichier courant.",
                Kind = ContextualActionKind.Editor,
                Command = "/action format",
                Relevance = 0.9
            },
            new ContextualAction
            {
                Id = "ai.explain",
                Title = "Expliquer le code",
                Description = "Demande à MOTO AI d'expliquer la sélection.",
                Kind = ContextualActionKind.Ai,
                Command = "/action explain",
                Relevance = 0.8
            },
            new ContextualAction
            {
                Id = "project.build",
                Title = "Compiler le projet",
                Description = "Lance la compilation et affiche les diagnostics.",
                Kind = ContextualActionKind.Project,
                Command = "/action build",
                Relevance = 0.9
            }
        };

        /// <summary>
        /// Retourne les actions pertinentes pour le contexte donné,
        /// triées par pertinence décroissante.
        /// </summary>
        public IReadOnlyList<ContextualAction> GetActions(ActionContext context)
        {
            var scored = new List<(ContextualAction action, double score)>();

            foreach (var action in AllActions)
            {
                var score = ComputeScore(action, context);
                if (score > 0.2) // seuil de pertinence
                    scored.Add((action, score));
            }

            return scored
                .OrderByDescending(x => x.score)
                .Select(x => x.action)
                .Take(6)
                .ToList();
        }

        /// <summary>Calcule un score de pertinence selon le contexte.</summary>
        private static double ComputeScore(ContextualAction action, ActionContext context)
        {
            var score = action.Relevance;

            switch (action.Id)
            {
                // Layout : pertinent si pas maximisé ou plusieurs onglets
                case "layout.maximize":
                    if (context.IsMaximized) score -= 0.5; // déjà maximisé
                    if (context.OpenTabsCount > 1) score += 0.2;
                    break;
                case "layout.restore":
                    if (!context.IsMaximized) score -= 0.4;
                    break;

                // Terminal : pertinent si caché
                case "terminal.open":
                case "terminal.test":
                    if (context.IsTerminalVisible) score -= 0.3;
                    break;

                // Éditeur : nécessite un document ouvert
                case "editor.format":
                case "ai.explain":
                    if (!context.HasOpenDocument) score -= 0.8;
                    break;

                // Projet : pertinent s'il y a des erreurs
                case "project.build":
                    if (context.HasErrors) score += 0.3;
                    break;
            }

            return Math.Clamp(score, 0.0, 1.0);
        }
    }
}
