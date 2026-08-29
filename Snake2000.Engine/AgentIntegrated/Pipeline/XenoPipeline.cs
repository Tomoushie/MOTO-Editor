using System;
using System.Collections.Generic;

using Snake2000.Engine.AgentIntegrated.Core;
using Snake2000.Engine.AgentIntegrated.Scanner;
using Snake2000.Engine.AgentIntegrated.Analyzer;
using Snake2000.Engine.AgentIntegrated.Synthesizer;
using Snake2000.Engine.AgentIntegrated.Connector;
using Snake2000.Engine.AgentIntegrated.Validator;

namespace Snake2000.Engine.AgentIntegrated.Pipeline
{
    /// <summary>
    /// XENO-SSS∞ : orchestrateur du pipeline Scanner → Analyzer → Synthesizer → Connector → Validator.
    ///
    /// Objectifs :
    /// - exécuter les agents dans le bon ordre ;
    /// - isoler les erreurs par étape ;
    /// - produire un rapport structuré ;
    /// - journaliser les résultats ;
    /// - permettre des extensions futures sans casser la signature publique Run().
    /// </summary>
    public class XenoPipeline
    {
        // ============================================================
        // Agents existants
        // ============================================================
        // On conserve l'instanciation des modules déjà présents dans l'architecture.
        private readonly AgentScanner _scanner = new AgentScanner();
        private readonly AgentAnalyzer _analyzer = new AgentAnalyzer();
        private readonly AgentSynthesizer _synthesizer = new AgentSynthesizer();
        private readonly AgentConnector _connector = new AgentConnector();
        private readonly AgentValidator _validator = new AgentValidator();

        // ============================================================
        // Extension future
        // ============================================================
        // Liste d'extensions exécutées après le pipeline.
        // Cela permet d'ajouter un agent de reporting, de commit, de nettoyage,
        // de métriques ou de supervision sans modifier la méthode Run().
        private readonly List<Action<AgentContext, PipelineReport>> _extensions =
            new List<Action<AgentContext, PipelineReport>>();

        // ============================================================
        // Événements d'observabilité
        // ============================================================

        /// <summary>
        /// Logger externe optionnel.
        /// Si aucun abonné n'est présent, le pipeline écrit sur Console.
        /// </summary>
        public event Action<string> PipelineLogged;

        /// <summary>
        /// Déclenché avant chaque étape.
        /// Utile pour UI, métriques, tracing ou debug.
        /// </summary>
        public event Action<string> StepStarted;

        /// <summary>
        /// Déclenché après chaque étape, succès ou échec.
        /// </summary>
        public event Action<AgentResult> StepCompleted;

        /// <summary>
        /// Déclenché lorsqu'une étape lève une exception.
        /// </summary>
        public event Action<string, Exception> StepFailed;

        // ============================================================
        // Rapport public
        // ============================================================

        /// <summary>
        /// Rapport structuré du dernier pipeline exécuté.
        /// Peut être consommé par une UI, un superviseur IA ou des tests.
        /// </summary>
        public PipelineReport LastReport { get; private set; } = new PipelineReport();

        /// <summary>
        /// Si vrai, toute erreur bloque la suite du pipeline.
        /// Recommandé pour un pipeline de génération/intégration de code.
        /// </summary>
        public bool StopOnStepError { get; set; } = true;

        // ============================================================
        // Signature publique existante
        // ============================================================

        /// <summary>
        /// Exécute le pipeline complet.
        /// La signature publique existante est volontairement conservée.
        /// </summary>
        public void Run(AgentContext context)
        {
            // Initialisation du rapport d'exécution.
            LastReport = new PipelineReport
            {
                Status = "running",
                Summary = "Pipeline started.",
                StartedAtUtc = DateTime.UtcNow
            };

            Log("[Pipeline] XENO-SSS∞ started.");

            AgentResult scan = null;
            AgentResult analysis = null;
            AgentResult synth = null;
            AgentResult connect = null;
            AgentResult validate = null;

            try
            {
                // --------------------------------------------------------
                // 1) SCANNER
                // Étape critique : sans ProjectMap, aucune analyse fiable.
                // --------------------------------------------------------
                scan = ExecuteStep(
                    stepName: "Scanner",
                    step: () => _scanner.ScanProject(context),
                    isMandatory: true
                );

                if (scan == null)
                {
                    FinalizeReport("error", "Pipeline stopped: Scanner returned null.");
                    return;
                }

                // --------------------------------------------------------
                // 2) ANALYZER
                // Étape critique : dépend directement du Scanner.
                // --------------------------------------------------------
                analysis = ExecuteStep(
                    stepName: "Analyzer",
                    step: () => _analyzer.Analyze(context, scan),
                    isMandatory: true
                );

                if (analysis == null)
                {
                    FinalizeReport("error", "Pipeline stopped: Analyzer returned null.");
                    return;
                }

                // --------------------------------------------------------
                // 3) SYNTHESIZER
                // Produit les fichiers générés.
                // S'il échoue, Connector ne peut pas travailler proprement.
                // --------------------------------------------------------
                synth = ExecuteStep(
                    stepName: "Synthesizer",
                    step: () => _synthesizer.Synthesize(context, analysis),
                    isMandatory: false
                );

                if (synth == null || synth.Status == "error")
                {
                    FinalizeReport(
                        "warning",
                        "Synthesizer failed or produced no usable result. Connector and Validator skipped."
                    );
                    return;
                }

                // --------------------------------------------------------
                // 4) CONNECTOR
                // Prépare l'intégration des fichiers générés.
                // --------------------------------------------------------
                connect = ExecuteStep(
                    stepName: "Connector",
                    step: () => _connector.Connect(context, synth),
                    isMandatory: false
                );

                if (connect == null || connect.Status == "error")
                {
                    FinalizeReport(
                        "warning",
                        "Connector failed or produced no usable integration plan. Validator skipped."
                    );
                    return;
                }

                // --------------------------------------------------------
                // 5) VALIDATOR
                // Contrôle final de cohérence.
                // --------------------------------------------------------
                validate = ExecuteStep(
                    stepName: "Validator",
                    step: () => _validator.Validate(context, connect),
                    isMandatory: false
                );

                if (validate == null)
                {
                    FinalizeReport("error", "Validator returned null.");
                    return;
                }

                // Le statut final dépend du résultat du Validator.
                var finalStatus = validate.Status == "error"
                    ? "error"
                    : validate.Status == "warning"
                        ? "warning"
                        : "success";

                FinalizeReport(finalStatus, validate.Summary ?? "Pipeline completed.");
                LogSummary();
            }
            catch (PipelineStepException ex)
            {
                // Erreur fonctionnelle identifiée dans une étape du pipeline.
                FinalizeReport(
                    "error",
                    $"Pipeline stopped during '{ex.StepName}': {ex.Message}"
                );
            }
            catch (Exception ex)
            {
                // Erreur fatale non anticipée.
                FinalizeReport(
                    "error",
                    $"Pipeline fatal error: {ex.Message}"
                );

                Log($"[Pipeline] Exception details: {ex}");
            }
            finally
            {
                // Clôture du rapport, même en cas d'erreur.
                LastReport.FinishedAtUtc = DateTime.UtcNow;

                // Exécution des extensions futures.
                ExecuteExtensions(context);

                Log("[Pipeline] XENO-SSS∞ finished.");
            }
        }

        // ============================================================
        // Extension future
        // ============================================================

        /// <summary>
        /// Enregistre une extension exécutée après le pipeline.
        /// Exemple : agent de commit Git, agent de metrics, agent de nettoyage,
        /// agent de notification ou agent IA supplémentaire.
        /// </summary>
        public void RegisterExtension(Action<AgentContext, PipelineReport> extension)
        {
            if (extension != null)
            {
                _extensions.Add(extension);
            }
        }

        // ============================================================
        // Exécution isolée d'une étape
        // ============================================================

        /// <summary>
        /// Exécute une étape du pipeline avec :
        /// - logs ;
        /// - capture d'exceptions ;
        /// - enregistrement dans LastReport ;
        /// - événements d'observabilité.
        /// </summary>
        private AgentResult ExecuteStep(
            string stepName,
            Func<AgentResult> step,
            bool isMandatory)
        {
            LastReport.CurrentStep = stepName;
            Log($"[Step] {stepName} started.");

            try
            {
                StepStarted?.Invoke(stepName);

                var result = step();

                // Normalisation : un agent ne doit jamais retourner null.
                if (result == null)
                {
                    result = new AgentResult
                    {
                        ModuleName = stepName,
                        Status = "error",
                        Summary = "Agent returned null."
                    };
                }

                RecordStep(result);
                LogAgentResult(result);
                StepCompleted?.Invoke(result);

                // Si une étape obligatoire échoue, on stoppe le pipeline
                // afin de ne pas produire un état projet incohérent.
                if (result.Status == "error" && (isMandatory || StopOnStepError))
                {
                    throw new PipelineStepException(
                        stepName,
                        result.Summary ?? "Unknown error."
                    );
                }

                return result;
            }
            catch (PipelineStepException)
            {
                // Déjà géré au niveau pipeline.
                throw;
            }
            catch (Exception ex)
            {
                StepFailed?.Invoke(stepName, ex);
                Log($"[Error] {stepName} threw an exception: {ex.Message}");

                var failed = new AgentResult
                {
                    ModuleName = stepName,
                    Status = "error",
                    Summary = ex.Message
                };

                RecordStep(failed);
                LogAgentResult(failed);
                StepCompleted?.Invoke(failed);

                if (isMandatory || StopOnStepError)
                {
                    throw new PipelineStepException(stepName, ex.Message, ex);
                }

                return failed;
            }
        }

        // ============================================================
        // Rapport et journalisation
        // ============================================================

        /// <summary>
        /// Ajoute le résultat d'une étape dans le rapport structuré.
        /// </summary>
        private void RecordStep(AgentResult result)
        {
            LastReport.Steps.Add(new PipelineStepResult
            {
                ModuleName = result.ModuleName ?? "Unknown",
                Status = result.Status ?? "unknown",
                Summary = result.Summary ?? string.Empty,
                ExecutedAtUtc = DateTime.UtcNow
            });
        }

        /// <summary>
        /// Journalise le résultat d'un agent, y compris ses détails.
        /// </summary>
        private void LogAgentResult(AgentResult result)
        {
            var moduleName = result.ModuleName ?? "Unknown";
            var status = result.Status ?? "unknown";
            var summary = result.Summary ?? "No summary.";

            Log($"[{moduleName}] {status}: {summary}");

            if (result.Details == null)
            {
                return;
            }

            foreach (var detail in result.Details)
            {
                Log($"[{moduleName}] - {detail}");
            }
        }

        /// <summary>
        /// Journalise une synthèse rapide du pipeline.
        /// </summary>
        private void LogSummary()
        {
            Log("[Summary] Pipeline execution summary:");

            foreach (var step in LastReport.Steps)
            {
                Log($"[Summary] {step.ModuleName} => {step.Status} | {step.Summary}");
            }
        }

        /// <summary>
        /// Finalise le rapport avec un statut et un résumé clairs.
        /// </summary>
        private void FinalizeReport(string status, string summary)
        {
            LastReport.Status = status;
            LastReport.Summary = summary;

            Log($"[Pipeline] {summary}");
        }

        /// <summary>
        /// Exécute les extensions enregistrées.
        /// Une extension ne doit jamais faire échouer le pipeline principal.
        /// </summary>
        private void ExecuteExtensions(AgentContext context)
        {
            foreach (var extension in _extensions)
            {
                try
                {
                    extension(context, LastReport);
                }
                catch (Exception ex)
                {
                    Log($"[Extension] Error: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Logger central.
        /// Priorité aux abonnés externes ; sinon, sortie console.
        /// </summary>
        private void Log(string message)
        {
            var line = $"[{DateTime.UtcNow:O}] {message}";

            if (PipelineLogged != null)
            {
                PipelineLogged(line);
            }
            else
            {
                Console.WriteLine(line);
            }
        }
    }

    /// <summary>
    /// Exception dédiée aux erreurs d'étape du pipeline.
    /// Permet d'identifier immédiatement l'agent fautif.
    /// </summary>
    public sealed class PipelineStepException : Exception
    {
        public string StepName { get; }

        public PipelineStepException(string stepName, string message)
            : base(message)
        {
            StepName = stepName;
        }

        public PipelineStepException(
            string stepName,
            string message,
            Exception innerException)
            : base(message, innerException)
        {
            StepName = stepName;
        }
    }

    /// <summary>
    /// Rapport structuré produit par l'orchestrateur.
    /// </summary>
    public sealed class PipelineReport
    {
        /// <summary>
        /// Statut global : running, success, warning, error.
        /// </summary>
        public string Status { get; set; } = "pending";

        /// <summary>
        /// Résumé lisible du résultat global.
        /// </summary>
        public string Summary { get; set; } = string.Empty;

        /// <summary>
        /// Dernière étape en cours d'exécution.
        /// </summary>
        public string CurrentStep { get; set; } = string.Empty;

        /// <summary>
        /// Heure de début UTC.
        /// </summary>
        public DateTime StartedAtUtc { get; set; }

        /// <summary>
        /// Heure de fin UTC.
        /// </summary>
        public DateTime FinishedAtUtc { get; set; }

        /// <summary>
        /// Liste ordonnée des étapes exécutées.
        /// </summary>
        public List<PipelineStepResult> Steps { get; } = new List<PipelineStepResult>();
    }

    /// <summary>
    /// Trace d'une étape du pipeline.
    /// </summary>
    public sealed class PipelineStepResult
    {
        /// <summary>
        /// Nom de l'agent ou du module.
        /// </summary>
        public string ModuleName { get; set; } = string.Empty;

        /// <summary>
        /// Statut de l'étape : success, warning, error, unknown.
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Résumé produit par l'agent.
        /// </summary>
        public string Summary { get; set; } = string.Empty;

        /// <summary>
        /// Heure d'exécution UTC.
        /// </summary>
        public DateTime ExecutedAtUtc { get; set; }
    }
}
