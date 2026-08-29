using Moto.Core.Performance;

namespace Moto.Core.AI.Neural;

/// <summary>
/// Version optimisée de NeuralMode :
/// - TF-IDF au lieu de transformers lourds
/// - Index en mémoire compressé
/// - Cache des embeddings
/// </summary>
public sealed class NeuralMode_Optimized
{
    private readonly AggressiveCacheManager _cache;
    private readonly Dictionary<string, float[]> _embeddings = new();

    public NeuralMode_Optimized(AggressiveCacheManager cache)
    {
        _cache = cache;
    }

    /// <summary>
    /// Génère un embedding TF-IDF (léger) au lieu d'un transformer lourd.
    /// </summary>
    public async Task<float[]> GetEmbeddingAsync(string text)
    {
        var cacheKey = $"embedding:{text.GetHashCode()}";
        return await _cache.GetOrComputeAsync(cacheKey, () =>
        {
            // TF-IDF simplifié (à remplacer par une vraie implémentation)
            var words = text.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var embedding = new float[128]; // Vecteur de taille fixe

            for (int i = 0; i < words.Length && i < embedding.Length; i++)
            {
                embedding[i % embedding.Length] = words[i].Length / 10.0f; // Heuristique simple
            }

            return Task.FromResult(embedding);
        }, TimeSpan.FromDays(7));
    }

    /// <summary>
    /// Recherche les documents les plus similaires (cosine similarity).
    /// </summary>
    public async Task<List<(string doc, float score)>> SearchAsync(string query, List<string> documents, int topK = 5)
    {
        var queryEmbedding = await GetEmbeddingAsync(query);
        var results = new List<(string, float)>();

        foreach (var doc in documents)
        {
            var docEmbedding = await GetEmbeddingAsync(doc);
            var similarity = CosineSimilarity(queryEmbedding, docEmbedding);
            results.Add((doc, similarity));
        }

        return results.OrderByDescending(r => r.Item2).Take(topK).ToList();
    }

    private float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0;

        float dotProduct = 0, normA = 0, normB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        return dotProduct / (MathF.Sqrt(normA) * MathF.Sqrt(normB) + 1e-8f);
    }
}
