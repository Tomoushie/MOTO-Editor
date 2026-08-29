// Integration/XenoTaskService.cs
using System.Threading.Tasks;
using Moto.Editor.Integration;

namespace Moto.Editor.Integration
{
    /// <summary>
    /// Service haut niveau pour appeler XENO-SSS∞ depuis MOTO Editor.
    /// MOTO Editor n'exécute pas lui-même l'analyse ou la génération :
    /// il délègue tout à XENO-SSS∞.
    /// </summary>
    public class XenoTaskService
    {
        private readonly HttpXenoClient _client;

        public XenoTaskService(HttpXenoClient client)
        {
            _client = client;
        }

        /// <summary>
        /// Pipeline complet :
        /// Scanner → Analyzer → Synthesizer → Connector → Validator.
        /// </summary>
        public Task<XenoResponse> RunFullPipelineAsync(string workspacePath)
        {
            return ExecuteAsync(workspacePath, "run-full-pipeline");
        }

        /// <summary>
        /// Refactor global du projet.
        /// </summary>
        public Task<XenoResponse> RefactorGlobalAsync(string workspacePath, string instruction)
        {
            return ExecuteAsync(workspacePath, $"refactor-global:{instruction}");
        }

        /// <summary>
        /// Génération d'un module complet.
        /// </summary>
        public Task<XenoResponse> GenerateModuleAsync(string workspacePath, string moduleName)
        {
            return ExecuteAsync(workspacePath, $"generate-module:{moduleName}");
        }

        /// <summary>
        /// Correction automatique des problèmes détectés.
        /// </summary>
        public Task<XenoResponse> AutoFixAsync(string workspacePath)
        {
            return ExecuteAsync(workspacePath, "auto-fix");
        }

        /// <summary>
        /// Validation automatique après intégration.
        /// </summary>
        public Task<XenoResponse> ValidateAsync(string workspacePath)
        {
            return ExecuteAsync(workspacePath, "validate");
        }

        /// <summary>
        /// Suggestions architecturales.
        /// </summary>
        public Task<XenoResponse> GetArchitecturalSuggestionsAsync(string workspacePath)
        {
            return ExecuteAsync(workspacePath, "architecture-suggestions");
        }

        private Task<XenoResponse> ExecuteAsync(string workspacePath, string goal)
        {
            var request = new XenoRequest
            {
                WorkspacePath = workspacePath,
                Goal = goal,
                Mode = "xeno-sss"
            };

            return _client.RunAsync(request);
        }
    }
}
