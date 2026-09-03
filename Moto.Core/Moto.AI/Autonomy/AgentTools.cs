// Moto.Core/AI/Autonomy/AgentTools.cs
// Un IAgentTool par capacité réelle — chacun une fine enveloppe autour d'un
// service DÉJÀ existant (System.IO, TerminalService). IsMutating est le champ
// qui compte : c'est lui qui dit à BackgroundAgentLoop quels appels DOIVENT
// passer par AiConfirmationService avant exécution.
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Moto.Editor.Services; // TerminalService (namespace historique, voir le fichier lui-même)

namespace Moto.Core.AI.Autonomy
{
    public interface IAgentTool
    {
        AgentActionKind Handles { get; }

        /// <summary>Vrai si cette action modifie quelque chose (disque, processus) —
        /// détermine si BackgroundAgentLoop doit obtenir une confirmation humaine
        /// avant d'appeler ExecuteAsync.</summary>
        bool IsMutating { get; }

        /// <summary>
        /// Construit le texte de la demande de confirmation À PARTIR DES SEULS
        /// CHAMPS LITTÉRAUX de l'action (Path/Content/Command) — jamais à partir
        /// d'une auto-description que le modèle aurait pu écrire, pour qu'un
        /// prompt confus ou hostile ne puisse pas afficher une chose et en faire
        /// une autre. Non appelé pour les outils non-mutants.
        /// </summary>
        string DescribeForConfirmation(AgentAction action, string workspaceRoot);

        Task<string> ExecuteAsync(AgentAction action, string workspaceRoot, CancellationToken ct);
    }

    /// <summary>Résout un chemin potentiellement relatif contre la racine du
    /// workspace — un agent qui répond "hello.txt" doit écrire dans le PROJET
    /// ouvert, pas dans le dossier courant du process. Le refus d'écrire hors du
    /// dossier du projet (chemins absolus qui s'évadent) est prévu au jalon 3
    /// (durcissement), pas ici : au jalon 1, la confirmation humaine — qui
    /// affiche toujours le chemin résolu en toutes lettres — reste le garde-fou.</summary>
    internal static class AgentPathResolver
    {
        public static string Resolve(string workspaceRoot, string path)
        {
            if (Path.IsPathRooted(path)) return path;
            var root = string.IsNullOrWhiteSpace(workspaceRoot) ? Directory.GetCurrentDirectory() : workspaceRoot;
            return Path.GetFullPath(Path.Combine(root, path));
        }
    }

    public sealed class ReadFileTool : IAgentTool
    {
        public AgentActionKind Handles => AgentActionKind.ReadFile;
        public bool IsMutating => false;

        public string DescribeForConfirmation(AgentAction action, string workspaceRoot) => string.Empty; // jamais appelé

        public async Task<string> ExecuteAsync(AgentAction action, string workspaceRoot, CancellationToken ct)
        {
            var full = AgentPathResolver.Resolve(workspaceRoot, action.Path ?? string.Empty);
            if (!File.Exists(full))
                return $"Fichier introuvable : {action.Path}";

            try
            {
                var content = await File.ReadAllTextAsync(full, ct);
                // Plafond généreux mais réel : un gros fichier ne doit pas noyer le
                // prompt du prochain pas (même patron que ChatService.BuildContextBlock).
                if (content.Length > 6000) content = content[..6000] + "\n… (tronqué)";
                return $"Contenu de {action.Path} :\n{content}";
            }
            catch (Exception ex)
            {
                return $"Impossible de lire {action.Path} : {ex.Message}";
            }
        }
    }

    public sealed class WriteFileTool : IAgentTool
    {
        public AgentActionKind Handles => AgentActionKind.WriteFile;
        public bool IsMutating => true;

        public string DescribeForConfirmation(AgentAction action, string workspaceRoot)
        {
            var full = AgentPathResolver.Resolve(workspaceRoot, action.Path ?? string.Empty);
            var preview = action.Content ?? string.Empty;
            if (preview.Length > 2000) preview = preview[..2000] + "\n… (tronqué pour l'aperçu)";
            return $"Chemin : {full}\n\nContenu proposé :\n{preview}";
        }

        public async Task<string> ExecuteAsync(AgentAction action, string workspaceRoot, CancellationToken ct)
        {
            var full = AgentPathResolver.Resolve(workspaceRoot, action.Path ?? string.Empty);
            try
            {
                var dir = Path.GetDirectoryName(full);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                await File.WriteAllTextAsync(full, action.Content ?? string.Empty, ct);
                return $"Fichier écrit : {action.Path}";
            }
            catch (Exception ex)
            {
                return $"Échec de l'écriture de {action.Path} : {ex.Message}";
            }
        }
    }

    public sealed class RunCommandTool : IAgentTool
    {
        private readonly TerminalService _terminal;

        public RunCommandTool(TerminalService terminal)
        {
            _terminal = terminal ?? throw new ArgumentNullException(nameof(terminal));
        }

        public AgentActionKind Handles => AgentActionKind.RunCommand;
        public bool IsMutating => true;

        public string DescribeForConfirmation(AgentAction action, string workspaceRoot) =>
            $"Commande : {action.Command}\nDossier : {workspaceRoot}";

        public async Task<string> ExecuteAsync(AgentAction action, string workspaceRoot, CancellationToken ct)
        {
            var result = await _terminal.ExecuteAsync(action.Command ?? string.Empty, workspaceRoot, ct);
            var output = string.IsNullOrWhiteSpace(result.Output) ? "(aucune sortie)" : result.Output;
            if (output.Length > 3000) output = output[..3000] + "\n… (tronqué)";
            return result.ExitCode == 0
                ? $"Commande exécutée (code 0) :\n{output}"
                : $"Commande terminée avec le code {result.ExitCode} :\n{output}\n{result.Error}";
        }
    }

    public sealed class FinishTool : IAgentTool
    {
        public AgentActionKind Handles => AgentActionKind.Finish;
        public bool IsMutating => false;

        public string DescribeForConfirmation(AgentAction action, string workspaceRoot) => string.Empty; // jamais appelé

        public Task<string> ExecuteAsync(AgentAction action, string workspaceRoot, CancellationToken ct) =>
            Task.FromResult(action.Summary ?? "Terminé.");
    }
}
