// Snake2000.Engine/AgentIntegrated/Pipeline/PipelineReport.cs
namespace Snake2000.Engine.AgentIntegrated.Pipeline
{
    /// <summary>
    /// Rapport global d'exécution du pipeline XENO-SSS∞.
    /// Destiné à être consommé par une UI, un superviseur IA ou un système de log.
    /// </summary>
    public class PipelineReport
    {
        /// <summary>
        /// Statut global du pipeline.
        /// Valeurs recommandées : "running", "success", "warning", "error".
        /// </summary>
        public string Status { get; set; } = "pending";

        /// <summary>
        /// Résumé court du résultat global.
        /// </summary>
        public string Summary { get; set; } = string.Empty;

        // TODO: ajouter :
        // - date de début
        // - date de fin
        // - liste des PipelineStepResult
        // - erreurs fatales
        // - avertissements
    }
}
