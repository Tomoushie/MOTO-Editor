// Moto.Editor/AI/Beginner/BeginnerAssistant.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Moto.Editor.AI.Beginner
{
    /// <summary>
    /// Actions disponibles pour les utilisateurs débutants.
    /// </summary>
    public enum BeginnerAction
    {
        ExplainCode,
        FixFile,
        MakeBetter,
        GenerateMissingFiles,
        ExplainErrors,
        Teach
    }

    /// <summary>
    /// Requête envoyée par MOTO Editor au BeginnerAssistant.
    /// </summary>
    public class BeginnerRequest
    {
        public BeginnerAction Action { get; set; }

        /// <summary>
        /// Workspace ouvert dans MOTO Editor.
        /// </summary>
        public string WorkspacePath { get; set; } = string.Empty;

        /// <summary>
        /// Fichier actif.
        /// </summary>
        public string FilePath { get; set; } = string.Empty;

        /// <summary>
        /// Contenu du fichier actif.
        /// </summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// Erreurs de compilation ou diagnostics locaux.
        /// </summary>
        public string CompilerErrors { get; set; } = string.Empty;

        /// <summary>
        /// Sujet demandé dans le mode apprentissage.
        /// Exemple : classe, interface, namespace, système, pipeline.
        /// </summary>
        public string Topic { get; set; } = string.Empty;
    }

    /// <summary>
    /// Patch proposé pour un fichier.
    /// Toujours affiché à l'utilisateur avant application.
    /// </summary>
    public class FilePatch
    {
        public string FilePath { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public string OriginalContent { get; set; } = string.Empty;
        public string ProposedContent { get; set; } = string.Empty;
    }

    /// <summary>
    /// Résultat produit par BeginnerAssistant.
    /// </summary>
    public class BeginnerResult
    {
        public bool Success { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Explanation { get; set; } = string.Empty;

        /// <summary>
        /// True si l'action propose une modification de fichier.
        /// L'UI doit demander confirmation avant d'appliquer.
        /// </summary>
        public bool RequiresUserConfirmation { get; set; }

        public List<string> Suggestions { get; } = new List<string>();
        public List<FilePatch> Patches { get; } = new List<FilePatch>();
    }

    /// <summary>
    /// Requête envoyée à XENO-SSS∞.
    /// </summary>
    public class XenoTaskRequest
    {
        public string WorkspacePath { get; set; } = string.Empty;

        /// <summary>
        /// Exemples :
        /// - fix-file
        /// - improve-file
        /// - generate-missing-files
        /// - validate-fix
        /// </summary>
        public string Task { get; set; } = string.Empty;

        /// <summary>
        /// Paramètre additionnel, souvent un chemin de fichier.
        /// </summary>
        public string Parameter { get; set; } = string.Empty;

        /// <summary>
        /// Contenu utile pour éviter à XENO de relire le fichier si MOTO l'a déjà.
        /// </summary>
        public string Content { get; set; } = string.Empty;
    }

    /// <summary>
    /// Réponse renvoyée par XENO-SSS∞ via un bridge.
    /// </summary>
    public class XenoTaskResult
    {
        public bool Success { get; set; }
        public string Summary { get; set; } = string.Empty;

        public List<string> Details { get; } = new List<string>();
        public List<FilePatch> Patches { get; } = new List<FilePatch>();
    }

    /// <summary>
    /// Client local Ollama.
    /// MOTO Editor peut implémenter cette interface avec HttpClient.
    /// </summary>
    public interface IOllamaClient
    {
        Task<string> GenerateAsync(string prompt);
    }

    /// <summary>
    /// Bridge vers XENO-SSS∞.
    /// Important : MOTO Editor ne doit pas exécuter lui-même
    /// les analyses lourdes, générations ou validations.
    /// </summary>
    public interface IXenoBridge
    {
        Task<XenoTaskResult> ExecuteAsync(XenoTaskRequest request);
    }

    /// <summary>
    /// Assistant débutant de MOTO Editor.
    /// Il transforme des actions simples en requêtes locales
    /// vers MOTO AI ou XENO-SSS∞.
    /// </summary>
    public class BeginnerAssistant
    {
        private readonly IOllamaClient _ollama;
        private readonly IXenoBridge _xeno;

        public BeginnerAssistant(IOllamaClient ollama, IXenoBridge xeno)
        {
            _ollama = ollama;
            _xeno = xeno;
        }

        /// <summary>
        /// Exécute une action débutant.
        /// </summary>
        public Task<BeginnerResult> ExecuteAsync(BeginnerRequest request)
        {
            if (request == null)
            {
                return Task.FromResult(new BeginnerResult
                {
                    Success = false,
                    Title = "Erreur",
                    Explanation = "Aucune demande fournie."
                });
            }

            switch (request.Action)
            {
                case BeginnerAction.ExplainCode:
                    return ExplainCodeAsync(request.FilePath, request.Content);

                case BeginnerAction.FixFile:
                    return FixFileAsync(request.WorkspacePath, request.FilePath, request.Content);

                case BeginnerAction.MakeBetter:
                    return MakeBetterAsync(request.WorkspacePath, request.FilePath, request.Content);

                case BeginnerAction.GenerateMissingFiles:
                    return GenerateMissingFilesAsync(request.WorkspacePath, request.FilePath, request.Content);

                case BeginnerAction.ExplainErrors:
                    return ExplainErrorsAsync(request.FilePath, request.Content, request.CompilerErrors);

                case BeginnerAction.Teach:
                    return Task.FromResult(TeachModeEngine.GetLesson(request.Topic));

                default:
                    return Task.FromResult(new BeginnerResult
                    {
                        Success = false,
                        Title = "Action inconnue",
                        Explanation = "Cette action n'est pas encore prise en charge."
                    });
            }
        }

        /// <summary>
        /// 1. Explain This Code.
        /// Lecture seule, aucun changement de fichier.
        /// </summary>
        private async Task<BeginnerResult> ExplainCodeAsync(string filePath, string content)
        {
            var prompt = BeginnerPromptFactory.ExplainCode(filePath, content);
            var answer = await _ollama.GenerateAsync(prompt);

            return new BeginnerResult
            {
                Success = true,
                Title = "Explication du code",
                Explanation = answer,
                RequiresUserConfirmation = false
            };
        }

        /// <summary>
        /// 2. Fix This File.
        /// Action d'écriture, déléguée à XENO-SSS∞.
        /// </summary>
        private async Task<BeginnerResult> FixFileAsync(
            string workspacePath,
            string filePath,
            string content)
        {
            var xenoResult = await _xeno.ExecuteAsync(new XenoTaskRequest
            {
                WorkspacePath = workspacePath,
                Task = "fix-file",
                Parameter = filePath,
                Content = content
            });

            return FromXenoResult("Réparation du fichier", xenoResult);
        }

        /// <summary>
        /// 3. Make This Better.
        /// Refactor léger, sans changement de comportement.
        /// </summary>
        private async Task<BeginnerResult> MakeBetterAsync(
            string workspacePath,
            string filePath,
            string content)
        {
            var xenoResult = await _xeno.ExecuteAsync(new XenoTaskRequest
            {
                WorkspacePath = workspacePath,
                Task = "improve-file",
                Parameter = filePath,
                Content = content
            });

            if (xenoResult.Success)
            {
                return FromXenoResult("Amélioration du fichier", xenoResult);
            }

            // Si XENO ne peut pas produire un patch fiable,
            // on retombe sur une explication locale via Ollama.
            var prompt = BeginnerPromptFactory.MakeBetter(filePath, content);
            var answer = await _ollama.GenerateAsync(prompt);

            return new BeginnerResult
            {
                Success = true,
                Title = "Améliorations proposées",
                Explanation = answer,
                RequiresUserConfirmation = false
            };
        }

        /// <summary>
        /// 4. Generate Missing Files.
        /// Génération structurelle déléguée à XENO-SSS∞.
        /// </summary>
        private async Task<BeginnerResult> GenerateMissingFilesAsync(
            string workspacePath,
            string filePath,
            string content)
        {
            var xenoResult = await _xeno.ExecuteAsync(new XenoTaskRequest
            {
                WorkspacePath = workspacePath,
                Task = "generate-missing-files",
                Parameter = filePath,
                Content = content
            });

            return FromXenoResult("Fichiers manquants générés", xenoResult);
        }

        /// <summary>
        /// 5. Explain Errors.
        /// Lecture seule, pédagogie locale.
        /// </summary>
        private async Task<BeginnerResult> ExplainErrorsAsync(
            string filePath,
            string content,
            string compilerErrors)
        {
            var prompt = BeginnerPromptFactory.ExplainErrors(filePath, content, compilerErrors);
            var answer = await _ollama.GenerateAsync(prompt);

            return new BeginnerResult
            {
                Success = true,
                Title = "Explication des erreurs",
                Explanation = answer,
                RequiresUserConfirmation = false
            };
        }

        /// <summary>
        /// Convertit une réponse XENO en résultat exploitable par l'UI.
        /// </summary>
        private BeginnerResult FromXenoResult(string title, XenoTaskResult xenoResult)
        {
            var result = new BeginnerResult
            {
                Success = xenoResult.Success,
                Title = title,
                Explanation = xenoResult.Summary,
                RequiresUserConfirmation = xenoResult.Patches.Count > 0
            };

            result.Suggestions.AddRange(xenoResult.Details);
            result.Patches.AddRange(xenoResult.Patches);

            if (!xenoResult.Success)
            {
                result.Explanation =
                    "XENO-SSS∞ n'a pas pu terminer l'opération proprement. " +
                    "Vérifie les détails, puis réessaie avec un fichier plus simple ou un workspace valide.";
            }

            return result;
        }
    }

    /// <summary>
    /// Fabrique de prompts pour les actions débutants.
    /// Les prompts sont volontairement simples et pédagogiques.
    /// </summary>
    public static class BeginnerPromptFactory
    {
        private const int MaxCodeChars = 12000;

        public static string ExplainCode(string filePath, string code)
        {
            return
                "Tu es MOTO AI, un assistant pédagogique local.\n" +
                "Explique le code suivant comme si l'utilisateur avait 12 ans.\n" +
                "Réponds en français simple, sans jargon inutile.\n" +
                "Découpe l'explication en petites parties claires.\n" +
                $"Fichier : {filePath}\n\n" +
                "Code :\n" +
                Truncate(code);
        }

        public static string MakeBetter(string filePath, string code)
        {
            return
                "Tu es MOTO AI, un assistant local de refactoring léger.\n" +
                "Propose des améliorations simples sans changer le comportement.\n" +
                "Réponds en français simple.\n" +
                "Donne uniquement les modifications utiles et sûres.\n" +
                $"Fichier : {filePath}\n\n" +
                "Code :\n" +
                Truncate(code);
        }

        public static string ExplainErrors(string filePath, string code, string errors)
        {
            return
                "Tu es MOTO AI, un assistant pédagogique local.\n" +
                "Explique pourquoi ce code ne compile pas ou produit des erreurs.\n" +
                "Réponds en français simple, sans jargon inutile.\n" +
                "Explique d'abord la cause probable, puis la correction possible.\n" +
                $"Fichier : {filePath}\n\n" +
                "Erreurs :\n" +
                Truncate(errors) +
                "\n\nCode :\n" +
                Truncate(code);
        }

        private static string Truncate(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return "(vide)";
            }

            if (text.Length <= MaxCodeChars)
            {
                return text;
            }

            return text.Substring(0, MaxCodeChars) + "\n... (contenu tronqué)";
        }
    }

    /// <summary>
    /// Mode apprentissage local.
    /// Fournit des explications simples sans dépendance externe.
    /// </summary>
    public static class TeachModeEngine
    {
        private static readonly Dictionary<string, (string Title, string Body)> Lessons =
            new Dictionary<string, (string Title, string Body)>(StringComparer.OrdinalIgnoreCase)
            {
                ["classe"] = (
                    "Classe",
                    "Une classe est comme un plan de construction. " +
                    "Elle décrit quelles données et quelles actions un objet pourra avoir. " +
                    "Par exemple, une classe 'Voiture' peut contenir une couleur, une vitesse, " +
                    "et une action 'Démarrer'."
                ),

                ["interface"] = (
                    "Interface",
                    "Une interface est un contrat. " +
                    "Elle dit : 'si tu veux être considéré comme ceci, tu dois fournir ces actions'. " +
                    "Par exemple, une interface 'IDemarable' peut obliger une classe à avoir une méthode 'Demarrer'."
                ),

                ["namespace"] = (
                    "Namespace",
                    "Un namespace est une boîte de rangement. " +
                    "Il permet de ranger les classes pour éviter les confusions. " +
                    "Par exemple, 'Moto.Editor.UI' contient les classes liées à l'interface de MOTO Editor."
                ),

                ["systeme"] = (
                    "Système",
                    "Un système est un module qui fait une chose précise. " +
                    "Par exemple, un système de rendu dessine l'image, " +
                    "un système de physique gère les collisions, " +
                    "et un système de validation vérifie que le projet est cohérent."
                ),

                ["pipeline"] = (
                    "Pipeline",
                    "Un pipeline est une suite d'étapes. " +
                    "Chaque étape reçoit un travail, fait une partie de la tâche, " +
                    "puis passe le résultat à l'étape suivante. " +
                    "Dans XENO-SSS∞, le pipeline est : Scanner, Analyzer, Synthesizer, Connector, Validator."
                )
            };

        /// <summary>
        /// Retourne une leçon simple pour un sujet donné.
        /// </summary>
        public static BeginnerResult GetLesson(string topic)
        {
            if (string.IsNullOrWhiteSpace(topic))
            {
                return GetLessonMenu();
            }

            if (Lessons.TryGetValue(topic.Trim(), out var lesson))
            {
                return new BeginnerResult
                {
                    Success = true,
                    Title = lesson.Title,
                    Explanation = lesson.Body,
                    RequiresUserConfirmation = false
                };
            }

            return GetLessonMenu();
        }

        private static BeginnerResult GetLessonMenu()
        {
            var result = new BeginnerResult
            {
                Success = true,
                Title = "Mode apprentissage",
                Explanation =
                    "Choisis un sujet simple à apprendre.\n" +
                    "Tu peux demander : classe, interface, namespace, système ou pipeline.",
                RequiresUserConfirmation = false
            };

            result.Suggestions.Add("classe");
            result.Suggestions.Add("interface");
            result.Suggestions.Add("namespace");
            result.Suggestions.Add("système");
            result.Suggestions.Add("pipeline");

            return result;
        }
    }
}
