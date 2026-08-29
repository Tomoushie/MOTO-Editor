// Moto.Core/AI/XenoFallbackBridge.cs
using System;
using System.Threading;
using System.Threading.Tasks;
using Moto.Core.AI.Models;
using Snake2000.Engine.AgentIntegrated.Core;

namespace Moto.Core.AI
{
    /// <summary>
    /// Pont entre le pipeline XENO-SSS∞ et le système de fallback IA.
    /// Permet au Synthesizer d'utiliser un modèle externe si besoin.
    /// </summary>
    public class XenoFallbackBridge
    {
        private readonly FallbackEngine _fallbackEngine;

        public XenoFallbackBridge(FallbackEngine fallbackEngine)
        {
            _fallbackEngine = fallbackEngine ?? throw new ArgumentNullException(nameof(fallbackEngine));
        }

        /// <summary>
        /// Génère du code via le pipeline de fallback.
        /// Utilisé par AgentSynthesizer quand le moteur local ne suffit pas.
        /// </summary>
        public async Task<AgentResult> GenerateCodeAsync(
            AgentContext context,
            string prompt,
            CancellationToken cancellationToken = default)
        {
            var result = new AgentResult
            {
                ModuleName = "XenoFallbackBridge",
                Status = "success",
                Summary = "External generation completed."
            };

            try
            {
                var aiResult = await _fallbackEngine.GenerateAsync(
                    prompt,
                    context.RootPath,
                    cancellationToken);

                if (aiResult.Success)
                {
                    result.Payload["GeneratedContent"] = aiResult.Content;
                    result.Payload["ProviderUsed"] = aiResult.ProviderName;
                    result.Payload["ModelUsed"] = aiResult.ModelUsed;
                    result.Details.Add($"Generated via {aiResult.ProviderName} ({aiResult.ModelUsed})");
                }
                else
                {
                    result.Status = "error";
                    result.Summary = aiResult.Error;
                    result.Details.Add("Fallback failed. Check AI provider settings.");
                }
            }
            catch (Exception ex)
            {
                result.Status = "error";
                result.Summary = $"Exception: {ex.Message}";
            }

            return result;
        }

        /// <summary>
        /// Corrige du code via le pipeline de fallback.
        /// </summary>
        public async Task<AgentResult> FixCodeAsync(
            string code,
            string errors,
            CancellationToken cancellationToken = default)
        {
            var result = new AgentResult
            {
                ModuleName = "XenoFallbackBridge",
                Status = "success",
                Summary = "External fix completed."
            };

            try
            {
                var aiResult = await _fallbackEngine.FixCodeAsync(code, errors, cancellationToken);

                if (aiResult.Success)
                {
                    result.Payload["FixedCode"] = aiResult.Content;
                    result.Payload["ProviderUsed"] = aiResult.ProviderName;
                }
                else
                {
                    result.Status = "error";
                    result.Summary = aiResult.Error;
                }
            }
            catch (Exception ex)
            {
                result.Status = "error";
                result.Summary = $"Exception: {ex.Message}";
            }

            return result;
        }

        /// <summary>
        /// Explique du code via le pipeline de fallback.
        /// </summary>
        public async Task<AgentResult> ExplainCodeAsync(
            string code,
            string language,
            CancellationToken cancellationToken = default)
        {
            var result = new AgentResult
            {
                ModuleName = "XenoFallbackBridge",
                Status = "success",
                Summary = "External explanation completed."
            };

            try
            {
                var aiResult = await _fallbackEngine.ExplainCodeAsync(code, language, cancellationToken);

                if (aiResult.Success)
                {
                    result.Payload["Explanation"] = aiResult.Content;
                    result.Payload["ProviderUsed"] = aiResult.ProviderName;
                }
                else
                {
                    result.Status = "error";
                    result.Summary = aiResult.Error;
                }
            }
            catch (Exception ex)
            {
                result.Status = "error";
                result.Summary = $"Exception: {ex.Message}";
            }

            return result;
        }
    }
}
