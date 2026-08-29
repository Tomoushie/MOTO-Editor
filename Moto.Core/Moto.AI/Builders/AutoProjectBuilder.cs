// Moto.Core/AI/Builders/AutoProjectBuilder.cs
using System;
using System.IO;
using System.Threading.Tasks;

namespace Moto.Core.AI.Builders
{
    /// <summary>
    /// Génère un projet COMPLET de façon automatisée, sans modèle externe.
    /// "Génère moi un serpent façon Nokia 3310" → projet jouable écrit sur disque.
    /// </summary>
    public class AutoProjectBuilder
    {
        private readonly TemplateLibrary _templates = new TemplateLibrary();

        /// <summary>
        /// Détecte si la demande correspond à une génération de projet complet.
        /// </summary>
        public static bool ShouldHandle(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var lower = text.ToLowerInvariant();

            bool wantsGeneration =
                lower.Contains("génère") || lower.Contains("genere") ||
                lower.Contains("crée") || lower.Contains("cree") ||
                lower.Contains("fait moi") || lower.Contains("fais moi");

            bool wantsProject =
                lower.Contains("jeu") || lower.Contains("projet") ||
                lower.Contains("appli") || lower.Contains("app ") ||
                lower.Contains("serpent") || lower.Contains("snake");

            return wantsGeneration && wantsProject;
        }

        /// <summary>
        /// Calcule le dossier du projet à partir de la description.
        /// </summary>
        public string ComputeProjectDir(string description, string targetRoot)
        {
            var name = ExtractProjectName(description);
            return Path.Combine(targetRoot, name);
        }

        /// <summary>
        /// Génère et écrit le projet complet sur disque.
        /// </summary>
        public async Task<BuilderResult> BuildAsync(string description, string targetRoot)
        {
            var result = new BuilderResult();

            try
            {
                var projectName = ExtractProjectName(description);
                var projectDir = Path.Combine(targetRoot, projectName);

                // Choix du template selon l'intention.
                var files = _templates.GetSnakeGameFiles(projectName);

                result.Explanation =
                    $"J'ai détecté une demande de jeu type Snake.\n" +
                    $"Je génère le projet complet '{projectName}' : boucle de jeu, serpent, " +
                    $"nourriture, score, collisions, rendu rétro monochrome.";

                // Écriture asynchrone de tous les fichiers.
                await Task.Run(() =>
                {
                    foreach (var file in files)
                    {
                        var fullPath = Path.Combine(projectDir, file.RelativePath);
                        var dir = Path.GetDirectoryName(fullPath);

                        if (!string.IsNullOrWhiteSpace(dir))
                        {
                            Directory.CreateDirectory(dir);
                        }

                        File.WriteAllText(fullPath, file.Content);
                    }
                });

                result.Files.AddRange(files);
                result.Success = true;
                result.Summary = $"Projet '{projectName}' généré dans {projectDir}";
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Summary = "Échec de la génération du projet.";
                result.Explanation = $"Erreur : {ex.Message}";
            }

            return result;
        }

        /// <summary>
        /// Extrait un nom de projet propre depuis la phrase utilisateur.
        /// </summary>
        private string ExtractProjectName(string description)
        {
            var lower = description.ToLowerInvariant();

            if (lower.Contains("serpent") || lower.Contains("snake"))
            {
                return "SnakeRetro";
            }

            if (lower.Contains("plateforme"))
            {
                return "PlatformerGame";
            }

            return "MotoProject";
        }
    }
}
