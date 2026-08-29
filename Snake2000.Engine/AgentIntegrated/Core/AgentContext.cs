// Snake2000.Engine.AgentIntegrated/Core/AgentContext.cs
using System.Collections.Generic;

namespace Snake2000.Engine.AgentIntegrated.Core
{
    /// <summary>
    /// Contexte transmis aux agents XENO-SSS∞.
    /// Il transporte le workspace et les données utiles au pipeline.
    /// </summary>
    public class AgentContext
    {
        /// <summary>
        /// Chemin racine du projet analysé.
        /// </summary>
        public string RootPath { get; set; } = string.Empty;

        /// <summary>
        /// Données libres partagées entre agents.
        /// </summary>
        public Dictionary<string, object> Data { get; } = new Dictionary<string, object>();
    }
}
