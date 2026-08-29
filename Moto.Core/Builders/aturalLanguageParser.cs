// Moto.Editor/AI/Builders/NaturalLanguageParser.cs
using System;
using System.Threading.Tasks;

namespace Moto.Editor.AI.Builders
{
    /// <summary>
    /// Contrat pour le client Ollama.
    /// Réutilise l'interface existante de MOTO Editor.
    /// </summary>
    public interface IBuilderOllamaClient
    {
        Task<string> GenerateAsync(string prompt);
    }

    /// <summary>
    /// Analyseur de langage naturel pour les builders.
    /// Transforme une phrase utilisateur en intention structurée.
    ///
    /// Utilise Ollama local pour la compréhension.
    /// Aucun cloud, aucune dépendance externe.
    /// </summary>
    public class NaturalLanguageParser
    {
        private readonly IBuilderOllamaClient _ollama;

        public NaturalLanguageParser(IBuilderOllamaClient ollama)
        {
            _ollama = ollama ?? throw new ArgumentNullException(nameof(ollama));
        }

        /// <summary>
        /// Détecte le type de builder à utiliser.
        /// </summary>
        public async Task<BuilderKind> DetectIntentAsync(string userInput)
        {
            if (string.IsNullOrWhiteSpace(userInput))
            {
                return BuilderKind.FixAll;
            }

            var prompt =
                "Tu es un classifieur d'intentions pour un éditeur de code.\n" +
                "Réponds avec UN SEUL mot parmi : blueprint, module, behavior, fix.\n\n" +
                "Règles :\n" +
                "- 'blueprint' si l'utilisateur décrit un projet complet\n" +
                "- 'module' si l'utilisateur veut ajouter un système/module précis\n" +
                "- 'behavior' si l'utilisateur décrit un comportement d'entité\n" +
                "- 'fix' si l'utilisateur veut réparer/corriger\n\n" +
                $"Phrase : \"{userInput}\"\n" +
                "Réponse (un seul mot) :";

            var answer = await _ollama.GenerateAsync(prompt);
            var cleaned = answer.Trim().ToLowerInvariant();

            if (cleaned.Contains("blueprint")) return BuilderKind.Blueprint;
            if (cleaned.Contains("module")) return BuilderKind.Module;
            if (cleaned.Contains("behavior")) return BuilderKind.Behavior;
            if (cleaned.Contains("fix")) return BuilderKind.FixAll;

            // Fallback : heuristique simple.
            var lower = userInput.ToLowerInvariant();
            if (lower.Contains("répare") || lower.Contains("corrige") || lower.Contains("fix"))
                return BuilderKind.FixAll;
            if (lower.Contains("système") || lower.Contains("module") || lower.Contains("sante"))
                return BuilderKind.Module;
            if (lower.Contains("suis") || lower.Contains("suive") || lower.Contains("attaque"))
                return BuilderKind.Behavior;

            return BuilderKind.Blueprint;
        }

        /// <summary>
        /// Extrait un descripteur de module depuis une phrase.
        /// </summary>
        public async Task<ModuleDescriptor> ParseModuleAsync(string userInput)
        {
            var prompt =
                "Tu es un extracteur de spécifications pour un moteur ECS.\n" +
                "Réponds UNIQUEMENT en JSON valide, sans markdown.\n\n" +
                "Format attendu :\n" +
                "{\n" +
                "  \"name\": \"NomDuModule\",\n" +
                "  \"description\": \"description courte\",\n" +
                "  \"properties\": [\"Type Nom\", \"Type Nom\"],\n" +
                "  \"methods\": [\"NomMethode\", \"NomMethode\"],\n" +
                "  \"dependencies\": [\"AutreModule\"]\n" +
                "}\n\n" +
                $"Phrase : \"{userInput}\"\n" +
                "JSON :";

            var answer = await _ollama.GenerateAsync(prompt);
            return ParseModuleJson(answer);
        }

        /// <summary>
        /// Extrait un descripteur de comportement depuis une phrase.
        /// </summary>
        public async Task<BehaviorDescriptor> ParseBehaviorAsync(string userInput)
        {
            var prompt =
                "Tu es un extracteur de spécifications pour un moteur ECS.\n" +
                "Réponds UNIQUEMENT en JSON valide, sans markdown.\n\n" +
                "Format attendu :\n" +
                "{\n" +
                "  \"subject\": \"Entite\",\n" +
                "  \"action\": \"Action\",\n" +
                "  \"target\": \"Cible\",\n" +
                "  \"parameters\": {\"cle\": \"valeur\"}\n" +
                "}\n\n" +
                $"Phrase : \"{userInput}\"\n" +
                "JSON :";

            var answer = await _ollama.GenerateAsync(prompt);
            return ParseBehaviorJson(answer);
        }

        /// <summary>
        /// Extrait un descripteur de projet complet depuis une phrase.
        /// </summary>
        public async Task<BlueprintDescriptor> ParseBlueprintAsync(string userInput)
        {
            var prompt =
                "Tu es un architecte logiciel qui conçoit des projets.\n" +
                "Réponds UNIQUEMENT en JSON valide, sans markdown.\n\n" +
                "Format attendu :\n" +
                "{\n" +
                "  \"projectName\": \"NomProjet\",\n" +
                "  \"projectType\": \"game|app|library\",\n" +
                "  \"description\": \"description\",\n" +
                "  \"modules\": [\n" +
                "    {\n" +
                "      \"name\": \"NomModule\",\n" +
                "      \"description\": \"description\",\n" +
                "      \"properties\": [\"Type Nom\"],\n" +
                "      \"methods\": [\"NomMethode\"]\n" +
                "    }\n" +
                "  ],\n" +
                "  \"behaviors\": [\n" +
                "    {\n" +
                "      \"subject\": \"Entite\",\n" +
                "      \"action\": \"Action\",\n" +
                "      \"target\": \"Cible\"\n" +
                "    }\n" +
                "  ]\n" +
                "}\n\n" +
                $"Phrase : \"{userInput}\"\n" +
                "JSON :";

            var answer = await _ollama.GenerateAsync(prompt);
            return ParseBlueprintJson(answer);
        }

        // --- Parsing JSON simple sans dépendance externe ---

        private ModuleDescriptor ParseModuleJson(string json)
        {
            var module = new ModuleDescriptor();

            try
            {
                module.Name = ExtractJsonString(json, "name") ?? "NewModule";
                module.Description = ExtractJsonString(json, "description") ?? "";

                var props = ExtractJsonArray(json, "properties");
                foreach (var prop in props)
                {
                    module.ComponentProperties.Add(prop);
                }

                var methods = ExtractJsonArray(json, "methods");
                foreach (var method in methods)
                {
                    module.SystemMethods.Add(method);
                }

                var deps = ExtractJsonArray(json, "dependencies");
                foreach (var dep in deps)
                {
                    module.Dependencies.Add(dep);
                }
            }
            catch
            {
                module.Name = "NewModule";
                module.Description = "Module généré automatiquement.";
            }

            return module;
        }

        private BehaviorDescriptor ParseBehaviorJson(string json)
        {
            var behavior = new BehaviorDescriptor();

            try
            {
                behavior.Subject = ExtractJsonString(json, "subject") ?? "Entity";
                behavior.Action = ExtractJsonString(json, "action") ?? "Update";
                behavior.Target = ExtractJsonString(json, "target") ?? "Player";
            }
            catch
            {
                behavior.Subject = "Entity";
                behavior.Action = "Update";
                behavior.Target = "Player";
            }

            return behavior;
        }

        private BlueprintDescriptor ParseBlueprintJson(string json)
        {
            var blueprint = new BlueprintDescriptor();

            try
            {
                blueprint.ProjectName = ExtractJsonString(json, "projectName") ?? "MyProject";
                blueprint.ProjectType = ExtractJsonString(json, "projectType") ?? "game";
                blueprint.Description = ExtractJsonString(json, "description") ?? "";
            }
            catch
            {
                blueprint.ProjectName = "MyProject";
                blueprint.ProjectType = "game";
                blueprint.Description = "Projet généré automatiquement.";
            }

            return blueprint;
        }

        /// <summary>
        /// Extraction simple d'une valeur string JSON.
        /// Pas de dépendance System.Text.Json pour rester ultra-léger.
        /// </summary>
        private static string ExtractJsonString(string json, string key)
        {
            var pattern = $"\"{key}\"\\s*:\\s*\"([^\"]*)\"";
            var match = System.Text.RegularExpressions.Regex.Match(json, pattern);
            return match.Success ? match.Groups[1].Value : null;
        }

        /// <summary>
        /// Extraction simple d'un tableau JSON de strings.
        /// </summary>
        private static string[] ExtractJsonArray(string json, string key)
        {
            var pattern = $"\"{key}\"\\s*:\\s*\\[([^\\]]*)\\]";
            var match = System.Text.RegularExpressions.Regex.Match(json, pattern);

            if (!match.Success)
            {
                return Array.Empty<string>();
            }

            var content = match.Groups[1].Value;
            var items = System.Text.RegularExpressions.Regex.Matches(content, "\"([^\"]*)\"");

            var result = new string[items.Count];
            for (int i = 0; i < items.Count; i++)
            {
                result[i] = items[i].Groups[1].Value;
            }

            return result;
        }
    }
}
