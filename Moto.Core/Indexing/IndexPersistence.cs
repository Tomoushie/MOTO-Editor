// Moto.Editor/Indexing/IndexPersistence.cs
using System;
using System.IO;
using System.Text.Json;

namespace Moto.Editor.Indexing
{
    /// <summary>
    /// Persistance légère de l'index.
    /// Sauvegarde en JSON pour redémarrage instantané.
    /// Pour 100k lignes, le fichier fait environ 5-15 Mo.
    /// </summary>
    public static class IndexPersistence
    {
        private static readonly string IndexDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MotoEditor",
            "Index"
        );

        /// <summary>
        /// Sauvegarde l'index pour un workspace donné.
        /// Le nom du workspace est hashé pour éviter les caractères invalides.
        /// </summary>
        public static void Save(ProjectIndex index, string workspacePath)
        {
            try
            {
                Directory.CreateDirectory(IndexDirectory);

                var fileName = GetIndexFileName(workspacePath);
                var filePath = Path.Combine(IndexDirectory, fileName);

                // Sérialisation simple. Pour de très gros index,
                // on pourrait passer à un format binaire.
                var data = new IndexFileData
                {
                    WorkspacePath = workspacePath,
                    SavedAtUtc = DateTime.UtcNow,
                    SymbolCount = index.SymbolCount,
                    FileCount = index.FileCount
                };

                var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(filePath, json);
            }
            catch
            {
                // La persistance est optionnelle : un échec ne doit pas bloquer l'éditeur.
            }
        }

        /// <summary>
        /// Vérifie si un index sauvegardé existe pour ce workspace.
        /// </summary>
        public static bool Exists(string workspacePath)
        {
            var fileName = GetIndexFileName(workspacePath);
            var filePath = Path.Combine(IndexDirectory, fileName);
            return File.Exists(filePath);
        }

        private static string GetIndexFileName(string workspacePath)
        {
            // Hash simple pour un nom de fichier stable.
            var hash = workspacePath.GetHashCode().ToString("X8");
            return $"index_{hash}.json";
        }

        private class IndexFileData
        {
            public string WorkspacePath { get; set; }
            public DateTime SavedAtUtc { get; set; }
            public int SymbolCount { get; set; }
            public int FileCount { get; set; }
        }
    }
}
