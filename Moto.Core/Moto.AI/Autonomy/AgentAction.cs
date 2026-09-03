// Moto.Core/AI/Autonomy/AgentAction.cs
// ★ AJOUT (03/09, fondation "agents autonomes en tâche de fond", demandé par
// Tom après avoir vu deux sessions Claude Code se parler entre elles). Plan
// conçu et vérifié contre le vrai code (workflow de conception, 3 architectures
// + jugement + synthèse — voir Docs/probes/agent-messaging-design-2026-09-03.json)
// avant d'écrire quoi que ce soit ici. Jalon 1 : UN agent autonome de bout en
// bout, aucune UI nouvelle, verrou de confirmation OBLIGATOIRE avant toute
// action qui écrit (fichier ou commande) — même exigence que SelfRepairAgent
// (voir CLAUDE.md/mémoire "selfrepairagent-supervision-requirement").
using System;

namespace Moto.Core.AI.Autonomy
{
    /// <summary>
    /// Les seules actions qu'un agent autonome peut proposer au jalon 1.
    /// SendMessage (jalon 2, messagerie inter-agents) volontairement absent ici.
    /// </summary>
    public enum AgentActionKind
    {
        ReadFile,
        WriteFile,
        RunCommand,
        Finish,
        /// <summary>Réponse du modèle qui ne correspond à aucun format reconnu —
        /// jamais une exception, toujours ce cas de repli (voir AgentActionParser).</summary>
        Malformed
    }

    /// <summary>
    /// Une décision unique, pour UN pas de la boucle perçoit→décide→agit→observe
    /// (BackgroundAgentLoop). Pure donnée : aucune logique, aucun accès disque —
    /// c'est exactement la forme que AgentActionParser extrait du texte brut
    /// renvoyé par MotoAiKernel.RouteAsync (qui ne fait AUCUN appel d'outil
    /// structuré, juste du texte).
    /// </summary>
    public sealed class AgentAction
    {
        public AgentActionKind Kind { get; set; } = AgentActionKind.Malformed;
        public string? Path { get; set; }
        public string? Content { get; set; }
        public string? Command { get; set; }
        public string? Summary { get; set; }

        /// <summary>Texte brut du modèle ayant produit cette action — jamais utilisé
        /// pour construire un message de confirmation (voir IAgentTool.DescribeForConfirmation),
        /// gardé seulement pour le journal/débogage.</summary>
        public string RawModelOutput { get; set; } = string.Empty;
    }
}
