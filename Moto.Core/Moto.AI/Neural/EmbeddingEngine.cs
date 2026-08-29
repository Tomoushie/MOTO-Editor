// Moto.Core/AI/Neural/EmbeddingEngine.cs
using System;
using System.Collections.Generic;
using System.Linq;

namespace Moto.Core.AI.Neural
{
    /// <summary>
    /// Moteur d'embeddings locaux (TF-IDF simplifié).
    /// Génère des vecteurs pour le code, permettant le retrieval sémantique.
    /// </summary>
    public class EmbeddingEngine
    {
        private readonly Dictionary<string, int> _vocabulary = new();
        private readonly Dictionary<string, int> _documentFrequency = new();
        private int _totalDocuments = 0;

        /// <summary>Ajoute un document au vocabulaire.</summary>
        public void AddDocument(string id, string content)
        {
            var tokens = Tokenize(content);
            var uniqueTokens = tokens.Distinct();

            foreach (var token in uniqueTokens)
            {
                if (!_vocabulary.ContainsKey(token))
                    _vocabulary[token] = _vocabulary.Count;

                if (!_documentFrequency.ContainsKey(token))
                    _documentFrequency[token] = 0;

                _documentFrequency[token]++;
            }

            _totalDocuments++;
        }

        /// <summary>Génère l'embedding TF-IDF d'un texte.</summary>
        public double[] Embed(string text)
        {
            var tokens = Tokenize(text);
            var tf = new Dictionary<string, int>();

            foreach (var token in tokens)
            {
                if (!tf.ContainsKey(token))
                    tf[token] = 0;
                tf[token]++;
            }

            var embedding = new double[_vocabulary.Count];

            foreach (var kv in tf)
            {
                if (_vocabulary.TryGetValue(kv.Key, out var index))
                {
                    var termFreq = (double)kv.Value / tokens.Count;
                    var docFreq = _documentFrequency.GetValueOrDefault(kv.Key, 1);
                    var idf = Math.Log((double)_totalDocuments / docFreq);

                    embedding[index] = termFreq * idf;
                }
            }

            return embedding;
        }

        /// <summary>Calcule la similarité cosinus entre deux embeddings.</summary>
        public double CosineSimilarity(double[] a, double[] b)
        {
            if (a.Length != b.Length) return 0;

            var dotProduct = 0.0;
            var normA = 0.0;
            var normB = 0.0;

            for (int i = 0; i < a.Length; i++)
            {
                dotProduct += a[i] * b[i];
                normA += a[i] * a[i];
                normB += b[i] * b[i];
            }

            if (normA == 0 || normB == 0) return 0;

            return dotProduct / (Math.Sqrt(normA) * Math.Sqrt(normB));
        }

        private List<string> Tokenize(string text)
        {
            // Tokenisation simple : mots + symboles C#
            var tokens = new List<string>();
            var current = "";

            foreach (var c in text)
            {
                if (char.IsLetterOrDigit(c) || c == '_')
                {
                    current += c;
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(current))
                    {
                        tokens.Add(current.ToLowerInvariant());
                        current = "";
                    }

                    if (!char.IsWhiteSpace(c))
                        tokens.Add(c.ToString());
                }
            }

            if (!string.IsNullOrWhiteSpace(current))
                tokens.Add(current.ToLowerInvariant());

            return tokens;
        }
    }
}
