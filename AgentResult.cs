// Snake2000.Engine/AgentIntegrated/Core/AgentResult.cs
using System.Collections.Generic;

namespace Snake2000.Engine.AgentIntegrated.Core
{
    /// <summary>
    /// Résultat standardisé produit par chaque agent du pipeline.
    /// Tous les agents doivent retourner ce type afin de garantir
    /// une orchestration propre dans XenoPipeline.
    /// </summary>
    public class AgentResult
    {
        /// <summary>
        /// Nom du module ou de l'agent ayant produit le résultat.
        /// Exemple : "Scanner", "Analyzer", "Synthesizer".
        /// </summary>
        public string ModuleName { get; set; } = string.Empty;

        /// <summary>
        /// Statut d'exécution.
        /// Valeurs recommandées : "success", "warning", "error".
        /// </summary>
        public string Status { get; set; } = "pending";

        /// <summary>
        /// Résumé court du résultat, lisible par un humain ou un superviseur IA.
        /// </summary>
        public string Summary { get; set; } = string.Empty;

        /// <summary>
        /// Détails additionnels, diagnostics, avertissements ou étapes intermédiaires.
        /// </summary>
        public List<string> Details { get; } = new List<string>();

        /// <summary>
        /// Payload structuré transmis aux agents suivants.
        /// Utiliser AgentPayloadKeys pour éviter les chaînes magiques.
        /// </summary>
        public Dictionary<string, object> Payload { get; } = new Dictionary<string, object>();
    }
}
