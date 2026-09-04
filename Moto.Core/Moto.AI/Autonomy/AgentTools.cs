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

        /// <summary>★ AJOUT (jalon 2) : `agentId` — nécessaire pour SendMessageTool
        /// (l'expéditeur du message), ignoré par les 4 autres outils.</summary>
        Task<string> ExecuteAsync(AgentAction action, string workspaceRoot, string agentId, CancellationToken ct);
    }

    /// <summary>Résout un chemin potentiellement relatif contre la racine du
    /// workspace — un agent qui répond "hello.txt" doit écrire dans le PROJET
    /// ouvert, pas dans le dossier courant du process.
    /// ★ AJOUT (jalon 3) : IsWithinRoot — le refus d'écrire/lire hors du dossier
    /// du projet, prévu depuis le jalon 1 mais pas encore fait (la confirmation
    /// humaine, qui affiche toujours le chemin résolu en toutes lettres, était
    /// le seul garde-fou jusqu'ici). Vérifié par BackgroundAgentLoop AVANT même
    /// de proposer une confirmation — un chemin qui s'évade ne doit pas arriver
    /// jusqu'à l'humain, il doit être refusé automatiquement.</summary>
    internal static class AgentPathResolver
    {
        /// <summary>Racine effective : le workspace ouvert, ou le dossier courant
        /// du process si aucun workspace n'est ouvert (même repli que Resolve) —
        /// le confinement doit s'appliquer aux DEUX cas, jamais être désactivé
        /// faute de workspace.</summary>
        public static string EffectiveRoot(string workspaceRoot) =>
            string.IsNullOrWhiteSpace(workspaceRoot) ? Directory.GetCurrentDirectory() : workspaceRoot;

        public static string Resolve(string workspaceRoot, string path)
        {
            if (Path.IsPathRooted(path)) return path;
            return Path.GetFullPath(Path.Combine(EffectiveRoot(workspaceRoot), path));
        }

        /// <summary>Vrai si `resolvedPath` (déjà passé par Resolve) reste SOUS la
        /// racine effective — bloque un chemin absolu qui pointe ailleurs, ou un
        /// "../.." qui s'évade du dossier du projet.</summary>
        public static bool IsWithinRoot(string workspaceRoot, string resolvedPath)
        {
            var root = Path.GetFullPath(EffectiveRoot(workspaceRoot));
            var rootWithSep = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
            var full = Path.GetFullPath(resolvedPath);
            return full.Equals(root, StringComparison.OrdinalIgnoreCase)
                || full.StartsWith(rootWithSep, StringComparison.OrdinalIgnoreCase);
        }
    }

    public sealed class ReadFileTool : IAgentTool
    {
        public AgentActionKind Handles => AgentActionKind.ReadFile;
        public bool IsMutating => false;

        public string DescribeForConfirmation(AgentAction action, string workspaceRoot) => string.Empty; // jamais appelé

        public async Task<string> ExecuteAsync(AgentAction action, string workspaceRoot, string agentId, CancellationToken ct)
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

        public async Task<string> ExecuteAsync(AgentAction action, string workspaceRoot, string agentId, CancellationToken ct)
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

        public async Task<string> ExecuteAsync(AgentAction action, string workspaceRoot, string agentId, CancellationToken ct)
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

        public Task<string> ExecuteAsync(AgentAction action, string workspaceRoot, string agentId, CancellationToken ct) =>
            Task.FromResult(action.Summary ?? "Terminé.");
    }

    /// <summary>★ AJOUT (jalon 2) : envoie un vrai message à un autre agent (ou à
    /// tous, si ToAgentId est vide) via AgentMessageBus. Jamais mutant — aucune
    /// confirmation requise, un message n'écrit rien sur le disque.</summary>
    public sealed class SendMessageTool : IAgentTool
    {
        private readonly AgentMessageBus _bus;

        public SendMessageTool(AgentMessageBus bus)
        {
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
        }

        public AgentActionKind Handles => AgentActionKind.SendMessage;
        public bool IsMutating => false;

        public string DescribeForConfirmation(AgentAction action, string workspaceRoot) => string.Empty; // jamais appelé

        public Task<string> ExecuteAsync(AgentAction action, string workspaceRoot, string agentId, CancellationToken ct)
        {
            _bus.Post(new AgentMessage
            {
                FromAgentId = agentId,
                ToAgentId = action.ToAgentId,
                Kind = AgentMessageKind.Note,
                Text = action.Summary ?? string.Empty
            });

            var target = action.ToAgentId ?? "tous les agents";
            return Task.FromResult($"Message envoyé à {target} : {action.Summary}");
        }
    }
}
