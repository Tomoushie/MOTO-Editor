// Snake2000.Engine/AgentIntegrated/Core/AgentContext.cs
namespace Snake2000.Engine.AgentIntegrated.Core
{
    /// <summary>
    /// Contexte d'exécution partagé par tous les agents du pipeline AgentIntegrated.
    /// Ce contexte doit transporter les informations globales nécessaires à l'orchestration :
    /// racine du projet, options, logger, jeton d'annulation, etc.
    /// </summary>
    public class AgentContext
    {
        /// <summary>
        /// Chemin racine du projet Snake2000.
        /// Exemple : dossier contenant Engine/, Game/, App/.
        /// </summary>
        public string RootPath { get; set; } = string.Empty;

        // TODO: ajouter si nécessaire :
        // - CancellationToken
        // - Logger
        // - Options d'analyse
        // - Modules ciblés
        // - Niveau de verbosité
    }
}
