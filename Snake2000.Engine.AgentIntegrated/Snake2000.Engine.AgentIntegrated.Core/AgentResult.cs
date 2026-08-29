// Snake2000.Engine.AgentIntegrated/Core/AgentResult.cs
using System.Collections.Generic;

namespace Snake2000.Engine.AgentIntegrated.Core
{
    /// <summary>
    /// Résultat standard produit par chaque agent XENO-SSS∞.
    /// </summary>
    public class AgentResult
    {
        /// <summary>
        /// Nom de l'agent producteur.
        /// </summary>
        public string ModuleName { get; set; } = string.Empty;

        /// <summary>
        /// Statut : success, warning, error.
        /// </summary>
        public string Status { get; set; } = "success";

        /// <summary>
        /// Résumé court du résultat.
        /// </summary>
        public string Summary { get; set; } = string.Empty;

        /// <summary>
        /// Détails techniques ou pédagogiques.
        /// </summary>
        public List<string> Details { get; } = new List<string>();

        /// <summary>
        /// Payload structuré transmis à l'agent suivant.
        /// </summary>
        public Dictionary<string, object> Payload { get; } = new Dictionary<string, object>();
    }
}
