// Moto.Editor/AI/Beginner/BeginnerAssistant.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Moto.Core.Integration;

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
        /// ⚠️ Pour FixFile/MakeBetter/GenerateMissingFiles (routées vers
        /// l'orchestrateur, voir plus bas) : ce champ ne sert qu'aux actions
        /// locales (ExplainCode/ExplainErrors). L'orchestrateur relit le
        /// fichier LUI-MÊME depuis le disque (même machine) — un buffer
        /// modifié mais non enregistré dans MOTO Editor n'est donc pas vu.
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
    /// Client local Ollama.
    /// MOTO Editor peut implémenter cette interface avec HttpClient.
    /// </summary>
    public interface IOllamaClient
    {
        Task<string> GenerateAsync(string prompt);
    }

    /// <summary>
    /// Assistant débutant de MOTO Editor.
    /// Il transforme des actions simples en requêtes locales (Ollama)
    /// ou en appels réels à l'orchestrateur XENO-SSS∞.
    ///
    /// ★ CORRECTION (08/09, Tom) : le pont interne (`IXenoBridge`, jamais
    /// implémenté ni instancié nulle part — voir [[moto-editor-xeno-bridge-usage]])
    /// est remplacé par `IOrchestratorClient` (Moto.Core/Integration), le
    /// client HTTP réel vérifié contre l'orchestrateur en direct. Deux
    /// canaux, comme demandé :
    /// - `/chat` pour les suggestions textuelles (repli de MakeBetter).
    /// - `/generate-batch` avec `ecrire:false` pour les patchs de code
    ///   (FixFile, MakeBetter, GenerateMissingFiles) : le code proposé part
    ///   en brouillon côté orchestrateur, JAMAIS sur le vrai fichier — la
    ///   confirmation + l'écriture réelle restent du ressort de MOTO Editor
    ///   (voir Pages/BeginnerAssistantPage).
    /// </summary>
    public class BeginnerAssistant
    {
        private readonly IOllamaClient _ollama;
        private readonly IOrchestratorClient _orchestrator;

        public BeginnerAssistant(IOllamaClient ollama, IOrchestratorClient orchestrator)
        {
            _ollama = ollama ?? throw new ArgumentNullException(nameof(ollama));
            _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
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
                    return FixFileAsync(request.FilePath);

                case BeginnerAction.MakeBetter:
                    return MakeBetterAsync(request.FilePath, request.Content);

                case BeginnerAction.GenerateMissingFiles:
                    return GenerateMissingFilesAsync(request.FilePath);

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
        /// Lecture seule, aucun changement de fichier. Reste sur Ollama local
        /// (Tom n'a demandé de remplacer que le canal xeno, pas celui-ci).
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
        /// Patch de code réel via /generate-batch (ecrire:false).
        /// </summary>
        private async Task<BeginnerResult> FixFileAsync(string filePath)
        {
            var patch = await _orchestrator.GenerateWithoutWritingAsync(new OrchestratorGenerateRequest
            {
                FilePath = filePath,
                Instruction = "Corrige les erreurs et problèmes dans ce fichier. Renvoie le fichier complet corrigé.",
                Tache = "code"
            });

            return await FromGenerateResultAsync("Réparation du fichier", filePath, patch);
        }

        /// <summary>
        /// 3. Make This Better.
        /// Refactor léger, sans changement de comportement — d'abord un
        /// patch via /generate-batch ; si l'orchestrateur ne produit rien
        /// de valide, repli sur une suggestion TEXTUELLE via /chat (plus de
        /// repli local Ollama : un seul canal IA pour cet assistant, comme
        /// demandé par Tom le 08/09).
        /// </summary>
        private async Task<BeginnerResult> MakeBetterAsync(string filePath, string content)
        {
            var patch = await _orchestrator.GenerateWithoutWritingAsync(new OrchestratorGenerateRequest
            {
                FilePath = filePath,
                Instruction = "Propose une amélioration légère (refactor) de ce fichier, sans changer son comportement. Renvoie le fichier complet amélioré.",
                Tache = "code"
            });

            if (patch.Success && patch.Valide && !string.IsNullOrWhiteSpace(patch.ProposedContent))
            {
                return await FromGenerateResultAsync("Amélioration du fichier", filePath, patch);
            }

            var chat = await _orchestrator.ChatAsync("raisonnement", BeginnerPromptFactory.MakeBetter(filePath, content));

            return new BeginnerResult
            {
                Success = chat.Success,
                Title = "Améliorations proposées",
                Explanation = chat.Success
                    ? chat.Texte
                    : $"XENO-SSS∞ n'a pas pu proposer de suggestion. {chat.Error}",
                RequiresUserConfirmation = false
            };
        }

        /// <summary>
        /// 4. Generate Missing Files.
        /// ⚠️ /generate-batch travaille sur UN fichier cible, pas sur un
        /// ensemble de fichiers manquants — cette action complète donc le
        /// contenu STRUCTUREL de `filePath` lui-même (classes/méthodes/using
        /// manquants), elle ne crée pas de nouveaux fichiers annexes.
        /// </summary>
        private async Task<BeginnerResult> GenerateMissingFilesAsync(string filePath)
        {
            var patch = await _orchestrator.GenerateWithoutWritingAsync(new OrchestratorGenerateRequest
            {
                FilePath = filePath,
                Instruction = "Complète ce fichier avec le contenu structurel manquant (classes, méthodes, using) nécessaire à sa cohérence. Renvoie le fichier complet.",
                Tache = "code"
            });

            return await FromGenerateResultAsync("Contenu manquant généré", filePath, patch);
        }

        /// <summary>
        /// 5. Explain Errors.
        /// Lecture seule, pédagogie locale (Ollama, inchangé).
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
        /// Convertit un verdict de /generate-batch en résultat exploitable par
        /// l'UI. Relit le fichier ORIGINAL depuis le disque (et non le
        /// paramètre `content` fourni par l'appelant, potentiellement obsolète)
        /// pour que le diff affiché corresponde exactement à ce que
        /// l'orchestrateur a réellement lu.
        /// </summary>
        private async Task<BeginnerResult> FromGenerateResultAsync(string title, string filePath, OrchestratorGenerateResult result)
        {
            if (!result.Success)
            {
                return new BeginnerResult
                {
                    Success = false,
                    Title = title,
                    Explanation = "XENO-SSS∞ n'a pas pu terminer l'opération proprement. " +
                        "Vérifie les détails, puis réessaie avec un fichier plus simple ou un workspace valide. " +
                        (result.Error ?? string.Empty)
                };
            }

            if (!result.Valide || string.IsNullOrWhiteSpace(result.ProposedContent))
            {
                return new BeginnerResult
                {
                    Success = false,
                    Title = title,
                    Explanation = $"XENO-SSS∞ a répondu mais le résultat n'a pas été jugé valide : {result.Raison}. " +
                        "Réessaie avec un fichier plus simple ou une instruction plus précise."
                };
            }

            var originalContent = string.Empty;
            try
            {
                if (File.Exists(filePath))
                {
                    originalContent = await File.ReadAllTextAsync(filePath);
                }
            }
            catch
            {
                // Lecture best-effort : un "avant" vide n'empêche pas d'afficher
                // le patch proposé, il rend juste le diff moins lisible.
            }

            var beginnerResult = new BeginnerResult
            {
                Success = true,
                Title = title,
                Explanation = $"Patch proposé par {result.ModeleUtilise} — jamais écrit sans ta confirmation.",
                RequiresUserConfirmation = true
            };

            beginnerResult.Patches.Add(new FilePatch
            {
                FilePath = filePath,
                Reason = title,
                OriginalContent = originalContent,
                ProposedContent = result.ProposedContent!
            });

            return beginnerResult;
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
