// Moto.Core/AI/Neural/CodeRetrieval.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Moto.Core.AI.Neural
{
    /// <summary>
    /// Retrieval sémantique de code : trouve les snippets les plus similaires
    /// à une requête, basés sur les embeddings.
    /// </summary>
    public class CodeRetrieval
    {
        private readonly EmbeddingEngine _embeddings = new();
        private readonly Dictionary<string, (string Content, double[] Embedding)> _index = new();

        /// <summary>Indexe tous les fichiers C# d'un workspace.</summary>
        public void IndexWorkspace(string workspace)
        {
            var files = Directory.GetFiles(workspace, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                         && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                .Take(500);

            foreach (var file in files)
            {
                try
                {
                    var content = File.ReadAllText(file);
                    IndexFile(file, content);
                }
                catch
                {
                    // Fichier illisible : ignoré
                }
            }
        }

        /// <summary>Indexe un fichier unique.</summary>
        public void IndexFile(string path, string content)
        {
            _embeddings.AddDocument(path, content);
            var embedding = _embeddings.Embed(content);
            _index[path] = (content, embedding);
        }

        /// <summary>Trouve les snippets les plus similaires à une requête.</summary>
        public List<RetrievalResult> Search(string query, int top = 5)
        {
            var queryEmbedding = _embeddings.Embed(query);
            var results = new List<RetrievalResult>();

            foreach (var kv in _index)
            {
                var similarity = _embeddings.CosineSimilarity(queryEmbedding, kv.Value.Embedding);

                if (similarity > 0.1)
                {
                    results.Add(new RetrievalResult
                    {
                        FilePath = kv.Key,
                        Content = kv.Value.Content,
                        Similarity = similarity
                    });
                }
            }

            return results
                .OrderByDescending(r => r.Similarity)
                .Take(top)
                .ToList();
        }
    }

    public class RetrievalResult
    {
        public string FilePath { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public double Similarity { get; set; }
    }
}
