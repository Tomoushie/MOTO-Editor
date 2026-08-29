// Snake2000.Engine/AgentIntegrated/Core/AgentPayloadKeys.cs
namespace Snake2000.Engine.AgentIntegrated.Core
{
    /// <summary>
    /// Clés standardisées pour le payload des agents.
    /// Objectif : éviter les chaînes magiques et fiabiliser la communication
    /// entre Scanner, Analyzer, Synthesizer, Connector et Validator.
    /// </summary>
    public static class AgentPayloadKeys
    {
        /// <summary>
        /// Liste des fichiers détectés par le Scanner.
        /// </summary>
        public const string Files = nameof(Files);

        /// <summary>
        /// Cartographie du projet produite par le Scanner ou enrichie par l'Analyzer.
        /// </summary>
        public const string ProjectMap = nameof(ProjectMap);

        /// <summary>
        /// Rapport d'analyse produit par l'Analyzer.
        /// </summary>
        public const string AnalysisReport = nameof(AnalysisReport);

        /// <summary>
        /// Fichiers générés par le Synthesizer.
        /// </summary>
        public const string GeneratedFiles = nameof(GeneratedFiles);

        /// <summary>
        /// Plan d'intégration produit par le Connector.
        /// </summary>
        public const string IntegrationPlan = nameof(IntegrationPlan);

        /// <summary>
        /// Rapport final produit par le Validator.
        /// </summary>
        public const string ValidationReport = nameof(ValidationReport);
    }
}
