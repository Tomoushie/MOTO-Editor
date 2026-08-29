// Moto.Editor/AI/Builders/ProjectFixer.cs
using System;
using System.Threading.Tasks;

namespace Moto.Editor.AI.Builders
{
    /// <summary>
    /// Contrat pour appeler XENO-SSS∞.
    /// Réutilise l'interface existante de MOTO Editor.
    /// </summary>
    public interface IProjectFixerBridge
    {
        Task<BuilderResult> RunFullPipelineAsync(string workspacePath, string mode);
    }

    /// <summary>
    /// Bouton magique "Répare tout".
    /// Déclenche le pipeline complet XENO-SSS∞ :
    /// Scanner → Analyzer → Synthesizer → Connector → Validator.
    ///
    /// L'utilisateur n'a qu'à cliquer.
    /// </summary>
    public class ProjectFixer
    {
        private readonly IProjectFixerBridge _bridge;

        public ProjectFixer(IProjectFixerBridge bridge)
        {
            _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        }

        /// <summary>
        /// Répare tout le projet.
        /// </summary>
        public async Task<BuilderResult> FixAllAsync(string workspacePath)
        {
            var result = new BuilderResult();

            try
            {
                result.Explanation =
                    "Je vais scanner le projet, détecter les erreurs, générer les fichiers manquants, " +
                    "connecter les briques et valider le tout. " +
                    "Cela peut prendre quelques secondes.";

                // Appelle le pipeline complet XENO-SSS∞
                var pipelineResult = await _bridge.RunFullPipelineAsync(workspacePath, "fix-all");

                if (pipelineResult.Success)
                {
                    result.Success = true;
                    result.Summary = "Projet réparé avec succès.";
                    result.Files.AddRange(pipelineResult.Files);
                    result.Integrations.AddRange(pipelineResult.Integrations);
                    result.Warnings.AddRange(pipelineResult.Warnings);
                }
                else
                {
                    result.Success = false;
                    result.Summary = "La réparation a rencontré des problèmes.";
                    result.Warnings.AddRange(pipelineResult.Warnings);
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Summary = "Erreur lors de la réparation.";
                result.Explanation = $"Erreur : {ex.Message}";
            }

            return result;
        }
    }
}
