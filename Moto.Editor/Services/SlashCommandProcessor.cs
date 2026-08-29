// Moto.Editor/Services/SlashCommandProcessor.cs
using System;

namespace Moto.Editor.Services
{
    public enum SlashAction
    {
        None, Help, Compact, Clear, AddFile, AddFolder,
        AddSelection, Model, Mode, Health, Fix
    }

    public class SlashParse
    {
        public SlashAction Action { get; set; } = SlashAction.None;
        public string Argument { get; set; } = string.Empty;
    }

    /// <summary>
    /// Analyse les commandes slash du chat, comme dans les IDE classiques :
    /// /compact, /add, /context, /model, /mode, /health, /fix, /help...
    /// </summary>
    public static class SlashCommandProcessor
    {
        /// <summary>Liste affichée dans l'autocomplétion quand on tape "/".</summary>
        public static readonly string[] KnownCommands =
        {
            "/help", "/compact", "/clear", "/add <chemin>", "/dossier <chemin>",
            "/selection", "/model <interne|ollama|openai|anthropic|mistral>",
            "/mode <beginner|expert>", "/health", "/fix"
        };

        public static SlashParse Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || !text.StartsWith("/"))
            {
                return new SlashParse { Action = SlashAction.None };
            }

            var parts = text.Split(' ', 2);
            var cmd = parts[0].ToLowerInvariant();
            var arg = parts.Length > 1 ? parts[1].Trim() : string.Empty;

            return cmd switch
            {
                "/help" or "/aide" => new SlashParse { Action = SlashAction.Help },
                "/compact" => new SlashParse { Action = SlashAction.Compact },
                "/clear" => new SlashParse { Action = SlashAction.Clear },
                "/add" or "/context" or "/fichier" => new SlashParse { Action = SlashAction.AddFile, Argument = arg },
                "/dossier" or "/folder" => new SlashParse { Action = SlashAction.AddFolder, Argument = arg },
                "/selection" => new SlashParse { Action = SlashAction.AddSelection },
                "/model" or "/modele" => new SlashParse { Action = SlashAction.Model, Argument = arg },
                "/mode" => new SlashParse { Action = SlashAction.Mode, Argument = arg },
                "/health" or "/sante" => new SlashParse { Action = SlashAction.Health },
                "/fix" => new SlashParse { Action = SlashAction.Fix },
                _ => new SlashParse { Action = SlashAction.Help, Argument = "Commande inconnue." }
            };
        }

        public static string HelpText()
        {
            return "Commandes disponibles :\n" +
                   "/compact — résume l'historique pour économiser le contexte\n" +
                   "/clear — efface la conversation\n" +
                   "/add <chemin> — attache un fichier au contexte\n" +
                   "/dossier <chemin> — attache un dossier au contexte\n" +
                   "/selection — attache la sélection de l'éditeur\n" +
                   "/model <nom> — choisit le modèle (interne, ollama, openai...)\n" +
                   "/mode <beginner|expert> — mode pédagogique ou direct\n" +
                   "/health — analyse la santé du projet\n" +
                   "/fix — réparation automatique";
        }
    }
}
