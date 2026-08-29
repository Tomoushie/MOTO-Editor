// Snake2000.Engine/AgentIntegrated/Pipeline/PipelineStepResult.cs
namespace Snake2000.Engine.AgentIntegrated.Pipeline
{
    /// <summary>
    /// Résultat d'une étape individuelle du pipeline.
    /// Permet de tracer Scanner, Analyzer, Synthesizer, Connector et Validator.
    /// </summary>
    public class PipelineStepResult
    {
        /// <summary>
        /// Nom de l'agent ou du module exécuté.
        /// </summary>
        public string ModuleName { get; set; } = string.Empty;

        /// <summary>
        /// Statut de l'étape.
        /// Valeurs recommandées : "success", "warning", "error", "skipped".
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Résumé court produit par l'agent.
        /// </summary>
        public string Summary { get; set; } = string.Empty;

        // TODO: ajouter :
        // - timestamp
        // - durée
        // - détails
        // - exceptions
    }
}
